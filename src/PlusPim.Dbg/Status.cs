using PlusPim.Instructions;

namespace PlusPim.Dbg;

/// <summary>
/// デバッガの実行状態
/// </summary>
public readonly union Status(Running, Trapped, Exited, DoubleFaulted, Faulted);

/// <summary>命令を実行できる</summary>
public readonly record struct Running;

/// <summary>
/// 例外が発生した. 
/// </summary>
/// <param name="Code">例外の種類</param>
/// <param name="Epc">例外を起こした命令のアドレス</param>
public readonly record struct Trapped(ExcCode Code, Address Epc);

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
/// カーネルモードで例外が発生した.
/// </summary>
/// <param name="Code">例外の種類</param>
/// <param name="Pc">例外を起こした命令のアドレス</param>
public readonly record struct DoubleFaulted(ExcCode Code, Address Pc);

/// <summary>
/// ランタイムエラーが発生した.
/// </summary>
/// <param name="Kind">種類</param>
/// <param name="Pc">発生した命令のアドレス</param>
public readonly record struct Faulted(RuntimeErrorKind Kind, Address Pc);
