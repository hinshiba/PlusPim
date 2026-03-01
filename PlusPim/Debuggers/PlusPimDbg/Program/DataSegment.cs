using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// .dataセグメントを表す
/// </summary>
internal sealed class DataSegment(Dictionary<Address, byte> memoryImage) {
    public static readonly Address DataSegmentBase = new(0x10000000);
    /// <summary>
    /// アドレス→バイト値のメモリイメージ
    /// </summary>
    public Dictionary<Address, byte> MemoryImage { get; } = new Dictionary<Address, byte>(memoryImage);
}
