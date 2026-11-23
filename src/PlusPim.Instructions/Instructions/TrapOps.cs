using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

public readonly struct Teq: ITrapOp {
    public static string Mnemonic => "teq";
    public static string ImmMnemonic => "teqi";
    public static bool Taken(uint rs, uint rt) { return rs == rt; }
}

public readonly struct Tne: ITrapOp {
    public static string Mnemonic => "tne";
    public static string ImmMnemonic => "tnei";
    public static bool Taken(uint rs, uint rt) { return rs != rt; }
}

public readonly struct Tge: ITrapOp {
    public static string Mnemonic => "tge";
    public static string ImmMnemonic => "tgei";
    public static bool Taken(uint rs, uint rt) { return unchecked((int)rt <= (int)rs); }
}

public readonly struct Tgeu: ITrapOp {
    public static string Mnemonic => "tgeu";
    public static string ImmMnemonic => "tgeiu";
    public static bool Taken(uint rs, uint rt) { return rt <= rs; }
}

public readonly struct Tlt: ITrapOp {
    public static string Mnemonic => "tlt";
    public static string ImmMnemonic => "tlti";
    public static bool Taken(uint rs, uint rt) { return unchecked((int)rs < (int)rt); }
}

public readonly struct Tltu: ITrapOp {
    public static string Mnemonic => "tltu";
    public static string ImmMnemonic => "tltiu";
    public static bool Taken(uint rs, uint rt) { return rs < rt; }
}
