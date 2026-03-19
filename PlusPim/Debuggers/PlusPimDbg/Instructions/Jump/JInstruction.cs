using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Jump;

internal sealed class JInstruction(string targetLabel, int lineIndex): JumpInstruction(targetLabel, lineIndex) {
    public override void Execute(RuntimeContext context) {
        this.JumpTo(context, this.TargetLabel!);
        context.Log($"j {this.TargetLabel}");
    }

    public override void Undo(RuntimeContext context) {
        this.UndoJump(context);
    }
}

internal sealed class JInstructionParser: IInstructionParser {
    public string Mnemonic => "j";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(JumpInstruction.TryParseLabelOperand(operands, out string? label)) {
            instruction = new JInstruction(label, lineIndex);
            return true;
        }
        return false;
    }
}
