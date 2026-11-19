using PlusPim.Terminal;
using Xunit;

namespace PlusPimTests;

public class WindowsConsoleUtf8BackendTests {
    /// <summary>
    /// コードページの読み書きを記録する偽のコンソール．kernel32 には触れない
    /// </summary>
    private sealed class FakeConsoleCodePage(bool attached, uint outputCp, uint inputCp, bool setOutputResult = true, bool setInputResult = true): IConsoleCodePage {
        public List<string> Calls { get; } = [];
        public Action? ControlHandler { get; private set; }

        public bool IsConsoleAttached {
            get {
                this.Calls.Add("Attached");
                return attached;
            }
        }

        public uint GetOutputCodePage() {
            this.Calls.Add("GetOut");
            return outputCp;
        }

        public uint GetInputCodePage() {
            this.Calls.Add("GetIn");
            return inputCp;
        }

        public bool SetOutputCodePage(uint codePage) {
            this.Calls.Add($"SetOut:{codePage}");
            return setOutputResult;
        }

        public bool SetInputCodePage(uint codePage) {
            this.Calls.Add($"SetIn:{codePage}");
            return setInputResult;
        }

        public void RegisterControlHandler(Action handler) {
            this.Calls.Add("Register");
            this.ControlHandler = handler;
        }
    }

    private static List<string> Sets(FakeConsoleCodePage console) => [.. console.Calls.Where(c => c.StartsWith("Set"))];

    [Fact]
    public void TrySwitchToUtf8_SwitchesBothAndRestoreReturnsThem() {
        FakeConsoleCodePage console = new(true, 932, 932);
        WindowsConsoleUtf8Backend backend = new(console);

        Assert.True(backend.TrySwitchToUtf8());
        Assert.Equal(["SetOut:65001", "SetIn:65001"], Sets(console));

        backend.Restore();
        Assert.Equal(["SetOut:65001", "SetIn:65001", "SetOut:932", "SetIn:932"], Sets(console));
    }

    [Fact]
    public void TrySwitchToUtf8_NotAttached_OnlyChecksAttachment() {
        FakeConsoleCodePage console = new(false, 932, 932);
        WindowsConsoleUtf8Backend backend = new(console);

        Assert.False(backend.TrySwitchToUtf8());
        backend.Restore();

        Assert.Equal(["Attached"], console.Calls);
    }

    [Fact]
    public void TrySwitchToUtf8_OutputAlreadyUtf8_LeavesOutputAndSwitchesInput() {
        FakeConsoleCodePage console = new(true, 65001, 932);
        WindowsConsoleUtf8Backend backend = new(console);

        Assert.True(backend.TrySwitchToUtf8());
        backend.Restore();

        Assert.Equal(["SetIn:65001", "SetIn:932"], Sets(console));
    }

    [Fact]
    public void TrySwitchToUtf8_BothAlreadyUtf8_ReturnsFalseAndChangesNothing() {
        FakeConsoleCodePage console = new(true, 65001, 65001);
        WindowsConsoleUtf8Backend backend = new(console);

        Assert.False(backend.TrySwitchToUtf8());
        backend.Restore();

        Assert.Empty(Sets(console));
    }

    [Fact]
    public void TrySwitchToUtf8_OutputSetFails_OutputNotRestoredInputStillHandled() {
        FakeConsoleCodePage console = new(true, 932, 437, setOutputResult: false);
        WindowsConsoleUtf8Backend backend = new(console);

        Assert.True(backend.TrySwitchToUtf8());
        backend.Restore();

        Assert.Equal(["SetOut:65001", "SetIn:65001", "SetIn:437"], Sets(console));
    }

    [Fact]
    public void TrySwitchToUtf8_BothSetsFail_ReturnsFalseAndRestoresNothing() {
        FakeConsoleCodePage console = new(true, 932, 932, setOutputResult: false, setInputResult: false);
        WindowsConsoleUtf8Backend backend = new(console);

        Assert.False(backend.TrySwitchToUtf8());
        backend.Restore();

        Assert.Equal(["SetOut:65001", "SetIn:65001"], Sets(console));
    }

    [Fact]
    public void RegisterTerminationHook_ForwardsToControlHandler() {
        FakeConsoleCodePage console = new(true, 932, 932);
        WindowsConsoleUtf8Backend backend = new(console);
        int called = 0;

        backend.RegisterTerminationHook(() => called++);
        console.ControlHandler!();

        Assert.Equal(1, called);
    }
}
