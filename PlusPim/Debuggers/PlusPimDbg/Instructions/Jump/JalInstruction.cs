using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Jump;

internal sealed class JalInstruction(string targetLabel, int lineIndex): JumpInstruction(targetLabel, lineIndex) {
    private readonly Stack<int> _previousRaValues = new();

    public override void Execute(RuntimeContext context) {
        // ラベル解決を先行して例外時の影響を最小化
        Label label = context.ResolveLabelName(this.TargetLabel!)
            ?? throw new InvalidOperationException($"Label '{this.TargetLabel}' not found.");

        // $ra変更前にレジスタスナップショット等を取得
        context.PushCallStack(label);

        InstructionIndex returnPC = context.PC + 1;

        // $raにアドレス形式で次の命令アドレスを保存
        this._previousRaValues.Push(context.Registers[RegisterID.Ra]);
        context.Registers[RegisterID.Ra] = (int)Address.FromInstructionIndex(returnPC, context.IsKernelMode()).Addr;

        // ジャンプ
        this.JumpTo(context, label);
        context.Log($"jal {this.TargetLabel}: $ra = 0x{Address.FromInstructionIndex(returnPC, context.IsKernelMode()).Addr:X8}");
    }

    public override void Undo(RuntimeContext context) {
        // ジャンプを戻す
        this.UndoJump(context);

        // コールスタックを戻す
        context.UndoPushCallStack();

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
