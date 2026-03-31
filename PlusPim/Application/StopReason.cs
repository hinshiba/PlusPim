namespace PlusPim.Application;

/// <summary>
/// 停止理由を表す列挙型
/// </summary>
internal enum StopReason {
    Step,
    Breakpoint,
    Terminated,
    Exception
}

