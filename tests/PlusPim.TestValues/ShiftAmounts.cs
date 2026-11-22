using System.Collections.Frozen;

namespace PlusPim.TestValues;

/// <summary>
/// シフト量 (docs/tests/instructions/values.md)
/// </summary>
public static class ShiftAmounts {
    /// <summary>即値のシフト量</summary>
    public static readonly IReadOnlySet<uint> Immediate = new uint[] { 0, 1, 15, 16, 31 }.ToFrozenSet();

    /// <summary>レジスタのシフト量: 即値のシフト量 ∪ 5bit を超える値</summary>
    public static readonly IReadOnlySet<uint> Register = Immediate.Union<uint>([32, 0xff, 0xffffffff]).ToFrozenSet();
}
