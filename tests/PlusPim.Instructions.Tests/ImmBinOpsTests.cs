using PlusPim.Instructions.Instructions;
using PlusPim.TestValues;
using System.Collections.Frozen;
using static PlusPim.Instructions.Tests.BinOpModel;

namespace PlusPim.Instructions.Tests;

/// <summary>
/// レジスタと即値の2項演算 <see cref="IImmBinOp"/> に関する命令をテストする.
/// 即値の拡張と演算を合成した結果を参照モデルと照合する
/// </summary>
public class ImmBinOpsTests {
    /// <summary>1演算の仕様</summary>
    /// <param name="Extend">テスト対象の即値の拡張</param>
    /// <param name="Apply">テスト対象の演算</param>
    /// <param name="Lhs">lhs に与える値</param>
    /// <param name="Imm">即値のビット列</param>
    /// <param name="Reference">実装に依存しない期待値</param>
    private sealed record Spec(Func<Immediate, uint?> Extend, ApplyFn Apply, IReadOnlySet<uint> Lhs, IReadOnlySet<ushort> Imm, Func<uint, ushort, Outcome> Reference);

    /// <summary>即値のシフト量 ∪ 5bit に収まらない値</summary>
    private static readonly IReadOnlySet<ushort> Shamts = ShiftAmounts.Immediate.Select(v => (ushort)v).Union<ushort>([32, 0xffff]).ToFrozenSet();

    private static readonly Dictionary<string, Spec> Specs = new() {
        ["addi"] = new(Addi.Extend, Addi.Apply, Signed32.All, Signed16.All, (a, i) => Trap(S(a) + S(i))),
        ["addiu"] = new(Addiu.Extend, Addiu.Apply, Signed32.All, Signed16.All, (a, i) => W(a + S(i))),
        ["andi"] = new(Andi.Extend, Andi.Apply, BitPatterns.All, Unsigned16.All, (a, i) => W(a & i)),
        ["ori"] = new(Ori.Extend, Ori.Apply, BitPatterns.All, Unsigned16.All, (a, i) => W(a | i)),
        ["xori"] = new(Xori.Extend, Xori.Apply, BitPatterns.All, Unsigned16.All, (a, i) => W(a ^ i)),
        ["slti"] = new(Slti.Extend, Slti.Apply, Signed32.All, Signed16.All, (a, i) => W(S(a) < S(i) ? 1 : 0)),
        // 符号拡張した即値を符号なしで比較する
        ["sltiu"] = new(Sltiu.Extend, Sltiu.Apply, Unsigned32.All, Unsigned16.All, (a, i) => W(a < unchecked((uint)S(i)) ? 1 : 0)),
        ["sll"] = new(Sll.Extend, Sll.Apply, RBinOpsTests.ShiftSources, Shamts, (a, i) => Shift(i, () => W(a * Pow2(i)))),
        ["srl"] = new(Srl.Extend, Srl.Apply, RBinOpsTests.ShiftSources, Shamts, (a, i) => Shift(i, () => W(a / Pow2(i)))),
        ["sra"] = new(Sra.Extend, Sra.Apply, RBinOpsTests.ShiftSources, Shamts, (a, i) => Shift(i, () => W(FloorDiv(S(a), Pow2(i))))),
    };

    public static TheoryData<string, uint, ushort> Cases() {
        TheoryData<string, uint, ushort> data = new();
        foreach((string mnemonic, Spec spec) in Specs) {
            foreach(uint lhs in spec.Lhs) {
                foreach(ushort imm in spec.Imm) {
                    data.Add(mnemonic, lhs, imm);
                }
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExtendThenApply_MatchesReference(string mnemonic, uint lhs, ushort imm) {
        Spec spec = Specs[mnemonic];
        Outcome actual = spec.Extend(new Immediate(imm)) is uint rhs ? Observe(spec.Apply, lhs, rhs) : new Rejected();
        Assert.Equal(spec.Reference(lhs, imm), actual);
    }

    /// <summary>シフト量が5bitに収まらなければ即値を受け付けない</summary>
    private static Outcome Shift(ushort shamt, Func<Outcome> shifted) {
        return shamt <= 31 ? shifted() : new Rejected();
    }
}
