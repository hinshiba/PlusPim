using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction;

/// <summary>
/// 命令が処理系に発生を要求する例外
/// </summary>
/// <param name="Code">例外コード</param>
/// <param name="BadVAddr">アドレス例外の実効アドレス．アドレス例外以外では <see langword="null"/></param>
internal readonly record struct ExceptionRequest(ExcCode Code, Address? BadVAddr);

/// <summary>
/// 命令の実行結果
/// </summary>
/// <param name="Exception">発生させる例外．<see langword="null"/>なら例外なし</param>
/// <param name="PcWritten">命令自身が PC を設定したか (ブランチ，ジャンプ，<c>eret</c>)</param>
/// <param name="Error">発生させるランタイムエラー．<see langword="null"/>ならランタイムエラーなし</param>
/// <remarks>
/// 例外またはランタイムエラーを要求する命令はコンテキストを一切変更せず，Undo 用の情報も積まない．
/// 例外とランタイムエラーを同時に要求することはない．
/// 例外・ランタイムエラーの適用と巻き戻しは <see cref="Processor"/> が行う
/// </remarks>
internal readonly record struct ExecuteResult(ExceptionRequest? Exception, bool PcWritten, RuntimeError? Error = null) {
    /// <summary>
    /// 例外を起こさず，PC も設定しなかった
    /// </summary>
    public static ExecuteResult Next => default;

    /// <summary>
    /// 命令自身が PC を設定した
    /// </summary>
    public static ExecuteResult PcSet => new(null, true);

    /// <summary>
    /// 例外の発生を要求する
    /// </summary>
    /// <param name="code">例外コード</param>
    /// <param name="badVAddr">アドレス例外の実効アドレス</param>
    public static ExecuteResult Raise(ExcCode code, Address? badVAddr = null) {
        return new(new ExceptionRequest(code, badVAddr), false);
    }

    /// <summary>
    /// ランタイムエラーの発生を要求する
    /// </summary>
    /// <param name="kind">種類</param>
    /// <param name="message">人間が読める説明文</param>
    public static ExecuteResult Fail(RuntimeErrorKind kind, string message) {
        return new(null, false, new RuntimeError(kind, message));
    }
}
