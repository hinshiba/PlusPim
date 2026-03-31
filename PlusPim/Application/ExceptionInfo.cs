using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Application;

/// <summary>
/// DAP層に公開する例外情報
/// </summary>
public sealed class ExceptionInfo {

    /// <summary>
    /// 例外番号
    /// </summary>
    public required ExcCode reason { get; init; }

    /// <summary>
    /// 例外の識別子 (ExcCode名: "AdEL", "Sys", etc.)
    /// </summary>
    public required string ExceptionId { get; init; }

    /// <summary>
    /// 人間が読める説明文
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 二重例外かどうか
    /// </summary>
    public required bool IsDouble { get; init; }
}
