using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Mem;

internal sealed class SwInstruction(RegisterID rt, RegisterID rs, Immediate offset, int lineIndex)
    : MemoryInstruction(rt, rs, offset, isWrite: true, isSign: false, lineIndex) {
    protected override int ByteNum => 4;
}

internal sealed class SwInstructionParser: IInstructionParser {
    public string Mnemonic => "sw";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(MemoryInstruction.TryParseMemoryOperands(operands, out RegisterID rt, out RegisterID rs, out Immediate? offset)) {
            instruction = new SwInstruction(rt, rs, offset, lineIndex);
            return true;
        }
        return false;
    }
}
