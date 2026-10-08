using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// <c>lui $rt imm</c> の命令レベルテスト (doc/tests/instructions/instruction_tests.md)
/// </summary>
/// <remarks>
/// 1レジスタ命令なのでエイリアスパターンはない．即値は符号なし16bit値を用いる
/// </remarks>
public class LuiInstructionTests {
    private const RegisterID Rt = RegisterAliases.Single;

    public static TheoryData<ushort> ImmCases() {
        TheoryData<ushort> data = new();
        foreach(ushort imm in TestValues.LuiImm) {
            data.Add(imm);
        }
        return data;
    }

    /// <summary>
    /// 実装に依存しない期待値の計算
    /// </summary>
    private static uint Expected(ushort imm) {
        return (uint)imm << 16;
    }

    private static IInstruction Parse(ushort imm) {
        return InstructionHarness.Parse($"lui {RegisterAliases.Name(Rt)}, {InstructionHarness.Imm(imm)}");
    }

    [Theory]
    [MemberData(nameof(ImmCases))]
    public void Execute_WritesShiftedImmediateToRt(ushort imm) {
        IInstruction inst = Parse(imm);
        RuntimeContext context = InstructionHarness.CreateUser();
        // rt の元の値の下位16bitが残らないことを確かめるため，全bitを1にしておく
        context.Registers[Rt] = 0xffffffff;
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState actual = MachineState.Capture(context);
        MachineState expected = before.WithRegister(Rt, Expected(imm));
        MachineState.AssertEqual(expected, actual);
        // 下位16bitが0になる
        Assert.Equal(0u, actual.Registers[(int)Rt] & 0xffffu);
    }

    [Theory]
    [MemberData(nameof(ImmCases))]
    public void Undo_RestoresState(ushort imm) {
        IInstruction inst = Parse(imm);
        RuntimeContext context = InstructionHarness.CreateUser();
        context.Registers[Rt] = 0xffffffff;
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    [Fact]
    public void Parameters_CoverImmediates() {
        Assert.NotEmpty(ImmCases());
    }

    [Fact]
    public void RepeatedExecute_UndoesInReverseOrder() {
        const ushort imm = 0xbabe;
        IInstruction inst = Parse(imm);
        RuntimeContext context = InstructionHarness.CreateUser();

        // 入力レジスタはないため，各実行前の rt の値を変える
        uint[] previousRt = [0xffffffff, 0x00000000, 0x0000babe];
        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            [.. previousRt.Select(v => RepeatedExecution.SetRegisters((Rt, v)))]
        );

        foreach((MachineState beforeExecute, MachineState afterExecute) in results) {
            MachineState.AssertEqual(beforeExecute.WithRegister(Rt, Expected(imm)), afterExecute);
        }
    }
}
