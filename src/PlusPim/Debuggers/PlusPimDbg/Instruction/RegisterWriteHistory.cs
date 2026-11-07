using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction;

/// <summary>
/// 1つのレジスタへの書き込みと，その逆操作のための以前の値の履歴を管理する
/// </summary>
/// <remarks>
/// 1つの命令インスタンスはループ内で複数回実行される可能性があるためスタックで管理する．
/// <see cref="Write"/>は命令の実行が成功した場合にのみ呼び出すこと．
/// </remarks>
/// <param name="register">書き込み先のレジスタ</param>
internal sealed class RegisterWriteHistory(RegisterID register) {
    private readonly Stack<uint> _previousValues = new();

    /// <summary>
    /// 現在の値を保存してからレジスタに値を書き込む
    /// </summary>
    /// <param name="context">レジスタを含むコンテキスト</param>
    /// <param name="value">書き込む値</param>
    public void Write(RuntimeContext context, uint value) {
        this._previousValues.Push(context.Registers[register]);
        context.Registers[register] = value;
    }

    /// <summary>
    /// 最後に保存した値をレジスタに戻す
    /// </summary>
    /// <param name="context">レジスタを含むコンテキスト</param>
    /// <exception cref="InvalidOperationException">保存された値がない場合</exception>
    public void Undo(RuntimeContext context) {
        if(this._previousValues.Count == 0) {
            throw new InvalidOperationException($"No previous value of ${register} to undo.");
        }
        context.Registers[register] = this._previousValues.Pop();
    }
}
