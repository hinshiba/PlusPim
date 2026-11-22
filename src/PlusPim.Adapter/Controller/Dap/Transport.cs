using System.Net;
using System.Net.Sockets;

namespace PlusPim.Adapter.Controller.Dap;

/// <summary>
/// DAPの通信路をTCPで確立する. デバッギの標準入出力とは別の通信路を使う
/// </summary>
public static class Transport {
    /// <summary>待ち受け開始の通知行の接頭辞. この直後に <c>ip:port</c> が続く. VS Code拡張が読む</summary>
    public const string AnnouncePrefix = "DAP server listening at: ";

    /// <summary>接続を待つ既定の時間</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    /// <summary>クライアントが待ち受けている接続先へ接続する. 再試行はしない</summary>
    /// <param name="endpoint">接続先</param>
    /// <param name="timeout">接続を待つ時間</param>
    /// <param name="cancellation">取り消し</param>
    /// <exception cref="DapTransportException">接続できなかった</exception>
    public static async Task<Stream> ConnectAsync(IPEndPoint endpoint, TimeSpan timeout, CancellationToken cancellation) {
        Socket socket = new(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try {
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(timeout);
            await socket.ConnectAsync(endpoint, deadline.Token);
            return new NetworkStream(socket, ownsSocket: true);
        } catch(OperationCanceledException) when(!cancellation.IsCancellationRequested) {
            socket.Dispose();
            throw new DapTransportException($"Timed out connecting to {endpoint} after {timeout.TotalSeconds:0.##}s.");
        } catch(SocketException e) {
            socket.Dispose();
            throw new DapTransportException($"Failed to connect to {endpoint}: {e.Message}", e);
        } catch {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>待ち受けて <paramref name="announce"/> に1行で通知し, 最初に来た1つの接続を返す</summary>
    /// <param name="endpoint">待ち受けるアドレス. ポート0なら空きポートを使う</param>
    /// <param name="announce">通知の出力先</param>
    /// <param name="cancellation">取り消し</param>
    /// <exception cref="DapTransportException">待ち受けられなかった</exception>
    public static async Task<Stream> ListenAsync(IPEndPoint endpoint, TextWriter announce, CancellationToken cancellation) {
        TcpListener listener = new(endpoint);
        try {
            listener.Start();
            announce.WriteLine($"{AnnouncePrefix}{listener.LocalEndpoint}");
            announce.Flush();
            Socket socket = await listener.AcceptSocketAsync(cancellation);
            socket.NoDelay = true;
            return new NetworkStream(socket, ownsSocket: true);
        } catch(SocketException e) {
            throw new DapTransportException($"Failed to listen on {endpoint}: {e.Message}", e);
        } finally {
            // 2つ目以降の接続は受け付けない
            listener.Stop();
        }
    }
}

/// <summary>
/// DAPの通信路を確立できなかったこと. メッセージはそのまま利用者に表示できる
/// </summary>
/// <param name="message">利用者に表示する理由</param>
/// <param name="inner">原因</param>
public sealed class DapTransportException(string message, Exception? inner = null): Exception(message, inner);
