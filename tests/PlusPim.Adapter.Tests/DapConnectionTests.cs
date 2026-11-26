using PlusPim.Adapter.Controller.Dap;
using PlusPim.Asm;
using PlusPim.Dbg;
using PlusPim.Image;
using PlusPim.Usecase;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace PlusPim.Adapter.Tests;

/// <summary>
/// <c>launch</c> の解釈と, 送信の失敗の後も接続が終われることを, クライアントから観測する
/// </summary>
public class DapConnectionTests {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private const string Initialize = """{"linesStartAt1":true,"columnsStartAt1":true,"pathFormat":"path"}""";
    private static readonly string Main = Path.Combine(AppContext.BaseDirectory, "main.asm");
    private static readonly string Sub = Path.Combine(AppContext.BaseDirectory, "sub.asm");

    /// <summary>
    /// 接続のクライアント側の端
    /// </summary>
    /// <param name="fault">送られるメッセージの本体から, その書き込みで投げる例外を決める. 例外を投げるのは1度だけ</param>
    private sealed class Client(Func<string, Exception?> fault): Stream {
        private readonly Channel<byte[]> _requests = Channel.CreateUnbounded<byte[]>();
        private readonly Channel<JsonElement> _messages = Channel.CreateUnbounded<JsonElement>();
        private ReadOnlyMemory<byte> _pending;
        private int _seq;
        private bool _faulted;

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <summary>要求を送る</summary>
        public void Send(string command, string arguments = "{}") {
            byte[] body = Encoding.UTF8.GetBytes($$"""{"seq":{{++this._seq}},"type":"request","command":"{{command}}","arguments":{{arguments}}}""");
            _ = this._requests.Writer.TryWrite([.. Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n"), .. body]);
        }

        /// <summary>条件に合うメッセージが来るまで, それ以外を読み捨てる</summary>
        public async Task<JsonElement> ExpectAsync(Func<JsonElement, bool> match) {
            using CancellationTokenSource cts = new(Timeout);
            while(true) {
                JsonElement m = await this._messages.Reader.ReadAsync(cts.Token);
                if(match(m)) {
                    return m;
                }
            }
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            if(this._pending.IsEmpty) {
                this._pending = await this._requests.Reader.ReadAsync(cancellationToken);
            }
            int n = Math.Min(buffer.Length, this._pending.Length);
            this._pending[..n].CopyTo(buffer);
            this._pending = this._pending[n..];
            return n;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
            // 1回の書き込みはヘッダと本体の揃った1つのメッセージ
            string text = Encoding.UTF8.GetString(buffer.Span);
            string body = text[(text.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];
            if(!this._faulted && fault(body) is Exception e) {
                this._faulted = true;
                throw e;
            }
            _ = this._messages.Writer.TryWrite(JsonDocument.Parse(body).RootElement.Clone());
            return ValueTask.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return this.ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            this.WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class Reader(SourcePath path, string source): ISourceReader {
        public string? Read(SourcePath p) {
            return p == path ? source : null;
        }
    }

    /// <summary>先頭のソースを, <c>main</c> からすぐに戻って終わるプログラムとして読み込む</summary>
    private static LoadResult LoadExiting(ImmutableArray<SourcePath> paths) {
        return Session.Load(paths, new Reader(paths[0], ".text\n.globl main\nmain:\njr $ra\nnop\n"), false, DelaySlotsConfig.All, Stream.Null, Stream.Null);
    }

    /// <summary><c>launch</c> の引数を作る</summary>
    /// <param name="program">パスの文字列か配列</param>
    /// <param name="cwd">省略するなら <see langword="null"/></param>
    private static string Launch(JsonNode program, bool stopOnEntry = true, string? cwd = null) {
        JsonObject arguments = new() { ["program"] = program, ["stopOnEntry"] = stopOnEntry };
        if(cwd is not null) {
            arguments["cwd"] = cwd;
        }
        return arguments.ToJsonString();
    }

    private static bool IsEvent(JsonElement m, string name) {
        return m.GetProperty("type").GetString() == "event" && m.GetProperty("event").GetString() == name;
    }

    private static bool IsResponse(JsonElement m, string command) {
        return m.GetProperty("type").GetString() == "response" && m.GetProperty("command").GetString() == command;
    }

    [Fact]
    public async Task FailureResponseNotSent_ConnectionCloses() {
        Client client = new(body => body.Contains("\"success\":false", StringComparison.Ordinal) ? new IOException("broken pipe") : null);
        DapConnection connection = new(client, _ => throw new InvalidOperationException("load failed"));
        Task run = connection.RunAsync();
        client.Send("initialize", Initialize);
        client.Send("launch", Launch(Main));
        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task ResultNotDelivered_TerminatesWithInternalError() {
        Client client = new(body => body.Contains("\"event\":\"terminated\"", StringComparison.Ordinal) ? new ObjectDisposedException("stream") : null);
        DapConnection connection = new(client, LoadExiting);
        Task run = connection.RunAsync();
        client.Send("initialize", Initialize);
        client.Send("launch", Launch(Main, stopOnEntry: false));
        _ = await client.ExpectAsync(m => IsEvent(m, "initialized"));
        client.Send("configurationDone");
        _ = await client.ExpectAsync(m => IsEvent(m, "output") && m.GetProperty("body").GetProperty("output").GetString()!.StartsWith("[PlusPim internal error]", StringComparison.Ordinal));
        _ = await client.ExpectAsync(m => IsEvent(m, "terminated"));
        client.Send("disconnect");
        _ = await client.ExpectAsync(m => IsResponse(m, "disconnect"));
        await run.WaitAsync(Timeout);
    }

    public static TheoryData<string> Programs => new() {
        Launch(new JsonArray(Main, Sub)),
        Launch(new JsonArray("main.asm", Sub), cwd: AppContext.BaseDirectory),
        Launch(new JsonArray(Path.Combine("dir", "..", "main.asm"), "sub.asm"), cwd: AppContext.BaseDirectory),
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task Program_ResolvedAgainstCwdAndLoadedInListedOrder(string arguments) {
        Client client = new(_ => null);
        ImmutableArray<SourcePath> loaded = [];
        DapConnection connection = new(client, p => {
            loaded = p;
            return LoadExiting(p);
        });
        Task run = connection.RunAsync();
        client.Send("initialize", Initialize);
        client.Send("launch", arguments);
        _ = await client.ExpectAsync(m => IsResponse(m, "launch"));
        Assert.Equal([Main, Sub], loaded.Select(p => p.Value));
        client.Send("disconnect");
        await run.WaitAsync(Timeout);
    }

    [Theory]
    [InlineData("""{"program":"main.asm"}""")]
    [InlineData("""{"program":[]}""")]
    [InlineData("""{"program":null}""")]
    [InlineData("""{"program":[1]}""")]
    [InlineData("""{"program":"main.asm","cwd":"dir"}""")]
    [InlineData("""{"program":"main.asm","cwd":""}""")]
    [InlineData("""{"program":"main.asm","cwd":1}""")]
    [InlineData("{}")]
    public async Task InvalidProgram_LaunchRejectedWithoutLoading(string arguments) {
        Client client = new(_ => null);
        DapConnection connection = new(client, _ => throw new InvalidOperationException("must not load"));
        Task run = connection.RunAsync();
        client.Send("initialize", Initialize);
        client.Send("launch", arguments);
        JsonElement response = await client.ExpectAsync(m => IsResponse(m, "launch"));
        Assert.False(response.GetProperty("success").GetBoolean());
        Assert.StartsWith("Invalid arguments", response.GetProperty("message").GetString(), StringComparison.Ordinal);
        client.Send("disconnect");
        await run.WaitAsync(Timeout);
    }
}
