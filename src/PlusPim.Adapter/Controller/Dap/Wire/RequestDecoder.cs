using PlusPim.Adapter.Controller.Dap.Protocol;
using PlusPim.Image;
using PlusPim.Usecase;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PlusPim.Adapter.Controller.Dap.Wire;

/// <summary>
/// メッセージの本体を一度だけ走査し, 不正な値を表せない <see cref="Request"/> に解析する
/// </summary>
internal static class RequestDecoder {
    [Flags]
    private enum Key: byte { Seq = 1, Type = 2, Command = 4, Arguments = 8 }

    /// <summary>本体を解析する</summary>
    /// <param name="body">1つのメッセージの本体</param>
    /// <returns>要求. 要求でないメッセージは <see langword="null"/></returns>
    /// <exception cref="FramingException">JSONとして壊れているか, 封筒の <c>seq</c> と <c>type</c> が不正</exception>
    public static Request? Decode(ReadOnlySpan<byte> body) {
        int seq = 0;
        bool isRequest = false;
        Command? command = null;
        string name = "";
        Range arguments = default;
        try {
            Utf8JsonReader reader = new(body);
            Key seen = 0;
            if(!reader.Read() || reader.TokenType != JsonTokenType.StartObject) {
                throw new FramingException("A DAP message must be a JSON object.");
            }
            while(reader.Read() && reader.TokenType == JsonTokenType.PropertyName) {
                Key key = reader.ValueTextEquals("seq"u8) ? Key.Seq
                    : reader.ValueTextEquals("type"u8) ? Key.Type
                    : reader.ValueTextEquals("command"u8) ? Key.Command
                    : reader.ValueTextEquals("arguments"u8) ? Key.Arguments
                    : 0;
                if((seen & key) != 0) {
                    throw new FramingException("A DAP message has a duplicate key.");
                }
                seen |= key;
                _ = reader.Read();
                switch(key) {
                    case Key.Seq:
                        seq = reader.GetInt32();
                        break;
                    case Key.Type:
                        isRequest = reader.ValueTextEquals("request"u8);
                        break;
                    case Key.Command when Names.Commands.TryParse(ref reader, out Command known):
                        command = known;
                        break;
                    case Key.Command:
                        name = reader.GetString()!;
                        break;
                    case Key.Arguments:
                        int start = (int)reader.TokenStartIndex;
                        reader.Skip();
                        arguments = start..(int)reader.BytesConsumed;
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
            // 末尾にゴミがあればReadが投げる
            if(reader.TokenType != JsonTokenType.EndObject || reader.Read()) {
                throw new FramingException("A DAP message has trailing data.");
            }
        } catch(Exception e) when(e is JsonException or InvalidOperationException or FormatException) {
            throw new FramingException($"A DAP message is malformed: {e.Message}");
        }
        if(!Seq.TryCreate(seq, out Seq s)) {
            throw new FramingException("A DAP message has no valid seq.");
        }
        if(!isRequest) {
            return null;
        }
        if(command is not Command c) {
            return new Request.Rejected(s, name, $"Unknown command '{name}'.");
        }
        try {
            return Parse(s, c, body[arguments]);
        } catch(JsonException e) {
            return new Request.Rejected(s, Names.Commands.Name(c), $"Invalid arguments: {e.Message}");
        }
    }

    private static Request Parse(Seq seq, Command command, ReadOnlySpan<byte> arguments) {
        switch(command) {
            case Command.Initialize:
                // 1始まりの行と列, パスで表したソースだけを扱う. 受け付けた後に残す情報はない
                return Read(arguments, DapJson.Default.InitializeArguments) is { LinesStartAt1: true, ColumnsStartAt1: true, PathFormat: "path" }
                    ? new Request.Simple(seq, command)
                    : new Request.Rejected(seq, Names.Commands.Name(command), "Only 1-based lines and columns and path sources are supported.");
            case Command.Launch:
                LaunchArguments launch = Read(arguments, DapJson.Default.LaunchArguments);
                return new Request.Launch(seq, launch.NoDebug ? StartAction.NoDebug : launch.StopOnEntry ? StartAction.StopAtEntry : StartAction.Run, Resolve(launch.Program, launch.Cwd));
            case Command.SetBreakpoints:
                SetBreakpointsArguments breakpoints = Read(arguments, DapJson.Default.SetBreakpointsArguments);
                return new Request.SetBreakpoints(seq, breakpoints.Source.Path, [.. (breakpoints.Breakpoints ?? []).Select(b => b.Line)]);
            case Command.SetExceptionBreakpoints:
                return new Request.SetExceptionFilters(seq, Read(arguments, DapJson.Default.SetExceptionBreakpointsArguments).Filters);
            case Command.StackTrace:
                StackTraceArguments stackTrace = Read(arguments, DapJson.Default.StackTraceArguments);
                return new Request.StackTrace(seq, stackTrace.StartFrame, stackTrace.Levels);
            case Command.Scopes:
                return new Request.Scopes(seq, Read(arguments, DapJson.Default.ScopesArguments).FrameId);
            case Command.Variables:
                return new Request.Variables(seq, Read(arguments, DapJson.Default.VariablesArguments).VariablesReference);
            case Command.ReadMemory:
                // 項目間の不変条件があるので, 1つの値の変換器ではなくここで範囲にする
                ReadMemoryArguments memory = Read(arguments, DapJson.Default.ReadMemoryArguments);
                return new Request.ReadMemory(seq, MemoryWindow.Create(memory.MemoryReference, memory.Offset, memory.Count));
            case Command.Continue:
                return Execute(seq, command, new Continue());
            case Command.ReverseContinue:
                return Execute(seq, command, new ReverseContinue());
            case Command.Next:
                return Execute(seq, command, new StepOver(GranularityOf(arguments)));
            case Command.StepIn:
                return Execute(seq, command, new StepIn(GranularityOf(arguments)));
            case Command.StepOut:
                return Execute(seq, command, new StepOut(GranularityOf(arguments)));
            case Command.StepBack:
                return Execute(seq, command, new StepBack(GranularityOf(arguments)));
            case Command.PseudoExpansions:
                return new Request.PseudoExpansions(seq, Read(arguments, DapJson.Default.PseudoExpansionsArguments).Source.Path);
            default:
                return new Request.Simple(seq, command);
        }
    }

    /// <summary>
    /// <c>program</c> の各パスを, 相対パスなら <c>cwd</c> を基準に解決する
    /// </summary>
    /// <param name="program">パスの文字列</param>
    /// <param name="cwd">絶対パスのディレクトリ. 省略されたなら相対パスを拒否する</param>
    private static ImmutableArray<SourcePath> Resolve(ImmutableArray<string> program, string? cwd) {
        if(cwd is not null && !Path.IsPathFullyQualified(cwd)) {
            return WireError.Throw<ImmutableArray<SourcePath>>($"The cwd '{cwd}' must be an absolute path.");
        }
        return [.. program.Select(raw => (cwd is not null || Path.IsPathFullyQualified(raw)) && SourcePath.TryResolve(raw, cwd ?? AppContext.BaseDirectory, out SourcePath path)
            ? path
            : WireError.Throw<SourcePath>($"Invalid program path '{raw}'. A relative path requires cwd."))];
    }

    private static Request.Execute Execute(Seq seq, Command command, Motion motion) {
        return new Request.Execute(seq, Names.Commands.Name(command), motion);
    }

    /// <summary>
    /// ステップの要求の粒度を読む. DAPの既定はstatementで, 文と行の区別はないので, instruction以外を行とする
    /// </summary>
    private static Granularity GranularityOf(ReadOnlySpan<byte> arguments) {
        return Read(arguments, DapJson.Default.SteppingArguments).Granularity == "instruction" ? Granularity.Instruction : Granularity.Line;
    }

    /// <summary>
    /// 引数を読む. 省略された引数は空のオブジェクトとして読み, 必須の項目の欠落を拒否させる
    /// </summary>
    private static T Read<T>(ReadOnlySpan<byte> arguments, JsonTypeInfo<T> info) {
        return JsonSerializer.Deserialize(arguments.IsEmpty ? "{}"u8 : arguments, info) ?? WireError.Throw<T>("The arguments must not be null.");
    }
}
