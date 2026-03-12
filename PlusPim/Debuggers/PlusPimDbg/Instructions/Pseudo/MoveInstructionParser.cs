using PlusPim.Debuggers.PlusPimDbg.Instructions.RType;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Pseudo;

/// <summary>
/// move疑似命令のパーサー
/// </summary>
/// <remarks>
/// <c>move $rt, $rs</c> を以下の1命令に展開する:
/// <code>
/// addu $rt, $rs, $zero
/// </code>
/// </remarks>
internal sealed partial class MoveInstructionParser: IPseudoInstructionParser {
    public string Mnemonic => "move";

    [GeneratedRegex(@"^\$(?<rt>\w+),\s*\$(?<rs>\w+)$")]
    private static partial Regex MoveOperandsPattern();

    public int GetExpansionSize(string operands) {
        return 1;
    }

    public bool TryExpand(string operands, int lineIndex, SymbolTable symbolTable,
                          [MaybeNullWhen(false)] out IInstruction[] instructions) {
        instructions = null;

        Match match = MoveOperandsPattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(!Enum.TryParse<RegisterID>(match.Groups["rt"].Value, true, out RegisterID rt)) {
            return false;
        }

        if(!Enum.TryParse<RegisterID>(match.Groups["rs"].Value, true, out RegisterID rs)) {
            return false;
        }

        instructions = [
            new AdduInstruction(rt, rs, RegisterID.Zero, lineIndex),
        ];
        return true;
    }
}
