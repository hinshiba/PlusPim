using PlusPim.EditorController.DebugAdapter;
using PlusPim.EditorController.Transport;
using PlusPim.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using App = PlusPim.Application.Application;

namespace PlusPimTests;

/// <summary>
/// DAPのトランスポート (--connect / --listen) のテスト．ポートは常にOSに割り当てさせる
/// </summary>
public partial class TransportTests {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [GeneratedRegex(@"^DAP server listening at: 127\.0\.0\.1:(\d+)$")]
    private static partial Regex AnnounceLine();

    /// <summary>
    /// WriteLineされた時点で通知を受け取れる <see cref="TextWriter"/>
    /// </summary>
    private sealed class SignalWriter: StringWriter {
        private readonly TaskCompletionSource<string> _line = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> FirstLine => this._line.Task;

        public override void WriteLine(string? value) {
            base.WriteLine(value);
            _ = this._line.TrySetResult(value ?? "");
        }
    }

    [Fact]
    public async Task Listen_AnnouncesOneLine_AndExchangesData() {
        SignalWriter announce = new();
        ListenTransport transport = new(new IPEndPoint(IPAddress.Loopback, 0), Logger.Null, announce);
        Task<Stream> opening = transport.OpenAsync();

        string line = await announce.FirstLine.WaitAsync(Timeout);
        Match match = AnnounceLine().Match(line);
        Assert.True(match.Success, line);
        int port = int.Parse(match.Groups[1].Value);
        Assert.NotEqual(0, port);

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await using Stream server = await opening.WaitAsync(Timeout);
        NetworkStream clientStream = client.GetStream();

        byte[] ping = Encoding.ASCII.GetBytes("ping");
        await clientStream.WriteAsync(ping);
        byte[] received = new byte[4];
        await server.ReadExactlyAsync(received).AsTask().WaitAsync(Timeout);
        Assert.Equal(ping, received);

        byte[] pong = Encoding.ASCII.GetBytes("pong");
        await server.WriteAsync(pong);
        await server.FlushAsync();
        await clientStream.ReadExactlyAsync(received).AsTask().WaitAsync(Timeout);
        Assert.Equal(pong, received);

        // 通知は1行だけ
        Assert.Equal(line + Environment.NewLine, announce.ToString());

        // 接続を受けたら待ち受けは閉じる．2つ目の接続は拒否される
        using TcpClient second = new();
        _ = await Assert.ThrowsAsync<SocketException>(() => second.ConnectAsync(IPAddress.Loopback, port));
    }

    [Fact]
    public async Task Listen_CanceledToken_Throws() {
        SignalWriter announce = new();
        ListenTransport transport = new(new IPEndPoint(IPAddress.Loopback, 0), Logger.Null, announce);
        using CancellationTokenSource cts = new();
        Task<Stream> opening = transport.OpenAsync(cts.Token);
        _ = await announce.FirstLine.WaitAsync(Timeout);

        await cts.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening.WaitAsync(Timeout));
    }

    [Fact]
    public async Task Connect_ConnectsAndEchoes() {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        try {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            ConnectTransport transport = new(new IPEndPoint(IPAddress.Loopback, port), Logger.Null);
            Task<Stream> opening = transport.OpenAsync();
            using TcpClient accepted = await listener.AcceptTcpClientAsync().WaitAsync(Timeout);
            await using Stream stream = await opening.WaitAsync(Timeout);

            byte[] data = Encoding.ASCII.GetBytes("echo");
            await stream.WriteAsync(data);
            await stream.FlushAsync();
            NetworkStream peer = accepted.GetStream();
            byte[] received = new byte[4];
            await peer.ReadExactlyAsync(received).AsTask().WaitAsync(Timeout);
            Assert.Equal(data, received);
            await peer.WriteAsync(received);
            byte[] echoed = new byte[4];
            await stream.ReadExactlyAsync(echoed).AsTask().WaitAsync(Timeout);
            Assert.Equal(data, echoed);
        } finally {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Connect_ClosedPort_ThrowsTransportException() {
        // 一度待ち受けて閉じることで，接続を受け付けないポートを得る
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        ConnectTransport transport = new(new IPEndPoint(IPAddress.Loopback, port), Logger.Null, TimeSpan.FromSeconds(2));
        _ = await Assert.ThrowsAsync<DapTransportException>(() => transport.OpenAsync());
    }

    [Fact]
    public async Task Connect_CanceledToken_ThrowsOperationCanceled() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        ConnectTransport transport = new(new IPEndPoint(IPAddress.Loopback, 1), Logger.Null);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.OpenAsync(cts.Token));
    }

    [Fact]
    public async Task Listen_WithDebugAdapter_RespondsToInitialize() {
        SignalWriter announce = new();
        ListenTransport transport = new(new IPEndPoint(IPAddress.Loopback, 0), Logger.Null, announce);
        Task<Stream> opening = transport.OpenAsync();
        string line = await announce.FirstLine.WaitAsync(Timeout);
        int port = int.Parse(AnnounceLine().Match(line).Groups[1].Value);

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await using Stream server = await opening.WaitAsync(Timeout);
        _ = new DebugAdapter(server, server, new App([], Logger.Null, new InMemoryDebuggeeIO()), Logger.Null);

        NetworkStream clientStream = client.GetStream();
        byte[] body = Encoding.UTF8.GetBytes("""{"seq":1,"type":"request","command":"initialize","arguments":{"adapterID":"pluspim"}}""");
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        await clientStream.WriteAsync(header);
        await clientStream.WriteAsync(body);
        await clientStream.FlushAsync();

        string response = await ReadMessageAsync(clientStream).WaitAsync(Timeout);
        Assert.Contains("\"type\":\"response\"", response);
        Assert.Contains("supportsStepBack", response);
    }

    /// <summary>
    /// Content-Lengthで区切られた1メッセージを読む
    /// </summary>
    private static async Task<string> ReadMessageAsync(Stream stream) {
        StringBuilder header = new();
        byte[] one = new byte[1];
        while(!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) {
            await stream.ReadExactlyAsync(one);
            _ = header.Append((char)one[0]);
        }
        int length = int.Parse(Regex.Match(header.ToString(), @"Content-Length: (\d+)", RegexOptions.IgnoreCase).Groups[1].Value);
        byte[] body = new byte[length];
        await stream.ReadExactlyAsync(body);
        return Encoding.UTF8.GetString(body);
    }
}
