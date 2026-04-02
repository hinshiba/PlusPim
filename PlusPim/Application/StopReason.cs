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
    Exception
}

