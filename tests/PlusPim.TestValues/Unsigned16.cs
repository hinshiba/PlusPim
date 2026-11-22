using System.Collections.Frozen;

namespace PlusPim.TestValues;

/// <summary>
/// 符号なし16bit値 (docs/tests/instructions/values.md)
/// </summary>
/// <remarks>値は即値のビット列そのものである</remarks>
public static class Unsigned16 {
    /// <summary>符号付き16bit値 ∪ {範囲の上限の隣接値}</summary>
    public static readonly IReadOnlySet<ushort> All = Signed16.All.Append((ushort)0xfffe).ToFrozenSet();
}
