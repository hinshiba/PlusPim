namespace PlusPim.Terminal;

/// <summary>
/// 端末をUTF-8に切り替えている間の状態を持ち，破棄または終了時に1回だけ元へ戻す
/// </summary>
internal sealed class TerminalUtf8Guard: IDisposable {
    private readonly ITerminalUtf8Backend backend;
    private int restored;

    private TerminalUtf8Guard(ITerminalUtf8Backend backend, bool isActive) {
        this.backend = backend;
        this.IsActive = isActive;
    }

    /// <summary>
    /// 実際に端末を切り替えたか
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// force のときだけ切り替えを試みる．切り替えられた場合は終了時の復元も登録する
    /// </summary>
    public static TerminalUtf8Guard Apply(ITerminalUtf8Backend backend, bool force) {
        if(!force || !backend.TrySwitchToUtf8()) {
            return new TerminalUtf8Guard(backend, false);
        }
        TerminalUtf8Guard guard = new(backend, true);
        backend.RegisterTerminationHook(guard.Restore);
        return guard;
    }

    /// <summary>
    /// 端末を元へ戻す
    /// </summary>
    public void Restore() {
        if(!this.IsActive || Interlocked.Exchange(ref this.restored, 1) != 0) {
            return;
        }
        this.backend.Restore();
    }

    public void Dispose() => this.Restore();
}
