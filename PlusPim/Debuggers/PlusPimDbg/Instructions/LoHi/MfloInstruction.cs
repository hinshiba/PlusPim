using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.LoHi;

internal class MfloInstruction(RegisterID reg, int lineIndex): LoHiRegisterInstruction(reg, lineIndex) {
    public override void Execute(ExecuteContext context) {
        // undoのために保存
        this._prevRegValues.Push(context.Registers[this.Reg]);
        context.Registers[this.Reg] = context.LO;
    }

    public override void Undo(ExecuteContext context) {
        context.Registers[this.Reg] = this._prevRegValues.Pop();
    }
}

internal sealed class MfloInstructionParser: IInstructionParser {
    public string Mnemonic => "mflo";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;

        if(LoHiRegisterInstruction.TryParseLoHiOperands(operands, out RegisterID reg)) {
            instruction = new MfloInstruction(reg, lineIndex);
            return true;
        }
        return false;
    }
}
