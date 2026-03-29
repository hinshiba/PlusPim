using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Logging;

namespace PlusPim.Debuggers.PlusPimDbg.Program;


internal sealed class TextSegmentBuilder(Address baseAddr, ILogger logger) {
    private readonly List<IInstruction> _instructions = [];

    /// <summary>
    /// テキストセグメントの1行を解析・展開する
    /// </summary>
    /// <param name="line">トリム済みの文字列</param>
    /// <param name="lineIndex">0-baseの行番号</param>
    /// <param name="symbolTable">解決済みのシンボルテーブル</param>
    public void AddLine(string line, int lineIndex, SymbolTable symbolTable) {
        // アセンブラ指令を無視
        if(line.StartsWith('.')) {
            return;
        }

        // 命令をパース
        if(InstructionRegistry.Default.TryParseAll(line, lineIndex + 1, symbolTable, out IInstruction[]? instructions)) {
            this._instructions.AddRange(instructions);
            logger.Debug("TextSegmentBuilder", $"Parsed: {line} ({instructions.Length} instruction(s))");
        } else {
            logger.Warning("TextSegmentBuilder", $"Parse failed for Instruction: {line}");
        }
    }

    public InstructionIndex CurrentInstructionIndex() {
        return new(this._instructions.Count);
    }

    public Address CurrentAddr() {
        return Address.FromInstructionIndex(this.CurrentInstructionIndex(), baseAddr);
    }

    public TextSegment Build() {
        return new TextSegment(this._instructions, baseAddr);
    }


}
