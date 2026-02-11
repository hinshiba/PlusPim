using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.RType;

internal sealed class AdduInstruction(RegisterID rd, RegisterID rs, RegisterID rt): RTypeInstruction(rd, rs, rt) {
    public override void Execute(IExecutionContext context) {
        int rsVal = this.ReadRs(context);
        int rtVal = this.ReadRt(context);
        int result = rsVal + rtVal;
        this.WriteRd(context, result);
        context.Log($"addu ${this.Rd}, ${this.Rs}, ${this.Rt}: 0x{rsVal:X8} + 0x{rtVal:X8} = 0x{result:X8}");
    }
}

internal sealed class AdduInstructionParser: IInstructionParser {
    public string Mnemonic => "addu";

    public bool TryParse(string operands, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(RTypeInstruction.TryParse3RegOperands(operands, out RegisterID rd, out RegisterID rs, out RegisterID rt)) {
            instruction = new AdduInstruction(rd, rs, rt);
            return true;
        }
        return false;
    }
}
