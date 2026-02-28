using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Jump;

internal sealed class JalInstruction(string targetLabel, int lineIndex): JumpInstruction(targetLabel, lineIndex) {
    private readonly Stack<int> _previousRaValues = new();

    public override void Execute(ExecuteContext context) {
        // ラベル解決を先行して例外時の影響を最小化
        _ = context.ResolveLabelName(this.TargetLabel!)
            ?? throw new InvalidOperationException($"Label '{this.TargetLabel}' not found.");


        // レジスタスナップショット取得（$ra変更前）
        _ = context.Registers.Clone();
        // string callerLabel = context.GetLabelForExecutionIndex(context.PC.Index) ?? "<unknown>";
        InstructionIndex returnPC = context.PC + 1;
        // CallStackFrame frame = new(returnPC, callerLabel, snapshot, context.HI, context.LO);

        // $ra にPC アドレス形式で次の命令アドレスを保存
        this._previousRaValues.Push(context.Registers[RegisterID.Ra]);
        context.Registers[RegisterID.Ra] = Address.FromInstructionIndex(returnPC).Addr;

        // コールスタックに push
        //context.CallStack.Push(frame);

        // ジャンプ
        this.JumpTo(context, this.TargetLabel!);
        context.Log($"jal {this.TargetLabel}: $ra = 0x{Address.FromInstructionIndex(returnPC).Addr:X8}");
    }

    public override void Undo(ExecuteContext context) {
        // ジャンプを戻す
        this.UndoJump(context);

        // コールスタックから pop
        _ = context.CallStack.Pop();

        // $ra を復元
        if(this._previousRaValues.Count == 0) {
            throw new InvalidOperationException("No previous $ra value to undo.");
        }
        context.Registers[RegisterID.Ra] = this._previousRaValues.Pop();
    }
}

internal sealed class JalInstructionParser: IInstructionParser {
    public string Mnemonic => "jal";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(JumpInstruction.TryParseLabelOperand(operands, out string? label)) {
            instruction = new JalInstruction(label, lineIndex);
            return true;
        }
        return false;
    }
}
