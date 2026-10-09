using PlusPim.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using PlusPimProgram = PlusPim.Program;

namespace PlusPimTests;

/// <summary>
/// TCP の待ち受けと拡張機能へのポートの通知 (doc/debugger.md「起動と接続」)
/// </summary>
public class DapListenerTests {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly Regex ListeningLine = new($"^{Regex.Escape(PlusPimProgram.ListeningMarker)}(\\d+)\\n$");

    /// <summary>
    /// 書き込みを記録し，Flush されたら <see cref="Flushed"/> を完了する
    /// </summary>
    private sealed class SignalingWriter: StringWriter {
        private readonly TaskCompletionSource _flushed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Flushed => this._flushed.Task;

        public override void Flush() {
            base.Flush();
            _ = this._flushed.TrySetResult();
        }
    }

    [Fact]
    public async Task AcceptSingleClient_Port0_AnnouncesBoundPortAndAcceptsOneSilentClient() {
        SignalingWriter announce = new();
        StringWriter error = new();
        Task<Socket?> accept = PlusPimProgram.AcceptSingleClientAsync(0, announce, error, Logger.Null);

        await announce.Flushed.WaitAsync(Timeout);
        Match match = ListeningLine.Match(announce.ToString());
        Assert.True(match.Success, announce.ToString());
        int port = int.Parse(match.Groups[1].Value);
        Assert.NotEqual(0, port);

        // 何も送らないクライアントも本番の接続として受け付ける (プローブとの区別をしない)
        using TcpClient client = new(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, port).WaitAsync(Timeout);
        using Socket? accepted = await accept.WaitAsync(Timeout);
        Assert.NotNull(accepted);
        Assert.True(accepted.Connected);
        Assert.Equal(client.Client.LocalEndPoint, accepted.RemoteEndPoint);
        Assert.Equal("", error.ToString());

        // 受け付けた後は待ち受けを閉じる
        using TcpClient second = new(AddressFamily.InterNetwork);
        _ = await Assert.ThrowsAnyAsync<SocketException>(() => second.ConnectAsync(IPAddress.Loopback, port).WaitAsync(Timeout));
    }

    [Fact]
    public async Task AcceptSingleClient_PortInUse_ReportsAndReturnsNull() {
        TcpListener occupant = new(IPAddress.Loopback, 0);
        occupant.Start();
        try {
            int port = ((IPEndPoint)occupant.LocalEndpoint).Port;
            StringWriter announce = new();
            StringWriter error = new();

            Socket? accepted = await PlusPimProgram.AcceptSingleClientAsync(port, announce, error, Logger.Null).WaitAsync(Timeout);

            Assert.Null(accepted);
            Assert.Equal("", announce.ToString());
            Assert.Equal($"Port {port} is already in use. Use --port 0 to pick a free port.{Environment.NewLine}", error.ToString());
        } finally {
            occupant.Stop();
        }
    }

    private const string Utf8Program = """
        .data
        msg:
          .asciiz "日本\n"

        .text
        main:
          la $a0, msg
          li $v0, 4
          syscall
          li $v0, 10
          syscall

        .ktext
        handler:
          runtime_call!
          mfc0 $k0, $14
          addiu $k0, $k0, 4
          mtc0 $k0, $14
          eret
        """;

    private static Process StartPlusPim(params string[] args) {
        string exe = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "PlusPim.exe" : "PlusPim");
        ProcessStartInfo info = new(exe) {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        foreach(string arg in args) {
            info.ArgumentList.Add(arg);
        }
        return Process.Start(info) ?? throw new InvalidOperationException("PlusPim did not start");
    }

    private static async Task SendAsync(Stream stream, int seq, string command, object arguments) {
        byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { seq, type = "request", command, arguments }));
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        await stream.WriteAsync(header);
        await stream.WriteAsync(body);
        await stream.FlushAsync();
    }

    [Fact]
    public async Task Process_Port0_AnnouncesPortAndWritesDebuggeeOutputAsUtf8() {
        FileInfo file = TestHelpers.WriteTempAsm(Utf8Program);
        using Process process = StartPlusPim("-d", "--port", "0", file.FullName);
        try {
            using CancellationTokenSource cts = new(Timeout);

            // 接続前に標準出力に書かれるのは待ち受けの行だけである
            string? line = await process.StandardOutput.ReadLineAsync(cts.Token);
            Assert.NotNull(line);
            Match match = ListeningLine.Match(line + "\n");
            Assert.True(match.Success, line);

            using TcpClient client = new(AddressFamily.InterNetwork);
            await client.ConnectAsync(IPAddress.Loopback, int.Parse(match.Groups[1].Value), cts.Token);
            NetworkStream stream = client.GetStream();
            await SendAsync(stream, 1, "initialize", new { adapterID = "pluspim" });
            await SendAsync(stream, 2, "launch", new { stopOnEntry = false });
            await SendAsync(stream, 3, "configurationDone", new { });

            // パイプへの出力がコンソールのコードページでなく UTF-8 である
            StringBuilder output = new();
            char[] buffer = new char[256];
            while(!output.ToString().Contains("日本")) {
                int read = await process.StandardOutput.ReadAsync(buffer, cts.Token);
                Assert.NotEqual(0, read);
                _ = output.Append(buffer, 0, read);
            }
            Assert.DoesNotContain('�', output.ToString());

            await SendAsync(stream, 4, "disconnect", new { });
            await process.WaitForExitAsync(cts.Token);
            Assert.Equal(0, process.ExitCode);
        } finally {
            if(!process.HasExited) {
                process.Kill();
            }
            file.Delete();
        }
    }

    [Fact]
    public async Task Process_PortInUse_ExitsWithCode2() {
        FileInfo file = TestHelpers.WriteTempAsm(Utf8Program);
        TcpListener occupant = new(IPAddress.Loopback, 0);
        occupant.Start();
        int port = ((IPEndPoint)occupant.LocalEndpoint).Port;
        using Process process = StartPlusPim("-d", "--port", port.ToString(), file.FullName);
        try {
            using CancellationTokenSource cts = new(Timeout);
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(cts.Token);
            Task<string> stderr = process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            Assert.Equal(PlusPimProgram.PortInUseExitCode, process.ExitCode);
            Assert.Equal("", await stdout);
            Assert.Contains($"Port {port} is already in use. Use --port 0 to pick a free port.", await stderr);
        } finally {
            if(!process.HasExited) {
                process.Kill();
            }
            occupant.Stop();
            file.Delete();
        }
    }
}
