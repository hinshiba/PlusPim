using System.Buffers;
using System.Text;

namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// デバッギの標準出力．print_int/print_string/print_char が書くバイト列を1つの UTF-8 のストリームとして復号し，<see cref="Console.Out"/> に書く
/// </summary>
/// <remarks>
/// 実行(<see cref="RuntimeContext"/>)ごとに1つ持つ．
/// 復号途中の多バイト文字のバイト列 (0 から 3 バイト) を状態として持ち，StepBack のために取得・復元できる．
/// ステップ実行は単一スレッドで行われるため，同期していない
/// </remarks>
internal sealed class DebuggeeOutput {
    private byte[] _pending = [];

    /// <summary>
    /// 出力を待っている，途中までの多バイト文字のバイト列 (0 から 3 バイト)
    /// </summary>
    public ReadOnlySpan<byte> Pending => this._pending;

    /// <summary>
    /// バイト列を書く．完成した文字は出力し，途中までの多バイト文字は次の書き込みまで保持する
    /// </summary>
    /// <remarks>不正なバイト列は，最大の不正な部分列ごとに U+FFFD として出力する</remarks>
    public void Write(ReadOnlySpan<byte> bytes) {
        byte[] data = [.. this._pending, .. bytes];
        StringBuilder text = new();
        Span<char> chars = stackalloc char[2];
        int index = 0;
        while(index < data.Length) {
            OperationStatus status = Rune.DecodeFromUtf8(data.AsSpan(index), out Rune rune, out int consumed);
            if(status == OperationStatus.NeedMoreData) {
                // 途中までの多バイト文字．続きのバイトを待つ
                break;
            }
            if(status == OperationStatus.Done) {
                _ = text.Append(chars[..rune.EncodeToUtf16(chars)]);
            } else {
                // InvalidData: 最大の不正な部分列を U+FFFD にする
                _ = text.Append((char)Rune.ReplacementChar.Value);
            }
            index += consumed;
        }
        this._pending = data[index..];

        if(text.Length > 0) {
            // サロゲートペアを分けないように，まとめて書く
            Console.Write(text.ToString());
        }
    }

    /// <summary>
    /// 途中までの多バイト文字があれば，U+FFFD を1つ出力して捨てる
    /// </summary>
    public void Flush() {
        if(this._pending.Length > 0) {
            this._pending = [];
            Console.Write((char)Rune.ReplacementChar.Value);
        }
    }

    /// <summary>
    /// 途中までの多バイト文字のバイト列を取得する (Undo用)
    /// </summary>
    public byte[] CapturePending() {
        return [.. this._pending];
    }

    /// <summary>
    /// 途中までの多バイト文字のバイト列を復元する (Undo用)．出力済みの文字は消えない
    /// </summary>
    public void RestorePending(byte[] pending) {
        this._pending = [.. pending];
    }
}
