using PlusPim.Debuggers.PlusPimDbg.Instructions.IType;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Pseudo;

/// <summary>
/// li疑似命令のパーサー
/// </summary>
/// <remarks>
/// <c>li $rt, imm</c> を以下の2命令か1命令に展開する:
/// <code>
/// lui $rt, upper16(imm)
/// ori $rt, $rt, lower16(imm)
/// </code>
/// or
/// <code>
/// ori $rt, $zero, lower16(imm)
/// </code>
/// </remarks>
internal sealed partial class LiInstructionParser: IPseudoInstructionParser {
    public string Mnemonic => "li";

    [GeneratedRegex(@"^\$(?<rt>\w+),\s*(?<imm>\S+)$")]
    private static partial Regex LiOperandsPattern();

    public int GetExpansionSize(string operands) {
        // TryExpandを呼び出して展開サイズを計算する
        // falseならinstructionsはnullになるため，結果を確認しなくてもよい
        _ = this.TryExpand(operands, 0, new SymbolTable(), out IInstruction[]? instructions);
        return instructions?.Length ?? 0;
    }

    public bool TryExpand(string operands, int lineIndex, SymbolTable symbolTable,
                          [MaybeNullWhen(false)] out IInstruction[] instructions) {
        instructions = null;

        Match match = LiOperandsPattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(!Enum.TryParse<RegisterID>(match.Groups["rt"].Value, true, out RegisterID rt)) {
            return false;
        }

        // 32bitの可能性があるため，Immediate.TryParseではなくint.TryParseを使う
        if(!int.TryParse(match.Groups["imm"].Value, null, out int imm)) {
            return false;
        }

        ushort upper = (ushort)((uint)imm >>> 16);
        ushort lower = (ushort)(imm & 0xFFFF);

        instructions =
            (upper == 0) ?
            [
                new OriInstruction(rt, RegisterID.Zero, new Immediate(lower), lineIndex),
            ] :
            [
                // lui命令は下位ビットを0にするため先行する必要がある
                new LuiInstruction(rt, new Immediate(upper), lineIndex),
                new OriInstruction(rt, rt, new Immediate(lower), lineIndex),
        ];
        return true;
    }
}
