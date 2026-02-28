using PlusPim.Debuggers.PlusPimDbg.Instructions;
using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

internal sealed class TextSegmentBuilder(Action<string> log) {
    private readonly List<IInstruction> _instructions = [];
    private readonly SymbolTable _symbolTable = new();

    /// <summary>
    /// テキストセグメントの1行を処理する
    /// </summary>
    /// <param name="Line">トリム済みの文字列</param>
    /// <param name="LineIndex">0-baseの行番号</param>
    /// <returns>現在の最大の命令アドレス</returns>
    public void AddLine(string Line, int LineIndex) {
        // アセンブラ指令を無視
        if(Line.StartsWith('.')) {
            return;
        }

        // 命令をパース
        if(InstructionRegistry.Default.TryParse(Line, LineIndex + 1, out IInstruction? instruction)) {
            this._instructions.Add(instruction);
            log.Invoke($"TextSegmentBuilder: Parsed: {Line}");
        } else {
            log.Invoke($"TextSegmentBuilder: Parse failed for Instruction: {Line}");
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
