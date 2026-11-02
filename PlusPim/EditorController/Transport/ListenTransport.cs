using PlusPim.Logging;
using System.Net;
using System.Net.Sockets;

namespace PlusPim.EditorController.Transport;

/// <summary>
/// PlusPimが待ち受け，最初に来た1つの接続をDAPの接続とする
/// </summary>
internal sealed class ListenTransport(IPEndPoint endpoint, ILogger logger, TextWriter announce): IDapTransport {
    /// <summary>
    /// 待ち受け開始の通知行の接頭辞．この直後に <c>ip:port</c> が続く
    /// </summary>
    public const string AnnouncePrefix = "DAP server listening at: ";

    public async Task<Stream> OpenAsync(CancellationToken cancellationToken = default) {
        TcpListener listener = new(endpoint);
        try {
            try {
                listener.Start();
            } catch(SocketException e) {
                throw new DapTransportException($"Failed to listen on {endpoint}: {e.Message}", e);
            }

            EndPoint actual = listener.LocalEndpoint;
            announce.WriteLine($"{AnnouncePrefix}{actual}");
            announce.Flush();
            logger.Debug("ListenTransport", $"Listening on {actual}");

            // 最初の接続をそのまま使う
            Socket socket = await listener.AcceptSocketAsync(cancellationToken);
            socket.NoDelay = true;
            logger.Debug("ListenTransport", "Accepted");
            return new NetworkStream(socket, ownsSocket: true);
        } finally {
            // 2つ目以降の接続は受け付けない
            listener.Stop();
        }
    }
}
