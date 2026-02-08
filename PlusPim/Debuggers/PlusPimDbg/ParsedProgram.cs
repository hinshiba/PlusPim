namespace PlusPim.Debuggers.PlusPimDbg;

internal class ParsedProgram {
    private readonly Mnemonic[] _mnemonics;
    private readonly int[] _sourceLines;
    private readonly Dictionary<string, int> _symbolTable;

    public string ProgramPath { get; }

    public ParsedProgram(string programPath, Action<string>? log = null) {
        this.ProgramPath = Path.GetFullPath(programPath);
        string[] lines = File.ReadAllLines(programPath);
        List<Mnemonic> mnemonicList = [];
        List<int> sourceLineList = [];
        this._symbolTable = [];

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
                this._symbolTable[labelName] = mnemonicList.Count;
                log?.Invoke($"Label: {labelName} at index {mnemonicList.Count}");
                continue;
            }

            // ニーモニックをパース
            if(Mnemonic.TryParse(processed, null, out Mnemonic? mnemonic)) {
                mnemonicList.Add(mnemonic);
                sourceLineList.Add(lineIndex + 1); // 1-baseの行番号
                log?.Invoke($"Parsed: {processed}");
            } else {
                log?.Invoke($"Parse failed: {processed}");
            }
        }

        this._mnemonics = mnemonicList.ToArray();
        this._sourceLines = sourceLineList.ToArray();
    }

    public Mnemonic GetMnemonic(int index) {
        return this._mnemonics[index];
    }

    public int GetSourceLine(int instructionIndex) {
        return this._sourceLines[instructionIndex];
    }

    public int MnemonicCount => this._mnemonics.Length;

    /// <summary>
    /// シンボルテーブル
    /// </summary>
    public IReadOnlyDictionary<string, int> SymbolTable => this._symbolTable;
    public int? GetLabelAddress(string label) {
        return this._symbolTable.TryGetValue(label, out int addr) ? addr : null;
    }
}
