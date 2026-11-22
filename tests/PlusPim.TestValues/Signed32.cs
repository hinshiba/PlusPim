using System.Collections.Frozen;

namespace PlusPim.TestValues;

/// <summary>
/// 符号付き32bit値 (docs/tests/instructions/values.md)
/// </summary>
public static class Signed32 {
    /// <summary>代表値: 正, 負, ゼロ</summary>
    public static readonly IReadOnlySet<uint> Representative = new uint[] { 0x000cafe0, 0x800babe0, 0x00000000 }.ToFrozenSet();

    /// <summary>境界値: INT_MIN, -1, 0, 1, INT_MAX</summary>
    public static readonly IReadOnlySet<uint> Boundary = new uint[] { 0x80000000, 0xffffffff, 0x00000000, 0x00000001, 0x7fffffff }.ToFrozenSet();

    /// <summary>準境界値: 境界値の隣接値</summary>
    public static readonly IReadOnlySet<uint> NearBoundary = new uint[] { 0x7ffffffe, 0x80000001 }.ToFrozenSet();

    /// <summary>代表値 ∪ 境界値 ∪ 準境界値</summary>
    public static readonly IReadOnlySet<uint> All = Representative.Union(Boundary).Union(NearBoundary).ToFrozenSet();
}
