using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
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
    /// .ktextセグメント
    /// </summary>
    public TextSegment KernelTextSegment { get; }

    /// <summary>
    /// シンボルテーブル
    /// </summary>
    public SymbolTable SymbolTable { get; }

    /// <summary>
    /// パースしたプログラムのファイル
    /// </summary>
    public FileInfo File { get; }

    public ParsedProgram(FileInfo file, Address textSegmentBase, Address dataSegmentBase, Address kernelTextSegmentBase, ILogger logger) {
        this.File = file;
        this.SymbolTable = new SymbolTable();


        // 前処理: 各行をトリムして，セグメントごとに分割する
        // 現在のファイルでの
        List<(string Trimmed, int LineIndex)> textLines = [];
        List<(string Trimmed, int LineIndex)> dataLines = [];

        List<(string Trimmed, int LineIndex)> kernelTextLines = [];

        SegmentType currentSegment = SegmentType.Text;
        {
            using StreamReader reader = file.OpenText();
            int lineIndex = -1;
            while(reader.ReadLine() is string line) {
                lineIndex++;
                string processed = RemoveComment(line).Trim();
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
                if(processed.Equals(".ktext", StringComparison.OrdinalIgnoreCase)) {
                    currentSegment = SegmentType.KernelText;
                    continue;
                }

                switch(currentSegment) {
                    case SegmentType.Text:
                        textLines.Add((processed, lineIndex));
                        break;
                    case SegmentType.Data:
                        dataLines.Add((processed, lineIndex));
                        break;
                    case SegmentType.KernelText:
                        kernelTextLines.Add((processed, lineIndex));
                        break;
                    default:
                        throw new Exception("cant reach here");
                }
            }
        }

        // パス1: シンボルテーブルの構築
        // 疑似命令の展開後命令数を考慮してラベルアドレスを計算する
        this.BuildTextSegmentSymbols(textLines, textSegmentBase, logger);
        this.BuildTextSegmentSymbols(kernelTextLines, kernelTextSegmentBase, logger);

        // データセグメント
        DataSegmentBuilder dataSegmentBuilder = new(dataSegmentBase, logger);
        foreach((string trimmed, int lineIndex) in dataLines) {
            if(IsLabel(trimmed)) {
                string labelName = trimmed[..^1];
                Label label = new(labelName, dataSegmentBuilder.NextDataAddress);
                if(this.SymbolTable.Add(label)) {
                    logger.Warning("ParsedProgram", $"Duplicate label '{labelName}' at line {lineIndex + 1}. The previous definition will be overwritten.");
                }
                logger.Debug("ParsedProgram", $"Line{lineIndex + 1} {label}");
            } else {
                dataSegmentBuilder.AddLine(trimmed);
            }
        }


        // パス2: 完成したシンボルテーブルを使って命令をパース
        // テキストセグメント
        TextSegmentBuilder textSegmentBuilder = new(textSegmentBase, logger);
        foreach((string trimmed, int lineIndex) in textLines) {
            if(!IsLabel(trimmed)) {
                textSegmentBuilder.AddLine(trimmed, lineIndex, this.SymbolTable);
            }
        }

        // カーネルテキストセグメント
        TextSegmentBuilder kernelTextSegmentBuilder = new(kernelTextSegmentBase, logger);
        foreach((string trimmed, int lineIndex) in kernelTextLines) {
            if(!IsLabel(trimmed)) {
                kernelTextSegmentBuilder.AddLine(trimmed, lineIndex, this.SymbolTable);
            }
        }

        this.TextSegment = textSegmentBuilder.Build();
        this.DataSegment = dataSegmentBuilder.Build();
        this.KernelTextSegment = kernelTextSegmentBuilder.Build();
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
    /// テキスト系セグメントのシンボルテーブルを構築する
    /// </summary>
    private void BuildTextSegmentSymbols(List<(string Trimmed, int LineIndex)> lines, Address segmentBase, ILogger logger) {
        int instructionCount = 0;
        foreach((string trimmed, int lineIndex) in lines) {
            if(IsLabel(trimmed)) {
                string labelName = trimmed[..^1];
                Label label = new(labelName, Address.FromInstructionIndex(new(instructionCount), segmentBase));
                if(this.SymbolTable.Add(label)) {
                    logger.Warning("ParsedProgram", $"Duplicate label '{labelName}' at line {lineIndex + 1}. The previous definition will be overwritten.");
                }
                logger.Debug("ParsedProgram", $"Line{lineIndex + 1} {label}");
            } else if(!trimmed.StartsWith('.')) {
                instructionCount += InstructionRegistry.Default.GetInstructionCount(trimmed);
            }
        }
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

    /// <summary>
    /// テキストセグメントのバイト数
    /// </summary>
    public Address TextSegmentSize => new((uint)this.TextSegment.Instructions.Length * 4);

    /// <summary>
    /// カーネルテキストセグメントのバイト数
    /// </summary>
    public Address KernelTextSegmentSize => new((uint)this.KernelTextSegment.Instructions.Length * 4);

    /// <summary>
    /// データセグメントのバイト数
    /// </summary>
    public Address DataSegmentSize => new(this.DataSegment.Size);

}
