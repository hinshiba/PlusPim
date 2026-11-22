using System.Collections.Frozen;

namespace PlusPim.TestValues;

/// <summary>
/// 符号付き16bit値 (docs/tests/instructions/values.md)
/// </summary>
/// <remarks>値は即値のビット列そのものである</remarks>
public static class Signed16 {
    /// <summary>代表値: 正, 負, ゼロ</summary>
    public static readonly IReadOnlySet<ushort> Representative = new ushort[] { 0x0afe, 0xbabe, 0x0000 }.ToFrozenSet();

    /// <summary>境界値: -32768, -1, 0, 1, 32767</summary>
    public static readonly IReadOnlySet<ushort> Boundary = new ushort[] { 0x8000, 0xffff, 0x0000, 0x0001, 0x7fff }.ToFrozenSet();

    /// <summary>準境界値: 境界値の隣接値</summary>
    public static readonly IReadOnlySet<ushort> NearBoundary = new ushort[] { 0x7ffe, 0x8001 }.ToFrozenSet();

    /// <summary>代表値 ∪ 境界値 ∪ 準境界値</summary>
    public static readonly IReadOnlySet<ushort> All = Representative.Union(Boundary).Union(NearBoundary).ToFrozenSet();
}
