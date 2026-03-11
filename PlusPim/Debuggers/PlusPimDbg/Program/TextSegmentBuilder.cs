using PlusPim.Debuggers.PlusPimDbg.Instructions;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Logging;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

internal sealed class TextSegmentBuilder(ILogger logger) {
    private readonly List<IInstruction> _instructions = [];

    /// <summary>
    /// テキストセグメントの1行を処理する
    /// </summary>
    /// <param name="line">トリム済みの文字列</param>
    /// <param name="lineIndex">0-baseの行番号</param>
    public void AddLine(string line, int lineIndex) {
        // アセンブラ指令を無視
        if(line.StartsWith('.')) {
            return;
        }

        // 命令をパース
        if(InstructionRegistry.Default.TryParse(line, lineIndex + 1, out IInstruction? instruction)) {
            this._instructions.Add(instruction);
            logger.Debug("TextSegmentBuilder", $"Parsed: {line}");
        } else {
            logger.Warning("TextSegmentBuilder", $"Parse failed for Instruction: {line}");
        }
    }

    public InstructionIndex CurrentInstructionIndex() {
        return new(this._instructions.Count);
    }

    public Address CurrentAddr() {
        return Address.FromInstructionIndex(this.CurrentInstructionIndex());
    }

    public TextSegment Build() {
        return new TextSegment(this._instructions);
    }


}
