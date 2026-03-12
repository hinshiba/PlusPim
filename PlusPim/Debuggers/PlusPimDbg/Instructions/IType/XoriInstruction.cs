using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.IType;

internal sealed class XoriInstruction(RegisterID rt, RegisterID rs, Immediate imm, int lineIndex): ITypeInstruction(rt, rs, imm, lineIndex) {
    public override void Execute(ExecuteContext context) {
        int rsVal = context.Registers[this.Rs];
        int result = (int)((uint)rsVal ^ this.Imm.ToUInt());
        this.WriteRt(context, result);
        context.Log($"xori ${this.Rt}, ${this.Rs}, {this.Imm}: 0x{rsVal:X8} ^ {this.Imm} = 0x{result:X8}");
    }
}

internal sealed class XoriInstructionParser: IInstructionParser {
    public string Mnemonic => "xori";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(ITypeInstruction.TryParseITypeOperands(operands, out RegisterID rt, out RegisterID rs, out Immediate? imm)) {
            instruction = new XoriInstruction(rt, rs, imm, lineIndex);
            return true;
        }
        return false;
    }
}
