using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.RType;

internal sealed class SraInstruction(RegisterID rd, RegisterID rt, Immediate shamt, int lineIndex): RTypeInstruction(rd, rt, shamt, lineIndex) {
    public override void Execute(ExecuteContext context) {
        int rtVal = context.Registers[this.Rt];
        int result = rtVal >> this.Shamt;
        this.WriteRd(context, result);
        context.Log($"sra ${this.Rd}, ${this.Rt}, {this.Shamt.Value}: 0x{rtVal:X8} >> {this.Shamt.Value} = 0x{result:X8}");
    }
}

internal sealed class SraInstructionParser: IInstructionParser {
    public string Mnemonic => "sra";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(RTypeInstruction.TryParse2RegShamtOperands(operands, out RegisterID rd, out RegisterID rt, out Immediate? shamt)) {
            if(31 < shamt) {
                return false;
            }
            instruction = new SraInstruction(rd, rt, shamt, lineIndex);
            return true;
        }
        return false;
    }
}
