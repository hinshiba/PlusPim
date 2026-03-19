using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.RType;

internal sealed class SllvInstruction(RegisterID rd, RegisterID rs, RegisterID rt, int lineIndex): RTypeInstruction(rd, rs, rt, lineIndex) {
    public override void Execute(RuntimeContext context) {
        int rsVal = context.Registers[this.Rs];
        int rtVal = context.Registers[this.Rt];
        int result = rtVal << (rsVal & 0x1F);
        this.WriteRd(context, result);
        context.Log($"sllv ${this.Rd}, ${this.Rt}, ${this.Rs}: 0x{rtVal:X8} << {rsVal & 0x1F} = 0x{result:X8}");
    }
}

internal sealed class SllvInstructionParser: IInstructionParser {
    public string Mnemonic => "sllv";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        // 構文: sllv $rd, $rt, $rs だが3-regパターンは $rd, $rs, $rt の順で解析
        // rs/rtをスワップして渡す
        if(RTypeInstruction.TryParse3RegOperands(operands, out RegisterID rd, out RegisterID parsedRs, out RegisterID parsedRt)) {
            instruction = new SllvInstruction(rd, parsedRt, parsedRs, lineIndex);
            return true;
        }
        return false;
    }
}
