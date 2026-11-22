using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PlusPim.Terminal;

/// <summary>
/// kernel32 を直接呼ぶ <see cref="IConsoleCodePage"/>
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class Win32ConsoleCodePage: IConsoleCodePage {
    private delegate int HandlerRoutine(uint ctrlType);

    // GCされるとネイティブ側から呼べなくなるので静的に保持する
    private static HandlerRoutine? controlHandler;

    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleOutputCP();

    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleCP();

    [DllImport("kernel32.dll")]
    private static extern int SetConsoleOutputCP(uint codePageId);

    [DllImport("kernel32.dll")]
    private static extern int SetConsoleCP(uint codePageId);

    [DllImport("kernel32.dll")]
    private static extern int SetConsoleCtrlHandler(HandlerRoutine? handlerRoutine, int add);

    // コンソールがなければどちらも0を返す
    public bool IsConsoleAttached => GetConsoleOutputCP() != 0 && GetConsoleCP() != 0;

    public uint GetOutputCodePage() => GetConsoleOutputCP();

    public uint GetInputCodePage() => GetConsoleCP();

    public bool SetOutputCodePage(uint codePage) => SetConsoleOutputCP(codePage) != 0;

    public bool SetInputCodePage(uint codePage) => SetConsoleCP(codePage) != 0;

    public void RegisterControlHandler(Action handler) {
        controlHandler = (_) => {
            try {
                handler();
            } catch {
                // 終了処理中なので失敗は無視する
            }
            // false を返して既定の処理(プロセス終了)を続けさせる
            return 0;
        };
        _ = SetConsoleCtrlHandler(controlHandler, 1);
    }
}
