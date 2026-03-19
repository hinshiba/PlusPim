using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.RType;

internal sealed class SllInstruction(RegisterID rd, RegisterID rt, Immediate shamt, int lineIndex): RTypeInstruction(rd, rt, shamt, lineIndex) {
    public override void Execute(RuntimeContext context) {
        int rtVal = context.Registers[this.Rt];
        // シフト量が符号に影響するような値になることはないので，Shamtは符号付整数として扱う
        int result = rtVal << this.Shamt.ToSInt();
        this.WriteRd(context, result);
        context.Log($"sll ${this.Rd}, ${this.Rt}, {this.Shamt}: 0x{rtVal:X8} << {this.Shamt} = 0x{result:X8}");
    }
}

internal sealed class SllInstructionParser: IInstructionParser {
    public string Mnemonic => "sll";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(RTypeInstruction.TryParse2RegShamtOperands(operands, out RegisterID rd, out RegisterID rt, out Immediate? shamt)) {
            // シフト量は0から31の範囲
            if(31 < shamt.ToUInt()) {
                return false;
            }
            instruction = new SllInstruction(rd, rt, shamt, lineIndex);
            return true;
        }
        return false;
    }
}
