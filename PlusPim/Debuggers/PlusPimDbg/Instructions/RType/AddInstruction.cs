using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.RType;

internal sealed class AddInstruction(RegisterID rd, RegisterID rs, RegisterID rt, int lineIndex): RTypeInstruction(rd, rs, rt, lineIndex) {
    public override void Execute(RuntimeContext context) {
        int rsVal = context.Registers[this.Rs];
        int rtVal = context.Registers[this.Rt];
        // TODO: オーバーフロー例外の発生の考慮
        int result = rsVal + rtVal;
        this.WriteRd(context, result);
        context.Log($"add ${this.Rd}, ${this.Rs}, ${this.Rt}: 0x{rsVal:X8} + 0x{rtVal:X8} = 0x{result:X8}");
    }
}

internal sealed class AddInstructionParser: IInstructionParser {
    public string Mnemonic => "add";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(RTypeInstruction.TryParse3RegOperands(operands, out RegisterID rd, out RegisterID rs, out RegisterID rt)) {
            instruction = new AddInstruction(rd, rs, rt, lineIndex);
            return true;
        }
        return false;
    }
}
