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
    /// 次に実行する命令にブレークポイントがあり，かつ例外による一時停止状態でない
    /// </summary>
    bool IsAtBreakpoint { get; }

    /// <summary>
    /// コールスタックの情報を取得する
    /// </summary>
    /// <returns><see cref="StackFrameInfo"/>の配列．ライブフレームが先頭である</returns>
    StackFrameInfo[] GetCallStack();

    /// <summary>
    /// コールスタックのフレーム数
    /// </summary>
    int CallStackDepth { get; }

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

    /// <summary>
    /// ブレークポイントを設定する
    /// </summary>
    /// <param name="file">ファイル</param>
    /// <param name="lines">1-indexedの行番号の配列</param>
    /// <returns>各行に対応するブレークポイント設定結果</returns>
    BreakpointResult[] SetBreakpoints(FileInfo file, int[] lines);

    /// <summary>
    /// メモリの内容を返す．書き込まれていない領域は0である
    /// </summary>
    /// <param name="address">先頭のアドレス</param>
    /// <param name="count">バイト数</param>
    /// <returns>読んだバイト列</returns>
    byte[] ReadMemory(uint address, int count);

    /// <summary>
    /// データセグメントの先頭のアドレスとバイト数
    /// </summary>
    (uint Start, uint Size) DataSegmentRange { get; }

    /// <summary>
    /// ファイルの疑似命令の行と展開先の命令を返す
    /// </summary>
    /// <param name="file">ソースファイル</param>
    /// <returns>行の順の配列．読み込んでいないファイルなら空</returns>
    PseudoExpansionInfo[] GetPseudoExpansions(FileInfo file);
}
