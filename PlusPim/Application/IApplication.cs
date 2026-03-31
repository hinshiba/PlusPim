namespace PlusPim.Application;

/// <summary>
/// アプリケーションの抽象化
/// </summary>
internal interface IApplication {
    /// <summary>
    /// プログラムを読み込んで起動する
    /// </summary>
    /// <returns>成功した場合は<see langword="true"/></returns>
    bool Load();

    [Obsolete("代わりにGetCallStackを使用してください")]
    (uint[] Registers, uint PC, uint HI, uint LO) GetRegisters();

    /// <summary>
    /// 表示されている次の行まで実行する
    /// </summary>
    StopReason StepOver();

    /// <summary>
    /// 次の命令を実行する
    /// </summary>
    StopReason StepIn();

    /// <summary>
    /// スタックフレームを1つ除去するまで実行する
    /// </summary>
    StopReason StepOut();

    /// <summary>
    /// 停止するまで実行する
    /// </summary>
    StopReason Continue();

    /// <summary>
    /// 1ステップ分，実行を巻き戻す
    /// </summary>
    /// <returns>巻き戻しに成功した場合は<see langword="true"/></returns>
    bool StepBack();

    /// <summary>
    /// 停止するまで実行を巻き戻す
    /// </summary>
    /// <returns>1ステップ以上巻き戻しに成功した場合は<see langword="true"/></returns>
    bool ReverseContinue();

    /// <summary>
    /// 現在の実行前の行を取得する
    /// </summary>
    /// <returns>1から始まる行番号．取得できない場合は0を返す．</returns>
    [Obsolete("代わりにGetCallStackを使用してください")]
    int GetCurrentLine();

    [Obsolete("代わりにGetCallStackを使用してください")]
    string GetProgramPath();

    [Obsolete("代わりにそれぞれの実行命令のStopReasonを参照してください")]
    bool IsTerminated();

    /// <summary>
    /// コールスタックの情報を取得する
    /// </summary>
    /// <returns><see cref="StackFrameInfo"/>の配列．ライブフレームが先頭である</returns>
    StackFrameInfo[] GetCallStack();

    /// <summary>
    /// フレームIDからスタックフレーム情報を取得する
    /// </summary>
    /// <param name="frameId">フレームID</param>
    /// <returns>見つかった場合は<see cref="StackFrameInfo"/>，見つからない場合はnull</returns>
    StackFrameInfo? GetStackFrame(int frameId);

    /// <summary>
    /// 直前のStepで発生した例外情報を取得する
    /// </summary>
    /// <returns>例外情報．例外が発生していない場合はnull</returns>
    ExceptionInfo? GetLastException();
}
