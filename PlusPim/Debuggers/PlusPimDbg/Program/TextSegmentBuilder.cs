using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Logging;

namespace PlusPim.Debuggers.PlusPimDbg.Program;


internal sealed class TextSegmentBuilder(Address baseAddr, ILogger logger) {
    private readonly List<IInstruction> _instructions = [];
    private readonly List<string> _errors = [];

    /// <summary>
    /// シンボルを解決できなかった行のエラー
    /// </summary>
    public IReadOnlyList<string> Errors => this._errors;

    /// <summary>
    /// パス1で解析済みの1行のシンボルを解決して命令列を追加する
    /// </summary>
    /// <remarks>シンボルを解決できなくても<see cref="ParsedLine.Size"/>個の命令を追加し，エラーを記録する</remarks>
    /// <param name="line">パス1で解析済みの行</param>
    /// <param name="lineNumber">1-basedの行番号</param>
    /// <param name="symbols">シンボルの解決に使う</param>
    /// <param name="fileName">エラーメッセージに使うファイル名</param>
    public void Add(ParsedLine line, int lineNumber, ISymbolResolver symbols, string fileName) {
        IInstruction[] instructions = line.Materialize(symbols, out string? unresolved);
        if(unresolved is not null) {
            this._errors.Add($"{fileName}:{lineNumber} Undefined label '{unresolved}'");
        }

        this._instructions.AddRange(instructions);
        logger.Debug("TextSegmentBuilder", $"{fileName}:{lineNumber} {instructions.Length} instruction(s)");
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
