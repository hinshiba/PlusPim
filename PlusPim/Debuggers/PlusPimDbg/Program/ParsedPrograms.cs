using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// 複数の<see cref="ParsedProgram"/>を管理するクラス"/>
/// </summary>
internal sealed class ParsedPrograms {

    /// <summary>
    /// 解析済みプログラムの配列
    /// </summary>
    private readonly ParsedProgram[] _programs;

    /// <summary>
    /// テキストセグメントの累積命令数
    /// </summary>
    private readonly int[] _textCumulativeLengths;

    /// <summary>
    /// カーネルテキストセグメントの累積命令数
    /// </summary>
    private readonly int[] _kernelTextCumulativeLengths;

    public ParsedPrograms(FileInfo[] files, ILogger logger) {
        // まず全部解析
        List<ParsedProgram> programList = [];
        Address textSegmentOffset = TextSegment.TextSegmentBase;
        Address kernelTextSegmentOffset = TextSegment.KernelTextSegmentBase;
        Address dataSegmentOffset = DataSegment.DataSegmentBase;
        List<int> textCumulativeLengths = [];
        List<int> kernelTextCumulativeLengths = [];
        int textTotal = 0;
        int kernelTextTotal = 0;
        foreach(FileInfo file in files) {
            ParsedProgram program = new(file, textSegmentOffset, dataSegmentOffset, kernelTextSegmentOffset, logger);

            // 開始アドレスの調整
            textSegmentOffset += program.TextSegmentSize;
            kernelTextSegmentOffset += program.KernelTextSegmentSize;
            dataSegmentOffset += program.DataSegmentSize;

            // 累積命令数の記録
            textTotal += program.TextSegment.Instructions.Length;
            textCumulativeLengths.Add(textTotal);
            kernelTextTotal += program.KernelTextSegment.Instructions.Length;
            kernelTextCumulativeLengths.Add(kernelTextTotal);

            // データセグメントの結合
            foreach(KeyValuePair<Address, byte> entry in program.DataSegment.MemoryImage) {
                if(this.MemoryImage.ContainsKey(entry.Key)) {
                    logger.Warning("ParsedPrograms", $"Memory address {entry.Key} defined in multiple files; overwriting.");
                }
                this.MemoryImage[entry.Key] = entry.Value;
            }

            programList.Add(program);
        }

        this._programs = [.. programList];
        this._textCumulativeLengths = [.. textCumulativeLengths];
        this._kernelTextCumulativeLengths = [.. kernelTextCumulativeLengths];
    }

    /// <summary>
    /// ユーザー空間の総命令数
    /// </summary>
    public int UserInstructionCount => 0 < this._textCumulativeLengths.Length ? this._textCumulativeLengths[^1] : 0;

    /// <summary>
    /// カーネル空間の総命令数
    /// </summary>
    public int KernelInstructionCount => 0 < this._kernelTextCumulativeLengths.Length ? this._kernelTextCumulativeLengths[^1] : 0;

    /// <summary>
    /// 統合されたデータセグメントのメモリイメージ
    /// </summary>
    public Dictionary<Address, byte> MemoryImage { get; } = [];

    /// <summary>
    /// 命令アドレスから命令を取得する
    /// MIPS例外が発生しうる (Ri, AdEL)
    /// </summary>
    /// <param name="pc">命令アドレス</param>
    /// <param name="context">コンテキスト</param>
    /// <returns>命令</returns>
    public IInstruction? GetInstruction(Address pc, RuntimeContext context) {
        // 有効なアドレスか確認
        if((pc.Addr & 0b11) != 0) {
            context.RaiseException(ExcCode.AdEL, pc);
            return null;
        }

        int globalIdx = (int)((pc.Addr - (context.IsKernelMode ? TextSegment.KernelTextSegmentBase.Addr : TextSegment.TextSegmentBase.Addr)) / 4);
        // 有効な範囲か確認
        if((context.IsKernelMode ? this.KernelInstructionCount : this.UserInstructionCount) <= globalIdx) {
            // 書き込まれていない範囲は無効な命令で埋まっていると見なす
            context.RaiseException(ExcCode.RI, pc);
            return null;
        }

        int[] cumulativeLengths = context.IsKernelMode ? this._kernelTextCumulativeLengths : this._textCumulativeLengths;
        int programIdx = FindProgramIndex(cumulativeLengths, globalIdx);
        int localIdx = 0 < programIdx ? globalIdx - cumulativeLengths[programIdx - 1] : globalIdx;

        return context.IsKernelMode
            ? this._programs[programIdx].KernelTextSegment.Instructions[localIdx]
            : this._programs[programIdx].TextSegment.Instructions[localIdx];
    }


    /// <summary>
    /// そのPCが指す命令のソースを返す
    /// </summary>
    /// <param name="pc">アドレス</param>
    /// <returns>ファイルと1-indexの行番号</returns>
    public (FileInfo? file, int lineIndex)? GetSourceInfo(Address pc) {
        // 有効なアドレスか確認
        if((pc.Addr & 0b11) != 0) {
            return null;
        }

        bool isKernelMode = TextSegment.KernelTextSegmentBase <= pc;

        int globalIdx = (int)((pc.Addr - (isKernelMode ? TextSegment.KernelTextSegmentBase.Addr : TextSegment.TextSegmentBase.Addr)) / 4);
        // 有効な範囲か確認
        if((isKernelMode ? this.KernelInstructionCount : this.UserInstructionCount) <= globalIdx) {
            return null;
        }

        int[] cumulativeLengths = isKernelMode ? this._kernelTextCumulativeLengths : this._textCumulativeLengths;
        int programIdx = FindProgramIndex(cumulativeLengths, globalIdx);
        int localIdx = 0 < programIdx ? globalIdx - cumulativeLengths[programIdx - 1] : globalIdx;

        ParsedProgram program = this._programs[programIdx];

        return isKernelMode
            ? (program.File, program.KernelTextSegment.Instructions[localIdx].SourceLine)
            : (program.File, program.TextSegment.Instructions[localIdx].SourceLine);
    }

    /// <summary>
    /// 実行時にPCとカーネルモードに基づいて所属ファイルのシンボルテーブルからラベルを解決するデリゲートを生成する
    /// </summary>
    public Func<string, Address, bool, Label?> CreateResolver() {
        return (name, pc, isKernel) => {
            // 実行中pcなので，かならず有効な範囲である
            int globalIdx = (int)((pc.Addr - (isKernel ? TextSegment.KernelTextSegmentBase.Addr : TextSegment.TextSegmentBase.Addr)) / 4);
            int[] lengths = isKernel
                ? this._kernelTextCumulativeLengths
                : this._textCumulativeLengths;
            int progIdx = FindProgramIndex(lengths, globalIdx);
            return this._programs[progIdx].SymbolTable.Resolve(name);
        };
    }


    /// <summary>
    /// 全プログラムからラベルを検索する
    /// </summary>
    public Label? ResolveFromAll(string name) {
        foreach(ParsedProgram program in this._programs) {
            Label? label = program.SymbolTable.Resolve(name);
            if(label is not null) {
                return label;
            }
        }
        return null;
    }

    /// <summary>
    /// 累積長配列を二分探索し、グローバルインデックスが属するプログラムのインデックスを返す
    /// </summary>
    private static int FindProgramIndex(int[] cumulativeLengths, int globalIndex) {
        int lo = 0;
        int hi = cumulativeLengths.Length - 1;
        while(lo < hi) {
            int mid = lo + ((hi - lo) / 2);
            if(cumulativeLengths[mid] <= globalIndex) {
                lo = mid + 1;
            } else {
                hi = mid;
            }
        }
        return lo;
    }
}
