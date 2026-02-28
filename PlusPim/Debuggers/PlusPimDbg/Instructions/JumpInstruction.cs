using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

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
    public int SourceLine => sourceLine;

    /// <summary>
    /// Undo用に前のPCをスタックで管理
    /// </summary>
    private readonly Stack<InstructionIndex> _previousPCs = new();

    public abstract void Execute(ExecuteContext context);
    public abstract void Undo(ExecuteContext context);

    /// <summary>
    /// ラベル名からExecutionIndexを解決してジャンプする
    /// </summary>
    protected void JumpTo(ExecuteContext context, string name) {
        Label label = context.ResolveLabelName(name) ?? throw new InvalidOperationException($"Label '{name}' not found.");
        this._previousPCs.Push(context.PC);
        // todo アライメント例外の処理
        context.PC = (InstructionIndex)InstructionIndex.FromAddress(label.Addr);
    }

    /// <summary>
    /// ProgramCounterを直接指定してジャンプする
    /// </summary>
    protected void JumpTo(ExecuteContext context, InstructionIndex target) {
        this._previousPCs.Push(context.PC);
        context.PC = target;
    }

    /// <summary>
    /// ジャンプを元に戻す
    /// </summary>
    protected void UndoJump(ExecuteContext context) {
        if(this._previousPCs.Count == 0) {
            throw new InvalidOperationException("No previous PC to undo.");
        }
        context.PC = this._previousPCs.Pop();
    }

    /// <summary>
    /// オペランドからラベル名を解析する
    /// </summary>
    internal static bool TryParseLabelOperand(string operands, [MaybeNullWhen(false)] out string label) {
        string trimmed = operands.Trim();
        if(string.IsNullOrEmpty(trimmed) || trimmed.Contains(' ') || trimmed.Contains(',')) {
            label = null;
            return false;
        }
        label = trimmed;
        return true;
    }
}
