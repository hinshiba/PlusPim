using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

public readonly struct Mult: IHiLoOp {
    public static string Mnemonic => "mult";

    public static Effect Apply(uint lhs, uint rhs, ref uint hi, ref uint lo) {
        ulong product = unchecked((ulong)((long)(int)lhs * (int)rhs));
        hi = (uint)(product >> 32);
        lo = (uint)product;
        return new Effect(new Next());
    }
}

public readonly struct Multu: IHiLoOp {
    public static string Mnemonic => "multu";

    public static Effect Apply(uint lhs, uint rhs, ref uint hi, ref uint lo) {
        ulong product = (ulong)lhs * rhs;
        hi = (uint)(product >> 32);
        lo = (uint)product;
        return new Effect(new Next());
    }
}

// MIPS では結果が未定義だが, ランタイムエラーにする.
// C# の除算はゼロ方向へ丸めるので, 商と剰余の符号は MIPS と一致する.

public readonly struct Div: IHiLoOp {
    public static string Mnemonic => "div";

    public static Effect Apply(uint lhs, uint rhs, ref uint hi, ref uint lo) {
        if(rhs == 0) {
            return new Effect(new Fail(RuntimeErrorKind.DivisionByZero));
        }
        if(lhs == 0x80000000 && rhs == 0xffffffff) {
            return new Effect(new Fail(RuntimeErrorKind.DivisionOverflow));
        }
        (int q, int r) = Math.DivRem(unchecked((int)lhs), unchecked((int)rhs));
        hi = unchecked((uint)r);
        lo = unchecked((uint)q);
        return new Effect(new Next());
    }
}

public readonly struct Divu: IHiLoOp {
    public static string Mnemonic => "divu";

    public static Effect Apply(uint lhs, uint rhs, ref uint hi, ref uint lo) {
        if(rhs == 0) {
            return new Effect(new Fail(RuntimeErrorKind.DivisionByZero));
        }
        (lo, hi) = Math.DivRem(lhs, rhs);
        return new Effect(new Next());
    }
}
