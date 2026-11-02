using PlusPim.Logging;
using System.Net;
using System.Net.Sockets;

namespace PlusPim.EditorController.Transport;

/// <summary>
/// クライアントが待ち受けている接続先へ接続する．再試行はしない．
/// </summary>
internal sealed class ConnectTransport(IPEndPoint endpoint, ILogger logger, TimeSpan? timeout = null): IDapTransport {
    /// <summary>
    /// 接続を待つ既定の時間
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _timeout = timeout ?? DefaultTimeout;

    public async Task<Stream> OpenAsync(CancellationToken cancellationToken = default) {
        Socket socket = new(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try {
            logger.Debug("ConnectTransport", $"Connecting to {endpoint}");
            using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(this._timeout);
            await socket.ConnectAsync(endpoint, timeoutSource.Token);
        } catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested) {
            socket.Dispose();
            throw new DapTransportException($"Timed out connecting to {endpoint} after {this._timeout.TotalSeconds:0.##}s.");
        } catch(SocketException e) {
            socket.Dispose();
            throw new DapTransportException($"Failed to connect to {endpoint}: {e.Message}", e);
        } catch {
            socket.Dispose();
            throw;
        }
        logger.Debug("ConnectTransport", "Connected");
        return new NetworkStream(socket, ownsSocket: true);
    }
}
