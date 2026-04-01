namespace PlusPim.Application;

/// <summary>
/// デバッガ本体とのインターフェース
/// </summary>
public interface IDebugger {

    /// <summary>
    /// 1ステップ実行する
    /// </summary>
    /// <returns>停止した理由</returns>
    StopReason Step();

    /// <summary>
    /// 1ステップ分，実行を巻き戻す
    /// </summary>
    /// <returns>巻き戻しに成功した場合は<see langword="true"/></returns>
    bool Back();

    /// <summary>
    /// コールスタックの情報を取得する
    /// </summary>
    /// <returns><see cref="StackFrameInfo"/>の配列．ライブフレームが先頭である</returns>
    StackFrameInfo[] GetCallStack();

    /// <summary>
    /// 直前のStepで発生した例外情報を取得する
    /// </summary>
    /// <returns>例外情報．例外が発生していない場合はnull</returns>
    ExceptionInfo? GetLastException();
}
