using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// 命令レベルテストの共通ヘルパー自体のテスト
/// </summary>
public class InstructionHarnessTests {
    [Fact]
    public void TestValues_SetSizes() {
        Assert.Equal(9, TestValues.Signed32.Count);
        Assert.Equal(10, TestValues.Unsigned32.Count);
        Assert.Equal(9, TestValues.Signed16.Count);
        Assert.Equal(10, TestValues.Unsigned16.Count);
        Assert.Equal(13, TestValues.ShiftSources.Count);
        Assert.Equal(8, TestValues.ShiftAmountsRegister.Count);
        Assert.Equal(0xffff8000u, TestValues.SignExtend16(0x8000));
        Assert.Equal(0x00007fffu, TestValues.SignExtend16(0x7fff));
    }

    [Fact]
    public void ThreeRegCases_CollapsesAliasedSources() {
        int n = TestValues.Signed32.Count;
        // None, RdRs, RdRt は直積，RsRt, All は縮約
        Assert.Equal((3 * n * n) + (2 * n), TestValues.ThreeRegCases(TestValues.Signed32).Count());
    }

    [Fact]
    public void CreateKernel_SetsExceptionState() {
        RuntimeContext context = InstructionHarness.CreateKernel(ExcCode.AdEL, new Address(0x00400010), 0x10000001);
        MachineState state = MachineState.Capture(context);

        Assert.True(context.IsKernelMode);
        Assert.Equal(InstructionHarness.KernelInstructionAddress, state.PC);
        Assert.Equal(0x10000001u, state.BadVAddr);
        Assert.Equal(0x2u, state.Status);
        Assert.Equal((uint)ExcCode.AdEL << 2, state.Cause);
        Assert.Equal(0x00400010u, state.EPC);
        Assert.Null(state.LastException);
    }

    [Fact]
    public void CreatePostEret_IsUserModeWithResidualCP0() {
        RuntimeContext context = InstructionHarness.CreatePostEret(ExcCode.Ov, new Address(0x00400010));
        MachineState state = MachineState.Capture(context);

        Assert.False(context.IsKernelMode);
        Assert.Equal(InstructionHarness.InstructionAddress, state.PC);
        Assert.Equal((uint)ExcCode.Ov << 2, state.Cause);
        Assert.Equal(0x00400010u, state.EPC);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ApplyTo_RestoresCapturedState(bool hasException, bool isDouble) {
        RuntimeContext context = InstructionHarness.CreateKernel(ExcCode.Bp, new Address(0x00400020), 0x1234);
        InstructionHarness.PushCallFrame(context, new Address(0x00400008), InstructionHarness.ForwardLabel);
        MachineState target = MachineState.Capture(context)
            .WithRegister(RegisterID.T0, 0xdeadbeef)
            .WithHiLo(1, 2)
            .WithPC(new Address(0x00400004))
            .WithCP0(badVAddr: 0, status: 0, cause: (uint)ExcCode.Sys << 2, epc: 0x00400030)
            .WithLastException(hasException ? new ExceptionEvent(ExcCode.Ov, isDouble) : null)
            .WithTerminated()
            .WithRuntimeError(hasException ? null : RuntimeErrorKind.DivisionByZero)
            .WithPendingInput("pending\n")
            .WithMemoryValue(MachineState.SeededMemoryBase + 4, 0x11223344, 4);

        target.ApplyTo(context);

        MachineState.AssertEqual(target, MachineState.Capture(context));
    }

    [Fact]
    public void RepeatedExecution_UndoesInReverseOrder() {
        RuntimeContext context = InstructionHarness.CreateUser();
        IInstruction inst = InstructionHarness.Parse("addu $t0, $t1, $t2");

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            RepeatedExecution.SetRegisters((RegisterID.T1, 1), (RegisterID.T2, 2)),
            RepeatedExecution.SetRegisters((RegisterID.T1, 0xffffffff), (RegisterID.T2, 2)),
            RepeatedExecution.SetRegisters((RegisterID.T1, 5), (RegisterID.T2, 0))
        );

        Assert.Equal([3u, 1u, 5u], results.Select(r => r.AfterExecute.Registers[(int)RegisterID.T0]));
    }

    [Fact]
    public void RuntimeErrorAssert_RejectsAccidentalExceptions() {
        // 偶発的な C# の例外はランタイムエラーではない
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => RuntimeErrorAssert.Raised(() => throw new DivideByZeroException()));

        // 正常に完了した実行はランタイムエラーではない
        RuntimeContext context = InstructionHarness.CreateUser();
        IInstruction inst = InstructionHarness.Parse("addu $t0, $t1, $t2");
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => RuntimeErrorAssert.Raised(() => Processor.Execute(context, inst)));
    }
}
