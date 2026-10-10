using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// <c>andi $rt $rs imm</c>，<c>ori $rt $rs imm</c>，<c>xori $rt $rs imm</c> の命令レベルテスト (doc/tests/instructions/instruction_tests.md)
/// </summary>
/// <remarks>
/// 即値はゼロ拡張して演算する．rs はビットパターン，即値は符号なし16bit値の全組を用いる
/// </remarks>
public class ITypeLogicalInstructionTests {
    private static readonly string[] Ops = ["andi", "ori", "xori"];

    public static TheoryData<string, Alias2Reg, uint, ushort> AllCases() {
        TheoryData<string, Alias2Reg, uint, ushort> data = new();
        foreach(string op in Ops) {
            foreach(object[] row in TestValues.DestSourceCases(TestValues.LogicalImmediateRs, TestValues.LogicalImmediateImm)) {
                data.Add(op, (Alias2Reg)row[0], (uint)row[1], (ushort)row[2]);
            }
        }
        return data;
    }

    /// <summary>
    /// 実装に依存しない期待値の計算
    /// </summary>
    private static uint Expected(string op, uint rsVal, ushort imm) {
        // ゼロ拡張: 上位16bitは0
        uint zeroExtended = imm;
        return op switch {
            "andi" => rsVal & zeroExtended,
            "ori" => rsVal | zeroExtended,
            "xori" => rsVal ^ zeroExtended,
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };
    }

    private static IInstruction Parse(string op, RegisterID rt, RegisterID rs, ushort imm) {
        return InstructionHarness.Parse($"{op} {RegisterAliases.Name(rt)}, {RegisterAliases.Name(rs)}, {InstructionHarness.Imm(imm)}");
    }

    /// <summary>
    /// ユーザーモードのコンテキストを作り，rs に入力値を設定する
    /// </summary>
    private static (IInstruction Inst, RuntimeContext Context, RegisterID Rt) Setup(string op, Alias2Reg alias, uint rsVal, ushort imm) {
        (RegisterID rt, RegisterID rs) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(op, rt, rs, imm);
        RuntimeContext context = InstructionHarness.CreateUser();
        context.Registers[rs] = rsVal;
        return (inst, context, rt);
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Execute_WritesResultToRt(string op, Alias2Reg alias, uint rsVal, ushort imm) {
        (IInstruction inst, RuntimeContext context, RegisterID rt) = Setup(op, alias, rsVal, imm);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // rt だけが変化し，例外も PC の変更も起きない
        MachineState expected = before.WithRegister(rt, Expected(op, rsVal, imm));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Undo_RestoresState(string op, Alias2Reg alias, uint rsVal, ushort imm) {
        (IInstruction inst, RuntimeContext context, _) = Setup(op, alias, rsVal, imm);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    [Fact]
    public void Parameters_CoverAllOpsAndSignBitImmediates() {
        TheoryData<string, Alias2Reg, uint, ushort> cases = AllCases();
        foreach(string op in Ops) {
            Assert.Contains(cases, row => (string)row[0] == op);
        }
        // 符号拡張とゼロ拡張を区別できる (最上位bitが1の) 即値を含む
        Assert.Contains(cases, row => ((ushort)row[3] & 0x8000) != 0);
    }

    [Theory]
    [InlineData("andi")]
    [InlineData("ori")]
    [InlineData("xori")]
    public void RepeatedExecute_UndoesInReverseOrder(string op) {
        RegisterID rt = RegisterID.T0, rs = RegisterID.T1;
        const ushort imm = 0xff00;
        IInstruction inst = Parse(op, rt, rs, imm);
        RuntimeContext context = InstructionHarness.CreateUser();

        uint[] inputs = [0xaaaaaaaa, 0xffffffff, 0x00000000];
        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            [.. inputs.Select(v => RepeatedExecution.SetRegisters((rs, v)))]
        );

        for(int i = 0; i < inputs.Length; i++) {
            MachineState.AssertEqual(results[i].BeforeExecute.WithRegister(rt, Expected(op, inputs[i], imm)), results[i].AfterExecute);
        }
    }
}
