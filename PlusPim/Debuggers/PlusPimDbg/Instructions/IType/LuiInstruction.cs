using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.IType;

// rt = imm << 16

internal class LuiInstruction(RegisterID rt, Immediate imm, int lineIndex): ITypeInstruction(rt, RegisterID.Zero, imm, lineIndex) {
    public override void Execute(ExecuteContext context) {
        this.WriteRt(context, unchecked((int)(this.Imm.ToUInt() << 16)));
    }
}

internal sealed partial class LuiInstructionParser: IInstructionParser {
    public string Mnemonic => "lui";

    [GeneratedRegex(@"^\$(?<rt>\w+),\s*(?<imm>\S+)$")]
    private static partial Regex OperandsLuiPattern();

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        Match match = OperandsLuiPattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(Enum.TryParse<RegisterID>(match.Groups["rt"].Value, true, out RegisterID rtParsed)
            && Immediate.TryParse(match.Groups["imm"].Value, null, out Immediate? immParsed)) {
            RegisterID rt = rtParsed;
            Immediate imm = immParsed;
            instruction = new LuiInstruction(rt, imm, lineIndex);
            return true;
        }
        return false;
    }
}
