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

    /// <summary>
    /// 1ステップ戻る
    /// </summary>
    /// <returns>位置が0で戻れなかったら <see langword="false"/></returns>
    /// <remarks>
    /// 直前のチェックポイントから前向きに再生するので, 最大 <see cref="Interval"/> ステップかかる
    /// TODO: 直近Intervalは密な状態を持つように変更する
    /// </remarks>
    public bool Back() {
        if(this.StepCount == 0) {
            return false;
        }
        this.StepCount--;
        State s = this._checkpoints[(int)(this.StepCount / Interval)].Fork();
        for(long i = this.StepCount % Interval; 0 < i; i--) {
            if(Executor.Step(s, this._image, this._input, Discard.Instance) != StepResult.Stepped) {
                throw new UnreachableException("Replay diverged from the recorded execution.");
            }
        }
        this._state = s;
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
}
