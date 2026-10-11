namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// アセンブルに失敗したことを表す例外
/// </summary>
/// <param name="errors">すべてのファイルで見つかったエラーのメッセージ</param>
internal sealed class AssemblyException(IReadOnlyList<string> errors)
    : Exception($"Assembly failed with {errors.Count} error(s):{Environment.NewLine}{string.Join(Environment.NewLine, errors)}") {
    /// <summary>
    /// エラーのメッセージ
    /// </summary>
    public IReadOnlyList<string> Errors { get; } = errors;
}
