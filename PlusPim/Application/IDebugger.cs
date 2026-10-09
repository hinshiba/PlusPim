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
    /// 次に実行する命令にブレークポイントがあり，例外が保留されていないかどうか
    /// </summary>
    /// <remarks>
    /// 順方向の実行がブレークポイントで止まる状態 (命令の実行前) と同じときに<see langword="true"/>となる．
    /// 例外ハンドラへの遷移を戻した直後は，例外を起こした命令は実行済みなので<see langword="false"/>となる
    /// </remarks>
    bool IsAtBreakpoint { get; }

    /// <summary>
    /// コールスタックの情報を取得する
    /// </summary>
    /// <returns><see cref="StackFrameInfo"/>の配列．ライブフレームが先頭である</returns>
    StackFrameInfo[] GetCallStack();

    /// <summary>
    /// コールスタックのフレーム数 (<see cref="GetCallStack"/>の長さ)
    /// </summary>
    /// <remarks>フレームの情報を作らないので，ステップごとに呼んでも軽い</remarks>
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
    /// メモリを読む．書き込まれていないアドレスは0である
    /// </summary>
    /// <param name="address">先頭のアドレス</param>
    /// <param name="count">バイト数</param>
    /// <returns>読んだバイト列．アドレス空間の末尾 (<c>0xFFFFFFFF</c>) を超える分は含まない</returns>
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
