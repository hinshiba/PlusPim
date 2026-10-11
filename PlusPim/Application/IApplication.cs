namespace PlusPim.Application;

/// <summary>
/// アプリケーションの抽象化
/// </summary>
internal interface IApplication {
    /// <summary>
    /// プログラムを読み込んで起動する
    /// </summary>
    /// <returns>成功した場合は<see langword="true"/></returns>
    /// <exception cref="Debuggers.PlusPimDbg.Program.AssemblyException">アセンブルに失敗した場合</exception>
    bool Load();

    /// <summary>
    /// プログラムを読み込み済みかどうか
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// スタックフレームを1つ除去するまで実行する
    /// </summary>
    StopReason StepOut();

    /// <summary>
    /// 表示されている次の行まで実行する
    /// </summary>
    StopReason StepOver();

    /// <summary>
    /// 次の命令を実行する
    /// </summary>
    StopReason StepIn();

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
    /// 停止する例外を設定する
    /// </summary>
    /// <param name="filters">例外フィルタ</param>
    void SetExceptionFilters(List<ExceptionFilter> filters);

    /// <summary>
    /// ブレークポイントを設定する
    /// </summary>
    /// <param name="file">ソースファイル</param>
    /// <param name="lines">1-indexedの行番号の配列</param>
    /// <returns>各行に対応するブレークポイント設定結果</returns>
    BreakpointResult[] SetBreakpoints(FileInfo file, int[] lines);


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

    /// <summary>
    /// 発生しているランタイムエラーの情報を取得する
    /// </summary>
    /// <returns>ランタイムエラーの情報．発生していない場合はnull</returns>
    RuntimeErrorInfo? GetRuntimeError();
}
