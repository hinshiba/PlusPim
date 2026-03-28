using PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program;
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

    public int GetExpansionSize(string operands) {
        return 1;
    }

    public bool TryExpand(string operands, int lineIndex, SymbolTable symbolTable,
                          [MaybeNullWhen(false)] out IInstruction[] instructions) {
        instructions = [
            InstructionFactory.Sll(RegisterID.Zero, RegisterID.Zero, new Immediate(0), lineIndex),
        ];
        return true;
    }
}
