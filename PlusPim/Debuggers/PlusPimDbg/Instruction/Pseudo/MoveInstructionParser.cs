using PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program;
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

    public int GetExpansionSize(string operands) {
        return 1;
    }

    public bool TryExpand(string operands, int lineIndex, SymbolTable symbolTable,
                          [MaybeNullWhen(false)] out IInstruction[] instructions) {
        instructions = null;

        if(!OperandParser.TryParse2RegOperands(operands, out RegisterID rt, out RegisterID rs)) {
            return false;
        }

        instructions = [
            InstructionFactory.Addu(rt, rs, RegisterID.Zero, lineIndex),
        ];
        return true;
    }
}
