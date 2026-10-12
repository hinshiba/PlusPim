using PlusPim.Logging;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.EditorController.Transport;

/// <summary>
/// <c>--connect</c> と <c>--listen</c> の指定から <see cref="IDapTransport"/> を作る
/// </summary>
internal static class DapTransportFactory {
    /// <summary>
    /// どちらか一方だけが指定されていて，かつ書式が正しいときに成功する
    /// </summary>
    /// <param name="connect"><c>--connect</c> の値．未指定ならnull</param>
    /// <param name="listen"><c>--listen</c> の値．未指定ならnull</param>
    /// <param name="logger">ロガー</param>
    /// <param name="announce"><see cref="ListenTransport"/> が待ち受け開始を通知する先</param>
    /// <param name="transport">成功時の通信路</param>
    /// <param name="error">失敗時の利用者向けメッセージ</param>
    public static bool TryCreate(string? connect, string? listen, ILogger logger, TextWriter announce,
                                 [NotNullWhen(true)] out IDapTransport? transport, [NotNullWhen(false)] out string? error) {
        transport = null;
        if(connect is null && listen is null) {
            error = "--debug requires exactly one of --connect <host:port> or --listen <host:port>.";
            return false;
        }
        if(connect is not null && listen is not null) {
            error = "--connect and --listen cannot be used together.";
            return false;
        }

        if(connect is not null) {
            if(!DapEndpoint.TryParse(connect, allowPortZero: false, out DapEndpoint endpoint, out string? reason)) {
                error = $"Invalid --connect value '{connect}': {reason}.";
                return false;
            }
            transport = new ConnectTransport(endpoint, logger);
        } else {
            if(!DapEndpoint.TryParse(listen, allowPortZero: true, out DapEndpoint endpoint, out string? reason)) {
                error = $"Invalid --listen value '{listen}': {reason}.";
                return false;
            }
            transport = new ListenTransport(endpoint, logger, announce);
        }
        error = null;
        return true;
    }
}
