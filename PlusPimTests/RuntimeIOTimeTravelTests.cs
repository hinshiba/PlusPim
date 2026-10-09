using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg;
using PlusPimTests.Instructions;
using Xunit;

namespace PlusPimTests;

/// <summary>
/// 入出力を行うランタイムコールを含む実行の時間遡行のテスト (doc/runtime.md「StepBack の挙動」)
/// </summary>
[Collection(ConsoleCollection.Name)]
public sealed class RuntimeIOTimeTravelTests: IDisposable {
    /// <summary>
    /// Sys なら <c>runtime_call!</c> を呼び，EPC+4 に復帰する最小の例外ハンドラ
    /// </summary>
    private const string Handler = """
        .ktext
        handler:
          runtime_call!
          mfc0 $k0, $14
          addiu $k0, $k0, 4
          mtc0 $k0, $14
          eret
        """;

    private readonly ConsoleRedirect _console = new();

    public void Dispose() {
        this._console.Dispose();
    }

    private static void WithDebugger(string asm, Action<PlusPimDbg> test) {
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            test(debugger);
        } finally {
            tempFile.Delete();
        }
    }

    /// <summary>
    /// 終了するまで (最大 <paramref name="maxSteps"/> 回) ステップ実行し，初期状態と各ステップ後のスナップショットを返す
    /// </summary>
    private static List<DebuggerSnapshot> RunToEnd(PlusPimDbg debugger, int maxSteps = 100) {
        List<DebuggerSnapshot> snapshots = [TestHelpers.TakeSnapshot(debugger)];
        for(int i = 0; i < maxSteps; i++) {
            StopReason reason = debugger.Step();
            snapshots.Add(TestHelpers.TakeSnapshot(debugger));
            if(reason is StopReason.Terminated or StopReason.RuntimeError) {
                break;
            }
        }
        return snapshots;
    }

    [Fact]
    public void StepBack_OverReadString_ReturnsConsumedInputToPendingInput() {
        string asm = $"""
            .text
            main:
              addiu $v0, $zero, 8
              lui $a0, 0x1001
              addiu $a1, $zero, 3
              syscall
              addiu $v0, $zero, 10
              syscall
            {Handler}
            """;
        WithDebugger(asm, debugger => {
            this._console.SetInput("abcdef\n");
            List<DebuggerSnapshot> snapshots = RunToEnd(debugger);

            // read_string は "ab" を書き込み，残りの "cdef\n" を入力バッファに残す
            int readStep = snapshots.FindIndex(s => s.PendingInput == "cdef\n");
            Assert.True(readStep > 0);
            Assert.Equal("", snapshots[readStep - 1].PendingInput);
            Assert.Equal("cdef\n", snapshots[^1].PendingInput);

            // read_string の後まで戻す
            for(int i = snapshots.Count - 2; i >= readStep; i--) {
                Assert.True(debugger.Back());
                TestHelpers.AssertSnapshotEqual(snapshots[i], debugger);
            }

            // read_string を戻すと，消費した入力が入力バッファの先頭に戻る
            Assert.True(debugger.Back());
            TestHelpers.AssertSnapshotEqual(snapshots[readStep - 1] with { PendingInput = "abcdef\n" }, debugger);

            // 再実行すると同じ入力を読む
            _ = debugger.Step();
            TestHelpers.AssertSnapshotEqual(snapshots[readStep], debugger);
        });
    }
}
