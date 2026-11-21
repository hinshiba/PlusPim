using PlusPim.Image;
using System.Buffers;
using System.Diagnostics;

namespace PlusPim.Dbg;

/// <summary>
/// デバッギの実行
/// </summary>
public sealed class Debuggee {

    /// <summary>チェックポイントの間隔</summary>
    private const int Interval = 4096;
    private readonly ProgramImage _image;
    private readonly InputTape _input = new();

    /// <summary><see cref="Interval"/> ステップごとの状態</summary>
    /// <remarks>書き換えないこと. 0番目は初期状態.</remarks>
    private readonly List<State> _checkpoints;
    private State _state;
    /// <summary>デバッギの出力先</summary>
    private readonly IBufferWriter<byte> _output;

    /// <summary>到達した最大のステップ数</summary>
    private long _frontier;

    /// <summary>ステップ数</summary>
    public long StepCount { get; private set; }

    /// <summary>現在の状態のビュー</summary>
    public StateView View => new(this._state);

    private Debuggee(ProgramImage image, State initial, IBufferWriter<byte> output) {
        this._image = image;
        this._state = initial;
        this._checkpoints = [initial.Fork()];
        this._output = output;
    }

    /// <summary>プログラムイメージを読み込む</summary>
    /// <param name="image">読み込むプログラムイメージ</param>
    /// <param name="output">デバッギの出力先</param>
    public static Debuggee Load(ProgramImage image, IBufferWriter<byte> output) {
        return new Debuggee(image, Loader.Initial(image), output);
    }

    /// <summary>
    /// 1ステップ進める
    /// </summary>
    /// <remarks>
    /// 例外の発生, 例外ハンドラへの遷移は1ステップである.
    /// </remarks>
    public StepResult Step() {
        bool fresh = this._frontier <= this.StepCount;
        StepResult r = Executor.Step(this._state, this._image, this._input, fresh ? this._output : Discard.Instance);
        if(r == StepResult.Stepped) {
            this.StepCount++;
            if(fresh) {
                this._frontier = this.StepCount;
                if(this.StepCount % Interval == 0) {
                    this._checkpoints.Add(this._state.Fork());
                }
            }
        }
        return r;
    }

    // TODO: Stepとの非対称性がある
    /// <summary>
    /// 少なくとも1ステップ戻り, <paramref name="until"/> が成り立つ最も近い過去の位置で止まる. 成り立つ位置がなければ位置0で止まる
    /// </summary>
    /// <param name="until">戻った先の各位置で評価する. <see langword="null"/> なら1ステップだけ戻る</param>
    /// <param name="ct">取り消されたら探索を終えた区間の先頭で止まる</param>
    /// <returns>位置が0で戻れなかったら <see langword="false"/></returns>
    /// <remarks>区間ごとに前向きに再生して探すので, 戻る距離 N に対して O(N) である</remarks>
    public bool Back(Func<StateView, bool>? until = null, CancellationToken ct = default) {
        if(this.StepCount == 0) {
            return false;
        }
        long target = this.StepCount - 1;
        if(until is null) {
            this.Seek(target);
            return true;
        }

        for(long seg = target / Interval; seg >= 0; seg--) {
            if(ct.IsCancellationRequested) {
                this.Seek(Math.Min(target, (seg + 1) * Interval));
                return true;
            }
            long start = seg * Interval;
            long end = Math.Min(target, start + Interval - 1);
            State s = this._checkpoints[(int)seg].Fork();
            long hit = -1;
            for(long p = start; ; p++) {
                if(until(new StateView(s))) {
                    hit = p;
                }
                if(p == end) {
                    break;
                }
                this.Replay(s);
            }
            if(hit >= 0) {
                this.Seek(hit);
                return true;
            }
        }
        this.Seek(0);
        return true;
    }

    /// <summary>入力を末尾に加える. 戻っても入力は失われない</summary>
    /// <exception cref="InvalidOperationException"><see cref="CloseInput"/> の後に書き込んだ場合</exception>
    public void Feed(ReadOnlySpan<byte> input) {
        this._input.Append(input);
    }

    /// <summary>EOFを入力する</summary>
    public void CloseInput() {
        this._input.Close();
    }

    private void Seek(long position) {
        State s = this._checkpoints[(int)(position / Interval)].Fork();
        for(long i = position % Interval; i > 0; i--) {
            this.Replay(s);
        }
        this._state = s;
        this.StepCount = position;
    }

    private void Replay(State s) {
        if(Executor.Step(s, this._image, this._input, Discard.Instance) != StepResult.Stepped) {
            throw new UnreachableException("Replay diverged from the recorded execution.");
        }
    }
}
