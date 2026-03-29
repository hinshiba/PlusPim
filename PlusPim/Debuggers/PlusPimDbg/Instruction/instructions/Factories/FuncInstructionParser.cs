using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Factories;

/// <summary>
/// ラムダベースの汎用パーサー
/// </summary>
internal sealed class FuncInstructionParser(
    string mnemonic,
    Func<string, int, IInstruction?> parseFunc
): IInstructionParser {
    public string Mnemonic => mnemonic;

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = parseFunc(operands, lineIndex);
        return instruction is not null;
    }
}
