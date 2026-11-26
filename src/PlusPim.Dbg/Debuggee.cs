using PlusPim.Image;
using System.Buffers;

namespace PlusPim.Dbg;

/// <summary>
/// デバッギの実行
/// </summary>
public sealed class Debuggee {
    private readonly ProgramImage _image;
    private readonly DelaySlotsConfig _slots;
    private readonly InputTape _input = new();

    /// <summary>現在の状態</summary>
    internal State State { get; set; }

    /// <summary>現在の状態のビュー</summary>
    public StateView View => new(this.State);

    private Debuggee(ProgramImage image, DelaySlotsConfig slots, State initial) {
        this._image = image;
        this._slots = slots;
        this.State = initial;
    }

    /// <summary>プログラムイメージを読み込む</summary>
    /// <param name="image">読み込むプログラムイメージ</param>
    /// <param name="slots">有効にする遅延スロットの種類</param>
    public static Debuggee Load(ProgramImage image, DelaySlotsConfig slots) {
        State s = Loader.Initial(image);
        s.Memory.Commit();
        return new Debuggee(image, slots, s);
    }

    /// <summary>
    /// 1ステップ進める
    /// </summary>
    /// <param name="output">デバッギの出力先</param>
    /// <remarks>例外の発生, 例外ハンドラへの遷移は1ステップである.</remarks>
    public StepResult Step(IBufferWriter<byte> output) {
        return Executor.Step(this.State, this._image, this._slots, this._input, output);
    }

    /// <summary>入力を末尾に加える. 戻っても入力は失われない</summary>
    public void Feed(ReadOnlySpan<byte> input) {
        this._input.Append(input);
    }

    /// <summary>EOFを入力する</summary>
    public void CloseInput() {
        this._input.Close();
    }
}
