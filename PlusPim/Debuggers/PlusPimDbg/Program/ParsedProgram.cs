using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Logging;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// 解析済みのプログラムファイルを表すクラス
/// </summary>
internal partial class ParsedProgram {
    /// <summary>
    /// 前処理済みの1行
    /// </summary>
    /// <param name="Text">ラベルならばラベル名．そうでなければトリム済みの文字列</param>
    /// <param name="LineNumber">このファイルでの1始まりの行番号</param>
    /// <param name="IsLabel">ラベルかどうか</param>
    private readonly record struct SourceLine(string Text, int LineNumber, bool IsLabel);

    /// <summary>
    /// 行頭のラベル定義 (<c>name:</c>) と，その後の空白
    /// </summary>
    [GeneratedRegex(@"^(?<label>[A-Za-z_.$][\w.$]*):\s*")]
    private static partial Regex LabelPrefixPattern();

    /// <summary>
    /// <c>.globl</c> または <c>.global</c> の定義．名前は <c>,</c> か空白で区切る
    /// </summary>
    [GeneratedRegex(@"^\.globa?l\s+(?<names>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex GlobalDirectivePattern();

    /// <summary>
    /// .textセグメント
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Assemble"/>の前に参照した場合</exception>
    public TextSegment TextSegment => this._textSegment ?? throw new InvalidOperationException("Assemble has not been called.");

    /// <summary>
    /// .dataセグメント
    /// </summary>
    public DataSegment DataSegment { get; }

    /// <summary>
    /// .ktextセグメント
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Assemble"/>の前に参照した場合</exception>
    public TextSegment KernelTextSegment => this._kernelTextSegment ?? throw new InvalidOperationException("Assemble has not been called.");

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
    /// <c>.globl</c> で宣言された名前と1始まりの行番号
    /// </summary>
    public IReadOnlyList<(string Name, int LineNumber)> GlobalDeclarations => this._globalDeclarations;

    /// <summary>
    /// アセンブルを失敗させるエラー
    /// </summary>
    public IReadOnlyList<string> Errors => this._errors;

    private readonly List<string> _errors = [];
    private readonly List<(string Name, int LineNumber)> _globalDeclarations = [];
    private readonly List<(ParsedLine Line, int LineNumber)> _parsedTextLines;
    private readonly List<(ParsedLine Line, int LineNumber)> _parsedKernelTextLines;
    private readonly Address _textSegmentBase;
    private readonly Address _kernelTextSegmentBase;
    private readonly ILogger _logger;
    private TextSegment? _textSegment;
    private TextSegment? _kernelTextSegment;

    /// <summary>
    /// ファイルを読み込み，パス1とデータセグメントの配置を行う
    /// </summary>
    /// <remarks>テキスト系セグメントの命令列は<see cref="Assemble"/>で作る</remarks>
    public ParsedProgram(FileInfo file, Address textSegmentBase, Address dataSegmentBase, Address kernelTextSegmentBase, ILogger logger, bool strict) {
        this.File = file;
        this.SymbolTable = new SymbolTable();
        this._textSegmentBase = textSegmentBase;
        this._kernelTextSegmentBase = kernelTextSegmentBase;
        this._logger = logger;


        // 前処理: 各行をトリムして，セグメントごとに分割する
        // 行頭のラベルは別の行として取り出す
        List<SourceLine> textLines = [];
        List<SourceLine> dataLines = [];
        List<SourceLine> kernelTextLines = [];

        SegmentType currentSegment = SegmentType.Unknown;

        // 現在のセグメントに行を振り分ける
        void Place(SourceLine sourceLine) {
            switch(currentSegment) {
                case SegmentType.Text:
                    textLines.Add(sourceLine);
                    break;
                case SegmentType.Data:
                    dataLines.Add(sourceLine);
                    break;
                case SegmentType.KernelText:
                    kernelTextLines.Add(sourceLine);
                    break;
                default:
                    logger.Warning("ParsedProgram", $"Line{sourceLine.LineNumber} Segment type is not specified. So set text segment.");
                    currentSegment = SegmentType.Text;
                    textLines.Add(sourceLine);
                    break;
            }
        }

        {
            using StreamReader reader = file.OpenText();
            int lineNumber = 0;
            while(reader.ReadLine() is string line) {
                lineNumber++;
                string processed = RemoveComment(line).Trim();

                // 行頭のラベルをすべて取り出す．ラベルは現在のセグメントに属する
                while(LabelPrefixPattern().Match(processed) is { Success: true } match) {
                    Place(new SourceLine(match.Groups["label"].Value, lineNumber, IsLabel: true));
                    processed = processed[match.Length..];
                }

                if(string.IsNullOrEmpty(processed)) {
                    continue;
                }

                // .globl はどのセグメントでも受け付け，セグメントには渡さない
                if(GlobalDirectivePattern().Match(processed) is { Success: true } global) {
                    foreach(string name in global.Groups["names"].Value.Split([',', ' ', '	'], StringSplitOptions.RemoveEmptyEntries)) {
                        this._globalDeclarations.Add((name, lineNumber));
                    }
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

                Place(new SourceLine(processed, lineNumber, IsLabel: false));
            }
        }

        // パス1: 各行を解析してシンボルテーブルを構築する
        // 解析した行の命令数はここで確定するため，ラベルのアドレスもここで確定する
        this._parsedTextLines = this.ParseTextLines(textLines, textSegmentBase, logger, strict);
        this._parsedKernelTextLines = this.ParseTextLines(kernelTextLines, kernelTextSegmentBase, logger, strict);
        this.TextInstructionCount = this._parsedTextLines.Sum(parsed => parsed.Line.Size);
        this.KernelTextInstructionCount = this._parsedKernelTextLines.Sum(parsed => parsed.Line.Size);

        // データセグメント
        DataSegmentBuilder dataSegmentBuilder = new(dataSegmentBase, logger);
        foreach(SourceLine sourceLine in dataLines) {
            if(sourceLine.IsLabel) {
                dataSegmentBuilder.AddLabel(sourceLine.Text, sourceLine.LineNumber);
            } else {
                dataSegmentBuilder.AddLine(sourceLine.Text);
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
    }

    /// <summary>
    /// パス2: ローカル，グローバルの順にシンボルを解決してテキスト系セグメントを作る
    /// </summary>
    /// <remarks>解決できなかったシンボルは<see cref="Errors"/>に記録する</remarks>
    /// <param name="globals">全ファイルのグローバルシンボル</param>
    public void Assemble(SymbolTable globals) {
        ScopedSymbolResolver symbols = new(this.SymbolTable, globals);
        this._textSegment = this.Materialize(this._parsedTextLines, this._textSegmentBase, symbols);
        this._kernelTextSegment = this.Materialize(this._parsedKernelTextLines, this._kernelTextSegmentBase, symbols);
    }

    /// <summary>
    /// パス1で解析した行のシンボルを解決してテキスト系セグメントを作る
    /// </summary>
    private TextSegment Materialize(List<(ParsedLine Line, int LineNumber)> lines, Address segmentBase, ISymbolResolver symbols) {
        TextSegmentBuilder builder = new(segmentBase, this._logger);
        foreach((ParsedLine line, int lineNumber) in lines) {
            builder.Add(line, lineNumber, symbols, this.File.Name);
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
    private List<(ParsedLine Line, int LineNumber)> ParseTextLines(List<SourceLine> lines, Address segmentBase, ILogger logger, bool strict) {
        List<(ParsedLine Line, int LineNumber)> parsedLines = [];
        int instructionCount = 0;
        foreach((string trimmed, int lineNumber, bool isLabel) in lines) {
            if(isLabel) {
                Label label = new(trimmed, Address.FromInstructionIndex(new(instructionCount), segmentBase));
                if(this.SymbolTable.Add(label)) {
                    logger.Warning("ParsedProgram", $"Duplicate label '{trimmed}' at line {lineNumber}. The previous definition will be overwritten.");
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
