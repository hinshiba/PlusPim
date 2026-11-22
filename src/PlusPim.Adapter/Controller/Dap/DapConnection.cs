using PlusPim.Adapter.Controller.Dap.Protocol;
using PlusPim.Adapter.Controller.Dap.Wire;
using PlusPim.Adapter.Logging;
using PlusPim.Dbg;
using PlusPim.Image;
using PlusPim.Usecase;
using System.Buffers;
using System.Collections.Immutable;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Frame = PlusPim.Usecase.StackFrame;
using StackFrame = PlusPim.Adapter.Controller.Dap.Protocol.StackFrame;

namespace PlusPim.Adapter.Controller.Dap;

/// <summary>
/// 接続のループが受け取る入力
/// </summary>
internal abstract record Input {
    /// <summary>クライアントからの要求</summary>
    internal sealed record Received(Request Request): Input;

    /// <summary>実行の操作が終わった. 完了したタスクを持つ</summary>
    internal sealed record Executed(Task<Stop> Run): Input;

    /// <summary>クライアントへ転送するログ</summary>
    internal sealed record Log(OutputCategory Category, string Text): Input;

    /// <summary>接続が終わったか, 枠組みの違反があった</summary>
    internal sealed record Closed(string Reason): Input;
}

/// <summary>
/// 接続の状態. 各状態は必要なデータだけを持つ
/// </summary>
internal abstract record State {
    internal sealed record Uninitialized: State;
    internal sealed record Initialized: State;

    /// <summary>セッションがある状態. 構成の要求を受け付ける</summary>
    internal abstract record Loaded(Session Session): State;

    /// <summary><c>configurationDone</c> を待つ</summary>
    internal sealed record Configuring(Session Session, StartAction Action): Loaded(Session);

    /// <summary>状態を検査できる. フレームは次の実行まで有効</summary>
    /// <param name="Exception">例外かランタイムエラーで止まったならその文面</param>
    internal sealed record Stopped(Session Session, ExceptionText? Exception): Loaded(Session) {
        public ImmutableArray<Frame> Frames { get; } = Session.Frames();
    }

    /// <summary><c>terminated</c> を送った後. <c>disconnect</c> を待つ</summary>
    internal sealed record Terminated(Session Session): Loaded(Session);

    /// <summary>実行の操作の最中. セッションの検査は呼べない</summary>
    /// <param name="Cancel">取り消すと <see cref="Paused"/> で止まる</param>
    internal abstract record Executing(Session Session, CancellationTokenSource Cancel): Loaded(Session);
    internal sealed record Running(Session Session, CancellationTokenSource Cancel): Executing(Session, Cancel);

    /// <summary>ブレークポイントと例外フィルタを適用せずに実行している</summary>
    internal sealed record RunningNoDebug(Session Session, CancellationTokenSource Cancel): Executing(Session, Cancel);

    /// <summary>実行の操作の終わりを待って <c>disconnect</c> に応答する</summary>
    internal sealed record Disconnecting(Seq Request, Session Session, CancellationToken Deadline): State;
    internal sealed record Closed: State;
}

/// <summary>
/// 1つの接続のDAPの状態機械
/// </summary>
/// <remarks>状態はループの中だけで読み書きし, 送信もループだけが行うのでロックは要らない</remarks>
/// <param name="stream">DAPの接続</param>
/// <param name="load"><c>launch</c> でプログラムを読み込む. <c>launch</c> の <c>program</c> は使わない</param>
public sealed class DapConnection(Stream stream, Func<LoadResult> load) {
    private const int InboxCapacity = 64;
    private const int MaxFrames = 1000;
    private static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(1);
    private static readonly ThreadsBody Threads = new([new DapThread(1, "main")]);
    private static readonly Capabilities Capabilities = new(Mapping.Filters);

    private readonly Channel<Input> _inbox = Channel.CreateBounded<Input>(new BoundedChannelOptions(InboxCapacity) {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait,
    });
    private readonly MessageWriter _writer = new(stream);
    private int _droppedLogs;

    /// <summary>
    /// クライアントへ転送するログを受け取る. <see cref="Logger.AddSink"/> に渡せる
    /// </summary>
    /// <remarks>ログの洪水が要求を締め出さないよう, 受信箱の半分を超えたら捨てて数える</remarks>
    public void PostLog(LogLevel level, string source, string message) {
        Input.Log log = new(LogLevel.Warning <= level ? OutputCategory.Stderr : OutputCategory.Console, $"[{level}][{source}] {message}\n");
        if(InboxCapacity / 2 <= this._inbox.Reader.Count || !this._inbox.Writer.TryWrite(log)) {
            _ = Interlocked.Increment(ref this._droppedLogs);
        }
    }

    /// <summary>接続が閉じるまで動く</summary>
    public async Task RunAsync() {
        using CancellationTokenSource receiving = new();
        _ = this.ReceiveAsync(receiving.Token);
        State state = new State.Uninitialized();
        while(state is not State.Closed) {
            Input input = await this.NextAsync(state);
            try {
                state = await this.StepAsync(state, input);
            } catch(IOException) {
                state = Close(state);
            } catch(Exception e) {
                if(input is Input.Received { Request: Request r }) {
                    await this._writer.FailAsync(r.Seq, r.Command, e.Message);
                }
                this.PostLog(LogLevel.Error, nameof(DapConnection), e.ToString());
            }
        }
        // 書き込みを待つ継続と受信タスクを解放する
        _ = this._inbox.Writer.TryComplete();
        await receiving.CancelAsync();
    }

    private async ValueTask<Input> NextAsync(State state) {
        if(state is not State.Disconnecting d) {
            return await this._inbox.Reader.ReadAsync();
        }
        try {
            return await this._inbox.Reader.ReadAsync(d.Deadline);
        } catch(OperationCanceledException) {
            return new Input.Closed("The debuggee did not stop in time.");
        }
    }

    private async Task ReceiveAsync(CancellationToken ct) {
        string reason = "The client closed the connection.";
        try {
            using FrameReader reader = new(stream);
            while(await reader.ReadAsync(ct) is ReadOnlyMemory<byte> body) {
                if(RequestDecoder.Decode(body.Span) is Request request) {
                    // 受信箱が満ちていれば待ち, TCPの受信を止めて背圧をかける
                    await this._inbox.Writer.WriteAsync(new Input.Received(request), ct);
                } else {
                    this.PostLog(LogLevel.Debug, nameof(DapConnection), "Ignored a message that is not a request.");
                }
            }
        } catch(Exception e) when(e is FramingException or IOException or ObjectDisposedException or OperationCanceledException or ChannelClosedException) {
            reason = e.Message;
        }
        _ = await this.TryWriteAsync(new Input.Closed(reason));
    }

    private async ValueTask<bool> TryWriteAsync(Input input) {
        try {
            await this._inbox.Writer.WriteAsync(input);
            return true;
        } catch(ChannelClosedException) {
            return false;
        }
    }

    private async ValueTask<State> StepAsync(State state, Input input) {
        switch(state, input) {
            case (_, Input.Received(Request request)):
                return await this.HandleAsync(state, request);
            case (State.Uninitialized, Input.Log):
                // initializeに応答する前はイベントを送れない
                return state;
            case (_, Input.Log log):
                await this.OutputAsync(log.Category, log.Text);
                return state;
            case (State.Disconnecting d, Input.Executed or Input.Closed):
                if(input is Input.Executed) {
                    d.Session.Dispose();
                }
                await this._writer.RespondAsync(d.Request, Names.Commands.Name(Command.Disconnect), Empty.Value, DapJson.Default.Empty);
                return new State.Closed();
            case (_, Input.Closed):
                return Close(state);
            case (State.Executing x, Input.Executed { Run.IsCompletedSuccessfully: false } e):
                await this.OutputAsync(OutputCategory.Stderr, MessageCatalog.InternalError(e.Run.Exception) + "\n");
                return await this.TerminateAsync(x.Session);
            case (State.Running x, Input.Executed e):
                return await this.StopAsync(x.Session, e.Run.Result);
            case (State.RunningNoDebug x, Input.Executed e):
                // 状態を調べられないので, 止まった理由を出して終える
                if(e.Run.Result is not Exited) {
                    await this.OutputAsync(OutputCategory.Stderr, MessageCatalog.Describe(e.Run.Result) + "\n");
                }
                return await this.TerminateAsync(x.Session);
            default:
                return state;
        }
    }

    private async ValueTask<State> HandleAsync(State state, Request request) {
        // noDebugではブレークポイントと例外フィルタをセッションへ渡さない
        bool noDebug = state is State.Configuring { Action: StartAction.NoDebug } or State.RunningNoDebug;
        switch(state, request) {
            case (_, Request.Rejected r):
                await this._writer.FailAsync(r.Seq, r.Command, r.Reason);
                return state;
            case (State.Uninitialized, Request.Simple { Kind: Command.Initialize }):
                await this.RespondAsync(request, Capabilities, DapJson.Default.Capabilities);
                return new State.Initialized();
            case (State.Initialized, Request.Launch launch):
                return await this.LaunchAsync(state, launch);
            case (State.Configuring c, Request.Simple { Kind: Command.ConfigurationDone }):
                await this.RespondAsync(request, Empty.Value, DapJson.Default.Empty);
                if(c.Action == StartAction.StopAtEntry) {
                    await this._writer.EventAsync(EventName.Stopped, new StoppedEvent("entry"), DapJson.Default.StoppedEvent);
                    return new State.Stopped(c.Session, null);
                }
                return this.Start(c.Session, new Continue(), c.Action == StartAction.NoDebug);
            case (State.Stopped s, Request.Execute e):
                // 応答をstoppedより先に送るため, 応答を書いてから実行を始める
                await this.RespondAsync(request, Empty.Value, DapJson.Default.Empty);
                return this.Start(s.Session, e.Motion, false);
            case (State.Stopped s, Request.StackTrace trace):
                await this.RespondAsync(request, StackTrace(s.Frames, trace), DapJson.Default.StackTraceBody);
                return state;
            case (State.Stopped s, Request.Scopes scopes) when scopes.Frame.Value < s.Frames.Length:
                int frame = scopes.Frame.Value;
                ScopeView[] views = Mapping.Scopes(s.Frames[frame], frame == 0);
                await this.RespondAsync(request, new ScopesBody([.. views.Select((v, i) => new Scope(v.Name, VariablesReference.Of(frame, i), false))]), DapJson.Default.ScopesBody);
                return state;
            case (State.Stopped s, Request.Variables variables):
                await this.RespondAsync(request, new VariablesBody(Variables(s.Frames, variables.Reference)), DapJson.Default.VariablesBody);
                return state;
            case (State.Stopped s, Request.ReadMemory memory):
                await this.ReadMemoryAsync(s.Session, request, memory.Window);
                return state;
            case (State.Stopped { Exception: ExceptionText x }, Request.Simple { Kind: Command.ExceptionInfo }):
                await this.RespondAsync(request, new ExceptionInfoBody(x.Id, x.Description, "always"), DapJson.Default.ExceptionInfoBody);
                return state;
            case (State.Stopped, Request.Simple { Kind: Command.Pause }):
                // 自然な停止と競合しても観測できないよう, 停止中のpauseは成功の無操作にする
                await this.RespondAsync(request, Empty.Value, DapJson.Default.Empty);
                return state;
            case (State.Running x, Request.Simple { Kind: Command.Pause }):
                await x.Cancel.CancelAsync();
                await this.RespondAsync(request, Empty.Value, DapJson.Default.Empty);
                return state;
            case (State.Executing x, Request.Simple { Kind: Command.Disconnect }):
                await x.Cancel.CancelAsync();
                return new State.Disconnecting(request.Seq, x.Session, new CancellationTokenSource(DisconnectTimeout).Token);
            case (not State.Disconnecting, Request.Simple { Kind: Command.Disconnect }):
                await this.RespondAsync(request, Empty.Value, DapJson.Default.Empty);
                return Close(state);
            case (not (State.Uninitialized or State.Disconnecting), Request.Simple { Kind: Command.Threads }):
                await this.RespondAsync(request, Threads, DapJson.Default.ThreadsBody);
                return state;
            case (State.Loaded l, Request.SetBreakpoints set):
                bool[] verified = noDebug ? new bool[set.Lines.Length] : l.Session.SetBreakpoints(set.Source, [.. set.Lines.Select(line => line.Value)]);
                await this.RespondAsync(request, new SetBreakpointsBody([.. set.Lines.Select((line, i) => new Protocol.Breakpoint(verified[i], line))]), DapJson.Default.SetBreakpointsBody);
                return state;
            case (State.Loaded l, Request.SetExceptionFilters set):
                if(!noDebug) {
                    l.Session.SetExceptionFilter(Mapping.Codes(set.Filters));
                }
                await this.RespondAsync(request, Empty.Value, DapJson.Default.Empty);
                return state;
            case (State.Loaded l, Request.PseudoExpansions pseudo):
                await this.RespondAsync(request, PseudoExpansions(l.Session.PseudoExpansions(pseudo.Source)), DapJson.Default.PseudoExpansionsBody);
                return state;
            case (State.Running, _):
                await this._writer.FailAsync(request.Seq, request.Command, "The debuggee is running. Pause it first.");
                return state;
            default:
                await this._writer.FailAsync(request.Seq, request.Command, $"'{request.Command}' is not accepted in the {state.GetType().Name} state.");
                return state;
        }
    }

    private async ValueTask<State> LaunchAsync(State state, Request.Launch launch) {
        LoadResult r = load();
        string diagnostics = string.Concat(r.Diagnostics.Select(d => MessageCatalog.Format(d) + "\n"));
        if(r.Session is not Session session) {
            // 読み込みの失敗には必ず診断が伴う
            await this._writer.FailAsync(launch.Seq, launch.Command, diagnostics.TrimEnd());
            await this.OutputAsync(OutputCategory.Stderr, diagnostics);
            return state;
        }
        if(diagnostics.Length != 0) {
            await this.OutputAsync(OutputCategory.Stderr, diagnostics);
        }
        // DAPの順序に従い, launchに応答してからinitializedを送る
        await this.RespondAsync(launch, Empty.Value, DapJson.Default.Empty);
        await this._writer.EventAsync(EventName.Initialized, Empty.Value, DapJson.Default.Empty);
        return new State.Configuring(session, launch.Action);
    }

    /// <summary>
    /// 実行の操作を始める. 終わると <see cref="Input.Executed"/> が受信箱に入る
    /// </summary>
    private State Start(Session s, Motion motion, bool noDebug) {
        CancellationTokenSource cancel = new();
        _ = this.ForwardAsync(s.RunAsync(motion, cancel.Token));
        return noDebug ? new State.RunningNoDebug(s, cancel) : new State.Running(s, cancel);
    }

    private async Task ForwardAsync(Task<Stop> run) {
        // 失敗はループがInput.Executedから読む
        await ((Task)run).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _ = await this.TryWriteAsync(new Input.Executed(run));
    }

    private async ValueTask<State> StopAsync(Session s, Stop stop) {
        (StoppedEvent? e, ExceptionText? exception) = Mapping.Translate(stop);
        if(e is null) {
            return await this.TerminateAsync(s);
        }
        await this._writer.EventAsync(EventName.Stopped, e, DapJson.Default.StoppedEvent);
        return new State.Stopped(s, exception);
    }

    private async ValueTask<State> TerminateAsync(Session s) {
        await this._writer.EventAsync(EventName.Terminated, Empty.Value, DapJson.Default.Empty);
        return new State.Terminated(s);
    }

    /// <summary>実行中なら取り消し, そうでなければセッションを閉じる</summary>
    private static State.Closed Close(State state) {
        switch(state) {
            case State.Executing x:
                x.Cancel.Cancel();
                break;
            case State.Loaded l:
                l.Session.Dispose();
                break;
        }
        return new State.Closed();
    }

    private async ValueTask ReadMemoryAsync(Session s, Request request, MemoryWindow window) {
        byte[] data = ArrayPool<byte>.Shared.Rent(window.Readable);
        try {
            s.ReadMemory(window.Start, data.AsSpan(0, window.Readable));
            ReadMemoryBody body = new(window.Start, data.AsMemory(0, window.Readable), window.Unreadable == 0 ? null : window.Unreadable);
            await this.RespondAsync(request, body, DapJson.Default.ReadMemoryBody);
        } finally {
            ArrayPool<byte>.Shared.Return(data);
        }
    }

    private ValueTask OutputAsync(OutputCategory category, string text) {
        int dropped = Interlocked.Exchange(ref this._droppedLogs, 0);
        string output = dropped == 0 ? text : $"[{dropped} log lines omitted]\n{text}";
        return this._writer.EventAsync(EventName.Output, new OutputEvent(Names.OutputCategories.Name(category), output), DapJson.Default.OutputEvent);
    }

    private ValueTask RespondAsync<T>(Request request, T body, JsonTypeInfo<T> info) {
        return this._writer.RespondAsync(request.Seq, request.Command, body, info);
    }

    private static StackTraceBody StackTrace(ImmutableArray<Frame> frames, Request.StackTrace trace) {
        int start = (int)Math.Min(trace.StartFrame, (uint)frames.Length);
        int count = (int)Math.Min(Math.Min(trace.Levels == 0 ? MaxFrames : trace.Levels, MaxFrames), (uint)(frames.Length - start));
        StackFrame[] result = new StackFrame[count];
        for(int i = 0; i < count; i++) {
            Frame f = frames[start + i];
            result[i] = new StackFrame(FrameId.Of(start + i), f.Name ?? $"0x{f.Pc.Addr:X8}", f.Location is SourceLocation loc ? new Source(loc.Path) : null, f.Location?.Line ?? 0, 0, f.Pc);
        }
        return new StackTraceBody(result, frames.Length);
    }

    /// <summary>参照先のフレームのスコープを作り直して変数を返す</summary>
    private static Variable[] Variables(ImmutableArray<Frame> frames, VariablesReference reference) {
        if(reference.Target is not (int frame, int scope) || frames.Length <= frame) {
            return [];
        }
        ScopeView[] scopes = Mapping.Scopes(frames[frame], frame == 0);
        return scope < scopes.Length ? scopes[scope].Variables : [];
    }

    private static PseudoExpansionsBody PseudoExpansions(ImmutableArray<PseudoExpansion> pseudos) {
        return new([.. pseudos.Select(p => new PseudoExpansionLine(
            SourceLine.TryCreate(p.Line, out SourceLine line) ? line : throw new InvalidOperationException("A source line must be positive."),
            p.Mnemonic,
            [.. p.Instructions.Select(e => new PseudoExpansionInstruction(e.Address, e.Text ?? "?"))]))]);
    }
}
