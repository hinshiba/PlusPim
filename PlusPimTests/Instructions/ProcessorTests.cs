using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// 処理系 (<see cref="Processor"/>) による例外・ランタイムエラーの適用と巻き戻しのテスト
/// (doc/tests/instructions/execution_model.md「異常系」「カーネルモードでの例外(二重例外)」，runtime_error_model.md)
/// </summary>
/// <remarks>命令の振る舞いから切り離すため，結果を指定できる偽の命令を用いる</remarks>
public class ProcessorTests {
    /// <summary>
    /// 指定した結果を返す偽の命令．例外もランタイムエラーも要求しないときだけ <see cref="Marker"/> を書き込む
    /// </summary>
    private sealed class FakeInstruction(ExecuteResult result): IInstruction {
        public const RegisterID Marker = RegisterID.T0;
        public const uint MarkerValue = 0xdeadbeef;

        private readonly Stack<uint> _prev = new();

        public int SourceLine => 1;
        public int ExecuteCount { get; private set; }
        public int UndoCount { get; private set; }

        public ExecuteResult Execute(RuntimeContext context) {
            this.ExecuteCount++;
            if(result.Exception is null && result.Error is null) {
                this._prev.Push(context.Registers[Marker]);
                context.Registers[Marker] = MarkerValue;
            }
            return result;
        }

        public void Undo(RuntimeContext context) {
            this.UndoCount++;
            context.Registers[Marker] = this._prev.Pop();
        }
    }

    private static readonly Address PostEretEpc = new(0x00400010);
    private const uint PostEretBadVAddr = 0x10000001;
    private static readonly Address FaultAddress = new(0x10000003);

    // ---- ユーザーモードでの例外 ----

    [Fact]
    public void Execute_Raise_AppliesCP0AndLastException() {
        FakeInstruction inst = new(ExecuteResult.Raise(ExcCode.Ov));
        RuntimeContext context = InstructionHarness.CreateUser();
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);

        Assert.True(record.Raised);
        MachineState.AssertEqual(before.WithException(ExcCode.Ov, InstructionHarness.InstructionAddress), MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [InlineData(ExcCode.Ov)]
    [InlineData(ExcCode.Sys)]
    [InlineData(ExcCode.Bp)]
    [InlineData(ExcCode.CpU)]
    [InlineData(ExcCode.RI)]
    public void Execute_NonAddressException_PreservesBadVAddr(ExcCode code) {
        FakeInstruction inst = new(ExecuteResult.Raise(code));
        RuntimeContext context = InstructionHarness.CreatePostEret(ExcCode.AdEL, PostEretEpc, PostEretBadVAddr);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState after = MachineState.Capture(context);
        Assert.Equal(PostEretBadVAddr, after.BadVAddr);
        MachineState.AssertEqual(before.WithException(code, InstructionHarness.InstructionAddress), after);
    }

    [Theory]
    [InlineData(ExcCode.AdEL)]
    [InlineData(ExcCode.AdES)]
    public void Execute_AddressException_SetsBadVAddr(ExcCode code) {
        FakeInstruction inst = new(ExecuteResult.Raise(code, FaultAddress));
        RuntimeContext context = InstructionHarness.CreatePostEret(ExcCode.AdEL, PostEretEpc, PostEretBadVAddr);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState.AssertEqual(before.WithException(code, InstructionHarness.InstructionAddress, FaultAddress.Addr), MachineState.Capture(context));
    }

    [Fact]
    public void Execute_Normal_DoesNotTouchExceptionState() {
        FakeInstruction inst = new(ExecuteResult.Next);
        RuntimeContext context = InstructionHarness.CreatePostEret(ExcCode.AdEL, PostEretEpc, PostEretBadVAddr);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);

        Assert.False(record.Raised);
        MachineState.AssertEqual(before.WithRegister(FakeInstruction.Marker, FakeInstruction.MarkerValue), MachineState.Capture(context));
    }

    [Fact]
    public void Undo_Normal_CallsInstructionUndo() {
        FakeInstruction inst = new(ExecuteResult.Next);
        RuntimeContext context = InstructionHarness.CreateUser();
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        Assert.Equal(1, inst.UndoCount);
        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    [Fact]
    public void Undo_RaisedRecord_DoesNotCallInstructionUndo() {
        FakeInstruction inst = new(ExecuteResult.Raise(ExcCode.Ov));
        RuntimeContext context = InstructionHarness.CreateUser();

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        Assert.Equal(1, inst.ExecuteCount);
        Assert.Equal(0, inst.UndoCount);
    }

    [Theory]
    [InlineData(ExcCode.Ov, false)]
    [InlineData(ExcCode.AdEL, true)]
    public void Undo_RaisedRecord_RestoresCP0LastExceptionIsTerminated(ExcCode code, bool isAddress) {
        FakeInstruction inst = new(ExecuteResult.Raise(code, isAddress ? FaultAddress : null));
        // eret 後 (Cause/EPC/BadVAddr に値が残る) から始める
        RuntimeContext context = InstructionHarness.CreatePostEret(ExcCode.Sys, PostEretEpc, PostEretBadVAddr);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.False(context.IsKernelMode);
    }

    // ---- カーネルモードでの例外 (二重例外) ----

    [Theory]
    [InlineData(ExcCode.Ov, false)]
    [InlineData(ExcCode.AdES, true)]
    public void Execute_Kernel_DoubleException_KeepsCP0AndTerminates(ExcCode code, bool isAddress) {
        FakeInstruction inst = new(ExecuteResult.Raise(code, isAddress ? FaultAddress : null));
        RuntimeContext context = InstructionHarness.CreateKernel(ExcCode.Sys, PostEretEpc, PostEretBadVAddr);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // CP0 は変化せず，二重例外として記録されて終了する
        MachineState expected = before.WithLastException(new ExceptionEvent(code, IsDouble: true)).WithTerminated();
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Fact]
    public void Undo_DoubleException_ClearsLastExceptionAndIsTerminated() {
        FakeInstruction inst = new(ExecuteResult.Raise(ExcCode.Ov));
        RuntimeContext context = InstructionHarness.CreateKernel(ExcCode.Sys, PostEretEpc, PostEretBadVAddr);
        MachineState before = MachineState.Capture(context);
        Assert.Null(before.LastException);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    // ---- ランタイムエラー ----

    private const RuntimeErrorKind ErrorKind = RuntimeErrorKind.DivisionByZero;
    private const string ErrorMessage = "fake runtime error";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Execute_RuntimeError_RecordsErrorOnly(bool postEret) {
        FakeInstruction inst = new(ExecuteResult.Fail(ErrorKind, ErrorMessage));
        RuntimeContext context = postEret
            ? InstructionHarness.CreatePostEret(ExcCode.AdEL, PostEretEpc, PostEretBadVAddr)
            : InstructionHarness.CreateUser();
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);

        Assert.True(record.Failed);
        Assert.False(record.Raised);
        Assert.False(record.Completed);
        // ランタイムエラーだけが記録され，CP0 も実行モードも変わらない
        MachineState.AssertEqual(before.WithRuntimeError(ErrorKind), MachineState.Capture(context));
        Assert.Equal(new RuntimeError(ErrorKind, ErrorMessage), context.RuntimeError);
        Assert.False(context.IsKernelMode);
    }

    [Fact]
    public void Execute_RuntimeError_InKernel_NoDoubleException() {
        FakeInstruction inst = new(ExecuteResult.Fail(ErrorKind, ErrorMessage));
        RuntimeContext context = InstructionHarness.CreateKernel(ExcCode.Sys, PostEretEpc, PostEretBadVAddr);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // 二重例外にならず，CP0 (EXL=1)・LastException・IsTerminated は変化しない
        MachineState.AssertEqual(before.WithRuntimeError(ErrorKind), MachineState.Capture(context));
        Assert.Null(context.LastException);
        Assert.False(context.IsTerminated);
        Assert.True(context.IsKernelMode);
    }

    [Fact]
    public void Undo_RuntimeError_DoesNotCallInstructionUndo_ClearsError() {
        FakeInstruction inst = new(ExecuteResult.Fail(ErrorKind, ErrorMessage));
        RuntimeContext context = InstructionHarness.CreateUser();
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        Assert.Equal(1, inst.ExecuteCount);
        Assert.Equal(0, inst.UndoCount);
        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.Null(context.RuntimeError);
    }

    // ---- 命令フェッチでの例外 ----

    [Fact]
    public void RaiseFetchFault_AppliesExceptionAndUndoRestores() {
        RuntimeContext context = InstructionHarness.CreatePostEret(ExcCode.AdEL, PostEretEpc, PostEretBadVAddr);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.RaiseFetchFault(context, new ExceptionRequest(ExcCode.RI, null));

        Assert.Null(record.Instruction);
        Assert.True(record.Raised);
        MachineState.AssertEqual(before.WithException(ExcCode.RI, InstructionHarness.InstructionAddress), MachineState.Capture(context));

        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }
}
