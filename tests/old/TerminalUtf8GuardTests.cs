using PlusPim.Terminal;
using Xunit;

namespace PlusPimTests;

public class TerminalUtf8GuardTests {
    /// <summary>
    /// 呼び出しを記録するだけの偽の切り替え機構
    /// </summary>
    private sealed class FakeBackend(bool switchResult): ITerminalUtf8Backend {
        public List<string> Calls { get; } = [];
        public Action? Hook { get; private set; }

        public bool TrySwitchToUtf8() {
            this.Calls.Add("Switch");
            return switchResult;
        }

        public void Restore() => this.Calls.Add("Restore");

        public void RegisterTerminationHook(Action handler) {
            this.Calls.Add("Hook");
            this.Hook = handler;
        }
    }

    [Fact]
    public void Apply_NotForced_DoesNotTouchBackend() {
        FakeBackend backend = new(true);

        using TerminalUtf8Guard guard = TerminalUtf8Guard.Apply(backend, false);

        Assert.False(guard.IsActive);
        Assert.Empty(backend.Calls);
    }

    [Fact]
    public void Apply_ForcedAndSwitched_RegistersHookOnce() {
        FakeBackend backend = new(true);

        using TerminalUtf8Guard guard = TerminalUtf8Guard.Apply(backend, true);

        Assert.True(guard.IsActive);
        Assert.Equal(["Switch", "Hook"], backend.Calls);
    }

    [Fact]
    public void Apply_ForcedButNotSwitched_IsInactiveAndRegistersNoHook() {
        FakeBackend backend = new(false);

        TerminalUtf8Guard guard = TerminalUtf8Guard.Apply(backend, true);
        guard.Dispose();

        Assert.False(guard.IsActive);
        Assert.Equal(["Switch"], backend.Calls);
    }

    [Fact]
    public void Restore_CallsBackendOnlyOnce() {
        FakeBackend backend = new(true);
        TerminalUtf8Guard guard = TerminalUtf8Guard.Apply(backend, true);

        guard.Restore();
        guard.Restore();
        guard.Dispose();

        Assert.Equal(["Switch", "Hook", "Restore"], backend.Calls);
    }

    [Fact]
    public void Dispose_RestoresWhenExceptionIsThrown() {
        FakeBackend backend = new(true);

        try {
            using TerminalUtf8Guard guard = TerminalUtf8Guard.Apply(backend, true);
            throw new InvalidOperationException();
        } catch(InvalidOperationException) {
        }

        Assert.Equal(["Switch", "Hook", "Restore"], backend.Calls);
    }

    [Fact]
    public void TerminationHook_Restores_AndLaterDisposeIsNoOp() {
        FakeBackend backend = new(true);
        TerminalUtf8Guard guard = TerminalUtf8Guard.Apply(backend, true);

        backend.Hook!();
        guard.Dispose();

        Assert.Equal(["Switch", "Hook", "Restore"], backend.Calls);
    }

    [Fact]
    public void NoOpBackend_DoesNothing() {
        NoOpTerminalUtf8Backend backend = new();

        Assert.False(backend.TrySwitchToUtf8());
        backend.RegisterTerminationHook(() => throw new InvalidOperationException());
        backend.Restore();
    }

    [Fact]
    public void Create_ReturnsBackendForCurrentOs() {
        ITerminalUtf8Backend backend = TerminalUtf8Backends.Create();

        if(OperatingSystem.IsWindows()) {
            Assert.IsType<WindowsConsoleUtf8Backend>(backend);
        } else {
            Assert.IsType<NoOpTerminalUtf8Backend>(backend);
        }
    }
}
