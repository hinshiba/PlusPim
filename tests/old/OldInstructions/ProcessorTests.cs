using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
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

    // ---- ユーザーモードでの例外 ----

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

    // ---- カーネルモードでの例外 (二重例外) ----

    // ---- ランタイムエラー ----

    private const RuntimeErrorKind ErrorKind = RuntimeErrorKind.DivisionByZero;
    private const string ErrorMessage = "fake runtime error";

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
}
