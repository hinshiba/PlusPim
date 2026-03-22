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
    (uint[] Registers, uint PC, uint HI, uint LO) GetRegisters();
    void Step();

    /// <summary>
    /// 1ステップ分，実行を巻き戻す
    /// </summary>
    /// <returns>巻き戻しに成功した場合は<see langword="true"/></returns>
    bool StepBack();

    /// <summary>
    /// 現在の実行前の行を取得する
    /// </summary>
    /// <returns>1から始まる行番号．取得できない場合は0を返す．</returns>
    int GetCurrentLine();
    string GetProgramPath();
    bool IsTerminated();

    /// <summary>
    /// 終了まで実行する
    /// </summary>
    void Continue();

    /// <summary>
    /// 先頭まで巻き戻す
    /// </summary>
    void ReverseContinue();

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
}
