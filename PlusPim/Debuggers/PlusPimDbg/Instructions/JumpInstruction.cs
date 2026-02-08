using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

/// <summary>
/// MIPSにおいてジャンプ命令を表す抽象基底クラス
/// </summary>
/// <remarks>すべて無条件ジャンプであることが前提であり，この派生クラスはPCの自動インクリメントは行われない</remarks>
internal abstract class JumpInstruction(string? targetLabel): IInstruction {
    /// <summary>
    /// ジャンプ先のラベル名
    /// jr等ラベルを使わない命令では null
    /// </summary>
    protected string? TargetLabel { get; } = targetLabel;

    /// <summary>
    /// Undo用に前のExecutionIndexをスタックで管理
    /// </summary>
    private readonly Stack<int> _previousExecutionIndices = new();

    public abstract void Execute(IExecutionContext context);
    public abstract void Undo(IExecutionContext context);

    /// <summary>
    /// ラベル名からExecutionIndexを解決してジャンプする
    /// </summary>
    protected void JumpTo(IExecutionContext context, string label) {
        int? address = context.GetLabelAddress(label) ?? throw new InvalidOperationException($"Label '{label}' not found.");
        this._previousExecutionIndices.Push(context.ExecutionIndex);
        context.ExecutionIndex = address.Value;
    }

    /// <summary>
    /// ExecutionIndexを直接指定してジャンプする
    /// </summary>
    protected void JumpTo(IExecutionContext context, int executionIndex) {
        this._previousExecutionIndices.Push(context.ExecutionIndex);
        context.ExecutionIndex = executionIndex;
    }

    /// <summary>
    /// ジャンプを元に戻す
    /// </summary>
    protected void UndoJump(IExecutionContext context) {
        if(this._previousExecutionIndices.Count == 0) {
            throw new InvalidOperationException("No previous ExecutionIndex to undo.");
        }
        context.ExecutionIndex = this._previousExecutionIndices.Pop();
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
