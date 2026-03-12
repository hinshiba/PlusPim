using PlusPim.Debuggers.PlusPimDbg.Instructions;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

internal sealed class MultInstruction(RegisterID rs, RegisterID rt, int lineIndex): MulDivInstruction(rs, rt, lineIndex) {
    public override void Execute(ExecuteContext context) {
        int rsVal = context.Registers[this.Rs];
        int rtVal = context.Registers[this.Rt];
        long result = (long)rsVal * rtVal;
        this.WriteHiLo(context, (int)(result >> 32), (int)(result & 0xFFFFFFFF));
    }
}

internal sealed class MultInstructionParser: IInstructionParser {
    public string Mnemonic => "mult";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(MulDivInstruction.TryParseMulDivOperands(operands, out RegisterID rs, out RegisterID rt)) {
            instruction = new MultInstruction(rs, rt, lineIndex);
            return true;
        }
        return false;
    }
}
