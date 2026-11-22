using PlusPim.Dbg;

namespace PlusPim.Usecase;

/// <summary>
/// 実行の操作が止まった理由
/// </summary>
/// <remarks>
/// <see cref="ExceptionRaised"/> は例外フィルタが捕捉した例外.
/// </remarks>
public readonly union Stop(Stepped, Breakpoint, ExceptionRaised, Exited, RuntimeErrorRaised, Paused, HistoryStart);

/// <summary>ステップの目標に達した</summary>
public readonly record struct Stepped;

/// <summary>次に実行する命令にブレークポイントがある</summary>
public readonly record struct Breakpoint;

/// <summary>取り消しで中断した</summary>
public readonly record struct Paused;

/// <summary>巻き戻しで履歴の先頭に達した</summary>
public readonly record struct HistoryStart;

/// <summary>
/// ステップの単位
/// </summary>
public enum Granularity: byte {
    /// <summary>ソースの行単位</summary>
    Line,
    /// <summary>機械命令単位</summary>
    Instruction,
}
