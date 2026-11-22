namespace PlusPim.Terminal;

/// <summary>
/// 何もしない切り替え機構．切り替え方法が分からないOSや，端末が接続されていない環境で使う
/// </summary>
internal sealed class NoOpTerminalUtf8Backend: ITerminalUtf8Backend {
    public bool TrySwitchToUtf8() => false;

    public void Restore() {
    }

    public void RegisterTerminationHook(Action handler) {
    }
}
