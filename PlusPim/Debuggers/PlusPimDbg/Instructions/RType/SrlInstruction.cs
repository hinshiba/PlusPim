using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.RType;

internal sealed class SrlInstruction(RegisterID rd, RegisterID rt, Immediate shamt, int lineIndex): RTypeInstruction(rd, rt, shamt, lineIndex) {
    public override void Execute(ExecuteContext context) {
        int rtVal = context.Registers[this.Rt];
        int result = (int)((uint)rtVal >> this.Shamt.ToSInt());
        this.WriteRd(context, result);
        context.Log($"srl ${this.Rd}, ${this.Rt}, {this.Shamt}: 0x{rtVal:X8} >>> {this.Shamt} = 0x{result:X8}");
    }
}

internal sealed class SrlInstructionParser: IInstructionParser {
    public string Mnemonic => "srl";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(RTypeInstruction.TryParse2RegShamtOperands(operands, out RegisterID rd, out RegisterID rt, out Immediate? shamt)) {
            if(31 < shamt.ToUInt()) {
                return false;
            }
            instruction = new SrlInstruction(rd, rt, shamt, lineIndex);
            return true;
        }
        return false;
    }
}
