using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Branch;

internal sealed class BneInstruction(RegisterID rs, RegisterID rt, string targetLabel, int lineIndex): BranchInstruction(rs, rt, targetLabel, lineIndex) {
    protected override bool EvaluateCondition(RuntimeContext context) {
        int rsVal = context.Registers[this.Rs];
        int rtVal = context.Registers[this.Rt];
        context.Log($"bne ${this.Rs}, ${this.Rt}, {this.TargetLabel}: 0x{rsVal:X8} != 0x{rtVal:X8} ? {rsVal != rtVal}");
        return rsVal != rtVal;
    }
}

internal sealed class BneInstructionParser: IInstructionParser {
    public string Mnemonic => "bne";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(BranchInstruction.TryParseBranchOperands(operands, out RegisterID rs, out RegisterID rt, out string? label)) {
            instruction = new BneInstruction(rs, rt, label, lineIndex);
            return true;
        }
        return false;
    }
}
