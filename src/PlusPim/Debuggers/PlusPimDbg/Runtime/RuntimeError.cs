namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// ランタイムエラーの種類
/// </summary>
public enum RuntimeErrorKind {
    /// <summary><c>div</c>/<c>divu</c> のゼロ除算</summary>
    DivisionByZero,
    /// <summary><c>div</c> の <c>0x80000000 / -1</c></summary>
    DivisionOverflow,
    /// <summary><c>mfc0</c>/<c>mtc0</c> で未対応の CP0 レジスタ番号</summary>
    UnsupportedCP0Register,
    /// <summary><c>runtime_call!</c> で未知の機能番号</summary>
    UnknownRuntimeCall,
}

/// <summary>
/// 実行を続けられない操作を検出したことを表すランタイムエラー
/// </summary>
/// <remarks>
/// MIPS の例外と異なり CP0 と例外ハンドラを使わない．
/// PC は進めないので，発生した命令のアドレスは PC そのものである
/// </remarks>
/// <param name="Kind">種類</param>
/// <param name="Message">人間が読める説明文</param>
internal readonly record struct RuntimeError(RuntimeErrorKind Kind, string Message);
