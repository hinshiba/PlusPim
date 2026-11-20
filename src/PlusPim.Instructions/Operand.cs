using System.Globalization;

namespace PlusPim.Instructions;

/// <summary>
/// 16bit即値のビット列. 符号の解釈は命令が選ぶ
/// </summary>
/// <param name="Bits">ビット列</param>
public readonly record struct Immediate(ushort Bits) {
    public uint SignExtended => unchecked((uint)(short)this.Bits);
    public uint ZeroExtended => this.Bits;

    /// <summary>シフト量として解釈した値. 5bitに収まらなければ <see langword="null"/></summary>
    public uint? Shamt => this.Bits <= 31 ? this.Bits : null;
}

/// <summary>
/// 即値の解釈. 32bitへの拡張と表記の基数を決める
/// </summary>
public enum ImmKind: byte {
    /// <summary>符号拡張. 10進で表記する</summary>
    Signed,
    /// <summary>ゼロ拡張したビット列. 16進で表記する</summary>
    Zero,
    /// <summary>5bitのシフト量. 10進で表記する</summary>
    Shamt,
}

public static class ImmKinds {
    /// <summary>即値を32bit値へ拡張する. 解釈に合わない即値なら <see langword="null"/></summary>
    public static uint? Extend(this ImmKind kind, Immediate imm) {
        return kind switch {
            ImmKind.Signed => imm.SignExtended,
            ImmKind.Zero => imm.ZeroExtended,
            _ => imm.Shamt,
        };
    }

    /// <summary><see cref="Extend"/> した値の表記</summary>
    public static string Format(this ImmKind kind, uint extended) {
        return kind switch {
            ImmKind.Signed => unchecked((int)extended).ToString(CultureInfo.InvariantCulture),
            ImmKind.Zero => "0x" + extended.ToString("x", CultureInfo.InvariantCulture),
            _ => extended.ToString(CultureInfo.InvariantCulture),
        };
    }
}

public readonly record struct Memory(GprId BaseRegId, Immediate Offset);

public readonly union Operand(GprId, Immediate, Memory, Address);
