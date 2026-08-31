using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Logging;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// .dataセグメントの行を処理し，メモリイメージを構築する
/// </summary>
internal sealed class DataSegmentBuilder(Address baseAddr, ILogger logger) {


    private readonly Dictionary<Address, byte> _memoryImage = [];

    /// <summary>
    /// アドレス未確定のラベル
    /// </summary>
    private readonly List<(string Name, int LineIndex)> _pendingLabels = [];

    private readonly List<(Label Label, int LineIndex)> _resolvedLabels = [];

    /// <summary>
    /// アドレスが確定したラベル．Build後に確定する
    /// </summary>
    public IReadOnlyList<(Label Label, int LineIndex)> ResolvedLabels => this._resolvedLabels;

    /// <summary>
    /// 空き領域の先頭を示す
    /// </summary>
    public Address NextDataAddress { get; private set; } = baseAddr;

    private readonly Address _baseAddr = baseAddr;


    /// <summary>
    /// ラベルを登録する．アドレスは直後に配置される実データの先頭で確定する
    /// </summary>
    /// <param name="name">ラベル名</param>
    /// <param name="lineIndex">0-indexedの行番号</param>
    public void AddLabel(string name, int lineIndex) {
        this._pendingLabels.Add((name, lineIndex));
    }

    /// <summary>
    /// 未確定ラベルを現在のカーソル位置で確定させる
    /// </summary>
    private void ResolvePendingLabels() {
        if(this._pendingLabels.Count == 0) {
            return;
        }
        foreach((string name, int lineIndex) in this._pendingLabels) {
            this._resolvedLabels.Add((new Label(name, this.NextDataAddress), lineIndex));
        }
        this._pendingLabels.Clear();
    }

    /// <summary>
    /// データセグメントの1行を処理する
    /// </summary>
    /// <param name="line">トリム済みの文字列</param>
    public void AddLine(string line) {
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
                this.ProcessAscii(operands, addNull: false);
                break;
            case ".asciiz":
                this.ProcessAscii(operands, addNull: true);
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
        return new DataSegment(this._memoryImage, this._baseAddr, this.NextDataAddress.Addr - this._baseAddr.Addr);
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

    private void ProcessAscii(string operands, bool addNull) {
        // 文字列は"..."で囲まれている
        int firstQuote = operands.IndexOf('"');
        int lastQuote = operands.LastIndexOf('"');
        if(firstQuote < 0 || lastQuote <= firstQuote) {
            logger.Warning("DataSegmentBuilder", $"Invalid string literal: {operands}");
            return;
        }

        string content = operands[(firstQuote + 1)..lastQuote];
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
                result.Add((byte)input[i]);
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
