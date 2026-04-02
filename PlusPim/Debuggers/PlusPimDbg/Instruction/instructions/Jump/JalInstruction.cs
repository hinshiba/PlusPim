using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Jump;

internal sealed class JalInstruction(string targetLabel, int lineIndex): JumpInstruction(targetLabel, lineIndex) {
    private readonly Stack<uint> _previousRaValues = new();

    public override void Execute(RuntimeContext context) {
        // ラベル解決を先行して例外時の影響を最小化
        Label label = context.ResolveLabelName(this.TargetLabel!) ?? Label.Invalid;

        // $ra変更前にレジスタスナップショット等を取得
        context.PushCallStack(label);

        Address returnPC = context.PC + 4;

        // $raにアドレス形式で次の命令アドレスを保存
        this._previousRaValues.Push(context.Registers[RegisterID.Ra]);
        context.Registers[RegisterID.Ra] = returnPC.Addr;

        // ジャンプ
        this.JumpTo(context, label.Addr);
        context.Log($"jal {this.TargetLabel}: $ra = {returnPC}");
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
        if(OperandParser.TryParseLabelOperand(operands, out string? label)) {
            instruction = new JalInstruction(label, lineIndex);
            return true;
        }
        return false;
    }
}
