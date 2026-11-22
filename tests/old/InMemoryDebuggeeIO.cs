using PlusPim.Application;
using System.Text;

namespace PlusPimTests;

/// <summary>
/// テスト用のメモリ上のデバッギ入出力．入力は <see cref="SetInput(byte[])"/> で与え，出力は <see cref="OutputBytes"/> から読む
/// </summary>
internal sealed class InMemoryDebuggeeIO: IDebuggeeIO {
    private readonly List<byte> _output = [];
    private byte[] _input = [];
    private int _inputPosition;

    /// <summary>
    /// 書き出されたバイト列
    /// </summary>
    public byte[] OutputBytes => [.. this._output];

    /// <summary>
    /// 書き出されたバイト列をUTF-8として復号した文字列
    /// </summary>
    public string Output => Encoding.UTF8.GetString(this.OutputBytes);

    /// <summary>
    /// まだ読まれていない入力
    /// </summary>
    public byte[] RemainingInput => this._input[this._inputPosition..];

    /// <summary>
    /// 入力を文字列のUTF-8表現で置き換える．読み位置は先頭に戻る
    /// </summary>
    public void SetInput(string utf8) {
        this.SetInput(Encoding.UTF8.GetBytes(utf8));
    }

    /// <summary>
    /// 入力をバイト列で置き換える．読み位置は先頭に戻る
    /// </summary>
    public void SetInput(params byte[] bytes) {
        this._input = bytes;
        this._inputPosition = 0;
    }

    public void Write(ReadOnlySpan<byte> data) {
        this._output.AddRange(data);
    }

    public int ReadByte() {
        return this._inputPosition < this._input.Length ? this._input[this._inputPosition++] : -1;
    }
}
