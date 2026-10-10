using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions.Jump;

internal sealed class JInstruction(string targetLabel, int lineNumber): JumpInstruction(targetLabel, lineNumber) {
    public override ExecuteResult Execute(RuntimeContext context) {
        this.JumpTo(context, this.TargetLabel!);
        context.Log($"j {this.TargetLabel}");
        return ExecuteResult.PcSet;
    }

    public override void Undo(RuntimeContext context) {
        this.UndoJump(context);
    }
}

internal sealed class JInstructionParser: IInstructionParser {
    public string Mnemonic => "j";

    public bool TryParse(string operands, int lineNumber, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(OperandParser.TryParseLabelOperand(operands, out string? label)) {
            instruction = new JInstruction(label, lineNumber);
            return true;
        }
        return false;
    }
}
