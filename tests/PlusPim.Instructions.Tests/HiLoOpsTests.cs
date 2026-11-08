using PlusPim.Instructions.Instructions;
using PlusPim.TestValues;

namespace PlusPim.Instructions.Tests;

/// <summary>
/// 乗除算 <see cref="IHiLoOp.Apply"/> に関する命令をテストする
/// </summary>
public class HiLoOpsTests {
    private delegate Effect HiLoFn(uint lhs, uint rhs, ref uint hi, ref uint lo);

    /// <summary>演算の観測可能な結果</summary>
    private abstract record Outcome;

    /// <summary>HIとLOへ書きこんだ</summary>
    private sealed record Written(uint Hi, uint Lo): Outcome;

    /// <summary>ランタイムエラー <paramref name="Kind"/> で停止した</summary>
    /// <param name="Touched">HIかLOを書き換えたか. <see cref="Fail"/> の保証により常に false であるべき</param>
    private sealed record Failed(RuntimeErrorKind Kind, bool Touched): Outcome;

    /// <summary>1演算の仕様</summary>
    private sealed record Spec(HiLoFn Apply, IReadOnlySet<uint> Values, Func<uint, uint, Outcome> Reference);

    /// <summary>失敗時にHIとLOが書き換わらないことを検出するための初期値</summary>
    private const uint Sentinel = 0xdeadbeef;

    private static readonly Dictionary<string, Spec> Specs = new() {
        ["mult"] = new(Mult.Apply, Signed32.All, (a, b) => Wide((long)(int)a * (int)b)),
        ["multu"] = new(Multu.Apply, Unsigned32.All, (a, b) => Wide((long)((ulong)a * b))),
        ["div"] = new(Div.Apply, Signed32.All, (a, b) =>
            b == 0 ? new Failed(RuntimeErrorKind.DivisionByZero, false)
            : (long)(int)a / (int)b > int.MaxValue ? new Failed(RuntimeErrorKind.DivisionOverflow, false)
            : new Written(unchecked((uint)((long)(int)a % (int)b)), unchecked((uint)((long)(int)a / (int)b)))),
        ["divu"] = new(Divu.Apply, Unsigned32.All, (a, b) =>
            b == 0 ? new Failed(RuntimeErrorKind.DivisionByZero, false) : new Written(a % b, a / b)),
    };

    public static TheoryData<string, uint, uint> Cases() {
        TheoryData<string, uint, uint> data = new();
        foreach((string mnemonic, Spec spec) in Specs) {
            foreach(uint lhs in spec.Values) {
                foreach(uint rhs in spec.Values) {
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
        uint hi = Sentinel, lo = Sentinel;
        Effect effect = spec.Apply(lhs, rhs, ref hi, ref lo);
        Outcome actual = effect.TryGetValue(out Fail fail)
            ? new Failed(fail.Kind, Touched: hi != Sentinel || lo != Sentinel)
            : new Written(hi, lo);
        Assert.Equal(spec.Reference(lhs, rhs), actual);
    }

    /// <summary>64bitの積を上位と下位に分ける</summary>
    private static Written Wide(long product) {
        return new(unchecked((uint)(product >> 32)), unchecked((uint)product));
    }
}
