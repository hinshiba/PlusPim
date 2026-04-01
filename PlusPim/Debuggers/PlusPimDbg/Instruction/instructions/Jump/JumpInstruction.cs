using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Label = PlusPim.Debuggers.PlusPimDbg.Program.records.Label;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Jump;

/// <summary>
/// MIPSにおいてジャンプ命令を表す抽象基底クラス
/// </summary>
/// <remarks>すべて無条件ジャンプであることが前提であり，この派生クラスはPCの自動インクリメントは行われない</remarks>
internal abstract class JumpInstruction(string? targetLabel, int sourceLine): IInstruction {
    /// <summary>
    /// ジャンプ先のラベル名
    /// jr等ラベルを使わない命令では null
    /// </summary>
    protected string? TargetLabel { get; } = targetLabel;

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = sourceLine;

    /// <summary>
    /// Undo用に前のPCをスタックで管理
    /// </summary>
    private readonly Stack<Address> _previousPCs = new();

    public abstract void Execute(RuntimeContext context);
    public abstract void Undo(RuntimeContext context);

    /// <summary>
    /// ラベル名からアドレスを解決してジャンプする
    /// </summary>
    protected void JumpTo(RuntimeContext context, string name) {
        Label? label = context.ResolveLabelName(name);
        this.JumpTo(context, label?.Addr ?? Address.InValid);
    }

    /// <summary>
    /// ProgramCounterを直接指定してジャンプする
    /// </summary>
    protected void JumpTo(RuntimeContext context, Address target) {
        this._previousPCs.Push(context.PC);
        context.PC = target;
    }

    /// <summary>
    /// ジャンプを元に戻す
    /// </summary>
    protected void UndoJump(RuntimeContext context) {
        if(this._previousPCs.Count == 0) {
            throw new InvalidOperationException("No previous PC to undo.");
        }
        context.PC = this._previousPCs.Pop();
    }

}
