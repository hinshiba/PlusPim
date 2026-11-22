namespace PlusPim.Terminal;

/// <summary>
/// Windows のコンソールのコードページを65001(UTF-8)へ切り替える．
/// 出力と入力は独立に扱い，片方の失敗でもう片方を妨げない
/// </summary>
internal sealed class WindowsConsoleUtf8Backend(IConsoleCodePage console): ITerminalUtf8Backend {
    private const uint Utf8CodePage = 65001;

    // 変更に成功した側だけ元の値を持つ
    private uint? originalOutput;
    private uint? originalInput;

    public bool TrySwitchToUtf8() {
        if(!console.IsConsoleAttached) {
            return false;
        }

        uint output = console.GetOutputCodePage();
        if(output != Utf8CodePage && console.SetOutputCodePage(Utf8CodePage)) {
            this.originalOutput = output;
        }

        uint input = console.GetInputCodePage();
        if(input != Utf8CodePage && console.SetInputCodePage(Utf8CodePage)) {
            this.originalInput = input;
        }

        return this.originalOutput is not null || this.originalInput is not null;
    }

    public void Restore() {
        if(this.originalOutput is uint output) {
            _ = console.SetOutputCodePage(output);
            this.originalOutput = null;
        }
        if(this.originalInput is uint input) {
            _ = console.SetInputCodePage(input);
            this.originalInput = null;
        }
    }

    public void RegisterTerminationHook(Action handler) => console.RegisterControlHandler(handler);
}
