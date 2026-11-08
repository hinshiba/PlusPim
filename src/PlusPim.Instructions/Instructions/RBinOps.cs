using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

public readonly struct Add: IRBinOp {
    public static string Mnemonic => "add";

    public static Effect Apply(uint lhs, uint rhs, ref uint result) {
        uint sum = unchecked(lhs + rhs);

        // 同符号の加算で結果の符号が反転したときだけオーバーフロー
        if(((lhs ^ sum) & (rhs ^ sum)) >> 31 != 0) {
            return new Effect(new Raise(ExcCode.Ov));
        }
        result = sum;
        return new Effect(new Next());
    }
}

public readonly struct Addu: IRBinOp {
    public static string Mnemonic => "addu";

    public static Effect Apply(uint lhs, uint rhs, ref uint result) {
        result = unchecked(lhs + rhs);
        return new Effect(new Next());
    }
}

public readonly struct Sub: IRBinOp {
    public static string Mnemonic => "sub";

    public static Effect Apply(uint lhs, uint rhs, ref uint result) {
        uint diff = unchecked(lhs - rhs);

        // 異符号の減算で結果の符号が被減数と異なるときだけオーバーフロー
        if(((lhs ^ rhs) & (lhs ^ diff)) >> 31 != 0) {
            return new Effect(new Raise(ExcCode.Ov));
        }
        result = diff;
        return new Effect(new Next());
    }
}

public readonly struct Subu: IRBinOp {
    public static string Mnemonic => "subu";

    public static Effect Apply(uint lhs, uint rhs, ref uint result) {
        result = unchecked(lhs - rhs);
        return new Effect(new Next());
    }
}

public readonly struct And: IRBinOp {
    public static string Mnemonic => "and";

    public static Effect Apply(uint lhs, uint rhs, ref uint result) {
        result = lhs & rhs;
        return new Effect(new Next());
    }
}

public readonly struct Or: IRBinOp {
    public static string Mnemonic => "or";

    public static Effect Apply(uint lhs, uint rhs, ref uint result) {
        result = lhs | rhs;
        return new Effect(new Next());
    }
}

public readonly struct Xor: IRBinOp {
    public static string Mnemonic => "xor";

    public static Effect Apply(uint lhs, uint rhs, ref uint result) {
        result = lhs ^ rhs;
        return new Effect(new Next());
    }
}

public readonly struct Nor: IRBinOp {
    public static string Mnemonic => "nor";

    public static Effect Apply(uint lhs, uint rhs, ref uint result) {
        result = ~(lhs | rhs);
        return new Effect(new Next());
    }
}

public readonly struct Slt: IRBinOp {
    public static string Mnemonic => "slt";

    public static Effect Apply(uint lhs, uint rhs, ref uint result) {
        result = unchecked((int)lhs < (int)rhs) ? 1u : 0u;
        return new Effect(new Next());
    }
}

public readonly struct Sltu: IRBinOp {
    public static string Mnemonic => "sltu";

    public static Effect Apply(uint lhs, uint rhs, ref uint result) {
        result = lhs < rhs ? 1u : 0u;
        return new Effect(new Next());
    }
}

// C# のシフトは int/uint で右オペランドの下位5bitのみを使うため MIPS と一致する.

public readonly struct Sllv: IRBinOp {
    public static string Mnemonic => "sllv";

    public static Effect Apply(uint lhs, uint rhs, ref uint result) {
        result = lhs << (int)rhs;
        return new Effect(new Next());
    }
}

public readonly struct Srlv: IRBinOp {
    public static string Mnemonic => "srlv";

    public static Effect Apply(uint lhs, uint rhs, ref uint result) {
        result = lhs >> (int)rhs;
        return new Effect(new Next());
    }
}

public readonly struct Srav: IRBinOp {
    public static string Mnemonic => "srav";

    public static Effect Apply(uint lhs, uint rhs, ref uint result) {
        result = unchecked((uint)((int)lhs >> (int)rhs));
        return new Effect(new Next());
    }
}
