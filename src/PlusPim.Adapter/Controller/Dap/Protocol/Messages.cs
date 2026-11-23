using PlusPim.Image;
using PlusPim.Instructions;
using PlusPim.Usecase;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlusPim.Adapter.Controller.Dap.Protocol;

/// <summary>
/// 受け付ける要求
/// </summary>
internal enum Command: byte {
    Initialize, Launch, ConfigurationDone, SetBreakpoints, SetExceptionBreakpoints, Threads, StackTrace, Scopes, Variables,
    ReadMemory, ExceptionInfo, Continue, Next, StepIn, StepOut, StepBack, ReverseContinue, Pause, Disconnect, PseudoExpansions,
}

/// <summary>
/// 列挙子と通信上の名前の全単射. 解読と符号化の両方で同じ表を引く
/// </summary>
/// <typeparam name="T">列挙型</typeparam>
/// <param name="table">すべての列挙子とその名前</param>
internal sealed class NameTable<T>(params (T Value, string Name)[] table) where T : struct, Enum {
    /// <summary>名前を得る</summary>
    public string Name(T value) {
        foreach((T v, string name) in table) {
            if(EqualityComparer<T>.Default.Equals(v, value)) {
                return name;
            }
        }
        throw new ArgumentOutOfRangeException(nameof(value), value, null);
    }

    /// <summary>読み取り位置の文字列を名前として引く</summary>
    /// <param name="reader">文字列の値にある読み取り器</param>
    /// <param name="value">見つけた列挙子</param>
    /// <returns>表にあれば <see langword="true"/></returns>
    public bool TryParse(ref Utf8JsonReader reader, out T value) {
        foreach((T v, string name) in table) {
            if(reader.ValueTextEquals(name)) {
                value = v;
                return true;
            }
        }
        value = default;
        return false;
    }
}

/// <summary>
/// 通信上の名前の表
/// </summary>
internal static class Names {
    /// <summary>要求名</summary>
    public static NameTable<Command> Commands { get; } = new(
        (Command.Initialize, "initialize"), (Command.Launch, "launch"), (Command.ConfigurationDone, "configurationDone"),
        (Command.SetBreakpoints, "setBreakpoints"), (Command.SetExceptionBreakpoints, "setExceptionBreakpoints"),
        (Command.Threads, "threads"), (Command.StackTrace, "stackTrace"), (Command.Scopes, "scopes"), (Command.Variables, "variables"),
        (Command.ReadMemory, "readMemory"), (Command.ExceptionInfo, "exceptionInfo"), (Command.Continue, "continue"), (Command.Next, "next"),
        (Command.StepIn, "stepIn"), (Command.StepOut, "stepOut"), (Command.StepBack, "stepBack"), (Command.ReverseContinue, "reverseContinue"),
        (Command.Pause, "pause"), (Command.Disconnect, "disconnect"),
        // PlusPimの拡張要求. VS Code拡張のEXPANSIONS_REQUESTと同じ名前
        (Command.PseudoExpansions, "pluspimPseudoExpansions"));

    /// <summary>例外フィルタの識別子</summary>
    public static NameTable<ExceptionFilter> ExceptionFilters { get; } = new(
        (ExceptionFilter.Fatal, "fatal"), (ExceptionFilter.Break, "break"), (ExceptionFilter.Syscall, "syscall"));

    /// <summary><c>output</c> イベントの種類</summary>
    public static NameTable<OutputCategory> OutputCategories { get; } = new(
        (OutputCategory.Console, "console"), (OutputCategory.Stderr, "stderr"));

    /// <summary>イベント名</summary>
    public static NameTable<EventName> Events { get; } = new(
        (EventName.Initialized, "initialized"), (EventName.Stopped, "stopped"), (EventName.Terminated, "terminated"), (EventName.Output, "output"));
}

/// <summary>
/// <c>output</c> イベントの種類
/// </summary>
internal enum OutputCategory: byte { Console, Stderr }

/// <summary>
/// 送るイベント
/// </summary>
internal enum EventName: byte { Initialized, Stopped, Terminated, Output }

/// <summary>
/// 境界で解析した要求
/// </summary>
/// <param name="Seq">要求の通し番号</param>
/// <param name="Command">応答に書く要求名</param>
internal abstract record Request(Seq Seq, string Command) {
    /// <summary>引数を読まない要求</summary>
    internal sealed record Simple(Seq Seq, Protocol.Command Kind): Request(Seq, Names.Commands.Name(Kind));

    /// <summary><c>launch</c></summary>
    internal sealed record Launch(Seq Seq, StartAction Action, ImmutableArray<SourcePath> Program): Request(Seq, Names.Commands.Name(Protocol.Command.Launch));

    /// <summary>実行の要求</summary>
    internal sealed record Execute(Seq Seq, string Command, Motion Motion): Request(Seq, Command);

    /// <summary><c>setBreakpoints</c></summary>
    internal sealed record SetBreakpoints(Seq Seq, SourcePath Source, SourceLine[] Lines): Request(Seq, Names.Commands.Name(Protocol.Command.SetBreakpoints));

    /// <summary><c>setExceptionBreakpoints</c></summary>
    internal sealed record SetExceptionFilters(Seq Seq, ExceptionFilter[] Filters): Request(Seq, Names.Commands.Name(Protocol.Command.SetExceptionBreakpoints));

    /// <summary><c>stackTrace</c>. <paramref name="Levels"/> が0なら上限まで返す</summary>
    internal sealed record StackTrace(Seq Seq, uint StartFrame, uint Levels): Request(Seq, Names.Commands.Name(Protocol.Command.StackTrace));

    /// <summary><c>scopes</c></summary>
    internal sealed record Scopes(Seq Seq, FrameId Frame): Request(Seq, Names.Commands.Name(Protocol.Command.Scopes));

    /// <summary><c>variables</c></summary>
    internal sealed record Variables(Seq Seq, VariablesReference Reference): Request(Seq, Names.Commands.Name(Protocol.Command.Variables));

    /// <summary><c>readMemory</c></summary>
    internal sealed record ReadMemory(Seq Seq, MemoryWindow Window): Request(Seq, Names.Commands.Name(Protocol.Command.ReadMemory));

    /// <summary><c>pluspimPseudoExpansions</c></summary>
    internal sealed record PseudoExpansions(Seq Seq, SourcePath Source): Request(Seq, Names.Commands.Name(Protocol.Command.PseudoExpansions));

    /// <summary>未知の要求か, 引数を解析できなかった要求</summary>
    internal sealed record Rejected(Seq Seq, string Command, string Reason): Request(Seq, Command);
}

// 以下はDAPの仕様の定義名とプロパティ名にそろえた引数と本体

internal sealed record InitializeArguments(bool LinesStartAt1 = true, bool ColumnsStartAt1 = true, string PathFormat = "path");
internal sealed record LaunchArguments([property: JsonConverter(typeof(Wire.ProgramConverter))] ImmutableArray<string> Program, string? Cwd = null, bool NoDebug = false, bool StopOnEntry = true);
internal sealed record Source(SourcePath Path);
internal sealed record SourceBreakpoint(SourceLine Line);
internal sealed record SetBreakpointsArguments(Source Source, SourceBreakpoint[]? Breakpoints = null);
internal sealed record SetExceptionBreakpointsArguments(ExceptionFilter[] Filters);
internal sealed record StackTraceArguments(uint StartFrame = 0, uint Levels = 0);
internal sealed record ScopesArguments(FrameId FrameId);
internal sealed record VariablesArguments(VariablesReference VariablesReference);
internal sealed record ReadMemoryArguments(Address MemoryReference, uint Count, int Offset = 0);
internal sealed record PseudoExpansionsArguments(Source Source);
internal sealed record SteppingArguments(string? Granularity = null);

/// <summary>内容のない本体</summary>
internal sealed record Empty {
    /// <summary>唯一の値</summary>
    public static Empty Value { get; } = new();
}

internal sealed record ExceptionBreakpointsFilter(ExceptionFilter Filter, string Label, string Description, bool Default);
internal sealed record Capabilities(ExceptionBreakpointsFilter[] ExceptionBreakpointFilters) {
    public bool SupportsConfigurationDoneRequest => true;
    public bool SupportsReadMemoryRequest => true;
    public bool SupportsStepBack => true;
    public bool SupportsExceptionInfoRequest => true;
}
internal sealed record DapThread(int Id, string Name);
internal sealed record ThreadsBody(DapThread[] Threads);
internal sealed record Breakpoint(bool Verified, SourceLine Line);
internal sealed record SetBreakpointsBody(Breakpoint[] Breakpoints);
internal sealed record StackFrame(FrameId Id, string Name, Source? Source, int Line, int Column, Address InstructionPointerReference);
internal sealed record StackTraceBody(StackFrame[] StackFrames, int TotalFrames);
internal sealed record Scope(string Name, VariablesReference VariablesReference, bool Expensive);
internal sealed record ScopesBody(Scope[] Scopes);
internal sealed record Variable(string Name, Word Value, VariablesReference VariablesReference, Address? MemoryReference);
internal sealed record VariablesBody(Variable[] Variables);
internal sealed record ReadMemoryBody(Address Address, ReadOnlyMemory<byte> Data, int? UnreadableBytes);
internal sealed record ExceptionInfoBody(string ExceptionId, string Description, string BreakMode);

/// <summary>
/// 疑似命令の展開先の1命令. <paramref name="Text"/> は表記に未対応の命令なら <c>?</c>
/// </summary>
internal sealed record PseudoExpansionInstruction(Address Address, string Text);

/// <summary>
/// 疑似命令の1行. <paramref name="Instructions"/> はアドレス順
/// </summary>
internal sealed record PseudoExpansionLine(SourceLine Line, string Mnemonic, PseudoExpansionInstruction[] Instructions);

/// <summary>
/// <c>pluspimPseudoExpansions</c> の応答. 読み込んでいないファイルなら <paramref name="Lines"/> は空
/// </summary>
internal sealed record PseudoExpansionsBody(PseudoExpansionLine[] Lines);

internal sealed record StoppedEvent(string Reason, string? Description = null, string? Text = null) {
    public int ThreadId => 1;
    public bool AllThreadsStopped => true;
}
internal sealed record OutputEvent(string Category, string Output);
