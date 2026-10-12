using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Logging;
using System.Text;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// .dataセグメントの行を処理し，メモリイメージを構築する
/// </summary>
/// <param name="baseAddr">データセグメントの開始アドレス</param>
/// <param name="logger">ロガー</param>
/// <param name="fileName">エラーの報告に使うファイル名</param>
internal sealed class DataSegmentBuilder(Address baseAddr, ILogger logger, string fileName) {


    private readonly Dictionary<Address, byte> _memoryImage = [];

    private readonly List<string> _errors = [];

    /// <summary>
    /// アセンブルを失敗させるエラー．ファイル名と行番号を含む
    /// </summary>
    /// <remarks>エラーになった指令は何も配置しない．ほかの行の処理は続ける</remarks>
    public IReadOnlyList<string> Errors => this._errors;

    /// <summary>
    /// アドレス未確定のラベル
    /// </summary>
    private readonly List<(string Name, int LineNumber)> _pendingLabels = [];

    private readonly List<(Label Label, int LineNumber)> _resolvedLabels = [];

    /// <summary>
    /// アドレスが確定したラベル．Build後に確定する
    /// </summary>
    public IReadOnlyList<(Label Label, int LineNumber)> ResolvedLabels => this._resolvedLabels;

    /// <summary>
    /// 空き領域の先頭を示す
    /// </summary>
    public Address NextDataAddress { get; private set; } = baseAddr;

    private readonly Address _baseAddr = baseAddr;


    /// <summary>
    /// ラベルを登録する．アドレスは直後に配置される実データの先頭で確定する
    /// </summary>
    /// <param name="name">ラベル名</param>
    /// <param name="lineNumber">1-indexedの行番号</param>
    public void AddLabel(string name, int lineNumber) {
        this._pendingLabels.Add((name, lineNumber));
    }

    /// <summary>
    /// 未確定ラベルを現在のカーソル位置で確定させる
    /// </summary>
    private void ResolvePendingLabels() {
        if(this._pendingLabels.Count == 0) {
            return;
        }
        foreach((string name, int lineNumber) in this._pendingLabels) {
            this._resolvedLabels.Add((new Label(name, this.NextDataAddress), lineNumber));
        }
        this._pendingLabels.Clear();
    }

    /// <summary>
    /// データセグメントの1行を処理する
    /// </summary>
    /// <param name="line">トリム済みの文字列</param>
    /// <param name="lineNumber">1-indexedの行番号</param>
    public void AddLine(string line, int lineNumber) {
        // ディレクティブ処理
        if(!line.StartsWith('.')) {
            logger.Warning("DataSegmentBuilder", $"Unexpected data segment content: {line}");
            return;
        }

        // ディレクティブ名と引数を分離
        int spaceIndex = line.IndexOf(' ');
        string directive;
        string operands;
        if(spaceIndex >= 0) {
            directive = line[..spaceIndex].ToLowerInvariant();
            operands = line[spaceIndex..].Trim();
        } else {
            directive = line.ToLowerInvariant();
            operands = "";
        }

        switch(directive) {
            case ".space":
                this.ProcessSpace(operands);
                break;
            case ".byte":
                this.ProcessByte(operands);
                break;
            case ".half":
                this.ProcessHalf(operands);
                break;
            case ".word":
                this.ProcessWord(operands);
                break;
            case ".ascii":
                this.ProcessString(directive, lineNumber, operands, asciiOnly: true, addNull: false);
                break;
            case ".asciiz":
                this.ProcessString(directive, lineNumber, operands, asciiOnly: true, addNull: true);
                break;
            case ".utf-8z":
                this.ProcessString(directive, lineNumber, operands, asciiOnly: false, addNull: true);
                break;
            case ".align":
                this.ProcessAlign(operands);
                break;
            default:
                logger.Warning("DataSegmentBuilder", $"Unknown data directive: {directive}");
                break;
        }
    }

    /// <summary>
    /// パース結果をDataSegmentとして返す
    /// </summary>
    public DataSegment Build() {
        // .dataの末尾に置かれたラベルを確定させる
        this.ResolvePendingLabels();
        return new DataSegment(this._memoryImage, this.NextDataAddress.Addr - this._baseAddr.Addr);
    }


    private void ProcessSpace(string operands) {
        string trimmed = operands.Trim();
        if(int.TryParse(trimmed, out int n)) {
            if(n <= 0) {
                logger.Warning("DataSegmentBuilder", $".space value cannot be negative or zero: {n}");
                return;
            }
            this.ResolvePendingLabels();
            this.NextDataAddress += n;
        } else {
            logger.Warning("DataSegmentBuilder", $"Invalid .space value: {trimmed}");
        }
    }

    private void ProcessByte(string operands) {
        string[] values = operands.Split(',');
        foreach(string val in values) {
            string trimmedVal = val.Trim();
            if(int.TryParse(trimmedVal, out int intVal)) {
                this.WriteByte((byte)(intVal & 0xFF));
            } else if(this.TryParseHex(trimmedVal, out int hexVal)) {
                this.WriteByte((byte)(hexVal & 0xFF));
            } else {
                logger.Warning("DataSegmentBuilder", $"Invalid .byte value: {trimmedVal}");
            }
        }
    }

    private void ProcessHalf(string operands) {
        string[] values = operands.Split(',');
        foreach(string val in values) {
            string trimmedVal = val.Trim();
            // 2バイトアラインメント
            this.ProcessAlign("1");

            if(int.TryParse(trimmedVal, out int intVal)) {
                this.WriteHalf(intVal);
            } else if(this.TryParseHex(trimmedVal, out int hexVal)) {
                this.WriteHalf(hexVal);
            } else {
                logger.Warning("DataSegmentBuilder", $"Invalid .half value: {trimmedVal}");
            }
        }
    }

    private void ProcessWord(string operands) {
        string[] values = operands.Split(',');
        foreach(string val in values) {
            string trimmedVal = val.Trim();
            // 4バイトアラインメント
            this.ProcessAlign("2");

            if(int.TryParse(trimmedVal, out int intVal)) {
                this.WriteWord(intVal);
            } else if(this.TryParseHex(trimmedVal, out int hexVal)) {
                this.WriteWord(hexVal);
            } else {
                logger.Warning("DataSegmentBuilder", $"Invalid .word value: {trimmedVal}");
            }
        }
    }

    /// <summary>
    /// 文字列配置の指令を処理する
    /// </summary>
    /// <param name="directive">小文字にした指令名</param>
    /// <param name="lineNumber">1-indexedの行番号</param>
    /// <param name="operands">指令の引数</param>
    /// <param name="asciiOnly"><see langword="true"/>ならASCII範囲外の文字をエラーにする．そうでなければUTF-8で配置する</param>
    /// <param name="addNull">NUL終端を付けるかどうか</param>
    private void ProcessString(string directive, int lineNumber, string operands, bool asciiOnly, bool addNull) {
        // 文字列は"..."で囲まれている
        int firstQuote = operands.IndexOf('"');
        int lastQuote = operands.LastIndexOf('"');
        if(firstQuote < 0 || lastQuote <= firstQuote) {
            logger.Warning("DataSegmentBuilder", $"Invalid string literal: {operands}");
            return;
        }

        string content = operands[(firstQuote + 1)..lastQuote];
        if(asciiOnly) {
            // エスケープはASCIIの文字だけなので，リテラルのまま判定する
            foreach(Rune rune in content.EnumerateRunes()) {
                if(!rune.IsAscii) {
                    this._errors.Add($"{fileName}:{lineNumber} Non-ASCII character U+{rune.Value:X4} in {directive} string (use .utf-8z): {operands}");
                    return;
                }
            }
        }

        byte[] bytes = this.ProcessEscapeSequences(content);
        foreach(byte b in bytes) {
            this.WriteByte(b);
        }

        if(addNull) {
            this.WriteByte(0);
        }
    }

    private void ProcessAlign(string operands) {
        string trimmed = operands.Trim();

        if(!int.TryParse(trimmed, out int n)) {
            logger.Warning("DataSegmentBuilder", $"Invalid .align value: {trimmed}");
            return;
        }

        if(n is < 0 or > 30) {
            logger.Warning("DataSegmentBuilder", $".align value out of range: {n}");
            return;
        }

        // アライメント処理
        uint mask = (uint)((1 << n) - 1);
        this.NextDataAddress = new((this.NextDataAddress.Addr + mask) & ~mask);
    }

    private byte[] ProcessEscapeSequences(string input) {
        List<byte> result = [];
        Span<byte> utf8 = stackalloc byte[4];
        for(int i = 0; i < input.Length; i++) {
            if(input[i] == '\\' && i + 1 < input.Length) {
                char next = input[i + 1];
                switch(next) {
                    case 'n':
                        result.Add((byte)'\n');
                        i++;
                        break;
                    case 't':
                        result.Add((byte)'\t');
                        i++;
                        break;
                    case '0':
                        result.Add(0);
                        i++;
                        break;
                    case '\\':
                        result.Add((byte)'\\');
                        i++;
                        break;
                    case '"':
                        result.Add((byte)'"');
                        i++;
                        break;
                    default:
                        result.Add((byte)input[i]);
                        break;
                }
            } else {
                // 文字列リテラルはUTF-8のバイト列として書く．孤立サロゲートはU+FFFDにする
                if(!Rune.TryGetRuneAt(input, i, out Rune rune)) {
                    rune = Rune.ReplacementChar;
                }
                int length = rune.EncodeToUtf8(utf8);
                for(int j = 0; j < length; j++) {
                    result.Add(utf8[j]);
                }
                i += rune.Utf16SequenceLength - 1;
            }
        }
        return result.ToArray();
    }

    private void WriteByte(byte value) {
        // 整列後の書き込み先でラベルを確定させる
        this.ResolvePendingLabels();
        this._memoryImage[this.NextDataAddress] = value;
        this.NextDataAddress++;
    }

    private void WriteHalf(int value) {
        // リトルエンディアン
        this.WriteByte((byte)(value & 0xFF));
        this.WriteByte((byte)((value >> 8) & 0xFF));
    }

    private void WriteWord(int value) {
        // リトルエンディアン
        this.WriteByte((byte)(value & 0xFF));
        this.WriteByte((byte)((value >> 8) & 0xFF));
        this.WriteByte((byte)((value >> 16) & 0xFF));
        this.WriteByte((byte)((value >> 24) & 0xFF));
    }

    private bool TryParseHex(string value, out int result) {
        result = 0;
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out result);
    }
}
