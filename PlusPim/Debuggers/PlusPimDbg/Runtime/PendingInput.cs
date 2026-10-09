using System.Text;

namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// 標準入力のうち，消費されずに残っている入力の UTF-8 のバイト列．read_int/read_char/read_stringで共有する
/// </summary>
/// <remarks>
/// 実行(<see cref="RuntimeContext"/>)ごとに1つ持つ．
/// ステップ実行は単一スレッドで行われるため，同期していない
/// </remarks>
internal sealed class PendingInput {
    /// <summary>
    /// EOF を表す <see cref="ReadChar"/> の戻り値．バイトの値 (0 から 255) と区別できる
    /// </summary>
    public const int Eof = -1;

    private byte[] _pending = [];

    /// <summary>
    /// 標準入力から読み込んだが，まだ消費されていない入力
    /// </summary>
    /// <remarks>標準入力に残っていてまだ読み込んでいない入力は含まない (端末では先読みするとブロックするため)</remarks>
    public ReadOnlySpan<byte> Buffered => this._pending;

    /// <summary>
    /// 1バイト読む．EOFなら <see cref="Eof"/>
    /// </summary>
    /// <remarks>入力バッファが空なら，標準入力から1文字を読み UTF-8 に変換して入力バッファに置く</remarks>
    public int ReadChar() {
        if(this._pending.Length == 0 && !this.FillOneChar()) {
            return Eof;
        }
        byte b = this._pending[0];
        this._pending = this._pending[1..];
        return b;
    }

    /// <summary>
    /// 改行を含む1行のバイト列を読む．何も読めない(EOF)なら空
    /// </summary>
    public byte[] ReadLine() {
        List<byte> line = [];
        int b;
        while((b = this.ReadChar()) != Eof) {
            line.Add((byte)b);
            if(b == '\n') {
                break;
            }
        }
        return [.. line];
    }

    /// <summary>
    /// 読み取り位置の手前にバイト列を戻す
    /// </summary>
    public void PushFront(ReadOnlySpan<byte> bytes) {
        this._pending = [.. bytes, .. this._pending];
    }

    /// <summary>
    /// 未消費の入力を置き換える (テストでの状態の書き戻し用)
    /// </summary>
    public void Restore(ReadOnlySpan<byte> buffered) {
        this._pending = buffered.ToArray();
    }

    /// <summary>
    /// 標準入力から1文字 (サロゲートペアは2つ) を読み，UTF-8 のバイト列として入力バッファに置く
    /// </summary>
    /// <returns>EOF なら <see langword="false"/></returns>
    private bool FillOneChar() {
        int c = Console.In.Read();
        if(c < 0) {
            return false;
        }
        string text = ((char)c).ToString();
        if(char.IsHighSurrogate((char)c)) {
            int low = Console.In.Read();
            if(low >= 0) {
                text += (char)low;
            }
        }
        // 対になっていないサロゲートは U+FFFD になる
        this._pending = [.. this._pending, .. Encoding.UTF8.GetBytes(text)];
        return true;
    }
}
