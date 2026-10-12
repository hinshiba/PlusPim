namespace PlusPim.Application;

/// <summary>
/// ストリームを介したデバッギの標準入出力
/// </summary>
internal sealed class StreamDebuggeeIO(Stream input, Stream output): IDebuggeeIO {
    /// <summary>
    /// プロセスの標準入出力につなぐ
    /// </summary>
    public static StreamDebuggeeIO OpenConsole() {
        return new StreamDebuggeeIO(Console.OpenStandardInput(), Console.OpenStandardOutput());
    }

    public void Write(ReadOnlySpan<byte> data) {
        output.Write(data);
        // 対話的な入出力なのですぐflushする
        output.Flush();
    }

    public int ReadByte() {
        // 先読みして入力を取り込みすぎないように1バイトずつ読む
        Span<byte> buffer = stackalloc byte[1];
        return input.Read(buffer) == 0 ? -1 : buffer[0];
    }
}
