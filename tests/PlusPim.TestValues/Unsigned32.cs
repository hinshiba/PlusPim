using System.Collections.Frozen;

namespace PlusPim.TestValues;

/// <summary>
/// 符号なし32bit値 (docs/tests/instructions/values.md)
/// </summary>
public static class Unsigned32 {
    /// <summary>符号付き32bit値 ∪ {範囲の上限の隣接値}</summary>
    public static readonly IReadOnlySet<uint> All = Signed32.All.Append(0xfffffffe).ToFrozenSet();
}
