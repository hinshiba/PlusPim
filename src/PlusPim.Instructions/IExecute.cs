namespace PlusPim.Instructions;

/// <summary>
/// 命令の実行の抽象化
/// </summary>
public interface IExecute {

    /// <summary>
    /// 宣言順の読み込むレジスタ番号
    /// </summary>
    ReadOnlySpan<RegisterId> Src { get; }

    /// <summary>
    /// 宣言順の書き込むレジスタ番号
    /// </summary>
    ReadOnlySpan<RegisterId> Dest { get; }

    /// <summary>
    /// 値を評価してランタイムへの副作用を返す
    /// </summary>
    Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest);

    /// <summary>
    /// その命令のファイル上での行番号(1-index)
    /// </summary>
    int SourceLine { get; }
}
