using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// <c>addi $rt $rs imm</c>，<c>addiu $rt $rs imm</c> の命令レベルテスト (doc/tests/instructions/instruction_tests.md)
/// </summary>
/// <remarks>
/// 即値は符号拡張して加算する．rs は符号付き32bit値，即値は符号付き16bit値の全組を用いる
/// </remarks>
public class ITypeArithmeticInstructionTests {
    public static TheoryData<Alias2Reg, uint, ushort> AddiNormalCases() {
        return TestValues.DestSourceCases(TestValues.ArithmeticImmediateRs, TestValues.ArithmeticImmediateImm, (rs, imm) => !Overflows(rs, imm));
    }

    public static TheoryData<Alias2Reg, uint, ushort> AddiOverflowCases() {
        return TestValues.DestSourceCases(TestValues.ArithmeticImmediateRs, TestValues.ArithmeticImmediateImm, Overflows);
    }

    public static TheoryData<Alias2Reg, uint, ushort> AddiuCases() {
        return TestValues.DestSourceCases(TestValues.ArithmeticImmediateRs, TestValues.ArithmeticImmediateImm);
    }

    /// <summary>
    /// 実装に依存しない期待値の計算 (rs と符号拡張した即値の符号付きの和)
    /// </summary>
    private static long ExactSum(uint rsVal, ushort imm) {
        return (long)(int)rsVal + (short)imm;
    }

    private static bool Overflows(uint rsVal, ushort imm) {
        long sum = ExactSum(rsVal, imm);
        return sum is < int.MinValue or > int.MaxValue;
    }

    /// <summary>
    /// 32bit に切り詰めた和 (addiu の結果，addi の正常系の結果)
    /// </summary>
    private static uint WrappedSum(uint rsVal, ushort imm) {
        return unchecked((uint)ExactSum(rsVal, imm));
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

    // ---- addi ----

    [Theory]
    [MemberData(nameof(AddiNormalCases))]
    public void Execute_AddiNormal_WritesSumToRt(Alias2Reg alias, uint rsVal, ushort imm) {
        (IInstruction inst, RuntimeContext context, RegisterID rt) = Setup("addi", alias, rsVal, imm);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // rt だけが変化し，例外も PC の変更も起きない
        MachineState expected = before.WithRegister(rt, WrappedSum(rsVal, imm));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(AddiNormalCases))]
    public void Undo_AddiNormal_RestoresState(Alias2Reg alias, uint rsVal, ushort imm) {
        (IInstruction inst, RuntimeContext context, _) = Setup("addi", alias, rsVal, imm);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(AddiOverflowCases))]
    public void Execute_AddiOverflow_RaisesOv(Alias2Reg alias, uint rsVal, ushort imm) {
        (IInstruction inst, RuntimeContext context, _) = Setup("addi", alias, rsVal, imm);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // rt は書き換わらず，CP0 だけが例外発生直後の状態になる
        MachineState expected = before.WithException(ExcCode.Ov, InstructionHarness.InstructionAddress);
        MachineState.AssertEqual(expected, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(AddiOverflowCases))]
    public void Undo_AddiOverflow_RestoresState(Alias2Reg alias, uint rsVal, ushort imm) {
        (IInstruction inst, RuntimeContext context, _) = Setup("addi", alias, rsVal, imm);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.False(context.IsKernelMode);
    }

    /// <summary>
    /// eret 後 (CP0 に前回の例外の値が残る) の Ov．BadVAddr は Ov では変化しない
    /// </summary>
    [Fact]
    public void Execute_AddiOverflowAfterEret_KeepsBadVAddr() {
        (IInstruction inst, RuntimeContext context) = SetupOverflowAfterEret();
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState expected = before.WithException(ExcCode.Ov, InstructionHarness.InstructionAddress);
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Fact]
    public void Undo_AddiOverflowAfterEret_RestoresState() {
        (IInstruction inst, RuntimeContext context) = SetupOverflowAfterEret();
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    /// <summary>
    /// アドレス例外の後に eret したコンテキストで，オーバーフローする addi を用意する
    /// </summary>
    private static (IInstruction Inst, RuntimeContext Context) SetupOverflowAfterEret() {
        RegisterID rt = RegisterID.T0, rs = RegisterID.T1;
        IInstruction inst = Parse("addi", rt, rs, 0x0001);
        RuntimeContext context = InstructionHarness.CreatePostEret(ExcCode.AdEL, new Address(0x00400010), 0x10000001);
        context.Registers[rs] = 0x7fffffff;
        return (inst, context);
    }

    // ---- addiu ----

    [Theory]
    [MemberData(nameof(AddiuCases))]
    public void Execute_Addiu_WritesWrappedSumToRt(Alias2Reg alias, uint rsVal, ushort imm) {
        (IInstruction inst, RuntimeContext context, RegisterID rt) = Setup("addiu", alias, rsVal, imm);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // オーバーフローしても例外は起きず，切り詰めた和が書き込まれる
        MachineState expected = before.WithRegister(rt, WrappedSum(rsVal, imm));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(AddiuCases))]
    public void Undo_Addiu_RestoresState(Alias2Reg alias, uint rsVal, ushort imm) {
        (IInstruction inst, RuntimeContext context, _) = Setup("addiu", alias, rsVal, imm);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    // ---- パラメータ ----

    [Fact]
    public void Parameters_CoverNormalOverflowAndWrapAround() {
        Assert.NotEmpty(AddiNormalCases());
        Assert.NotEmpty(AddiOverflowCases());
        // addiu にも符号付きオーバーフローする (= 切り詰めが起きる) 組が含まれる
        Assert.Contains(AddiuCases(), row => Overflows((uint)row[1], (ushort)row[2]));
    }

    // ---- 繰り返し実行 ----

    [Fact]
    public void RepeatedExecute_AddiNormalOverflowNormal_UndoesInReverseOrder() {
        RegisterID rt = RegisterID.T0, rs = RegisterID.T1;
        const ushort imm = 0x0001;
        IInstruction inst = Parse("addi", rt, rs, imm);
        RuntimeContext context = InstructionHarness.CreateUser();

        uint[] inputs = [
            0x000cafe0, // 正常系
            0x7fffffff, // 異常系 (Ov)
            0x80000000, // 正常系 (例外ハンドラ内)
        ];
        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            [.. inputs.Select(v => RepeatedExecution.SetRegisters((rs, v)))]
        );

        MachineState.AssertEqual(results[0].BeforeExecute.WithRegister(rt, WrappedSum(inputs[0], imm)), results[0].AfterExecute);
        MachineState.AssertEqual(results[1].BeforeExecute.WithException(ExcCode.Ov, InstructionHarness.InstructionAddress), results[1].AfterExecute);
        MachineState.AssertEqual(results[2].BeforeExecute.WithRegister(rt, WrappedSum(inputs[2], imm)), results[2].AfterExecute);
    }

    [Fact]
    public void RepeatedExecute_Addiu_UndoesInReverseOrder() {
        RegisterID rt = RegisterID.T0, rs = RegisterID.T1;
        const ushort imm = 0x8000;
        IInstruction inst = Parse("addiu", rt, rs, imm);
        RuntimeContext context = InstructionHarness.CreateUser();

        uint[] inputs = [0x000cafe0, 0x80000000, 0x7fffffff];
        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            [.. inputs.Select(v => RepeatedExecution.SetRegisters((rs, v)))]
        );

        for(int i = 0; i < inputs.Length; i++) {
            MachineState.AssertEqual(results[i].BeforeExecute.WithRegister(rt, WrappedSum(inputs[i], imm)), results[i].AfterExecute);
        }
    }
}
