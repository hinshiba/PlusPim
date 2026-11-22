namespace PlusPim.Terminal;

/// <summary>
/// 実行中のOSに合った <see cref="ITerminalUtf8Backend"/> を選ぶ
/// </summary>
internal static class TerminalUtf8Backends {
    public static ITerminalUtf8Backend Create() {
        if(OperatingSystem.IsWindows()) {
            return new WindowsConsoleUtf8Backend(new Win32ConsoleCodePage());
        }
        // 他のOSで端末がUTF-8でない場合に対応するときは，ここに分岐とバックエンドを足す
        return new NoOpTerminalUtf8Backend();
    }
}
