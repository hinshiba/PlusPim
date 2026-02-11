using PlusPim.Debuggers.PlusPimDbg.Instructions;

namespace PlusPim.Debuggers.PlusPimDbg;

internal class ParsedProgram {
    private readonly IInstruction[] _instructions;
    private readonly int[] _sourceLines;
    private readonly SymbolTable _symbolTable;

    public string ProgramPath { get; }

    public ParsedProgram(string programPath, Action<string>? log = null) {
        this.ProgramPath = Path.GetFullPath(programPath);
        string[] lines = File.ReadAllLines(programPath);
        List<IInstruction> instructionList = [];
        List<int> sourceLineList = [];
        this._symbolTable = new SymbolTable();

        for(int lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
            string line = lines[lineIndex];
            // コメント除去 (#より後)
            string processed = line;
            int commentIndex = processed.IndexOf('#');
            if(commentIndex >= 0) {
                processed = processed[..commentIndex];
            }

            // ディレクティブ除去 (.より後)
            int dotIndex = processed.IndexOf('.');
            if(dotIndex >= 0) {
                processed = processed[..dotIndex];
            }

            // 前後の空白を除去
            processed = processed.Trim();

            // 空行はスキップ
            if(string.IsNullOrEmpty(processed)) {
                continue;
            }

            // ラベル判定: `:` で終わり、空白を含まない
            if(processed.EndsWith(':') && !processed.Contains(' ')) {
                string labelName = processed[..^1]; // 末尾の `:` を除去
                this._symbolTable.Add(new Label(labelName, instructionList.Count));
                log?.Invoke($"Label: {labelName} at index {instructionList.Count}");
                continue;
            }

            // 命令をパース
            if(InstructionRegistry.Default.TryParse(processed, out IInstruction? instruction)) {
                instructionList.Add(instruction);
                sourceLineList.Add(lineIndex + 1); // 1-baseの行番号
                log?.Invoke($"Parsed: {processed}");
            } else {
                log?.Invoke($"Parse failed: {processed}");
            }
        }

        this._instructions = instructionList.ToArray();
        this._sourceLines = sourceLineList.ToArray();
    }

    public IInstruction GetInstruction(int index) {
        return this._instructions[index];
    }

    public int GetSourceLine(int instructionIndex) {
        return this._sourceLines[instructionIndex];
    }

    public int InstructionCount => this._instructions.Length;

    /// <summary>
    /// シンボルテーブル
    /// </summary>
    public SymbolTable SymbolTable => this._symbolTable;

    public int? GetLabelAddress(string label) {
        return this._symbolTable.Resolve(label);
    }

    public string? GetLabelForExecutionIndex(int index) {
        return this._symbolTable.FindByIndex(index);
    }
}
