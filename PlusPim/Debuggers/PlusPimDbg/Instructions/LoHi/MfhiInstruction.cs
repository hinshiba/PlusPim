using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.LoHi;

internal class MfhiInstruction(RegisterID reg, int lineIndex): LoHiRegisterInstruction(reg, lineIndex) {
    public override void Execute(ExecuteContext context) {
        // undoのために保存
        this._prevRegValues.Push(context.Registers[this.Reg]);
        context.Registers[this.Reg] = context.HI;
    }

    public override void Undo(ExecuteContext context) {
        context.Registers[this.Reg] = this._prevRegValues.Pop();
    }
}

internal sealed class MfhiInstructionParser: IInstructionParser {
    public string Mnemonic => "mfhi";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;

        if(LoHiRegisterInstruction.TryParseLoHiOperands(operands, out RegisterID reg)) {
            instruction = new MfhiInstruction(reg, lineIndex);
            return true;
        }
        return false;
    }
}
