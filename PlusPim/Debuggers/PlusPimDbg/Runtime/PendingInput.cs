namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// 標準入力のうち，消費されずに残っている文字．read_int/read_char/read_stringで共有する
/// </summary>
/// <remarks>
/// 実行(<see cref="RuntimeContext"/>)ごとに1つ持つ．
/// ステップ実行は単一スレッドで行われるため，同期していない
/// </remarks>
internal sealed class PendingInput {
    private string _pending = "";

    /// <summary>
    /// 標準入力から読み込んだが，まだ消費されていない文字列
    /// </summary>
    /// <remarks>標準入力に残っていてまだ読み込んでいない入力は含まない (端末では先読みするとブロックするため)</remarks>
    public string Buffered => this._pending;

    /// <summary>
    /// 1文字読む．EOFなら-1
    /// </summary>
    public int ReadChar() {
        if(this._pending.Length == 0) {
            return Console.In.Read();
        }
        char c = this._pending[0];
        this._pending = this._pending[1..];
        return c;
    }

    /// <summary>
    /// 改行を含む1行を読む．何も読めない(EOF)なら <c>null</c>
    /// </summary>
    public string? ReadLine() {
        System.Text.StringBuilder sb = new();
        int c;
        while((c = this.ReadChar()) >= 0) {
            sb.Append((char)c);
            if(c == '\n') {
                break;
            }
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    /// <summary>
    /// 読み取り位置の手前に文字列を戻す
    /// </summary>
    public void PushFront(string text) {
        this._pending = text + this._pending;
    }

    /// <summary>
    /// 未消費の入力を置き換える (テストでの状態の書き戻し用)
    /// </summary>
    public void Restore(string buffered) {
        this._pending = buffered;
    }
}
