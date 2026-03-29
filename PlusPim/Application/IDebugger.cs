namespace PlusPim.Application;

internal interface IDebugger {
    (uint[] Registers, uint PC, uint HI, uint LO) GetRegisters();
    void Step();

    /// <summary>
    /// 1ステップ分，実行を巻き戻す
    /// </summary>
    /// <returns>巻き戻しに成功した場合は<see langword="true"/></returns>
    bool StepBack();

    /// <summary>
    /// 1から始まる現在実行前の行番号を取得する
    /// </summary>
    /// <returns>行番号．ただし何かしらの問題で無効であった場合は0</returns>
    int GetCurrentLine();
    string GetProgramPath();
    bool IsTerminated();

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
