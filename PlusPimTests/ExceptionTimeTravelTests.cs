using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests;

/// <summary>
/// 例外を含む実行の時間遡行のテスト (doc/tests/debugger/history_model.md)
/// </summary>
public class ExceptionTimeTravelTests {
    /// <summary>
    /// EPC+4 に復帰する例外ハンドラ
    /// </summary>
    private const string SkippingHandler = """
        .ktext
        handler:
          mfc0 $k0, $14
          addiu $k0, $k0, 4
          mtc0 $k0, $14
          eret
        """;

    private const uint TextBase = 0x00400000;
    private const uint KernelTextBase = 0x80000180;

    /// <summary>
    /// <paramref name="steps"/> 回ステップし，初期状態と各ステップ後の状態を返す (index 0 が初期状態)
    /// </summary>
    private static List<DebuggerSnapshot> StepAndRecord(PlusPimDbg debugger, int steps) {
        List<DebuggerSnapshot> snapshots = [TestHelpers.TakeSnapshot(debugger)];
        for(int i = 0; i < steps; i++) {
            _ = debugger.Step();
            snapshots.Add(TestHelpers.TakeSnapshot(debugger));
        }
        return snapshots;
    }

    /// <summary>
    /// 1ステップずつ戻り，各ステップ前の状態と一致することを確認する．最後は初期状態で，それ以上戻れない
    /// </summary>
    private static void AssertRewind(PlusPimDbg debugger, List<DebuggerSnapshot> snapshots) {
        for(int i = snapshots.Count - 2; i >= 0; i--) {
            Assert.True(debugger.Back());
            TestHelpers.AssertSnapshotEqual(snapshots[i], debugger);
        }
        Assert.False(debugger.Back());
    }

    private static void WithDebugger(string asm, Action<PlusPimDbg> test) {
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            test(debugger);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void StepBack_AfterOverflow_RestoresCP0AndLastException() {
        string asm = $"""
            .text
            main:
              lui $t0, 0x7fff
              ori $t0, $t0, 0xffff
              addi $t1, $t0, 1
            {SkippingHandler}
            """;
        WithDebugger(asm, debugger => {
            _ = debugger.Step();
            _ = debugger.Step();
            DebuggerSnapshot beforeAddi = TestHelpers.TakeSnapshot(debugger);

            Assert.Equal(StopReason.Exception, debugger.Step());
            Assert.Equal(ExcCode.Ov, debugger.GetLastException()?.Reason);
            Assert.Equal(0x2u, debugger.GetCallStack()[0].CP0Status);

            Assert.True(debugger.Back());

            TestHelpers.AssertSnapshotEqual(beforeAddi, debugger);
            Assert.Null(debugger.GetLastException());
            Assert.Equal(0x0u, debugger.GetCallStack()[0].CP0Status);
        });
    }

    [Fact]
    public void StepBack_OverHandlerTransition_RestoresPcAndLastException() {
        string asm = $"""
            .text
            main:
              lui $t0, 0x7fff
              ori $t0, $t0, 0xffff
              addi $t1, $t0, 1
            {SkippingHandler}
            """;
        WithDebugger(asm, debugger => {
            _ = debugger.Step();
            _ = debugger.Step();
            Assert.Equal(StopReason.Exception, debugger.Step());
            DebuggerSnapshot atException = TestHelpers.TakeSnapshot(debugger);

            // ハンドラへの遷移
            Assert.Equal(StopReason.Step, debugger.Step());
            Assert.Equal(KernelTextBase, debugger.GetCallStack()[0].PC);
            Assert.Null(debugger.GetLastException());

            Assert.True(debugger.Back());

            TestHelpers.AssertSnapshotEqual(atException, debugger);
            Assert.Equal(TextBase + 8, debugger.GetCallStack()[0].PC);
            Assert.Equal(ExcCode.Ov, debugger.GetLastException()?.Reason);
        });
    }

    [Fact]
    public void StepBack_MisalignedLw_ThroughHandler_ReturnsToLwInUserMode() {
        string asm = $"""
            .text
            main:
              lui $t0, 0x1000
              lw $t1, 1($t0)
              addiu $t2, $zero, 1
            {SkippingHandler}
            """;
        WithDebugger(asm, debugger => {
            _ = debugger.Step(); // lui
            DebuggerSnapshot beforeLw = TestHelpers.TakeSnapshot(debugger);

            Assert.Equal(StopReason.Exception, debugger.Step()); // lw (AdEL)
            Assert.Equal(0x10000001u, debugger.GetCallStack()[0].CP0BadVAddr);
            _ = debugger.Step(); // ハンドラへの遷移
            _ = debugger.Step(); // mfc0

            Assert.True(debugger.Back());
            Assert.True(debugger.Back());
            Assert.True(debugger.Back());

            TestHelpers.AssertSnapshotEqual(beforeLw, debugger);
            Assert.Equal(TextBase + 4, debugger.GetCallStack()[0].PC);
            Assert.Equal(0x0u, debugger.GetCallStack()[0].CP0Status);
            Assert.Equal(0x0u, debugger.GetCallStack()[0].CP0BadVAddr);
        });
    }

    [Fact]
    public void StepBack_ToStartAfterException_ReexecutesFromFirstInstruction() {
        string asm = $"""
            .text
            main:
              lui $t0, 0x7fff
              ori $t0, $t0, 0xffff
              addi $t1, $t0, 1
              addiu $t2, $zero, 2
              addiu $t3, $zero, 3
            {SkippingHandler}
            """;
        WithDebugger(asm, debugger => {
            // lui, ori, addi(Ov), 遷移, mfc0, addiu, mtc0, eret, addiu $t2, addiu $t3
            const int steps = 10;
            List<DebuggerSnapshot> forward = StepAndRecord(debugger, steps);
            Assert.Equal(TextBase + 20, forward[^1].PC);
            Assert.Equal(2u, forward[^1].Registers[(int)RegisterID.T2]);

            AssertRewind(debugger, forward);

            // 先頭から再実行すると同じ状態を辿る
            List<DebuggerSnapshot> again = StepAndRecord(debugger, steps);
            for(int i = 0; i < forward.Count; i++) {
                TestHelpers.AssertSnapshotEqual(forward[i], again[i]);
            }
        });
    }

    [Fact]
    public void StepBack_SyscallHandlerEretRoundtrip_MatchesInitialState() {
        string asm = $"""
            .text
            main:
              addiu $t0, $zero, 1
              syscall
              addiu $t1, $zero, 2
            {SkippingHandler}
            """;
        WithDebugger(asm, debugger => {
            // addiu, syscall(Sys), 遷移, mfc0, addiu, mtc0, eret, addiu
            List<DebuggerSnapshot> forward = StepAndRecord(debugger, 8);
            Assert.Equal(ExcCode.Sys, forward[2].ExceptionCode);
            // eret で syscall の次の命令に戻り，ユーザーモードになる
            Assert.Equal(TextBase + 8, forward[7].PC);
            Assert.Equal(0x0u, forward[7].Status);
            Assert.Equal(2u, forward[8].Registers[(int)RegisterID.T1]);

            AssertRewind(debugger, forward);
        });
    }

    [Fact]
    public void StepBack_DoubleException_LeavesNoLastException() {
        string asm = """
            .text
            main:
              syscall
            .ktext
            handler:
              break
            """;
        WithDebugger(asm, debugger => {
            Assert.Equal(StopReason.Exception, debugger.Step()); // syscall
            _ = debugger.Step(); // ハンドラへの遷移
            DebuggerSnapshot beforeBreak = TestHelpers.TakeSnapshot(debugger);

            Assert.Equal(StopReason.Terminated, debugger.Step()); // break (二重例外)
            Assert.True(debugger.GetLastException()?.IsDouble);

            Assert.True(debugger.Back());

            TestHelpers.AssertSnapshotEqual(beforeBreak, debugger);
            Assert.Null(debugger.GetLastException());

            // 終了状態も戻っているので，再実行すると再び二重例外が起きる
            Assert.Equal(StopReason.Terminated, debugger.Step());
            Assert.True(debugger.GetLastException()?.IsDouble);
        });
    }

    [Fact]
    public void StepBack_FetchFault_RestoresState() {
        string asm = $"""
            .text
            main:
              addiu $t0, $zero, 1
            {SkippingHandler}
            """;
        WithDebugger(asm, debugger => {
            _ = debugger.Step();
            DebuggerSnapshot beforeFetch = TestHelpers.TakeSnapshot(debugger);

            // テキストセグメントの範囲外の命令は RI．BadVAddr は変化しない
            Assert.Equal(StopReason.Exception, debugger.Step());
            Assert.Equal(ExcCode.RI, debugger.GetLastException()?.Reason);
            StackFrameInfo frame = debugger.GetCallStack()[0];
            Assert.Equal(TextBase + 4, frame.CP0EPC);
            Assert.Equal(0x0u, frame.CP0BadVAddr);

            Assert.True(debugger.Back());

            TestHelpers.AssertSnapshotEqual(beforeFetch, debugger);
        });
    }

    [Fact]
    public void Step_MisalignedFetch_SetsBadVAddrToPc() {
        string asm = $"""
            .text
            main:
              lui $t0, 0x0040
              ori $t0, $t0, 0x0002
              jr $t0
            {SkippingHandler}
            """;
        WithDebugger(asm, debugger => {
            _ = debugger.Step();
            _ = debugger.Step();
            _ = debugger.Step();
            DebuggerSnapshot beforeFetch = TestHelpers.TakeSnapshot(debugger);

            Assert.Equal(StopReason.Exception, debugger.Step());
            Assert.Equal(ExcCode.AdEL, debugger.GetLastException()?.Reason);
            Assert.Equal(TextBase + 2, debugger.GetCallStack()[0].CP0BadVAddr);

            Assert.True(debugger.Back());

            TestHelpers.AssertSnapshotEqual(beforeFetch, debugger);
        });
    }

    [Fact]
    public void Step_WhenTerminated_IsNoOpAndPushesNoHistory() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 1
              jr $ra
            """;
        WithDebugger(asm, debugger => {
            List<DebuggerSnapshot> forward = StepAndRecord(debugger, 1);
            Assert.Equal(StopReason.Terminated, debugger.Step()); // jr $ra (main からの戻り)
            DebuggerSnapshot terminated = TestHelpers.TakeSnapshot(debugger);

            Assert.Equal(StopReason.Terminated, debugger.Step());
            Assert.Equal(StopReason.Terminated, debugger.Step());
            TestHelpers.AssertSnapshotEqual(terminated, debugger);

            // 終了後のステップは履歴に積まれないので，1回の Back で jr の前に戻る
            Assert.True(debugger.Back());
            TestHelpers.AssertSnapshotEqual(forward[1], debugger);
            Assert.True(debugger.Back());
            TestHelpers.AssertSnapshotEqual(forward[0], debugger);
            Assert.False(debugger.Back());
        });
    }

    [Theory]
    [InlineData("j end")]
    [InlineData("beq $zero, $zero, end")]
    public void Step_SelfJump_StaysOnInstruction(string selfJump) {
        string asm = $"""
            .text
            main:
              addiu $t0, $zero, 1
            end:
              {selfJump}
            """;
        WithDebugger(asm, debugger => {
            _ = debugger.Step();
            Assert.Equal(TextBase + 4, debugger.GetCallStack()[0].PC);

            Assert.Equal(StopReason.Step, debugger.Step());
            Assert.Equal(TextBase + 4, debugger.GetCallStack()[0].PC);
            Assert.Equal(StopReason.Step, debugger.Step());
            Assert.Equal(TextBase + 4, debugger.GetCallStack()[0].PC);

            Assert.True(debugger.Back());
            Assert.Equal(TextBase + 4, debugger.GetCallStack()[0].PC);
            Assert.True(debugger.Back());
            Assert.Equal(TextBase + 4, debugger.GetCallStack()[0].PC);
            Assert.True(debugger.Back());
            Assert.Equal(TextBase, debugger.GetCallStack()[0].PC);
        });
    }
}
