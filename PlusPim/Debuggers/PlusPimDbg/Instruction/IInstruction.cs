using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction;

/// <summary>
/// 命令を意味するインターフェース
/// </summary>
internal interface IInstruction {
    /// <summary>
    /// 命令を実行し，コンテキストを変更する
    /// </summary>
    /// <param name="context">レジスタ，メモリ状態等を示す</param>
    void Execute(RuntimeContext context);

    /// <summary>
    /// 命令の逆操作を実行し，コンテキストを元に戻す
    /// </summary>
    /// <param name="context">レジスタ，メモリ状態等を示す</param>
    void Undo(RuntimeContext context);

    /// <summary>
    /// その命令のファイル上での行番号(1-index)
    /// </summary>
    int SourceLine { get; }
}
