namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

internal interface IInstruction {
    /// <summary>
    /// 命令を実行し，コンテキストを変更する
    /// </summary>
    /// <param name="context">レジスタ，メモリ状態等を示す</param>
    void Execute(IExecutionContext context);
    /// <summary>
    /// 命令の逆操作を実行し，コンテキストを元に戻す
    /// </summary>
    /// <param name="context">レジスタ，メモリ状態等を示す</param>
    void Undo(IExecutionContext context);
}
