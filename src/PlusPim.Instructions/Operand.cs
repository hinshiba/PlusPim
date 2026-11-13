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

public readonly record struct Memory(GprId BaseRegId, Immediate Offset);

public readonly union Operand(GprId, Immediate, Memory, Address);
