namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// MIPSの例外コードを表す列挙型
/// 公式の略称を用いること
/// </summary>
internal enum ExcCode {
    /// <summary>
    /// Loadアドレスエラー
    /// </summary>
    AdEL = 4,

    /// <summary>
    /// Storeアドレスエラー
    /// </summary>
    AdES = 5,

    /// <summary>
    /// syscall
    /// </summary>
    Sys = 8,

    /// <summary>
    /// break
    /// </summary>
    Bp = 9,

    /// <summary>
    /// 予約命令
    /// </summary>
    RI = 10,

    /// <summary>
    /// コプロセッサ例外
    /// </summary>
    CpU = 11,

    /// <summary>
    /// 算術オーバーフロー
    /// </summary>
    Ov = 12,
}
