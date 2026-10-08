using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Application;

/// <summary>
/// DAP層に公開するランタイムエラーの情報
/// </summary>
public sealed class RuntimeErrorInfo {

    /// <summary>
    /// ランタイムエラーの種類
    /// </summary>
    public required RuntimeErrorKind Kind { get; init; }

    /// <summary>
    /// ランタイムエラーの識別子 (RuntimeErrorKind名: "DivisionByZero", etc.)
    /// </summary>
    public string Id => this.Kind.ToString();

    /// <summary>
    /// 人間が読める説明文
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// ランタイムエラーを起こした命令のアドレス
    /// </summary>
    public required uint Address { get; init; }
}
