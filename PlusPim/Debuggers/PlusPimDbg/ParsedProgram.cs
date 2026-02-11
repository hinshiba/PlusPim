using PlusPim.Debuggers.PlusPimDbg.Instructions;

namespace PlusPim.Debuggers.PlusPimDbg;

/// <summary>
/// 解析済みのプログラムファイルを表すクラス
/// </summary>
internal class ParsedProgram {
    private readonly IInstruction[] _instructions;
    private readonly int[] _sourceLines;

    /// <summary>
    /// シンボルテーブル
    /// </summary>
    public SymbolTable SymbolTable { get; }

    /// <summary>
    /// パースしたプログラムのファイルパス
    /// </summary>
    public string ProgramPath { get; }

    public ParsedProgram(string programPath, Action<string> log) {
        this.ProgramPath = Path.GetFullPath(programPath);
        string[] lines = File.ReadAllLines(programPath);
        List<IInstruction> instructionList = [];
        List<int> sourceLineList = [];
        this.SymbolTable = new SymbolTable();

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
                this.SymbolTable.Add(new Label(labelName, instructionList.Count));
                log.Invoke($"Label: {labelName} at index {instructionList.Count}");
                continue;
            }

            // 命令をパース
            if(InstructionRegistry.Default.TryParse(processed, out IInstruction? instruction)) {
                instructionList.Add(instruction);
                sourceLineList.Add(lineIndex + 1); // 1-baseの行番号
                log.Invoke($"Parsed: {processed}");
            } else {
                log.Invoke($"Parse failed: {processed}");
            }
        }

        this._instructions = instructionList.ToArray();
        this._sourceLines = sourceLineList.ToArray();
    }

    /// <summary>
    /// その行の命令を取得する
    /// </summary>
    /// <param name="index">命令インデックス</param>
    /// <returns>命令</returns>
    public IInstruction GetInstruction(int index) {
        return this._instructions[index];
    }

    /// <summary>
    /// 実行インデックスのソースコード上の行番号を取得する
    /// </summary>
    /// <param name="instructionIndex">実行インデックス</param>
    /// <returns>行番号</returns>
    public int GetSourceLine(int instructionIndex) {
        return this._sourceLines[instructionIndex];
    }

    /// <summary>
    /// 命令数
    /// </summary>
    public int InstructionCount => this._instructions.Length;

}
