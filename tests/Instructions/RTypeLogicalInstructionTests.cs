using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// R形式の論理演算 <c>and</c>，<c>or</c>，<c>xor</c>，<c>nor</c> (<c>op $rd, $rs, $rt</c>) の命令レベルテスト
/// (doc/tests/instructions/instruction_tests.md「R形式命令 / 論理演算」)
/// </summary>
public class RTypeLogicalInstructionTests {
    private static readonly string[] Mnemonics = ["and", "or", "xor", "nor"];

    // ---- パラメータ ----

    public static TheoryData<string, Alias3Reg, uint, uint> AllCases() {
        TheoryData<string, Alias3Reg, uint, uint> data = new();
        foreach(string mnemonic in Mnemonics) {
            foreach(object[] row in TestValues.ThreeRegCases(TestValues.Logical)) {
                data.Add(mnemonic, (Alias3Reg)row[0], (uint)row[1], (uint)row[2]);
            }
        }
        return data;
    }

    // ---- 実装に依存しない期待値の計算 ----

    /// <summary>
    /// 1bit の真理値表 (index = rs のビット * 2 + rt のビット)
    /// </summary>
    private static int[] TruthTable(string mnemonic) {
        return mnemonic switch {
            "and" => [0, 0, 0, 1],
            "or" => [0, 1, 1, 1],
            "xor" => [0, 1, 1, 0],
            "nor" => [1, 0, 0, 0],
            _ => throw new ArgumentOutOfRangeException(nameof(mnemonic)),
        };
    }

    /// <summary>
    /// 真理値表をビットごとに適用して rd へ書き込まれる値を求める
    /// </summary>
    private static uint Reference(string mnemonic, uint rsVal, uint rtVal) {
        int[] table = TruthTable(mnemonic);
        uint result = 0;
        for(int bit = 0; bit < 32; bit++) {
            int rsBit = (int)((rsVal >> bit) & 1);
            int rtBit = (int)((rtVal >> bit) & 1);
            result |= (uint)table[(rsBit * 2) + rtBit] << bit;
        }
        return result;
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
    public void Parameters_CoverEveryMnemonicAndAlias() {
        List<(string Mnemonic, Alias3Reg Alias)> cases = [];
        foreach(object[] row in AllCases()) {
            cases.Add(((string)row[0], (Alias3Reg)row[1]));
        }

        foreach(string mnemonic in Mnemonics) {
            foreach(Alias3Reg alias in Enum.GetValues<Alias3Reg>()) {
                Assert.Contains((mnemonic, alias), cases);
            }
        }
    }

    // ---- 繰り返し実行 ----

    [Theory]
    [InlineData("and")]
    [InlineData("or")]
    [InlineData("xor")]
    [InlineData("nor")]
    public void RepeatedExecute_UndoesInReverseOrder(string mnemonic) {
        (uint Rs, uint Rt)[] inputs = [
            (0xaaaaaaaa, 0x55555555),
            (0xff00ff00, 0xffffffff),
            (0x00ff00ff, 0x00000000),
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
