using PlusPim.Application;

namespace PlusPim.EditorController.DebugAdapter;

/// <summary>
/// ランタイムエラーと MIPS の例外をエディタに表示するときの文字列
/// </summary>
/// <remarks>
/// どちらも DAP の停止理由は <c>exception</c> とする (VS Code はこの理由のときだけ例外ウィジェットを表示するため)．
/// 区別は例外ウィジェットのタイトル (<c>exceptionId</c>)，本文と，コールスタックの状態表示 (<c>StoppedEvent.description</c>) で行う
/// </remarks>
internal static class RuntimeErrorDisplay {
    /// <summary>
    /// ランタイムエラーで停止したときのコールスタックの状態表示
    /// </summary>
    public const string StateLabel = "Paused on PlusPim runtime error";

    private const string ContinueNote =
        "This is a PlusPim runtime error, not a MIPS exception: no exception handler runs and execution cannot continue. "
        + "Use Step Back to return to the state before this instruction.";

    /// <summary>
    /// 例外ウィジェットのタイトル
    /// </summary>
    public static string ExceptionId(RuntimeErrorInfo info) {
        return $"PlusPim runtime error ({info.Id})";
    }

    /// <summary>
    /// 例外ウィジェットの本文
    /// </summary>
    public static string WidgetDescription(RuntimeErrorInfo info) {
        return $"{info.Description}\n\n{ContinueNote}";
    }

    /// <summary>
    /// デバッグコンソール (stderr) に出力する1行 (改行を含む)
    /// </summary>
    public static string ConsoleLine(RuntimeErrorInfo info) {
        string location = info.SourceFile is not null && 0 < info.Line
            ? $" ({info.SourceFile.Name}:{info.Line})"
            : "";
        return $"[PlusPim runtime error] {info.Id} at 0x{info.Address:X8}{location}: {info.Description.TrimEnd('.')}. Execution cannot continue; use Step Back.\n";
    }

    /// <summary>
    /// MIPS の例外の例外ウィジェットのタイトル
    /// </summary>
    public static string MipsExceptionId(ExceptionInfo info) {
        return info.IsDouble
            ? $"MIPS double exception ({info.ExceptionId})"
            : $"MIPS exception ({info.ExceptionId})";
    }

    /// <summary>
    /// MIPS の例外で停止したときのコールスタックの状態表示
    /// </summary>
    public static string MipsExceptionStateLabel(ExceptionInfo info) {
        return info.IsDouble
            ? "Paused on MIPS double exception"
            : "Paused on MIPS exception";
    }
}
