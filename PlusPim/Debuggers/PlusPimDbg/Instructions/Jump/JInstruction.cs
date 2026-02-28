using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Jump;

internal sealed class JInstruction(string targetLabel, int LineIndex): JumpInstruction(targetLabel, LineIndex) {
    public override void Execute(ExecuteContext context) {
        this.JumpTo(context, this.TargetLabel!);
        context.Log($"j {this.TargetLabel}");
    }

    public override void Undo(ExecuteContext context) {
        this.UndoJump(context);
    }
}

internal sealed class JInstructionParser: IInstructionParser {
    public string Mnemonic => "j";

    public bool TryParse(string operands, int LineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(JumpInstruction.TryParseLabelOperand(operands, out string? label)) {
            instruction = new JInstruction(label, LineIndex);
            return true;
        }
        return false;
    }
}
