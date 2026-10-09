namespace PlusPim.Application;

/// <summary>
/// 停止理由を表す列挙型
/// </summary>
public enum StopReason {
    /// 実行すべき命令の実行が完了した
    Step,
    /// 次の命令にブレークポイントが配置されている
    Breakpoint,
    /// デバッギが終了した
    Terminated,
    /// 例外が発生した
    Exception,
    /// ランタイムエラーが発生した．StepBack 以外では先へ進めない
    RuntimeError,
    /// 実行中に一時停止を要求された
    Pause,
    /// 巻き戻しで実行の履歴の先頭に達した
    HistoryStart
}

