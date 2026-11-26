using BenchmarkDotNet.Attributes;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

/// <summary>
/// 配布形態の実行ファイルを DAP クライアントとして操作し，利用者から見える時間とメモリを測る
/// </summary>
/// <remarks>
/// 実行ファイルは環境変数 <c>PLUSPIM_V050</c> と <c>PLUSPIM_CURRENT</c> で渡す
/// </remarks>
[Config(typeof(StepConfig))]
public class Dap {
    [Params("v0.5.0", "current")]
    public string Version = "";

    /// <summary>fact と，その syscall を処理する例外ハンドラ</summary>
    private string[] _fact = [];
    private DapClient _client = null!;

    [GlobalSetup]
    public void Setup() {
        this._fact = [Workload.Locate("samples/delay-slots/fact.asm"), Workload.Locate("samples/delay-slots/kseg.asm")];
    }

    /// <summary>起動から stopOnEntry で止まり Step を受け付けるまで</summary>
    [Benchmark]
    public void Startup() {
        this._client = DapClient.Launch(this.Version, this._fact);
    }

    /// <summary>起動から fact を終了まで実行するまで</summary>
    [Benchmark]
    public void Fact() {
        this._client = DapClient.Launch(this.Version, this._fact);
        this._client.Request("continue", new JsonObject { ["threadId"] = 1 });
        this._client.WaitEvent("terminated");
    }

    [IterationCleanup(Target = nameof(Fact))]
    public void CleanupFact() {
        Samples.Record(nameof(Dap), nameof(this.Fact), this.Version, this._client.PeakPrivateBytes);
        this._client.Dispose();
    }

    [IterationCleanup(Target = nameof(Startup))]
    public void CleanupStartup() {
        this._client.Dispose();
    }
}

/// <summary>
/// 1つの PlusPim プロセスとの DAP セッション
/// </summary>
public sealed class DapClient: IDisposable {
    private readonly Process _process;
    private readonly TcpClient _tcp;
    private readonly Stream _stream;
    private int _seq;

    private DapClient(Process process, TcpClient tcp) {
        this._process = process;
        this._tcp = tcp;
        // 期待するイベントが来なければ BDN の打ち切りより先に失敗させる
        tcp.ReceiveTimeout = 30_000;
        this._stream = tcp.GetStream();
    }

    /// <summary>
    /// 起動して <c>launch</c> と <c>configurationDone</c> を済ませ，入口の <c>stopped</c> を待つ
    /// </summary>
    /// <param name="version"><c>v0.5.0</c> か <c>current</c>．ファイルの渡し方が異なる</param>
    /// <param name="files">読み込ませるファイル</param>
    public static DapClient Launch(string version, string[] files) {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        string endpoint = $"127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        ProcessStartInfo psi = version == "v0.5.0"
            ? new(Environment.GetEnvironmentVariable("PLUSPIM_V050")!, [.. files, "--debug", "--connect", endpoint])
            : new(Environment.GetEnvironmentVariable("PLUSPIM_CURRENT")!, ["debug", "connect", endpoint]);
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        Process p = Process.Start(psi)!;
        p.OutputDataReceived += (_, _) => { };
        p.ErrorDataReceived += (_, _) => { };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        // 入力は使わないので EOF にする
        p.StandardInput.Close();

        DapClient c = new(p, listener.AcceptTcpClient());
        c.Request("initialize", new JsonObject { ["adapterID"] = "pluspim", ["linesStartAt1"] = true, ["columnsStartAt1"] = true, ["pathFormat"] = "path" });
        // v0.5.0 は program を読まず，コマンドラインのファイルを使う
        c.Request("launch", new JsonObject { ["program"] = new JsonArray([.. files.Select(f => JsonValue.Create(f))]), ["stopOnEntry"] = true });
        c.WaitEvent("initialized");
        c.Request("configurationDone", null);
        c.WaitEvent("stopped");
        return c;
    }

    /// <summary>プロセスのコミット済みプライベートメモリのピーク</summary>
    public long PeakPrivateBytes {
        get {
            this._process.Refresh();
            return this._process.PeakPagedMemorySize64;
        }
    }

    /// <summary>応答を待たずに要求を送る</summary>
    public void Request(string command, JsonObject? arguments) {
        JsonObject msg = new() { ["seq"] = ++this._seq, ["type"] = "request", ["command"] = command };
        if(arguments is not null) {
            msg["arguments"] = arguments;
        }
        byte[] body = Encoding.UTF8.GetBytes(msg.ToJsonString());
        this._stream.Write(Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n"));
        this._stream.Write(body);
    }

    /// <summary><paramref name="name"/> のイベントまで読み捨てる．失敗の応答は例外にする</summary>
    public void WaitEvent(string name) {
        while(true) {
            JsonNode msg = this.Read();
            if((string?)msg["type"] == "event" && (string?)msg["event"] == name) {
                return;
            }
            if((string?)msg["type"] == "response" && msg["success"]?.GetValue<bool>() == false) {
                throw new InvalidOperationException(msg.ToJsonString());
            }
        }
    }

    private JsonNode Read() {
        int length = 0;
        string line;
        while((line = this.ReadLine()) != "") {
            if(line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) {
                length = int.Parse(line["Content-Length:".Length..]);
            }
        }
        byte[] body = new byte[length];
        this._stream.ReadExactly(body);
        return JsonNode.Parse(body)!;
    }

    private string ReadLine() {
        StringBuilder sb = new();
        int b;
        while((b = this._stream.ReadByte()) != '\n') {
            if(b < 0) {
                throw new EndOfStreamException();
            }
            if(b != '\r') {
                _ = sb.Append((char)b);
            }
        }
        return sb.ToString();
    }

    /// <summary><c>disconnect</c> を送り，終わらなければ強制終了する</summary>
    public void Dispose() {
        try {
            this.Request("disconnect", new JsonObject { ["terminateDebuggee"] = true });
        } catch(IOException) {
            // すでに切断している
        }
        if(!this._process.WaitForExit(5000)) {
            this._process.Kill(true);
        }
        this._tcp.Dispose();
        this._process.Dispose();
    }
}
