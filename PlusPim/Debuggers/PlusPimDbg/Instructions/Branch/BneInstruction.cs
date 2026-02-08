using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Branch;

internal sealed class BneInstruction(RegisterID rs, RegisterID rt, string targetLabel): BranchInstruction(rs, rt, targetLabel) {
    protected override bool EvaluateCondition(IExecutionContext context) {
        int rsVal = this.ReadRs(context);
        int rtVal = this.ReadRt(context);
        context.Log($"bne ${this.Rs}, ${this.Rt}, {this.TargetLabel}: 0x{rsVal:X8} != 0x{rtVal:X8} ? {rsVal != rtVal}");
        return rsVal != rtVal;
    }
}

internal sealed class BneInstructionParser: IInstructionParser {
    public string Mnemonic => "bne";

    public bool TryParse(string operands, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(BranchInstruction.TryParseBranchOperands(operands, out RegisterID rs, out RegisterID rt, out string? label)) {
            instruction = new BneInstruction(rs, rt, label);
            return true;
        }
        return false;
    }
}
