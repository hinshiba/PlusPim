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

    public bool TryParse(string operands, int lineNumber, [MaybeNullWhen(false)] out ParsedLine line) {
        line = null;

        if(!OperandParser.TryParseRegTokenOperands(operands, out RegisterID rt, out string? labelName)) {
            return false;
        }

        // 前方参照があるため，ラベルはパス2で解決する．未定義でも配置をずらさないよう常に2命令にする
        line = ParsedLine.Deferred(2, (ISymbolResolver symbols, out string? unresolved) => {
            uint addr = 0;
            if(symbols.Resolve(labelName) is { } label) {
                addr = label.Addr.Addr;
                unresolved = null;
            } else {
                unresolved = labelName;
            }

            ushort upper = (ushort)(addr >>> 16);
            ushort lower = (ushort)(addr & 0xFFFF);

            return [
                // lui命令は下位ビットを0にするため先行する必要がある
                InstructionFactory.Lui(rt, new Immediate(upper), lineNumber),
                InstructionFactory.Ori(rt, rt, new Immediate(lower), lineNumber),
            ];
        });
        return true;
    }
}
