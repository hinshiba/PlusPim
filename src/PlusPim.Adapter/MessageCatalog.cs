using PlusPim.Asm;
using PlusPim.Dbg;
using PlusPim.Image;
using PlusPim.Instructions;
using PlusPim.Usecase;

namespace PlusPim.Adapter;

/// <summary>
/// 例外とランタイムエラーの文面
/// </summary>
/// <param name="Id">識別子. 例外では公式の略称, ランタイムエラーでは種類の名前</param>
/// <param name="Description">説明</param>
public readonly record struct ExceptionText(string Id, string Description);

/// <summary>
/// CLIとDAPが共有する概念を利用者に見せる文面の置き場所
/// </summary>
/// <remarks>
/// 診断, 例外, ランタイムエラー, 停止理由, 実行の失敗を扱う.
/// 将来的なi18nのためアダプタ間で共通のメッセージはここに集約する.
/// 現在は英語のみ
/// </remarks>
public static class MessageCatalog {
    /// <summary>
    /// 診断を1行の文面にする
    /// </summary>
    public static string Format(Diagnostic d) {
        string body = d.Code switch {
            WarningCode w => $"warning PPW{(ushort)w:D4}: {Text(w, d.Args.AsSpan())}",
            ErrorCode e => $"error PPE{(ushort)e:D4}: {Text(e, d.Args.AsSpan())}",
        };
        return d.Location is SourceLocation loc ? $"{Format(loc)}: {body}" : body;
    }

    /// <summary>
    /// ソース上の位置を <c>path:line</c> にする
    /// </summary>
    public static string Format(SourceLocation loc) {
        return Where(loc.Path.Value, loc.Line.ToString());
    }

    private static string Where(string path, string line) {
        return $"{path}:{line}";
    }

    private static string Text(WarningCode c, ReadOnlySpan<string> a) {
        return c switch {
            WarningCode.UnparsableLine => "Unparsable line; discarded",
            WarningCode.UnknownMnemonic => $"Unknown mnemonic '{a[0]}'; line discarded",
            WarningCode.UnknownDirective => $"Unknown directive '{a[0]}'; line discarded",
            WarningCode.DiscardedOperand => $"Operand '{a[0]}' discarded",
            WarningCode.MissingSegment => "No segment directive before this line; treated as .text",
            WarningCode.MisplacedDirective => "Data directive in a text section; discarded",
            WarningCode.DuplicateLabel => $"Label '{a[0]}' redefined; the later definition wins",
            WarningCode.UndefinedGlobal => $"'.globl {a[0]}' names an undefined label; discarded",
            WarningCode.DuplicateGlobal => $"Global '{a[0]}' defined in multiple files; the later definition wins",
            WarningCode.UndefinedSymbol => $"Undefined symbol '{a[0]}'; resolved to 0x00000000",
            WarningCode.MissingEntry => $"No global '{a[0]}'; starting at the beginning of the text segment",
            WarningCode.OperandMismatch => $"Operands do not match '{a[0]}'; line discarded",
            WarningCode.MisplacedInstruction => "Instruction in a data section; discarded",
            WarningCode.DuplicateSource => $"Source '{a[0]}' specified more than once; ignored",
            _ => throw new ArgumentOutOfRangeException(nameof(c)),
        };
    }

    private static string Text(ErrorCode c, ReadOnlySpan<string> a) {
        return c switch {
            ErrorCode.SourceNotFound => $"Source '{a[0]}' not found",
            ErrorCode.ValueOutOfRange => $"Value {a[0]} out of range",
            ErrorCode.NonAsciiString => $"String \"{a[0]}\" contains non-ASCII characters",
            ErrorCode.MisalignedText => $"Text address {a[0]} is not a multiple of 4",
            ErrorCode.SectionOutOfRange => $"Section at {a[0]} ({a[1]} bytes) does not fit in its segment",
            ErrorCode.SectionOverlap => $"Section at {a[0]} overlaps the section at {Where(a[1], a[2])}",
            _ => throw new ArgumentOutOfRangeException(nameof(c)),
        };
    }

    /// <summary>
    /// 例外を識別子と説明にする
    /// </summary>
    public static ExceptionText Describe(ExcCode c) {
        return c switch {
            ExcCode.AdEL => new(nameof(ExcCode.AdEL), "Address error on load or instruction fetch"),
            ExcCode.AdES => new(nameof(ExcCode.AdES), "Address error on store"),
            ExcCode.Sys => new(nameof(ExcCode.Sys), "System call"),
            ExcCode.Bp => new(nameof(ExcCode.Bp), "Breakpoint"),
            ExcCode.RI => new(nameof(ExcCode.RI), "Reserved instruction"),
            ExcCode.CpU => new(nameof(ExcCode.CpU), "Coprocessor unusable"),
            ExcCode.Ov => new(nameof(ExcCode.Ov), "Arithmetic overflow"),
            _ => throw new ArgumentOutOfRangeException(nameof(c)),
        };
    }

    /// <summary>
    /// ランタイムエラーを識別子と説明にする
    /// </summary>
    public static ExceptionText Describe(RuntimeError e) {
        return e switch {
            RuntimeErrorKind k => Describe(k),
            DoubleException d => new(nameof(DoubleException), $"Double exception ({Describe(d.Code).Id}: {Describe(d.Code).Description})"),
        };
    }

    private static ExceptionText Describe(RuntimeErrorKind k) {
        return k switch {
            RuntimeErrorKind.DivisionByZero => new(nameof(RuntimeErrorKind.DivisionByZero), "Division by zero"),
            RuntimeErrorKind.UnknownRuntimeCall => new(nameof(RuntimeErrorKind.UnknownRuntimeCall), "Unknown runtime call number"),
            RuntimeErrorKind.NonInstructionExecution => new(nameof(RuntimeErrorKind.NonInstructionExecution), "Executing memory without an assembled instruction is not supported yet"),
            RuntimeErrorKind.TextAccess => new(nameof(RuntimeErrorKind.TextAccess), "Loading from or storing to text memory is not supported yet"),
            _ => throw new ArgumentOutOfRangeException(nameof(k)),
        };
    }

    /// <summary>
    /// 実行の操作の想定外の失敗を文面にする
    /// </summary>
    /// <param name="e">失敗の原因. 取り消されたなら <see langword="null"/></param>
    public static string InternalError(Exception? e) {
        return $"[PlusPim internal error] {e}";
    }

    /// <summary>
    /// 停止中のフレーム表にないフレームを指した
    /// </summary>
    /// <param name="index">要求されたフレームの添字</param>
    public static string UnknownFrame(int index) {
        return $"Unknown frame {index}.";
    }

    /// <summary>
    /// 例外でもランタイムエラーでもない停止で例外の情報を求められた
    /// </summary>
    public static string NoException() {
        return "The debuggee did not stop on an exception.";
    }

    /// <summary>
    /// 停止理由を説明にする
    /// </summary>
    /// <returns>ステップやブレークポイントのように説明の要らない停止では <see langword="null"/></returns>
    public static string? Describe(Stop s) {
        return s switch {
            Stepped or Breakpoint or Paused => null,
            ExceptionRaised t => Describe(t.Code).Description,
            RuntimeErrorRaised f => $"{Describe(f.Error).Description}; execution aborted",
            Exited e => e.Cause switch {
                ExitCause.Returned => "Program returned from main",
                ExitCause.ExitCall => "Program exited by the exit runtime call",
                _ => throw new ArgumentOutOfRangeException(nameof(s)),
            },
            HistoryStart => "Reached the start of the execution history",
        };
    }
}
