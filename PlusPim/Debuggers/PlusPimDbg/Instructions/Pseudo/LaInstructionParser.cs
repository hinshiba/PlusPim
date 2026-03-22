using PlusPim.Debuggers.PlusPimDbg.Instructions.IType;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Pseudo;

/// <summary>
/// la疑似命令のパーサー
/// </summary>
/// <remarks>
/// <c>la $rt, label</c> を以下の2命令に展開する:
/// <code>
/// lui $rt, upper16(addr)
/// ori $rt, $rt, lower16(addr)
/// </code>
/// </remarks>
internal sealed partial class LaInstructionParser: IPseudoInstructionParser {
    public string Mnemonic => "la";

    [GeneratedRegex(@"^\$(?<rt>\w+),\s*(?<label>\w+)$")]
    private static partial Regex LaOperandsPattern();

    public int GetExpansionSize(string operands) {
        return 2;
    }

    public bool TryExpand(string operands, int lineIndex, SymbolTable symbolTable,
                          [MaybeNullWhen(false)] out IInstruction[] instructions) {
        instructions = null;

        Match match = LaOperandsPattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(!Enum.TryParse<RegisterID>(match.Groups["rt"].Value, true, out RegisterID rt)) {
            return false;
        }

        string labelName = match.Groups["label"].Value;
        if(symbolTable.Resolve(labelName) is not { } label) {
            return false;
        }

        uint addr = label.Addr.Addr;
        ushort upper = (ushort)(addr >>> 16);
        ushort lower = (ushort)(addr & 0xFFFF);

        instructions = [
            // lui命令は下位ビットを0にするため先行する必要がある
            new LuiInstruction(rt, new Immediate(upper), lineIndex),
            new OriInstruction(rt, rt, new Immediate(lower), lineIndex),
        ];
        return true;
    }
}
