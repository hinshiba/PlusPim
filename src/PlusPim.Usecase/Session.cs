using PlusPim.Asm;
using PlusPim.Dbg;
using PlusPim.Image;
using PlusPim.Instructions;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Threading.Channels;

namespace PlusPim.Usecase;

/// <summary>
/// 読み込みの結果
/// </summary>
/// <param name="Session">失敗したら <see langword="null"/></param>
/// <param name="Diagnostics">出現順の診断</param>
public sealed record LoadResult(Session? Session, ImmutableArray<Diagnostic> Diagnostics);

/// <summary>
/// 呼び出しまたは例外のフレームの表示用の情報
/// </summary>
/// <param name="Name">フレームの名前. なければ <see langword="null"/></param>
/// <param name="Pc">このフレームで実行中の命令. 呼び出し元のフレームでは呼び出した命令 (例外なら EPC)</param>
/// <param name="Location"><paramref name="Pc"/> のソース上の行. なければ <see langword="null"/></param>
/// <param name="Registers">ライブフレームは現在の値, それ以外は呼び出した時点の値</param>
/// <param name="Code">例外ハンドラのフレームならその例外</param>
public readonly record struct StackFrame(string? Name, Address Pc, SourceLocation? Location, Registers Registers, ExcCode? Code);

/// <summary>
/// 疑似命令の展開先の1命令
/// </summary>
/// <param name="Text">命令のアセンブリ表記. 命令がなければ <see langword="null"/></param>
public readonly record struct Expansion(Address Address, string? Text);

/// <summary>
/// 疑似命令の1行の表示用の情報
/// </summary>
/// <param name="Line">1-indexed の行</param>
/// <param name="Mnemonic">疑似命令のニーモニック</param>
/// <param name="Instructions">アドレス順の展開先</param>
public readonly record struct PseudoExpansion(int Line, string Mnemonic, ImmutableArray<Expansion> Instructions);

/// <summary>
/// 読み込んだプログラムのデバッグセッション
/// </summary>
/// <remarks>
/// 呼び出し元は1スレッドずつ呼ぶこと. 実行の操作は同時に1つだけで, 実行中に呼べるのはブレークポイントと例外フィルタの設定だけである.
/// 違反は <see cref="InvalidOperationException"/> とする.
/// </remarks>
public sealed class Session: IDisposable {
    /// <summary>取り消しの確認と出力の書き出しをするステップ数</summary>
    private const int BatchSize = 4096;

    private readonly Debuggee _debuggee;
    /// <summary><see cref="_debuggee"/> の出力のうち書き出していないもの</summary>
    private readonly ArrayBufferWriter<byte> _pending;
    private readonly Stream _output;
    private readonly ChannelReader<byte[]> _input;
    private readonly CancellationTokenSource _pump;

    /// <summary>呼び出し元のスレッドだけが触る</summary>
    private readonly Dictionary<SourcePath, Address[]> _byFile = [];
    // 実行中のスレッドも読むので, 不変の値を差し替える
    private FrozenSet<Address> _breakpoints = [];
    private uint _filter;
    /// <summary>実行の操作の最中なら1</summary>
    private int _running;

    /// <summary>不変なので実行中も読める</summary>
    private readonly AssembledProgram _program;

    private Session(AssembledProgram program, ArrayBufferWriter<byte> pending, Stream output, ChannelReader<byte[]> input, CancellationTokenSource pump) {
        this._program = program;
        this._pending = pending;
        this._debuggee = Debuggee.Load(program.Image, pending);
        this._output = output;
        this._input = input;
        this._pump = pump;
    }

    /// <summary>
    /// アセンブルして読み込む
    /// </summary>
    /// <param name="paths">配置する順のソース</param>
    /// <param name="reader">ソースを読む方法</param>
    /// <param name="strict">警告があれば失敗にする</param>
    /// <param name="input">デバッギの入力. 0バイトの読み込みを EOF とする</param>
    /// <param name="output">デバッギの出力</param>
    public static LoadResult Load(IEnumerable<SourcePath> paths, ISourceReader reader, bool strict, Stream input, Stream output) {
        AssemblyResult r = Assembler.Assemble(paths, reader);
        if(r.Program is not AssembledProgram p || (strict && !r.Diagnostics.IsEmpty)) {
            return new LoadResult(null, r.Diagnostics);
        }
        Channel<byte[]> channel = Channel.CreateUnbounded<byte[]>(new() { SingleReader = true, SingleWriter = true });
        CancellationTokenSource pump = new();
        // stdin の ReadAsync は取り消しに従わないことがあるので, 別のタスクで読んで待ちを取り消せるようにする
        _ = Task.Run(() => PumpAsync(input, channel.Writer, pump));
        return new LoadResult(new Session(p, new(), output, channel.Reader, pump), r.Diagnostics);
    }

    /// <summary>入力を読み終わるまで <paramref name="w"/> へ移す</summary>
    private static async Task PumpAsync(Stream input, ChannelWriter<byte[]> w, CancellationTokenSource cts) {
        byte[] buf = new byte[4096];
        try {
            int n;
            while(0 < (n = await input.ReadAsync(buf, cts.Token))) {
                _ = w.TryWrite(buf[..n]);
            }
        } catch(Exception e) when(e is IOException or OperationCanceledException or ObjectDisposedException) {
            // 読めなくなった入力は EOF とみなす
        } finally {
            _ = w.TryComplete();
            cts.Dispose();
        }
    }

    /// <summary>
    /// デバッギの状態を変化させる
    /// </summary>
    /// <param name="motion">操作</param>
    /// <param name="ct">取り消すと <see cref="Paused"/> で止まる</param>
    public Task<Stop> RunAsync(Motion motion, CancellationToken ct = default) {
        return this.ExclusiveAsync(() => {
            int depth = this._debuggee.View.Depth;
            return motion switch {
                StepIn m => this.ForwardAsync(Goal.Any(m.Granularity), ct),
                StepOver m => this.ForwardAsync(new(m.Granularity, depth), ct),
                StepOut m => this.ForwardAsync(new(m.Granularity, depth - 1), ct),
                Continue => this.ForwardAsync(Goal.None, ct),
                StepBack m => ValueTask.FromResult(this.Backward(Goal.Any(m.Granularity), ct)),
                ReverseContinue => ValueTask.FromResult(this.Backward(Goal.None, ct)),
            };
        });
    }

    /// <summary>
    /// <paramref name="path"/> のブレークポイントを設定する. 実行中も呼べる
    /// </summary>
    /// <param name="path">ソース</param>
    /// <param name="lines">1-indexed の行</param>
    /// <returns>各行が命令を持ち有効か</returns>
    public bool[] SetBreakpoints(SourcePath path, ReadOnlySpan<int> lines) {
        SourceMap map = this._program.DebugInfo.SourceMap;
        bool[] valid = new bool[lines.Length];
        List<Address> addrs = [];
        for(int i = 0; i < lines.Length; i++) {
            if(map.AddressOf(new SourceLocation(path, lines[i])) is Address a) {
                valid[i] = true;
                addrs.Add(a);
            }
        }
        this._byFile[path] = [.. addrs];
        Volatile.Write(ref this._breakpoints, this._byFile.Values.SelectMany(a => a).ToFrozenSet());
        return valid;
    }

    /// <summary>
    /// 例外フィルタを設定する. 実行中も呼べる
    /// </summary>
    public void SetExceptionFilter(ReadOnlySpan<ExcCode> codes) {
        uint mask = 0;
        foreach(ExcCode c in codes) {
            mask |= 1u << (int)c;
        }
        Volatile.Write(ref this._filter, mask);
    }

    /// <summary>
    /// <paramref name="path"/> の疑似命令の行. 読み込んでいなければ空. 実行中も呼べる
    /// </summary>
    public ImmutableArray<PseudoExpansion> PseudoExpansions(SourcePath path) {
        ProgramImage image = this._program.Image;
        return [.. this._program.DebugInfo.SourceMap.PseudosIn(path).Select(span => new PseudoExpansion(
            span.Location.Line,
            span.Pseudo!,
            [.. Enumerable.Range(0, span.Count).Select(i => span.Start + (uint)(i * 4)).Select(a => new Expansion(a, image.Fetch(a)?.Text))]))];
    }

    /// <summary>
    /// <paramref name="destination"/> のすべてのバイトを <paramref name="start"/> から読む. 書き込まれていないメモリは未定義. 停止中のみ
    /// </summary>
    public void ReadMemory(Address start, Span<byte> destination) {
        this.Idle.ReadMemory(start, destination);
    }

    /// <summary>
    /// ライブフレームを先頭とするフレームの列. 停止中のみ
    /// </summary>
    public ImmutableArray<StackFrame> Frames() {
        StateView v = this.Idle;
        DebugInfo info = this._program.DebugInfo;
        ImmutableArray<StackFrame>.Builder frames = ImmutableArray.CreateBuilder<StackFrame>(v.Depth + 1);
        Address pc = v.Pc;
        Registers regs = v.Registers;
        // 積んだフレームはその呼び出し先の入口と, 呼び出し元の位置を持つ
        foreach(Frame f in v.CallStack) {
            frames.Add(new StackFrame(info.SymbolAt(f.Entry), pc, info.SourceMap.LocationOf(pc), regs, f.Code));
            pc = f.CallSite;
            regs = f.CallerRegs;
        }
        frames.Add(new StackFrame(info.SymbolAt(this._program.Image.Entry), pc, info.SourceMap.LocationOf(pc), regs, null));
        return frames.MoveToImmutable();
    }

    /// <summary>入力の読み込みを止める. 停止中のみ</summary>
    public void Dispose() {
        this.EnsureIdle();
        try {
            this._pump.Cancel();
        } catch(ObjectDisposedException) {
            // 入力はすでに読み終わっている
        }
    }

    private Rules Rules => new(Volatile.Read(ref this._breakpoints), Volatile.Read(ref this._filter), this._program.DebugInfo.SourceMap);

    /// <summary>停止中の状態</summary>
    private StateView Idle {
        get {
            this.EnsureIdle();
            return this._debuggee.View;
        }
    }

    private void EnsureIdle() {
        if(Volatile.Read(ref this._running) != 0) {
            throw new InvalidOperationException("An execution operation is running.");
        }
    }

    /// <summary>
    /// 排他して <paramref name="op"/> をスレッドプールで実行し, 出力を書き出す
    /// </summary>
    private Task<Stop> ExclusiveAsync(Func<ValueTask<Stop>> op) {
        return Interlocked.Exchange(ref this._running, 1) != 0
            ? throw new InvalidOperationException("An execution operation is running.")
            : Task.Run(async () => {
                try {
                    return await op();
                } finally {
                    await this.FlushAsync();
                    Volatile.Write(ref this._running, 0);
                }
            });
    }

    private async ValueTask<Stop> ForwardAsync(Goal g, CancellationToken ct) {
        try {
            while(!ct.IsCancellationRequested) {
                Rules rules = this.Rules;
                for(int i = 0; i < BatchSize; i++) {
                    if(this._debuggee.Step() == StepResult.NeedInput) {
                        await this.FlushAsync();
                        await this.FeedAsync(ct);
                    } else if(Judge.Forward(g, this._debuggee.View, rules) is { } s) {
                        return s;
                    }
                }
                // 終わらないループの出力も見えるようにする
                await this.FlushAsync();
            }
        } catch(OperationCanceledException) when(ct.IsCancellationRequested) {
            // 入力待ちの取り消し
        }
        return new Paused();
    }

    private Stop Backward(Goal g, CancellationToken ct) {
        while(!ct.IsCancellationRequested) {
            Rules rules = this.Rules;
            for(int i = 0; i < BatchSize; i++) {
                if(Judge.Backward(this._debuggee.Back(), g, this._debuggee.View, rules) is { } s) {
                    return s;
                }
            }
        }
        return new Paused();
    }

    /// <summary>入力が届くまで待って与える. 入力が終わっていれば EOF を与える</summary>
    private async ValueTask FeedAsync(CancellationToken ct) {
        if(!await this._input.WaitToReadAsync(ct)) {
            this._debuggee.CloseInput();
            return;
        }
        while(this._input.TryRead(out byte[]? b)) {
            this._debuggee.Feed(b);
        }
    }

    private async ValueTask FlushAsync() {
        if(this._pending.WrittenCount == 0) {
            return;
        }
        await this._output.WriteAsync(this._pending.WrittenMemory);
        await this._output.FlushAsync();
        this._pending.ResetWrittenCount();
    }
}
