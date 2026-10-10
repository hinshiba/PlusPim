using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
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

    /// <summary>
    /// パス1で確定したテキストセグメントの命令数
    /// </summary>
    public int TextInstructionCount { get; }

    /// <summary>
    /// パス1で確定したカーネルテキストセグメントの命令数
    /// </summary>
    public int KernelTextInstructionCount { get; }

    /// <summary>
    /// アセンブルを失敗させるエラー
    /// </summary>
    public IReadOnlyList<string> Errors => this._errors;

    private readonly List<string> _errors = [];

    public ParsedProgram(FileInfo file, Address textSegmentBase, Address dataSegmentBase, Address kernelTextSegmentBase, ILogger logger) {
        this.File = file;
        this.SymbolTable = new SymbolTable();


        // 前処理: 各行をトリムして，セグメントごとに分割する
        // 行番号はこのファイルでの1始まりの値
        List<(string Trimmed, int LineNumber)> textLines = [];
        List<(string Trimmed, int LineNumber)> dataLines = [];

        List<(string Trimmed, int LineNumber)> kernelTextLines = [];

        SegmentType currentSegment = SegmentType.Unknown;
        {
            using StreamReader reader = file.OpenText();
            int lineNumber = 0;
            while(reader.ReadLine() is string line) {
                lineNumber++;
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
                        textLines.Add((processed, lineNumber));
                        break;
                    case SegmentType.Data:
                        dataLines.Add((processed, lineNumber));
                        break;
                    case SegmentType.KernelText:
                        kernelTextLines.Add((processed, lineNumber));
                        break;
                    default:
                        logger.Warning("ParsedProgram", $"Line{lineNumber} Segment type is not specified. So set text segment.");
                        currentSegment = SegmentType.Text;
                        textLines.Add((processed, lineNumber));
                        break;
                }
            }
        }

        // パス1: 各行を解析してシンボルテーブルを構築する
        // 解析した行の命令数はここで確定するため，ラベルのアドレスもここで確定する
        List<(ParsedLine Line, int LineNumber)> parsedTextLines = this.ParseTextLines(textLines, textSegmentBase, logger, strict);
        List<(ParsedLine Line, int LineNumber)> parsedKernelTextLines = this.ParseTextLines(kernelTextLines, kernelTextSegmentBase, logger, strict);
        this.TextInstructionCount = parsedTextLines.Sum(parsed => parsed.Line.Size);
        this.KernelTextInstructionCount = parsedKernelTextLines.Sum(parsed => parsed.Line.Size);

        // データセグメント
        DataSegmentBuilder dataSegmentBuilder = new(dataSegmentBase, logger);
        foreach((string trimmed, int lineNumber) in dataLines) {
            if(IsLabel(trimmed)) {
                dataSegmentBuilder.AddLabel(trimmed[..^1], lineNumber);
            } else {
                dataSegmentBuilder.AddLine(trimmed);
            }
        }
        this.DataSegment = dataSegmentBuilder.Build();

        // ラベルのアドレスは直後のデータの配置位置で確定するため，Build後にシンボルテーブルへ登録する
        foreach((Label label, int lineNumber) in dataSegmentBuilder.ResolvedLabels) {
            if(this.SymbolTable.Add(label)) {
                logger.Warning("ParsedProgram", $"Duplicate label '{label.Name}' at line {lineNumber}. The previous definition will be overwritten.");
            }
            logger.Debug("ParsedProgram", $"Line{lineNumber} {label}");
        }


        // パス2: 完成したシンボルテーブルを使ってシンボルを解決する
        this.TextSegment = this.Materialize(parsedTextLines, textSegmentBase, logger);
        this.KernelTextSegment = this.Materialize(parsedKernelTextLines, kernelTextSegmentBase, logger);
    }

    /// <summary>
    /// パス1で解析した行のシンボルを解決してテキスト系セグメントを作る
    /// </summary>
    private TextSegment Materialize(List<(ParsedLine Line, int LineNumber)> lines, Address segmentBase, ILogger logger) {
        TextSegmentBuilder builder = new(segmentBase, logger);
        foreach((ParsedLine line, int lineNumber) in lines) {
            builder.Add(line, lineNumber, this.SymbolTable, this.File.Name);
        }
        this._errors.AddRange(builder.Errors);
        return builder.Build();
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
    /// テキスト系セグメントの各行を解析し，ラベルをシンボルテーブルに登録する
    /// </summary>
    /// <remarks>
    /// 解析できない行と未対応の指令は警告を出して読み飛ばす．
    /// <paramref name="strict"/>が<see langword="true"/>ならば警告の代わりにエラーを記録する
    /// </remarks>
    /// <returns>解析できた行と1始まりの行番号</returns>
    private List<(ParsedLine Line, int LineNumber)> ParseTextLines(List<(string Trimmed, int LineNumber)> lines, Address segmentBase, ILogger logger, bool strict) {
        List<(ParsedLine Line, int LineNumber)> parsedLines = [];
        int instructionCount = 0;
        foreach((string trimmed, int lineNumber) in lines) {
            if(IsLabel(trimmed)) {
                string labelName = trimmed[..^1];
                Label label = new(labelName, Address.FromInstructionIndex(new(instructionCount), segmentBase));
                if(this.SymbolTable.Add(label)) {
                    logger.Warning("ParsedProgram", $"Duplicate label '{labelName}' at line {lineNumber}. The previous definition will be overwritten.");
                }
                logger.Debug("ParsedProgram", $"Line{lineNumber} {label}");
            } else if(trimmed.StartsWith('.')) {
                this.ReportSkipped($"{this.File.Name}:{lineNumber} Directive ignored (unsupported in text segment): {trimmed}", logger, strict);
            } else if(InstructionRegistry.Default.TryParseLine(trimmed, lineNumber, out ParsedLine? parsed)) {
                parsedLines.Add((parsed, lineNumber));
                instructionCount += parsed.Size;
            } else {
                this.ReportSkipped($"{this.File.Name}:{lineNumber} Line skipped (cannot parse): {trimmed}", logger, strict);
            }
        }
        return parsedLines;
    }

    /// <summary>
    /// 読み飛ばした行を報告する．
    /// strictならエラー，そうでなければ警告とする
    /// </summary>
    private void ReportSkipped(string message, ILogger logger, bool strict) {
        if(strict) {
            this._errors.Add(message);
        } else {
            logger.Warning("ParsedProgram", message);
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
    /// テキストセグメントのバイト数
    /// </summary>
    public Address TextSegmentSize => new((uint)this.TextInstructionCount * 4);

    /// <summary>
    /// カーネルテキストセグメントのバイト数
    /// </summary>
    public Address KernelTextSegmentSize => new((uint)this.KernelTextInstructionCount * 4);

    /// <summary>
    /// データセグメントのバイト数
    /// </summary>
    public Address DataSegmentSize => new(this.DataSegment.Size);

}
