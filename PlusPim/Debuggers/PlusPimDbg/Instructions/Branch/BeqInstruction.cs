using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Branch;

internal sealed class BeqInstruction(RegisterID rs, RegisterID rt, string targetLabel): BranchInstruction(rs, rt, targetLabel) {
    protected override bool EvaluateCondition(ExecuteContext context) {
        int rsVal = context.Registers[this.Rs];
        int rtVal = context.Registers[this.Rt];
        context.Log($"beq ${this.Rs}, ${this.Rt}, {this.TargetLabel}: 0x{rsVal:X8} == 0x{rtVal:X8} ? {rsVal == rtVal}");
        return rsVal == rtVal;
    }
}

internal sealed class BeqInstructionParser: IInstructionParser {
    public string Mnemonic => "beq";

    public bool TryParse(string operands, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(BranchInstruction.TryParseBranchOperands(operands, out RegisterID rs, out RegisterID rt, out string? label)) {
            instruction = new BeqInstruction(rs, rt, label);
            return true;
        }
        return false;
    }
}
