using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests;

/// <summary>
/// ランタイムエラーを含む実行の時間遡行のテスト
/// (doc/tests/debugger/history_model.md，doc/tests/instructions/runtime_error_model.md)
/// </summary>
public class RuntimeErrorTimeTravelTests {
    private const uint TextBase = 0x00400000;
    private const uint KernelTextBase = 0x80000180;

    /// <summary>
    /// 7 をゼロで割る．<c>div</c> は <see cref="TextBase"/> + 4 にある
    /// </summary>
    private const string DivByZero = """
        .text
        main:
          addiu $t0, $zero, 7
          div $t0, $zero
          addiu $t1, $zero, 1
        """;

    private const uint DivAddress = TextBase + 4;

    /// <summary>
    /// kseg.asm と同様の例外ハンドラ．Sys なら <c>runtime_call!</c> を呼び，EPC+4 に復帰する．
    /// <c>runtime_call!</c> はハンドラの先頭から 11 命令目にある
    /// </summary>
    private const string KsegLikeHandler = """
        .ktext
        handler:
          mfc0 $k0, $13
          srl $k0, $k0, 2
          andi $k0, $k0, 0x1f
          addiu $k1, $zero, 8
          beq $k0, $k1, handle_syscall
          nop
          mfc0 $k0, $14
          addiu $k0, $k0, 4
          mtc0 $k0, $14
          eret
        handle_syscall:
          runtime_call!
          mfc0 $k0, $14
          addiu $k0, $k0, 4
          mtc0 $k0, $14
          eret
        """;

    private const uint RuntimeCallAddress = KernelTextBase + (10 * 4);

    private static void WithDebugger(string asm, Action<PlusPimDbg> test) {
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            test(debugger);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void Step_DivByZero_StopsAtDivWithRuntimeError() {
        WithDebugger(DivByZero, debugger => {
            _ = debugger.Step(); // addiu
            DebuggerSnapshot beforeDiv = TestHelpers.TakeSnapshot(debugger);

            Assert.Equal(StopReason.RuntimeError, debugger.Step());

            // PC は div に留まり，HI/LO と CP0 は変化しない．例外は発生しない
            TestHelpers.AssertSnapshotEqual(beforeDiv with { RuntimeError = RuntimeErrorKind.DivisionByZero }, debugger);
            Assert.Equal(DivAddress, debugger.GetCallStack()[0].PC);
            Assert.Null(debugger.GetLastException());

            RuntimeErrorInfo? error = debugger.GetRuntimeError();
            Assert.NotNull(error);
            Assert.Equal(RuntimeErrorKind.DivisionByZero, error.Kind);
            Assert.Equal(nameof(RuntimeErrorKind.DivisionByZero), error.Id);
            Assert.Equal(DivAddress, error.Address);
            Assert.False(string.IsNullOrEmpty(error.Description));
        });
    }

    [Fact]
    public void Step_AfterRuntimeError_IsNoOpAndPushesNoHistory() {
        WithDebugger(DivByZero, debugger => {
            DebuggerSnapshot initial = TestHelpers.TakeSnapshot(debugger);
            _ = debugger.Step(); // addiu
            DebuggerSnapshot beforeDiv = TestHelpers.TakeSnapshot(debugger);
            Assert.Equal(StopReason.RuntimeError, debugger.Step());
            DebuggerSnapshot atError = TestHelpers.TakeSnapshot(debugger);

            // ランタイムエラーの後のステップは何もしない
            Assert.Equal(StopReason.RuntimeError, debugger.Step());
            Assert.Equal(StopReason.RuntimeError, debugger.Step());
            TestHelpers.AssertSnapshotEqual(atError, debugger);

            // 履歴に積まれないので，1回の Back で div の前に戻る
            Assert.True(debugger.Back());
            TestHelpers.AssertSnapshotEqual(beforeDiv, debugger);
            Assert.Null(debugger.GetRuntimeError());
            Assert.True(debugger.Back());
            TestHelpers.AssertSnapshotEqual(initial, debugger);
            Assert.False(debugger.Back());
        });
    }

    [Fact]
    public void StepBack_FromRuntimeError_RestoresAndReexecutesSameError() {
        WithDebugger(DivByZero, debugger => {
            _ = debugger.Step(); // addiu
            DebuggerSnapshot beforeDiv = TestHelpers.TakeSnapshot(debugger);
            Assert.Equal(StopReason.RuntimeError, debugger.Step());
            DebuggerSnapshot atError = TestHelpers.TakeSnapshot(debugger);

            Assert.True(debugger.Back());
            TestHelpers.AssertSnapshotEqual(beforeDiv, debugger);

            // 再実行すると同じランタイムエラーが起きる
            Assert.Equal(StopReason.RuntimeError, debugger.Step());
            TestHelpers.AssertSnapshotEqual(atError, debugger);
        });
    }

    [Fact]
    public void StepBack_UnknownSyscall_InHandler_RewindsToInitial() {
        string asm = $"""
            .text
            main:
              li $v0, 13
              syscall
              addiu $t0, $zero, 1
            {KsegLikeHandler}
            """;
        WithDebugger(asm, debugger => {
            List<DebuggerSnapshot> snapshots = [TestHelpers.TakeSnapshot(debugger)];
            List<StopReason> reasons = [];
            while(reasons.Count < 50) {
                StopReason reason = debugger.Step();
                reasons.Add(reason);
                snapshots.Add(TestHelpers.TakeSnapshot(debugger));
                if(reason is StopReason.RuntimeError or StopReason.Terminated) {
                    break;
                }
            }

            // syscall で Sys，次のステップでハンドラへ遷移し，runtime_call! でランタイムエラー
            Assert.Equal(StopReason.RuntimeError, reasons[^1]);
            int syscallStep = reasons.IndexOf(StopReason.Exception);
            Assert.True(syscallStep >= 0);
            Assert.Equal(ExcCode.Sys, snapshots[syscallStep + 1].ExceptionCode);
            Assert.Equal(KernelTextBase, snapshots[syscallStep + 2].PC);

            DebuggerSnapshot atError = snapshots[^1];
            Assert.Equal(RuntimeErrorKind.UnknownRuntimeCall, atError.RuntimeError);
            Assert.Equal(RuntimeCallAddress, atError.PC);
            Assert.Equal(0x2u, atError.Status); // EXL=1 のまま (二重例外にならない)
            Assert.Null(atError.ExceptionCode);
            Assert.Equal(RuntimeCallAddress, debugger.GetRuntimeError()?.Address);

            // 先頭まで戻ると初期状態と一致する
            for(int i = snapshots.Count - 2; i >= 0; i--) {
                Assert.True(debugger.Back());
                TestHelpers.AssertSnapshotEqual(snapshots[i], debugger);
            }
            Assert.False(debugger.Back());
        });
    }

    [Fact]
    public void Step_UnsupportedCP0InHandler_StopsWithRuntimeError() {
        string asm = """
            .text
            main:
              syscall
            .ktext
            handler:
              mfc0 $k0, $15
              eret
            """;
        WithDebugger(asm, debugger => {
            Assert.Equal(StopReason.Exception, debugger.Step()); // syscall
            Assert.Equal(StopReason.Step, debugger.Step()); // ハンドラへの遷移
            DebuggerSnapshot beforeMfc0 = TestHelpers.TakeSnapshot(debugger);

            // カーネルモードでも二重例外 (終了) ではなくランタイムエラー
            Assert.Equal(StopReason.RuntimeError, debugger.Step());
            TestHelpers.AssertSnapshotEqual(beforeMfc0 with { RuntimeError = RuntimeErrorKind.UnsupportedCP0Register }, debugger);
            Assert.Equal(KernelTextBase, debugger.GetCallStack()[0].PC);
            Assert.Null(debugger.GetLastException());

            Assert.True(debugger.Back());
            TestHelpers.AssertSnapshotEqual(beforeMfc0, debugger);
        });
    }
}
