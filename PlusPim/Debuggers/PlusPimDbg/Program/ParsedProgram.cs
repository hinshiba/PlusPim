using PlusPim.Debuggers.PlusPimDbg.Instructions;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Logging;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// 解析済みのプログラムファイルを表すクラス
/// </summary>
internal class ParsedProgram {


    /// <summary>
    /// .textセグメント
    /// </summary>
    public TextSegment TextSegment { get; }

    /// <summary>
    /// .dataセグメント
    /// </summary>
    public DataSegment DataSegment { get; }

    /// <summary>
    /// シンボルテーブル
    /// </summary>
    public SymbolTable SymbolTable { get; }

    /// <summary>
    /// パースしたプログラムのファイルパス
    /// </summary>
    public string ProgramPath { get; }

    public ParsedProgram(string programPath, ILogger logger) {
        this.ProgramPath = Path.GetFullPath(programPath);

        string[] lines = File.ReadAllLines(programPath);
        this.SymbolTable = new SymbolTable();


        // 前処理: 各行をトリムして，セグメントごとに分割する
        List<(string Trimmed, int LineIndex)> textLines = [];
        List<(string Trimmed, int LineIndex)> dataLines = [];
        SegmentType currentSegment = SegmentType.Text;

        for(int lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
            string processed = RemoveComment(lines[lineIndex]).Trim();
            if(string.IsNullOrEmpty(processed)) {
                continue;
            }

            // セグメント切替判定
            if(processed.Equals(".data", StringComparison.OrdinalIgnoreCase)) {
                currentSegment = SegmentType.Data;
                continue;
            }
            if(processed.Equals(".text", StringComparison.OrdinalIgnoreCase)) {
                currentSegment = SegmentType.Text;
                continue;
            }

            if(currentSegment == SegmentType.Text) {
                textLines.Add((processed, lineIndex));
            } else {
                dataLines.Add((processed, lineIndex));
            }
        }

        // パス1: シンボルテーブルの構築
        // 疑似命令の展開後命令数を考慮してラベルアドレスを計算する
        int instructionCount = 0;
        foreach((string trimmed, int lineIndex) in textLines) {
            if(IsLabel(trimmed)) {
                string labelName = trimmed[..^1];
                Label label = new(labelName, Address.FromInstructionIndex(new(instructionCount)));
                this.SymbolTable.Add(label);
                logger.Debug("ParsedProgram", $"Line{lineIndex + 1} {label}");
            } else if(!trimmed.StartsWith('.')) {
                instructionCount += InstructionRegistry.Default.GetInstructionCount(trimmed);
            }
        }
        // この時点でデータセグメントは構築できる
        DataSegmentBuilder dataSegmentBuilder = new(logger);

        foreach((string trimmed, int lineIndex) in dataLines) {
            if(IsLabel(trimmed)) {
                string labelName = trimmed[..^1];
                Label label = new(labelName, dataSegmentBuilder.NextDataAddress);
                this.SymbolTable.Add(label);
                logger.Debug("ParsedProgram", $"Line{lineIndex + 1} {label}");
            } else {
                dataSegmentBuilder.AddLine(trimmed);
            }
        }


        // パス2: 完成したシンボルテーブルを使って命令をパース
        TextSegmentBuilder textSegmentBuilder = new(logger);
        foreach((string trimmed, int lineIndex) in textLines) {
            if(!IsLabel(trimmed)) {
                textSegmentBuilder.AddLine(trimmed, lineIndex, this.SymbolTable);
            }
        }

        this.TextSegment = textSegmentBuilder.Build();
        this.DataSegment = dataSegmentBuilder.Build();
    }

    /// <summary>
    /// コメントを除去する
    /// </summary>
    private static string RemoveComment(string line) {
        bool inString = false;
        // 文字列リテラル内の#は除去しない
        for(int i = 0; i < line.Length; i++) {
            char c = line[i];
            if(c == '"' && (i == 0 || line[i - 1] != '\\')) {
                inString = !inString;
            } else if(c == '#' && !inString) {
                return line[..i];
            }
        }
        return line;
    }

    /// <summary>
    /// ラベルか判定する
    /// </summary>
    /// <param name="line">トリム済みの文字列</param>
    /// <returns></returns>
    private static bool IsLabel(string line) {
        return line.EndsWith(':') && !line.Contains(' ');
    }


    /// <summary>
    /// その行の命令を取得する
    /// </summary>
    /// <param name="index">命令インデックス</param>
    /// <returns>命令</returns>
    public IInstruction GetInstruction(InstructionIndex index) {
        return this.TextSegment.Instructions[index.Idx];
    }

    /// <summary>
    /// 命令数
    /// </summary>
    public int InstructionCount => this.TextSegment.Instructions.Length;

}
