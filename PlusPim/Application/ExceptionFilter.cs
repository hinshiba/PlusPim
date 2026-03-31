namespace PlusPim.Application;

/// <summary>
/// 例外フィルタを表す列挙型
/// </summary>
public enum ExceptionFilter {
    /// 二重例外    
    Double,
    /// 致命的な例外(AdEL, AdES, RI, CpU, Ov)
    Fatal,
    /// break命令
    Break,
    /// syscall命令
    Syscall
}
