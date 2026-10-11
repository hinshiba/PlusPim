using PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Pseudo;

/// <summary>
/// move疑似命令のパーサー
/// </summary>
/// <remarks>
/// <c>move $rt, $rs</c> を以下の1命令に展開する:
/// <code>
/// addu $rt, $rs, $zero
/// </code>
/// </remarks>
internal sealed class MoveInstructionParser: IPseudoInstructionParser {
    public string Mnemonic => "move";

    public bool TryParse(string operands, int lineNumber, [MaybeNullWhen(false)] out ParsedLine line) {
        line = null;

        if(!OperandParser.TryParse2RegOperands(operands, out RegisterID rt, out RegisterID rs)) {
            return false;
        }

        line = ParsedLine.Fixed(InstructionFactory.Addu(rt, rs, RegisterID.Zero, lineNumber));
        return true;
    }
}
