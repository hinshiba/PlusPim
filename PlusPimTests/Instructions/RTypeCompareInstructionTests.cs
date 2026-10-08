using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// R形式の比較 <c>slt</c>，<c>sltu</c> (<c>op $rd, $rs, $rt</c>) の命令レベルテスト
/// (doc/tests/instructions/instruction_tests.md「R形式命令 / 比較」)
/// </summary>
public class RTypeCompareInstructionTests {
    private static readonly string[] Mnemonics = ["slt", "sltu"];

    // ---- パラメータ ----

    /// <summary>
    /// 命令ごとのソースの値 (slt は符号付き32bit値，sltu は符号なし32bit値)
    /// </summary>
    private static IReadOnlyList<uint> SourceValues(string mnemonic) {
        return mnemonic switch {
            "slt" => TestValues.SignedArithmetic,
            "sltu" => TestValues.UnsignedArithmetic,
            _ => throw new ArgumentOutOfRangeException(nameof(mnemonic)),
        };
    }

    public static TheoryData<string, Alias3Reg, uint, uint> AllCases() {
        TheoryData<string, Alias3Reg, uint, uint> data = new();
        foreach(string mnemonic in Mnemonics) {
            foreach(object[] row in TestValues.ThreeRegCases(SourceValues(mnemonic))) {
                data.Add(mnemonic, (Alias3Reg)row[0], (uint)row[1], (uint)row[2]);
            }
        }
        return data;
    }

    // ---- 実装に依存しない期待値の計算 ----

    /// <summary>
    /// 比較に用いる数学的な値 (slt は2の補数として解釈，sltu はそのまま)
    /// </summary>
    private static long Interpret(string mnemonic, uint value) {
        return mnemonic switch {
            "slt" => value >= 0x80000000u ? value - (1L << 32) : value,
            "sltu" => value,
            _ => throw new ArgumentOutOfRangeException(nameof(mnemonic)),
        };
    }

    /// <summary>
    /// rd へ書き込まれる値
    /// </summary>
    private static uint Reference(string mnemonic, uint rsVal, uint rtVal) {
        return Interpret(mnemonic, rsVal) < Interpret(mnemonic, rtVal) ? 1u : 0u;
    }

    // ---- 準備 ----

    private static IInstruction Parse(string mnemonic, RegisterID rd, RegisterID rs, RegisterID rt) {
        return InstructionHarness.Parse($"{mnemonic} {RegisterAliases.Name(rd)}, {RegisterAliases.Name(rs)}, {RegisterAliases.Name(rt)}");
    }

    private static RuntimeContext Setup(RegisterID rs, RegisterID rt, uint rsVal, uint rtVal) {
        RuntimeContext context = InstructionHarness.CreateUser();
        context.Registers[rs] = rsVal;
        context.Registers[rt] = rtVal;
        return context;
    }

    // ---- 正常系 ----

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Execute_WritesResultToRd(string mnemonic, Alias3Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rd, RegisterID rs, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(mnemonic, rd, rs, rt);
        RuntimeContext context = Setup(rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // rd だけが変化し，例外も PC の変更も起きない
        MachineState expected = before.WithRegister(rd, Reference(mnemonic, rsVal, rtVal));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Undo_RestoresState(string mnemonic, Alias3Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rd, RegisterID rs, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(mnemonic, rd, rs, rt);
        RuntimeContext context = Setup(rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    // ---- パラメータ自体の検証 ----

    [Fact]
    public void Parameters_CoverEveryMnemonicAliasAndResult() {
        List<(string Mnemonic, Alias3Reg Alias, uint Rs, uint Rt)> cases = [];
        foreach(object[] row in AllCases()) {
            cases.Add(((string)row[0], (Alias3Reg)row[1], (uint)row[2], (uint)row[3]));
        }

        foreach(string mnemonic in Mnemonics) {
            foreach(Alias3Reg alias in Enum.GetValues<Alias3Reg>()) {
                Assert.Contains(cases, c => c.Mnemonic == mnemonic && c.Alias == alias);
            }
            Assert.Contains(cases, c => c.Mnemonic == mnemonic && Reference(mnemonic, c.Rs, c.Rt) == 1u);
            Assert.Contains(cases, c => c.Mnemonic == mnemonic && Reference(mnemonic, c.Rs, c.Rt) == 0u);
            // 符号付きと符号なしで大小が逆転する組を含む
            Assert.Contains(cases, c => c.Mnemonic == mnemonic && Reference("slt", c.Rs, c.Rt) != Reference("sltu", c.Rs, c.Rt));
        }
    }

    // ---- 繰り返し実行 ----

    [Theory]
    [InlineData("slt")]
    [InlineData("sltu")]
    public void RepeatedExecute_UndoesInReverseOrder(string mnemonic) {
        (uint Rs, uint Rt)[] inputs = [
            (0x00000001, 0x7fffffff), // slt: 1, sltu: 1
            (0x80000000, 0x00000000), // slt: 1, sltu: 0
            (0xffffffff, 0xfffffffe), // slt: 0, sltu: 0
        ];
        (RegisterID rd, RegisterID rs, RegisterID rt) = RegisterAliases.Registers(Alias3Reg.None);
        IInstruction inst = Parse(mnemonic, rd, rs, rt);
        RuntimeContext context = InstructionHarness.CreateUser();

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            [.. inputs.Select(i => RepeatedExecution.SetRegisters((rs, i.Rs), (rt, i.Rt)))]
        );

        for(int i = 0; i < inputs.Length; i++) {
            (MachineState beforeExecute, MachineState afterExecute) = results[i];
            MachineState expected = beforeExecute.WithRegister(rd, Reference(mnemonic, inputs[i].Rs, inputs[i].Rt));
            MachineState.AssertEqual(expected, afterExecute);
        }
    }
}
