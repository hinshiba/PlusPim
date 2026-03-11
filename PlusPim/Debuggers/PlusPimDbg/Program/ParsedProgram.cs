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
        // 現在のセグメント
        SegmentType currentSegment = SegmentType.Text;

        TextSegmentBuilder textSegmentBuilder = new(logger);
        DataSegmentBuilder dataSegmentBuilder = new(logger);

        this.SymbolTable = new SymbolTable();

        for(int lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
            string line = lines[lineIndex];
            // コメント除去
            string processed = RemoveComment(line);

            // 前後の空白を除去
            string trimmed = processed.Trim();

            // 空行はスキップ
            if(string.IsNullOrEmpty(trimmed)) {
                continue;
            }

            // セグメント切替判定
            if(trimmed.Equals(".data", StringComparison.OrdinalIgnoreCase)) {
                currentSegment = SegmentType.Data;
                continue;
            }
            if(trimmed.Equals(".text", StringComparison.OrdinalIgnoreCase)) {
                currentSegment = SegmentType.Text;
                continue;
            }

            // セグメント固有の処理
            switch(currentSegment) {
                case SegmentType.Text:
                    // テキストセグメント内のラベルの場合
                    if(IsLabel(trimmed)) {
                        string labelName = trimmed[..^1]; // 末尾の `:` を除去
                        // 次の命令のインデックスのアドレスを設定
                        Label label = new(labelName, Address.FromInstructionIndex(textSegmentBuilder.CurrentInstructionIndex()));
                        this.SymbolTable.Add(label);
                        logger.Debug("ParsedProgram", $"Line{lineIndex + 1} {label}");
                        continue;
                    }
                    // 命令だった場合
                    textSegmentBuilder.AddLine(trimmed, lineIndex);
                    break;
                case SegmentType.Data:
                    // データセグメント内のラベルの場合
                    if(IsLabel(trimmed)) {
                        string labelName = trimmed[..^1]; // 末尾の `:` を除去
                        // 次の空いている領域のアドレスを設定
                        Label label = new(labelName, dataSegmentBuilder.NextDataAddress);
                        this.SymbolTable.Add(label);
                        logger.Debug("ParsedProgram", $"Line{lineIndex + 1} {label}");
                        continue;
                    }
                    dataSegmentBuilder.AddLine(trimmed);
                    break;

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
