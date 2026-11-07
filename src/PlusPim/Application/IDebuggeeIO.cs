namespace PlusPim.Application;

/// <summary>
/// デバッギの標準入出力を行うインターフェース
/// </summary>
internal interface IDebuggeeIO {
    /// <summary>
    /// バイト列をそのまま出力する
    /// </summary>
    void Write(ReadOnlySpan<byte> data);

    /// <summary>
    /// 1バイト読む．EOFなら-1．入力が来るまでブロックする
    /// </summary>
    int ReadByte();
}
