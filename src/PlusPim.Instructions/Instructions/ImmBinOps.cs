namespace PlusPim.Instructions.Instructions;

// 演算は即値を拡張すれば R形式と同一なので RBinOps へ委譲する.

public readonly struct Addi: IImmBinOp {
    public static string Mnemonic => "addi";
    public static uint? Extend(Immediate imm) { return imm.SignExtended; }
    public static Effect Apply(uint lhs, uint rhs, ref uint result) { return Add.Apply(lhs, rhs, ref result); }
}

public readonly struct Addiu: IImmBinOp {
    public static string Mnemonic => "addiu";
    public static uint? Extend(Immediate imm) { return imm.SignExtended; }
    public static Effect Apply(uint lhs, uint rhs, ref uint result) { return Addu.Apply(lhs, rhs, ref result); }
}

public readonly struct Andi: IImmBinOp {
    public static string Mnemonic => "andi";
    public static uint? Extend(Immediate imm) { return imm.ZeroExtended; }
    public static Effect Apply(uint lhs, uint rhs, ref uint result) { return And.Apply(lhs, rhs, ref result); }
}

public readonly struct Ori: IImmBinOp {
    public static string Mnemonic => "ori";
    public static uint? Extend(Immediate imm) { return imm.ZeroExtended; }
    public static Effect Apply(uint lhs, uint rhs, ref uint result) { return Or.Apply(lhs, rhs, ref result); }
}

public readonly struct Xori: IImmBinOp {
    public static string Mnemonic => "xori";
    public static uint? Extend(Immediate imm) { return imm.ZeroExtended; }
    public static Effect Apply(uint lhs, uint rhs, ref uint result) { return Xor.Apply(lhs, rhs, ref result); }
}

public readonly struct Slti: IImmBinOp {
    public static string Mnemonic => "slti";
    public static uint? Extend(Immediate imm) { return imm.SignExtended; }
    public static Effect Apply(uint lhs, uint rhs, ref uint result) { return Slt.Apply(lhs, rhs, ref result); }
}

/// <remarks>MIPSの仕様通り, 符号拡張した即値と符号なしで比較する</remarks>
public readonly struct Sltiu: IImmBinOp {
    public static string Mnemonic => "sltiu";
    public static uint? Extend(Immediate imm) { return imm.SignExtended; }
    public static Effect Apply(uint lhs, uint rhs, ref uint result) { return Sltu.Apply(lhs, rhs, ref result); }
}

public readonly struct Sll: IImmBinOp {
    public static string Mnemonic => "sll";
    public static uint? Extend(Immediate imm) { return imm.Shamt; }
    public static Effect Apply(uint lhs, uint rhs, ref uint result) { return Sllv.Apply(lhs, rhs, ref result); }
}

public readonly struct Srl: IImmBinOp {
    public static string Mnemonic => "srl";
    public static uint? Extend(Immediate imm) { return imm.Shamt; }
    public static Effect Apply(uint lhs, uint rhs, ref uint result) { return Srlv.Apply(lhs, rhs, ref result); }
}

public readonly struct Sra: IImmBinOp {
    public static string Mnemonic => "sra";
    public static uint? Extend(Immediate imm) { return imm.Shamt; }
    public static Effect Apply(uint lhs, uint rhs, ref uint result) { return Srav.Apply(lhs, rhs, ref result); }
}
