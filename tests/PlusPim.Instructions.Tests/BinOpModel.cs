using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Tests;

/// <summary><see cref="IRBinOp.Apply"/> と <see cref="IImmBinOp.Apply"/> の共通の形</summary>
public delegate Effect ApplyFn(uint lhs, uint rhs, ref uint result);

/// <summary>演算の観測可能な結果</summary>
public abstract record Outcome;

/// <summary>result へ <paramref name="Value"/> を書きこんだ</summary>
public sealed record Written(uint Value): Outcome;

/// <summary>例外 <paramref name="Code"/> を発生させた</summary>
/// <param name="Touched">result を書き換えたか. <see cref="Raise"/> の保証により常に false であるべき</param>
public sealed record Raised(ExcCode Code, bool Touched): Outcome;

/// <summary>命令が即値を受け付けなかった</summary>
public sealed record Rejected: Outcome;

/// <summary>
/// 2項演算の観測と, 実装に依存しない参照モデルの部品
/// </summary>
public static class BinOpModel {
    /// <summary>例外時に result が書き換わらないことを検出するための初期値</summary>
    private const uint Sentinel = 0xdeadbeef;

    /// <summary><paramref name="apply"/> を実行して結果を観測する</summary>
    public static Outcome Observe(ApplyFn apply, uint lhs, uint rhs) {
        uint result = Sentinel;
        Effect effect = apply(lhs, rhs, ref result);
        return effect.TryGetValue(out Raise raise)
            ? new Raised(raise.Code, Touched: result != Sentinel)
            : new Written(result);
    }

    /// <summary>符号付き32bitとして解釈する</summary>
    public static long S(uint value) {
        return (int)value;
    }

    /// <summary>符号付き16bitとして解釈する</summary>
    public static long S(ushort value) {
        return (short)value;
    }

    /// <summary>MIPS はシフト量の下位5bitのみを用いる</summary>
    public static long Pow2(uint amount) {
        return 1L << (int)(amount % 32);
    }

    /// <summary>-∞ 方向へ丸める除算. 算術右シフトに等しい</summary>
    public static long FloorDiv(long dividend, long divisor) {
        (long q, long r) = Math.DivRem(dividend, divisor);
        return r < 0 ? q - 1 : q;
    }

    /// <summary>正確な結果の下位32bitを書き込む</summary>
    public static Written W(long exact) {
        return new(unchecked((uint)exact));
    }

    /// <summary>正確な結果が符号付き32bitに収まらなければ <see cref="ExcCode.Ov"/></summary>
    public static Outcome Trap(long exact) {
        return exact is >= int.MinValue and <= int.MaxValue ? W(exact) : new Raised(ExcCode.Ov, Touched: false);
    }
}
