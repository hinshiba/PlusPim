using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;
using Xunit;
using App = PlusPim.Application.Application;

namespace PlusPimTests;

/// <summary>
/// <see cref="App"/> の実行の取り消しと，実行中の読み取りのテスト
/// </summary>
/// <remarks>待機は状態の変化を条件にし，時間で成否を決めない (タイムアウトはハングの検出のためだけに使う)</remarks>
public class ApplicationExecutionTests {
    private const uint TextBase = 0x00400000;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// <c>$t0</c> を増やし続ける無限ループ．ループは <see cref="TextBase"/> + 4 から + 8
    /// </summary>
    internal const string InfiniteLoop = """
        .text
        main:
          addiu $t0, $zero, 0
        loop:
          addiu $t0, $t0, 1
          j loop
        """;

    private static async Task WithApplication(string asm, Func<App, FileInfo, Task> test) {
        FileInfo tempFile = TestHelpers.WriteTempAsm(asm);
        try {
            App app = new([tempFile], Logger.Null);
            Assert.True(app.Load());
            await test(app, tempFile);
        } finally {
            tempFile.Delete();
        }
    }

    /// <summary>
    /// 操作を専用のスレッドで始める
    /// </summary>
    private static Task<StopReason> Start(Func<StopReason> operation) {
        return Task.Factory.StartNew(operation, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private static uint T0(App app) {
        return app.GetCallStack()[0].Registers[(int)RegisterID.T0];
    }

    /// <summary>
    /// 条件が成り立つまで待つ
    /// </summary>
    private static void WaitUntil(Func<bool> condition) {
        DateTime deadline = DateTime.UtcNow + Timeout;
        while(!condition()) {
            Assert.True(DateTime.UtcNow < deadline, "Condition was not met.");
            _ = Thread.Yield();
        }
    }

    [Fact]
    public Task Continue_WithCancelledToken_PausesWithoutStepping() {
        return WithApplication(InfiniteLoop, (app, _) => {
            using CancellationTokenSource cts = new();
            cts.Cancel();

            Assert.Equal(StopReason.Pause, app.Continue(cts.Token));
            Assert.Equal(TextBase, app.GetCallStack()[0].PC);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public Task Continue_IsPausedWhileRunning_AndStateIsReadableDuringRun() {
        return WithApplication(InfiniteLoop, async (app, _) => {
            using CancellationTokenSource cts = new();
            Task<StopReason> run = Start(() => app.Continue(cts.Token));

            // 実行中もレジスタを読める．ループが回っていることを確かめる
            WaitUntil(() => 1000 < T0(app));
            Assert.False(run.IsCompleted);

            cts.Cancel();
            Assert.Equal(StopReason.Pause, await run.WaitAsync(Timeout));

            uint pc = app.GetCallStack()[0].PC;
            Assert.InRange(pc, TextBase + 4, TextBase + 8);

            // 停止後も実行を続けられる
            uint t0 = T0(app);
            Assert.Equal(StopReason.Step, app.StepIn());
            Assert.NotEqual(pc, app.GetCallStack()[0].PC);
            Assert.True(t0 <= T0(app));
        });
    }

    [Fact]
    public Task ReverseContinue_IsPausedWhileRunning() {
        return WithApplication(InfiniteLoop, async (app, _) => {
            using(CancellationTokenSource forward = new()) {
                Task<StopReason> run = Start(() => app.Continue(forward.Token));
                WaitUntil(() => 100_000 < T0(app));
                forward.Cancel();
                Assert.Equal(StopReason.Pause, await run.WaitAsync(Timeout));
            }

            uint before = T0(app);
            using CancellationTokenSource cts = new();
            Task<StopReason> reverse = Start(() => app.ReverseContinue(cts.Token));

            // 巻き戻し中も読める
            WaitUntil(() => T0(app) < before);
            cts.Cancel();
            StopReason result = await reverse.WaitAsync(Timeout);

            // 履歴の先頭に達する前に止めたなら Pause で，まだ戻れる．先に達したなら HistoryStart
            Assert.Contains(result, new[] { StopReason.Pause, StopReason.HistoryStart });
            Assert.Equal(result == StopReason.Pause, app.StepBack());
        });
    }

    [Fact]
    public Task ReverseContinue_ReachesHistoryStart() {
        return WithApplication(InfiniteLoop, (app, _) => {
            for(int i = 0; i < 5; i++) {
                Assert.Equal(StopReason.Step, app.StepIn());
            }

            Assert.Equal(StopReason.HistoryStart, app.ReverseContinue());
            Assert.Equal(TextBase, app.GetCallStack()[0].PC);
            Assert.Equal(0u, T0(app));

            // 先頭でも HistoryStart
            Assert.Equal(StopReason.HistoryStart, app.ReverseContinue());
            Assert.False(app.StepBack());
            return Task.CompletedTask;
        });
    }

    [Fact]
    public Task SetBreakpoints_WhileRunning_StopsTheRun() {
        return WithApplication(InfiniteLoop, async (app, file) => {
            Task<StopReason> run = Start(() => app.Continue());
            WaitUntil(() => 1000 < T0(app));

            // 実行中に設定したブレークポイントで止まる (j loop の行)
            Assert.True(app.SetBreakpoints(file, [6])[0].Verified);
            Assert.Equal(StopReason.Breakpoint, await run.WaitAsync(Timeout));
            Assert.Equal(TextBase + 8, app.GetCallStack()[0].PC);
        });
    }
}
