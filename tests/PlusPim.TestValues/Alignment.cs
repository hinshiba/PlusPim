using System.Collections.Frozen;

namespace PlusPim.TestValues;

/// <summary>
/// 実効アドレスのアライメント (docs/tests/instructions/values.md)
/// </summary>
public static class Alignment {
    /// <summary>実効アドレスの下位2bitのパターン</summary>
    public static readonly IReadOnlySet<uint> LowBits = new uint[] { 0b00, 0b01, 0b10, 0b11 }.ToFrozenSet();
}
