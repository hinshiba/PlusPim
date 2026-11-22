using PlusPim.Adapter.Controller.Dap.Protocol;
using PlusPim.Image;
using PlusPim.Instructions;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlusPim.Adapter.Controller.Dap.Wire;

/// <summary>
/// DAPの引数と本体を読み書きするソース生成の文脈
/// </summary>
/// <remarks>
/// 他のプロジェクトの型は通信形式を知らないので, その変換器はここに並べる.
/// 登録を忘れたNewtypeは警告なしにオブジェクトとして書かれるため, テストですべてのNewtypeを確かめる.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowDuplicateProperties = false,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    Converters = [typeof(AddressConverter), typeof(SourcePathConverter)])]
[JsonSerializable(typeof(InitializeArguments))]
[JsonSerializable(typeof(LaunchArguments))]
[JsonSerializable(typeof(SetBreakpointsArguments))]
[JsonSerializable(typeof(SetExceptionBreakpointsArguments))]
[JsonSerializable(typeof(StackTraceArguments))]
[JsonSerializable(typeof(ScopesArguments))]
[JsonSerializable(typeof(VariablesArguments))]
[JsonSerializable(typeof(ReadMemoryArguments))]
[JsonSerializable(typeof(PseudoExpansionsArguments))]
[JsonSerializable(typeof(SteppingArguments))]
[JsonSerializable(typeof(Empty))]
[JsonSerializable(typeof(Capabilities))]
[JsonSerializable(typeof(ThreadsBody))]
[JsonSerializable(typeof(SetBreakpointsBody))]
[JsonSerializable(typeof(StackTraceBody))]
[JsonSerializable(typeof(ScopesBody))]
[JsonSerializable(typeof(VariablesBody))]
[JsonSerializable(typeof(ReadMemoryBody))]
[JsonSerializable(typeof(ExceptionInfoBody))]
[JsonSerializable(typeof(PseudoExpansionsBody))]
[JsonSerializable(typeof(StoppedEvent))]
[JsonSerializable(typeof(OutputEvent))]
internal sealed partial class DapJson: JsonSerializerContext;

/// <summary>
/// 通信の値が不正なことを知らせる唯一の送出点
/// </summary>
internal static class WireError {
    /// <summary><see cref="JsonException"/> を投げる</summary>
    /// <typeparam name="T">呼び出し元の式の型</typeparam>
    /// <param name="message">理由</param>
    [DoesNotReturn]
    public static T Throw<T>(string message = "The value is out of range.") {
        throw new JsonException(message);
    }
}

/// <summary>
/// 32ビットの値を <c>0x</c> と16進8桁の文字列として読み書きする
/// </summary>
/// <remarks>読むときは <c>0x</c> と1から8桁の16進だけを受け付ける</remarks>
/// <typeparam name="T">値の型</typeparam>
/// <param name="create">値から作る関数</param>
/// <param name="value">値を取り出す関数</param>
internal abstract class HexConverter<T>(Func<uint, T> create, Func<T, uint> value): JsonConverter<T> {
    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string text = reader.GetString()!;
        return 3 <= text.Length && text.Length <= 10 && text.StartsWith("0x", StringComparison.Ordinal)
            && uint.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint parsed)
            ? create(parsed)
            : WireError.Throw<T>($"Invalid hexadecimal value '{text}'.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T v, JsonSerializerOptions options) {
        Span<byte> text = stackalloc byte[10];
        "0x"u8.CopyTo(text);
        _ = value(v).TryFormat(text[2..], out _, "X8", CultureInfo.InvariantCulture);
        writer.WriteStringValue(text);
    }
}

/// <summary><see cref="Address"/> の変換器</summary>
internal sealed class AddressConverter(): HexConverter<Address>(v => new Address(v), a => a.Addr);

/// <summary><see cref="Word"/> の変換器</summary>
internal sealed class WordConverter(): HexConverter<Word>(v => new Word(v), w => w.Value);

/// <summary>
/// <see cref="SourcePath"/> を文字列として読み書きする
/// </summary>
/// <remarks>DAPのパスは絶対パスなので, 相対パスを拒否する. 基準のディレクトリは使われない</remarks>
internal sealed class SourcePathConverter: JsonConverter<SourcePath> {
    /// <inheritdoc/>
    public override SourcePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string raw = reader.GetString()!;
        return Path.IsPathFullyQualified(raw) && SourcePath.TryResolve(raw, AppContext.BaseDirectory, out SourcePath path) ? path : WireError.Throw<SourcePath>("Invalid path.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, SourcePath value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// <see cref="ExceptionFilter"/> を <see cref="Names.ExceptionFilters"/> の識別子として読み書きする
/// </summary>
internal sealed class ExceptionFilterConverter: JsonConverter<ExceptionFilter> {
    /// <inheritdoc/>
    public override ExceptionFilter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return Names.ExceptionFilters.TryParse(ref reader, out ExceptionFilter filter) ? filter : WireError.Throw<ExceptionFilter>($"Unknown exception filter '{reader.GetString()}'.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, ExceptionFilter value, JsonSerializerOptions options) {
        writer.WriteStringValue(Names.ExceptionFilters.Name(value));
    }
}

/// <summary>
/// 枠組みか封筒の違反. 応答できないので接続を閉じる
/// </summary>
/// <param name="message">理由</param>
internal sealed class FramingException(string message): Exception(message);
