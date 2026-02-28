using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.RType;

internal sealed class SubInstruction(RegisterID rd, RegisterID rs, RegisterID rt, int LineIndex): RTypeInstruction(rd, rs, rt, LineIndex) {
    public override void Execute(ExecuteContext context) {
        int rsVal = context.Registers[this.Rs];
        int rtVal = context.Registers[this.Rt];
        int result = rsVal - rtVal;
        this.WriteRd(context, result);
        context.Log($"sub ${this.Rd}, ${this.Rs}, ${this.Rt}: 0x{rsVal:X8} - 0x{rtVal:X8} = 0x{result:X8}");
    }
}

internal sealed class SubInstructionParser: IInstructionParser {
    public string Mnemonic => "sub";

    public bool TryParse(string operands, int LineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(RTypeInstruction.TryParse3RegOperands(operands, out RegisterID rd, out RegisterID rs, out RegisterID rt)) {
            instruction = new SubInstruction(rd, rs, rt, LineIndex);
            return true;
        }
        return false;
    }
}
