using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PlusPim.EditorController.Transport;

/// <summary>
/// <c>host:port</c> 形式の接続先
/// </summary>
internal readonly record struct DapEndpoint(string Host, int Port) {

    /// <summary>
    /// <c>host:port</c> と <c>[v6]:port</c> を解釈する
    /// </summary>
    /// <param name="text">解釈する文字列</param>
    /// <param name="allowPortZero">ポート0を許すか</param>
    /// <param name="endpoint">成功時の接続先</param>
    /// <param name="error">失敗時の理由</param>
    public static bool TryParse(string? text, bool allowPortZero, out DapEndpoint endpoint, [NotNullWhen(false)] out string? error) {
        endpoint = default;
        error = $"expected host:port (port {(allowPortZero ? 0 : 1)}-65535)";
        if(string.IsNullOrEmpty(text)) {
            return false;
        }

        string host;
        string portText;
        if(text[0] == '[') {
            // [v6]:port
            int close = text.IndexOf(']');
            if(close < 0 || close + 1 >= text.Length || text[close + 1] != ':') {
                return false;
            }
            host = text[1..close];
            portText = text[(close + 2)..];
        } else {
            int separator = text.LastIndexOf(':');
            if(separator < 0) {
                return false;
            }
            host = text[..separator];
            portText = text[(separator + 1)..];
            // 括弧なしのIPv6リテラルは区切りが曖昧なので受け付けない
            if(host.Contains(':')) {
                return false;
            }
        }

        if(host.Length == 0) {
            return false;
        }
        if(!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out int port)) {
            return false;
        }
        if(port > 65535 || (port == 0 && !allowPortZero)) {
            return false;
        }

        endpoint = new DapEndpoint(host, port);
        error = null;
        return true;
    }

    /// <summary>
    /// <c>:</c> を含むホストは <c>[]</c> で囲んで返す
    /// </summary>
    public override string ToString() {
        return this.Host.Contains(':') ? $"[{this.Host}]:{this.Port}" : $"{this.Host}:{this.Port}";
    }
}
