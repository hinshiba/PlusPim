using PlusPim.Application;

namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// 標準入力のうち消費されずに残っているバイトを管理する
/// </summary>
internal sealed class PendingInput(IDebuggeeIO io) {
    /// <summary>
    /// 戻されたバイト．先頭(Pop)が次に読むバイト
    /// </summary>
    private readonly Stack<byte> _front = new();

    /// <summary>
    /// 1バイト読む．EOFなら-1
    /// </summary>
    public int ReadByte() {
        return this._front.Count > 0 ? this._front.Pop() : io.ReadByte();
    }

    /// <summary>
    /// 改行を含む1行を読む．EOFなら <c>null</c>
    /// </summary>
    public byte[]? ReadLine() {
        List<byte> line = [];
        int b;
        while((b = this.ReadByte()) >= 0) {
            line.Add((byte)b);
            if(b == '\n') {
                break;
            }
        }
        return line.Count == 0 ? null : [.. line];
    }

    /// <summary>
    /// 読み取り位置の手前にバイト列を戻す．<paramref name="bytes"/> の先頭が次に読まれる
    /// </summary>
    public void PushFront(ReadOnlySpan<byte> bytes) {
        for(int i = bytes.Length - 1; i >= 0; i--) {
            this._front.Push(bytes[i]);
        }
    }

    /// <summary>
    /// 戻されているバイトを読む順に返す
    /// </summary>
    public byte[] ToArray() {
        return this._front.ToArray();
    }
}
