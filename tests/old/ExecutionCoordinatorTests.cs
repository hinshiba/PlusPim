using PlusPim.Application;
using Xunit;

namespace PlusPimTests;

/// <summary>
/// <see cref="ExecutionCoordinator"/> のテスト．待機はすべてイベントで行い，時間に依存しない
/// </summary>
public class ExecutionCoordinatorTests {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 取り消されるまで待ってから <see cref="StopReason.Pause"/> を返す操作
    /// </summary>
    private static StopReason WaitForCancel(CancellationToken ct, ManualResetEventSlim started) {
        started.Set();
        _ = ct.WaitHandle.WaitOne(Timeout);
        return ct.IsCancellationRequested ? StopReason.Pause : StopReason.Step;
    }

    [Fact]
    public void TryStart_RunsBeforeStartThenOperationThenOnStopped() {
        ExecutionCoordinator coordinator = new();
        List<string> calls = [];
        using ManualResetEventSlim stopped = new();
        StopReason? result = null;

        Assert.True(coordinator.TryStart(
            _ => {
                lock(calls) {
                    calls.Add("operation");
                }
                return StopReason.Breakpoint;
            },
            () => {
                lock(calls) {
                    calls.Add("beforeStart");
                }
            },
            reason => {
                result = reason;
                stopped.Set();
            },
            ex => Assert.Fail(ex.ToString())));

        Assert.True(stopped.Wait(Timeout));
        Assert.Equal(StopReason.Breakpoint, result);
        Assert.Equal(["beforeStart", "operation"], calls);
    }

    [Fact]
    public void TryStart_RejectsWhileRunning_AndPauseStopsIt() {
        ExecutionCoordinator coordinator = new();
        using ManualResetEventSlim started = new();
        using ManualResetEventSlim stopped = new();
        StopReason? result = null;

        Assert.False(coordinator.RequestPause());
        Assert.True(coordinator.TryStart(ct => WaitForCancel(ct, started), () => { }, reason => {
            result = reason;
            stopped.Set();
        }, ex => Assert.Fail(ex.ToString())));
        Assert.True(started.Wait(Timeout));
        Assert.True(coordinator.IsRunning);

        // 実行中は次の操作を始めない
        bool secondRan = false;
        Assert.False(coordinator.TryStart(_ => {
            secondRan = true;
            return StopReason.Step;
        }, () => secondRan = true, _ => { }, _ => { }));

        Assert.True(coordinator.RequestPause());
        Assert.True(stopped.Wait(Timeout));
        Assert.Equal(StopReason.Pause, result);
        Assert.False(secondRan);

        // 停止の報告が終われば次の操作を始められる (報告の終わりはロックで待つ)
        using ManualResetEventSlim stoppedAgain = new();
        Assert.True(coordinator.TryStart(_ => StopReason.Step, () => { }, _ => stoppedAgain.Set(), _ => { }));
        Assert.True(stoppedAgain.Wait(Timeout));
    }

    [Fact]
    public void TryStart_ReportsFault() {
        ExecutionCoordinator coordinator = new();
        using ManualResetEventSlim faulted = new();
        Exception? reported = null;

        Assert.True(coordinator.TryStart(_ => throw new InvalidOperationException("boom"), () => { },
            _ => Assert.Fail("onStopped must not be called"),
            ex => {
                reported = ex;
                faulted.Set();
            }));

        Assert.True(faulted.Wait(Timeout));
        Assert.IsType<InvalidOperationException>(reported);
    }

    [Fact]
    public void Shutdown_CancelsAndRejectsLaterStarts() {
        ExecutionCoordinator coordinator = new();
        using ManualResetEventSlim started = new();
        bool reported = false;

        Assert.True(coordinator.TryStart(ct => WaitForCancel(ct, started), () => { }, _ => reported = true, _ => reported = true));
        Assert.True(started.Wait(Timeout));

        coordinator.Shutdown(Timeout);

        Assert.False(coordinator.IsRunning);
        // 終了処理の後は停止を報告しない
        Assert.False(reported);
        Assert.False(coordinator.TryStart(_ => StopReason.Step, () => { }, _ => { }, _ => { }));
    }
}
