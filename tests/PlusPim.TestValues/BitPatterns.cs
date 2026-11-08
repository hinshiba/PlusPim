using System.Collections.Frozen;

namespace PlusPim.TestValues;

/// <summary>
/// ビットパターン (docs/tests/instructions/values.md)
/// </summary>
public static class BitPatterns {
    /// <summary>全0, 全1, 交互, バイト単位</summary>
    public static readonly IReadOnlySet<uint> All = new uint[] { 0x00000000, 0xffffffff, 0xaaaaaaaa, 0x55555555, 0xff00ff00, 0x00ff00ff }.ToFrozenSet();
}
