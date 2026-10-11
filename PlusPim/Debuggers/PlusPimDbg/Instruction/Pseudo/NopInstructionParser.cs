using PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Pseudo;

/// <summary>
/// nop疑似命令のパーサー
/// </summary>
/// <remarks>
/// <c>nop</c> を以下の1命令に展開する:
/// <code>
/// sll $zero, $zero, 0
/// </code>
/// </remarks>
internal sealed class NopInstructionParser: IPseudoInstructionParser {
    public string Mnemonic => "nop";

    public bool TryParse(string operands, int lineNumber, [MaybeNullWhen(false)] out ParsedLine line) {
        line = null;

        if(!OperandParser.TryParseNoOperand(operands)) {
            return false;
        }

        line = ParsedLine.Fixed(InstructionFactory.Sll(RegisterID.Zero, RegisterID.Zero, new Immediate(0), lineNumber));
        return true;
    }
}
