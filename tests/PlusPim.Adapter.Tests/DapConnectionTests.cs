using PlusPim.Adapter.Controller.Dap;
using PlusPim.Asm;
using PlusPim.Image;
using PlusPim.Usecase;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace PlusPim.Adapter.Tests;

/// <summary>
/// 送信の失敗の後も接続が終われることを, クライアントから観測する
/// </summary>
public class DapConnectionTests {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private const string Initialize = """{"linesStartAt1":true,"columnsStartAt1":true,"pathFormat":"path"}""";

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

    /// <summary><c>main</c> からすぐに戻って終わるプログラムを読み込む</summary>
    private static LoadResult LoadExiting() {
        SourcePath path = SourcePath.TryResolve("main.asm", AppContext.BaseDirectory, out SourcePath p) ? p : throw new InvalidOperationException();
        return Session.Load([path], new Reader(path, ".text\n.globl main\nmain:\njr $ra\n"), false, Stream.Null, Stream.Null);
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
        DapConnection connection = new(client, () => throw new InvalidOperationException("load failed"));
        Task run = connection.RunAsync();
        client.Send("initialize", Initialize);
        client.Send("launch");
        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task ResultNotDelivered_TerminatesWithInternalError() {
        Client client = new(body => body.Contains("\"event\":\"terminated\"", StringComparison.Ordinal) ? new ObjectDisposedException("stream") : null);
        DapConnection connection = new(client, LoadExiting);
        Task run = connection.RunAsync();
        client.Send("initialize", Initialize);
        client.Send("launch", """{"stopOnEntry":false}""");
        _ = await client.ExpectAsync(m => IsEvent(m, "initialized"));
        client.Send("configurationDone");
        _ = await client.ExpectAsync(m => IsEvent(m, "output") && m.GetProperty("body").GetProperty("output").GetString()!.StartsWith("[PlusPim internal error]", StringComparison.Ordinal));
        _ = await client.ExpectAsync(m => IsEvent(m, "terminated"));
        client.Send("disconnect");
        _ = await client.ExpectAsync(m => IsResponse(m, "disconnect"));
        await run.WaitAsync(Timeout);
    }
}
