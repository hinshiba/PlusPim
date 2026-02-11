using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.RType;

internal sealed class SllInstruction(RegisterID rd, RegisterID rt, Immediate shamt): RTypeInstruction(rd, rt, shamt) {
    public override void Execute(IExecutionContext context) {
        int rtVal = this.ReadRt(context);
        int result = rtVal << this.Shamt;
        this.WriteRd(context, result);
        context.Log($"sll ${this.Rd}, ${this.Rt}, {this.Shamt.Value}: 0x{rtVal:X8} << {this.Shamt.Value} = 0x{result:X8}");
    }
}

internal sealed class SllInstructionParser: IInstructionParser {
    public string Mnemonic => "sll";

    public bool TryParse(string operands, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(RTypeInstruction.TryParse2RegShamtOperands(operands, out RegisterID rd, out RegisterID rt, out Immediate? shamt)) {
            // シフト量は0から31の範囲
            if(31 < shamt) {
                return false;
            }
            instruction = new SllInstruction(rd, rt, shamt);
            return true;
        }
        return false;
    }
}
