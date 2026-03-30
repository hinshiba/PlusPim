using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;

internal interface IInstructionParser {
    string Mnemonic { get; }
    bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction);
}
