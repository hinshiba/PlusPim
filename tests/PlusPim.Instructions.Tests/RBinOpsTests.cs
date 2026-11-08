using PlusPim.Instructions.Instructions;
using PlusPim.TestValues;
using System.Collections.Frozen;

namespace PlusPim.Instructions.Tests;

/// <summary>
/// R形式2項演算 <see cref="IRBinOp.Apply"/> に関する命令をテストする
/// </summary>
public class RBinOpsTests {
    private delegate Effect ApplyFn(uint lhs, uint rhs, ref uint result);

    /// <summary>演算の観測可能な結果</summary>
    public abstract record Outcome;

    /// <summary>result へ <paramref name="Value"/> を書きこんだ</summary>
    public sealed record Written(uint Value): Outcome;

    /// <summary>例外 <paramref name="Code"/> を発生させた</summary>
    /// <param name="Touched">result を書き換えたか. <see cref="Raise"/> の保証により常に false であるべき</param>
    public sealed record Raised(ExcCode Code, bool Touched): Outcome;

    /// <summary>1演算の仕様</summary>
    /// <param name="Apply">テスト対象</param>
    /// <param name="Lhs">lhs に与える値</param>
    /// <param name="Rhs">rhs に与える値</param>
    /// <param name="Reference">実装に依存しない期待値</param>
    private sealed record Spec(ApplyFn Apply, IReadOnlySet<uint> Lhs, IReadOnlySet<uint> Rhs, Func<uint, uint, Outcome> Reference);

    /// <summary>シフト命令の被シフト値: ビットパターン ∪ 符号付き32bit値</summary>
    private static readonly IReadOnlySet<uint> ShiftSources = BitPatterns.All.Union(Signed32.All).ToFrozenSet();

    private static readonly Dictionary<string, Spec> Specs = new() {
        ["add"] = new(Add.Apply, Signed32.All, Signed32.All, (a, b) => Trap(S(a) + S(b))),
        ["addu"] = new(Addu.Apply, Unsigned32.All, Unsigned32.All, (a, b) => W((long)a + b)),
        ["sub"] = new(Sub.Apply, Signed32.All, Signed32.All, (a, b) => Trap(S(a) - S(b))),
        ["subu"] = new(Subu.Apply, Unsigned32.All, Unsigned32.All, (a, b) => W((long)a - b)),
        ["and"] = new(And.Apply, BitPatterns.All, BitPatterns.All, (a, b) => W(a & b)),
        ["or"] = new(Or.Apply, BitPatterns.All, BitPatterns.All, (a, b) => W(a | b)),
        ["xor"] = new(Xor.Apply, BitPatterns.All, BitPatterns.All, (a, b) => W(a ^ b)),
        ["nor"] = new(Nor.Apply, BitPatterns.All, BitPatterns.All, (a, b) => W(~(long)(a | b))),
        ["slt"] = new(Slt.Apply, Signed32.All, Signed32.All, (a, b) => W(S(a) < S(b) ? 1 : 0)),
        ["sltu"] = new(Sltu.Apply, Unsigned32.All, Unsigned32.All, (a, b) => W(a < b ? 1 : 0)),
        ["sllv"] = new(Sllv.Apply, ShiftSources, ShiftAmounts.Register, (a, b) => W(a * Pow2(b))),
        ["srlv"] = new(Srlv.Apply, ShiftSources, ShiftAmounts.Register, (a, b) => W(a / Pow2(b))),
        ["srav"] = new(Srav.Apply, ShiftSources, ShiftAmounts.Register, (a, b) => W(FloorDiv(S(a), Pow2(b)))),
    };

    public static TheoryData<string, uint, uint> Cases() {
        TheoryData<string, uint, uint> data = new();
        foreach((string mnemonic, Spec spec) in Specs) {
            foreach(uint lhs in spec.Lhs) {
                foreach(uint rhs in spec.Rhs) {
                    data.Add(mnemonic, lhs, rhs);
                }
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Apply_MatchesReference(string mnemonic, uint lhs, uint rhs) {
        Spec spec = Specs[mnemonic];
        Assert.Equal(spec.Reference(lhs, rhs), Observe(spec.Apply, lhs, rhs));
    }

    // ---- 観測 ----

    /// <summary>例外時に result が書き換わらないことを検出するための初期値</summary>
    private const uint Sentinel = 0xdeadbeef;

    private static Outcome Observe(ApplyFn apply, uint lhs, uint rhs) {
        uint result = Sentinel;
        Effect effect = apply(lhs, rhs, ref result);
        return effect.TryGetValue(out Raise raise)
            ? new Raised(raise.Code, Touched: result != Sentinel)
            : new Written(result);
    }

    // ---- 参照となる実装のユーティリティ ----

    private static long S(uint value) {
        return (int)value;
    }

    /// <summary>MIPS はシフト量の下位5bitのみを用いる</summary>
    private static long Pow2(uint amount) {
        return 1L << (int)(amount % 32);
    }

    /// <summary>-∞ 方向へ丸める除算. 算術右シフトに等しい</summary>
    private static long FloorDiv(long dividend, long divisor) {
        (long q, long r) = Math.DivRem(dividend, divisor);
        return r < 0 ? q - 1 : q;
    }

    private static Written W(long exact) {
        return new(unchecked((uint)exact));
    }

    /// <summary>正確な結果が符号付き32bitに収まらなければ <see cref="ExcCode.Ov"/></summary>
    private static Outcome Trap(long exact) {
        return exact is >= int.MinValue and <= int.MaxValue ? W(exact) : new Raised(ExcCode.Ov, Touched: false);
    }
}
