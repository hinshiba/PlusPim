namespace PlusPim.Instructions.Instructions;

public readonly struct Mfhi: IMoveOp {
    public static string Mnemonic => "mfhi";
    public static RegisterName Dest(GprId r) { return r; }
    public static RegisterName Src(GprId r) { return RName.Hi; }
}

public readonly struct Mflo: IMoveOp {
    public static string Mnemonic => "mflo";
    public static RegisterName Dest(GprId r) { return r; }
    public static RegisterName Src(GprId r) { return RName.Lo; }
}

public readonly struct Mthi: IMoveOp {
    public static string Mnemonic => "mthi";
    public static RegisterName Dest(GprId r) { return RName.Hi; }
    public static RegisterName Src(GprId r) { return r; }
}

public readonly struct Mtlo: IMoveOp {
    public static string Mnemonic => "mtlo";
    public static RegisterName Dest(GprId r) { return RName.Lo; }
    public static RegisterName Src(GprId r) { return r; }
}
