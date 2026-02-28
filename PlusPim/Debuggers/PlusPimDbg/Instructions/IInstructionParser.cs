using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

internal interface IInstructionParser {
    string Mnemonic { get; }
    bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction);
}
