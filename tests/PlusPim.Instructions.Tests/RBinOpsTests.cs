using PlusPim.Instructions.Instructions;
using PlusPim.TestValues;

namespace PlusPim.Instructions.Tests;

/// <summary>
/// R形式2項演算 <see cref="IRBinOp.Apply"/> を, 64bit で計算する参照モデルと照合する
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

    private static readonly Dictionary<string, Spec> Specs = new() {
        ["add"] = new(Add.Apply, Signed32.All, Signed32.All, (a, b) => Trap(S(a) + S(b))),
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

    // ---- 参照モデルの部品 (実装のビット演算に依存しない) ----

    private static long S(uint value) {
        return (int)value;
    }

    private static Written W(long exact) {
        return new(unchecked((uint)exact));
    }

    /// <summary>正確な結果が符号付き32bitに収まらなければ <see cref="ExcCode.Ov"/></summary>
    private static Outcome Trap(long exact) {
        return exact is >= int.MinValue and <= int.MaxValue ? W(exact) : new Raised(ExcCode.Ov, Touched: false);
    }
}
