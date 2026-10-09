using PlusPim.EditorController.DebugAdapter;
using PlusPim.Logging;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Xunit;
using App = PlusPim.Application.Application;

namespace PlusPimTests;

/// <summary>
/// 実際の <see cref="DebugAdapter"/> とパイプでつなぎ，生の DAP メッセージを送受信するテスト用のクライアント
/// </summary>
/// <remarks>
/// 受信したメッセージはすべて受信順に記録する．待機は条件に合うメッセージが届くまでで，
/// 時間で成否を決めない (タイムアウトはハングを検出するためだけに使う)
/// </remarks>
internal sealed class DapClient: IDisposable {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly AnonymousPipeServerStream _clientToAdapter = new(PipeDirection.Out);
    private readonly AnonymousPipeClientStream _adapterInput;
    private readonly AnonymousPipeServerStream _adapterOutput = new(PipeDirection.Out);
    private readonly AnonymousPipeClientStream _clientInput;
    private readonly List<JsonElement> _messages = [];
    private readonly object _lock = new();
    private readonly object _sendLock = new();
    private readonly Thread _reader;
    private int _seq;

    /// <summary>
    /// テスト対象のアダプタ
    /// </summary>
    public DebugAdapter Adapter { get; }

    /// <summary>
    /// アダプタが出力に使うストリーム．セッション終了時の挙動を確かめるために閉じられる
    /// </summary>
    public Stream AdapterOutput => this._adapterOutput;

    public DapClient(FileInfo[] files, bool strict = false) {
        this._adapterInput = new AnonymousPipeClientStream(PipeDirection.In, this._clientToAdapter.ClientSafePipeHandle);
        this._clientInput = new AnonymousPipeClientStream(PipeDirection.In, this._adapterOutput.ClientSafePipeHandle);
        this._reader = new Thread(this.ReadLoop) { IsBackground = true, Name = "DapClient reader" };
        this._reader.Start();
        this.Adapter = new DebugAdapter(this._adapterInput, this._adapterOutput, new App(files, Logger.Null, strict), Logger.Null);
    }

    /// <summary>
    /// これまでに受信したメッセージの数．以降に届くメッセージだけを待つときの開始位置に使う
    /// </summary>
    public int Mark {
        get {
            lock(this._lock) {
                return this._messages.Count;
            }
        }
    }

    /// <summary>
    /// 受信したメッセージの複製
    /// </summary>
    public JsonElement[] Messages {
        get {
            lock(this._lock) {
                return [.. this._messages];
            }
        }
    }

    /// <summary>
    /// 要求を送る
    /// </summary>
    /// <returns>要求の seq</returns>
    public int Send(string command, object? arguments = null) {
        int seq = Interlocked.Increment(ref this._seq);
        string json = JsonSerializer.Serialize(new { seq, type = "request", command, arguments = arguments ?? new { } });
        byte[] body = Encoding.UTF8.GetBytes(json);
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        lock(this._sendLock) {
            this._clientToAdapter.Write(header);
            this._clientToAdapter.Write(body);
            this._clientToAdapter.Flush();
        }
        return seq;
    }

    /// <summary>
    /// 要求を送り，その応答を待つ
    /// </summary>
    public JsonElement Request(string command, object? arguments = null) {
        int seq = this.Send(command, arguments);
        return this.WaitForResponse(seq);
    }

    /// <summary>
    /// 要求を送り，成功の応答を待つ
    /// </summary>
    public JsonElement RequestOk(string command, object? arguments = null) {
        JsonElement response = this.Request(command, arguments);
        Assert.True(response.GetProperty("success").GetBoolean(), $"{command} failed: {response}");
        return response;
    }

    /// <summary>
    /// 指定した seq の要求への応答を待つ
    /// </summary>
    public JsonElement WaitForResponse(int requestSeq) {
        return this.WaitFor(m => IsResponseTo(m, requestSeq)).Message;
    }

    /// <summary>
    /// <paramref name="from"/> 番目以降に受信した，指定した名前のイベントを待つ
    /// </summary>
    public JsonElement WaitForEvent(string name, int from = 0) {
        return this.WaitFor(m => IsEvent(m, name), from).Message;
    }

    /// <summary>
    /// <paramref name="from"/> 番目以降に受信した，条件に合う最初のメッセージを待つ
    /// </summary>
    /// <returns>メッセージとその受信順の位置</returns>
    public (JsonElement Message, int Index) WaitFor(Func<JsonElement, bool> predicate, int from = 0) {
        DateTime deadline = DateTime.UtcNow + Timeout;
        lock(this._lock) {
            int i = from;
            while(true) {
                for(; i < this._messages.Count; i++) {
                    if(predicate(this._messages[i])) {
                        return (this._messages[i], i);
                    }
                }
                TimeSpan remaining = deadline - DateTime.UtcNow;
                if(remaining <= TimeSpan.Zero || !Monitor.Wait(this._lock, remaining)) {
                    throw new TimeoutException($"No matching DAP message. Received: {string.Join(", ", this._messages.Select(Describe))}");
                }
            }
        }
    }

    public static bool IsResponseTo(JsonElement message, int requestSeq) {
        return message.GetProperty("type").GetString() == "response"
            && message.GetProperty("request_seq").GetInt32() == requestSeq;
    }

    public static bool IsEvent(JsonElement message, string name) {
        return message.GetProperty("type").GetString() == "event"
            && message.GetProperty("event").GetString() == name;
    }

    /// <summary>
    /// メッセージを短く表す (失敗時のメッセージ用)
    /// </summary>
    public static string Describe(JsonElement message) {
        return message.GetProperty("type").GetString() switch {
            "event" => $"event:{message.GetProperty("event").GetString()}",
            "response" => $"response:{message.GetProperty("command").GetString()}",
            string other => other,
            null => "?"
        };
    }

    private void ReadLoop() {
        try {
            while(this.ReadMessage() is JsonElement message) {
                lock(this._lock) {
                    this._messages.Add(message);
                    Monitor.PulseAll(this._lock);
                }
            }
        } catch(IOException) {
            // パイプが閉じられた
        } catch(ObjectDisposedException) {
            // パイプが閉じられた
        }
    }

    private JsonElement? ReadMessage() {
        int contentLength = -1;
        while(true) {
            string? line = this.ReadHeaderLine();
            if(line is null) {
                return null;
            }
            if(line.Length == 0) {
                break;
            }
            if(line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) {
                contentLength = int.Parse(line["Content-Length:".Length..].Trim());
            }
        }

        byte[] body = new byte[contentLength];
        this._clientInput.ReadExactly(body);
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private string? ReadHeaderLine() {
        StringBuilder builder = new();
        while(true) {
            int b = this._clientInput.ReadByte();
            if(b < 0) {
                return null;
            }
            if(b == '\n') {
                return builder.ToString().TrimEnd('\r');
            }
            _ = builder.Append((char)b);
        }
    }

    public void Dispose() {
        this._clientToAdapter.Dispose();
        this._adapterInput.Dispose();
        this._adapterOutput.Dispose();
        this._clientInput.Dispose();
    }
}
