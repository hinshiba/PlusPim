using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// .dataセグメントを表す
/// </summary>
internal sealed class DataSegment(Dictionary<Address, byte> memoryImage, Address addr, uint size) {

    /// <summary>
    /// データセグメントのベースアドレス
    /// </summary>
    public static readonly Address DataSegmentBase = new(0x10000000);

    /// <summary>
    /// このインスタンスのベースアドレス
    /// </summary>
    public readonly Address BaseAddress = addr;

    /// <summary>
    /// データセグメントのバイト数（.spaceによる空き領域を含む）
    /// </summary>
    public readonly uint Size = size;

    /// <summary>
    /// アドレス→バイト値のメモリイメージ
    /// </summary>
    public Dictionary<Address, byte> MemoryImage { get; } = new Dictionary<Address, byte>(memoryImage);
}
