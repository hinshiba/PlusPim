using PlusPim.Debuggers.PlusPimDbg.Instructions.RType;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Pseudo;

/// <summary>
/// nop疑似命令のパーサー
/// </summary>
/// <remarks>
/// <c>nop</c> を以下の1命令に展開する:
/// <code>
/// sll $zero, $zero, 0
/// </code>
/// </remarks>
internal sealed partial class NopInstructionParser: IPseudoInstructionParser {
    public string Mnemonic => "nop";

    public int GetExpansionSize(string operands) {
        return 1;
    }

    public bool TryExpand(string operands, int lineIndex, SymbolTable symbolTable,
                          [MaybeNullWhen(false)] out IInstruction[] instructions) {
        instructions = [
            new SllInstruction(RegisterID.Zero, RegisterID.Zero, new Immediate(0), lineIndex),
        ];
        return true;
    }
}
