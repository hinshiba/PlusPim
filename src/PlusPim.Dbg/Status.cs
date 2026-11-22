using PlusPim.Instructions;

namespace PlusPim.Dbg;

/// <summary>
/// デバッガの実行状態
/// </summary>
public readonly union Status(Running, ExceptionRaised, Exited, RuntimeErrorRaised);

/// <summary>命令を実行できる</summary>
public readonly record struct Running;

/// <summary>
/// 例外が発生し, まだ例外ベクタへ移っていない.
/// </summary>
/// <param name="Code">例外の種類</param>
/// <param name="Epc">例外を起こした命令のアドレス</param>
public readonly record struct ExceptionRaised(ExcCode Code, Address Epc);

/// <summary>デバッギが終了した</summary>
public readonly record struct Exited(ExitCause Cause);

/// <summary>終了の経路</summary>
public enum ExitCause: byte {
    /// <summary>ローダの <c>$ra</c> 初期値にジャンプした</summary>
    Returned,
    /// <summary><c>runtime_call!</c> の exit</summary>
    ExitCall,
}

/// <summary>
/// ランタイムエラーが発生した.
/// </summary>
/// <param name="Error">種類</param>
/// <param name="Pc">発生した命令のアドレス</param>
public readonly record struct RuntimeErrorRaised(RuntimeError Error, Address Pc);

/// <summary>
/// ランタイムエラーの種類
/// </summary>
public readonly union RuntimeError(RuntimeErrorKind, DoubleException);

/// <summary>
/// 二重例外. カーネルモードで例外の発生条件を満たした
/// </summary>
/// <param name="Code">発生条件を満たした例外の種類</param>
public readonly record struct DoubleException(ExcCode Code);
