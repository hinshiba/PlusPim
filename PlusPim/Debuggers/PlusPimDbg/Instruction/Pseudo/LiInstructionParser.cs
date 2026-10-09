using PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Pseudo;

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
internal sealed class LiInstructionParser: IPseudoInstructionParser {
    public string Mnemonic => "li";

    public bool TryParse(string operands, int lineNumber, [MaybeNullWhen(false)] out ParsedLine line) {
        line = null;

        if(!OperandParser.TryParseRegTokenOperands(operands, out RegisterID rt, out string? token)) {
            return false;
        }

        // 32bitの可能性があるため，Immediate.TryParseではなくImmediate.TryParse32を使う
        if(!Immediate.TryParse32(token, out uint imm)) {
            return false;
        }

        ushort upper = (ushort)(imm >> 16);
        ushort lower = (ushort)(imm & 0xFFFF);

        // 命令数は値だけで決まる
        line = (upper == 0)
            ? ParsedLine.Fixed(
                InstructionFactory.Ori(rt, RegisterID.Zero, new Immediate(lower), lineNumber))
            : ParsedLine.Fixed(
                // lui命令は下位ビットを0にするため先行する必要がある
                InstructionFactory.Lui(rt, new Immediate(upper), lineNumber),
                InstructionFactory.Ori(rt, rt, new Immediate(lower), lineNumber));
        return true;
    }
}
