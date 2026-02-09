using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Jump;

internal sealed class JalInstruction(string targetLabel): JumpInstruction(targetLabel) {
    private readonly Stack<int> _previousRaValues = new();

    public override void Execute(IExecutionContext context) {
        // レジスタスナップショット取得（$ra変更前）
        int[] snapshot = (int[])context.Registers.Clone();
        string callerLabel = context.GetLabelForExecutionIndex(context.ExecutionIndex) ?? "<unknown>";
        CallStackFrame frame = new(context.ExecutionIndex + 1, callerLabel, snapshot, context.HI, context.LO);

        // $ra にPC アドレス形式で次の命令アドレスを保存
        this._previousRaValues.Push(context.Registers[(int)RegisterID.Ra]);
        int returnAddress = context.ExecutionIndex + 1 + ExecuteContext.TextSegmentBase;
        context.Registers[(int)RegisterID.Ra] = returnAddress;

        // コールスタックに push
        context.CallStack.Push(frame);

        // ジャンプ
        this.JumpTo(context, this.TargetLabel!);
        context.Log($"jal {this.TargetLabel}: $ra = 0x{returnAddress:X8}");
    }

    public override void Undo(IExecutionContext context) {
        // ジャンプを戻す
        this.UndoJump(context);

        // コールスタックから pop
        _ = context.CallStack.Pop();

        // $ra を復元
        if(this._previousRaValues.Count == 0) {
            throw new InvalidOperationException("No previous $ra value to undo.");
        }
        context.Registers[(int)RegisterID.Ra] = this._previousRaValues.Pop();
    }
}

internal sealed class JalInstructionParser: IInstructionParser {
    public string Mnemonic => "jal";

    public bool TryParse(string operands, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(JumpInstruction.TryParseLabelOperand(operands, out string? label)) {
            instruction = new JalInstruction(label);
            return true;
        }
        return false;
    }
}
