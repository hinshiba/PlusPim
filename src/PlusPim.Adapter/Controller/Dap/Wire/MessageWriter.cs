using PlusPim.Adapter.Controller.Dap.Protocol;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;

namespace PlusPim.Adapter.Controller.Dap.Wire;

/// <summary>
/// 応答とイベントを書く. 接続の状態機械のループだけが使う
/// </summary>
/// <remarks>
/// バッファの先頭にヘッダの空きを残して本体を書き, 長さが決まってからヘッダを空きの末尾に右詰めで書く.
/// ヘッダと本体が連続するので, 1回の書き込みで送れる.
/// </remarks>
internal sealed class MessageWriter {
    // "Content-Length: "とintの10桁と"\r\n\r\n"が収まる
    private const int HeaderSpace = 32;

    private readonly Stream _stream;
    private readonly ArrayBufferWriter<byte> _buffer = new(4096);
    private readonly Utf8JsonWriter _json;
    private int _seq;

    /// <summary>接続に書く書き手を作る</summary>
    /// <param name="stream">DAPの接続</param>
    public MessageWriter(Stream stream) {
        this._stream = stream;
        // 非ASCIIを \uでエスケープせずUTF-8のまま書く
        this._json = new Utf8JsonWriter(this._buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>成功の応答を書く</summary>
    /// <param name="request">応答する要求の通し番号</param>
    /// <param name="command">要求名</param>
    /// <param name="body">本体</param>
    /// <param name="info">本体の型情報</param>
    public ValueTask RespondAsync<T>(Seq request, string command, T body, JsonTypeInfo<T> info) {
        Utf8JsonWriter json = this.BeginResponse(request, command, true);
        json.WritePropertyName("body"u8);
        JsonSerializer.Serialize(json, body, info);
        return this.SendAsync();
    }

    /// <summary>失敗の応答を書く</summary>
    /// <param name="request">応答する要求の通し番号</param>
    /// <param name="command">要求名</param>
    /// <param name="message">人が読む理由</param>
    public ValueTask FailAsync(Seq request, string command, string message) {
        this.BeginResponse(request, command, false).WriteString("message"u8, message);
        return this.SendAsync();
    }

    /// <summary>イベントを書く</summary>
    /// <param name="name">イベント名</param>
    /// <param name="body">本体</param>
    /// <param name="info">本体の型情報</param>
    public ValueTask EventAsync<T>(EventName name, T body, JsonTypeInfo<T> info) {
        Utf8JsonWriter json = this.Begin("event"u8);
        json.WriteString("event"u8, Names.Events.Name(name));
        json.WritePropertyName("body"u8);
        JsonSerializer.Serialize(json, body, info);
        return this.SendAsync();
    }

    private Utf8JsonWriter BeginResponse(Seq request, string command, bool success) {
        Utf8JsonWriter json = this.Begin("response"u8);
        json.WriteNumber("request_seq"u8, request.Value);
        json.WriteBoolean("success"u8, success);
        json.WriteString("command"u8, command);
        return json;
    }

    private Utf8JsonWriter Begin(ReadOnlySpan<byte> type) {
        this._buffer.ResetWrittenCount();
        _ = this._buffer.GetSpan(HeaderSpace);
        this._buffer.Advance(HeaderSpace);
        this._json.Reset(this._buffer);
        this._json.WriteStartObject();
        this._json.WriteNumber("seq"u8, ++this._seq);
        this._json.WriteString("type"u8, type);
        return this._json;
    }

    private ValueTask SendAsync() {
        this._json.WriteEndObject();
        this._json.Flush();
        Memory<byte> message = MemoryMarshal.AsMemory(this._buffer.WrittenMemory);
        // 長さは文字数ではなくUTF-8のバイト数
        Span<byte> header = stackalloc byte[HeaderSpace];
        _ = Utf8.TryWrite(header, $"Content-Length: {message.Length - HeaderSpace}\r\n\r\n", out int length);
        int start = HeaderSpace - length;
        header[..length].CopyTo(message.Span[start..]);
        return this._stream.WriteAsync(message[start..]);
    }
}
