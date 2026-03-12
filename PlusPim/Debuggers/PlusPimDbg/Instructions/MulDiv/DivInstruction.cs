using PlusPim.Debuggers.PlusPimDbg.Instructions;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

internal sealed class DivInstruction(RegisterID rs, RegisterID rt, int lineIndex): MulDivInstruction(rs, rt, lineIndex) {
    public override void Execute(ExecuteContext context) {
        int rsVal = context.Registers[this.Rs];
        int rtVal = context.Registers[this.Rt];
        this.WriteHiLo(context, rsVal % rtVal, rsVal / rtVal);
    }
}

internal sealed class DivInstructionParser: IInstructionParser {
    public string Mnemonic => "div";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(MulDivInstruction.TryParseMulDivOperands(operands, out RegisterID rs, out RegisterID rt)) {
            instruction = new DivInstruction(rs, rt, lineIndex);
            return true;
        }
        return false;
    }
}
