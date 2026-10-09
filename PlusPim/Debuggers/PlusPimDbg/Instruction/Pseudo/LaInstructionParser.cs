using PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Pseudo;

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
internal sealed class LaInstructionParser: IPseudoInstructionParser {
    public string Mnemonic => "la";

    public int GetExpansionSize(string operands) {
        return 2;
    }

    public bool TryExpand(string operands, int lineNumber, SymbolTable symbolTable,
                          [MaybeNullWhen(false)] out IInstruction[] instructions) {
        instructions = null;

        if(!OperandParser.TryParseRegTokenOperands(operands, out RegisterID rt, out string? labelName)) {
            return false;
        }

        if(symbolTable.Resolve(labelName) is not { } label) {
            return false;
        }

        uint addr = label.Addr.Addr;
        ushort upper = (ushort)(addr >>> 16);
        ushort lower = (ushort)(addr & 0xFFFF);

        instructions = [
            // lui命令は下位ビットを0にするため先行する必要がある
            InstructionFactory.Lui(rt, new Immediate(upper), lineNumber),
            InstructionFactory.Ori(rt, rt, new Immediate(lower), lineNumber),
        ];
        return true;
    }
}
