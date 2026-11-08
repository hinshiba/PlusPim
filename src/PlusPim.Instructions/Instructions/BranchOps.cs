using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

public readonly struct Beq: IBranchOp {
    public static string Mnemonic => "beq";
    public static bool Taken(uint rs, uint rt) { return rs == rt; }
}

public readonly struct Bne: IBranchOp {
    public static string Mnemonic => "bne";
    public static bool Taken(uint rs, uint rt) { return rs != rt; }
}

public readonly struct Bgez: IBranchZOp {
    public static string Mnemonic => "bgez";
    public static bool Taken(int rs) { return rs >= 0; }
}

public readonly struct Bgtz: IBranchZOp {
    public static string Mnemonic => "bgtz";
    public static bool Taken(int rs) { return rs > 0; }
}

public readonly struct Blez: IBranchZOp {
    public static string Mnemonic => "blez";
    public static bool Taken(int rs) { return rs <= 0; }
}

public readonly struct Bltz: IBranchZOp {
    public static string Mnemonic => "bltz";
    public static bool Taken(int rs) { return rs < 0; }
}
