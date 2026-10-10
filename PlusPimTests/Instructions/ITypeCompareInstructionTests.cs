using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// <c>slti $rt $rs imm</c>，<c>sltiu $rt $rs imm</c> の命令レベルテスト (doc/tests/instructions/instruction_tests.md)
/// </summary>
/// <remarks>
/// slti: rs は符号付き32bit値，即値は符号付き16bit値．即値を符号拡張し，符号付きで比較する．
/// sltiu: rs は符号なし32bit値，即値は符号なし16bit値．
/// 仕様書は sltiu の即値の拡張方式を明記していないため，MIPS の定義に従い
/// 「即値を符号拡張した上で，符号なしで比較する」ことを前提とする
/// (例: 即値 0xffff は 0xffffffff と比較される)
/// </remarks>
public class ITypeCompareInstructionTests {
    public static TheoryData<Alias2Reg, uint, ushort> SltiCases() {
        return TestValues.DestSourceCases(TestValues.ArithmeticImmediateRs, TestValues.ArithmeticImmediateImm);
    }

    public static TheoryData<Alias2Reg, uint, ushort> SltiuCases() {
        return TestValues.DestSourceCases(TestValues.SltiuRs, TestValues.SltiuImm);
    }

    /// <summary>
    /// 実装に依存しない slti の期待値
    /// </summary>
    private static uint ExpectedSlti(uint rsVal, ushort imm) {
        return (int)rsVal < (short)imm ? 1u : 0u;
    }

    /// <summary>
    /// 実装に依存しない sltiu の期待値 (即値を符号拡張し，符号なしで比較する)
    /// </summary>
    private static uint ExpectedSltiu(uint rsVal, ushort imm) {
        uint signExtended = unchecked((uint)(int)(short)imm);
        return rsVal < signExtended ? 1u : 0u;
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

    // ---- slti ----

    [Theory]
    [MemberData(nameof(SltiCases))]
    public void Execute_Slti_WritesSignedComparisonToRt(Alias2Reg alias, uint rsVal, ushort imm) {
        (IInstruction inst, RuntimeContext context, RegisterID rt) = Setup("slti", alias, rsVal, imm);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState expected = before.WithRegister(rt, ExpectedSlti(rsVal, imm));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(SltiCases))]
    public void Undo_Slti_RestoresState(Alias2Reg alias, uint rsVal, ushort imm) {
        (IInstruction inst, RuntimeContext context, _) = Setup("slti", alias, rsVal, imm);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    // ---- sltiu ----

    [Theory]
    [MemberData(nameof(SltiuCases))]
    public void Execute_Sltiu_WritesUnsignedComparisonToRt(Alias2Reg alias, uint rsVal, ushort imm) {
        (IInstruction inst, RuntimeContext context, RegisterID rt) = Setup("sltiu", alias, rsVal, imm);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState expected = before.WithRegister(rt, ExpectedSltiu(rsVal, imm));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(SltiuCases))]
    public void Undo_Sltiu_RestoresState(Alias2Reg alias, uint rsVal, ushort imm) {
        (IInstruction inst, RuntimeContext context, _) = Setup("sltiu", alias, rsVal, imm);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    // ---- パラメータ ----

    [Fact]
    public void Parameters_CoverBothResults() {
        // 両命令とも結果 0 と 1 の両方を含む
        Assert.Contains(SltiCases(), row => ExpectedSlti((uint)row[1], (ushort)row[2]) == 1u);
        Assert.Contains(SltiCases(), row => ExpectedSlti((uint)row[1], (ushort)row[2]) == 0u);
        Assert.Contains(SltiuCases(), row => ExpectedSltiu((uint)row[1], (ushort)row[2]) == 1u);
        Assert.Contains(SltiuCases(), row => ExpectedSltiu((uint)row[1], (ushort)row[2]) == 0u);
        // 符号付き比較と符号なし比較で結果が異なる組を含む
        Assert.Contains(SltiCases(), row => ExpectedSlti((uint)row[1], (ushort)row[2]) != ExpectedSltiu((uint)row[1], (ushort)row[2]));
    }

    // ---- 繰り返し実行 ----

    [Fact]
    public void RepeatedExecute_Slti_UndoesInReverseOrder() {
        RegisterID rt = RegisterID.T0, rs = RegisterID.T1;
        const ushort imm = 0xffff; // -1
        IInstruction inst = Parse("slti", rt, rs, imm);
        RuntimeContext context = InstructionHarness.CreateUser();

        uint[] inputs = [0x80000000, 0x00000000, 0xfffffffe];
        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            [.. inputs.Select(v => RepeatedExecution.SetRegisters((rs, v)))]
        );

        for(int i = 0; i < inputs.Length; i++) {
            MachineState.AssertEqual(results[i].BeforeExecute.WithRegister(rt, ExpectedSlti(inputs[i], imm)), results[i].AfterExecute);
        }
    }

    [Fact]
    public void RepeatedExecute_Sltiu_UndoesInReverseOrder() {
        RegisterID rt = RegisterID.T0, rs = RegisterID.T1;
        const ushort imm = 0x8000; // 0xffff8000 と比較される
        IInstruction inst = Parse("sltiu", rt, rs, imm);
        RuntimeContext context = InstructionHarness.CreateUser();

        uint[] inputs = [0x00000000, 0xfffffffe, 0x80000000];
        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            [.. inputs.Select(v => RepeatedExecution.SetRegisters((rs, v)))]
        );

        for(int i = 0; i < inputs.Length; i++) {
            MachineState.AssertEqual(results[i].BeforeExecute.WithRegister(rt, ExpectedSltiu(inputs[i], imm)), results[i].AfterExecute);
        }
    }
}
