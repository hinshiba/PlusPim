namespace PlusPim.Terminal;

/// <summary>
/// 端末の符号化をUTF-8へ切り替える実装のインターフェース
/// </summary>
internal interface ITerminalUtf8Backend {
    /// <summary>
    /// 切り替えを試みる
    /// </summary>
    /// <returns>実際に何かを変更したら true</returns>
    bool TrySwitchToUtf8();

    /// <summary>
    /// 変更を元へ戻す
    /// </summary>
    void Restore();

    /// <summary>
    /// プロセスが制御イベント等で終了させられるときに handler を呼ぶ仕組みを登録する．対応するOSのみ
    /// </summary>
    void RegisterTerminationHook(Action handler);
}
