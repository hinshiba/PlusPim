using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// .dataセグメントの行を処理し、メモリイメージを構築する
/// </summary>
internal sealed class DataSegmentBuilder(Action<string> log) {


    private readonly Dictionary<Address, byte> _memoryImage = [];

    /// <summary>
    /// 空き領域の先頭を示す
    /// </summary>
    public Address NextDataAddress { get; private set; } = DataSegment.DataSegmentBase;

    /// <summary>
    /// データセグメントの1行を処理する
    /// </summary>
    /// <param name="line">トリム済みの文字列</param>
    public void AddLine(string line) {
        // ディレクティブ処理
        if(!line.StartsWith('.')) {
            log.Invoke($"Warning: unexpected data segment content: {line}");
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
            case ".byte":
                this.ProcessByte(operands);
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
                log.Invoke($"Warning: unknown data directive: {directive}");
                break;
        }
    }

    /// <summary>
    /// パース結果をDataSegmentとして返す
    /// </summary>
    public DataSegment Build() {
        return new DataSegment(this._memoryImage);
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
                log.Invoke($"Warning: invalid .byte value: {trimmedVal}");
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
                log.Invoke($"Warning: invalid .word value: {trimmedVal}");
            }
        }
    }

    private void ProcessAscii(string operands, bool addNull) {
        // 文字列は"..."で囲まれている
        int firstQuote = operands.IndexOf('"');
        int lastQuote = operands.LastIndexOf('"');
        if(firstQuote < 0 || lastQuote <= firstQuote) {
            log.Invoke($"Warning: invalid string literal: {operands}");
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
            log.Invoke($"Warning: invalid .align value: {trimmed}");
            return;
        }

        if(n is < 0 or > 30) {
            log.Invoke($"Warning: .align value out of range: {n}");
            return;
        }

        // アライメント処理
        int mask = (1 << n) - 1;
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
        this._memoryImage[this.NextDataAddress] = value;
        this.NextDataAddress++;
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
