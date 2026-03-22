using PlusPim.Debuggers.PlusPimDbg.Instructions;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
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
    /// グローバルインデックスから命令を取得する
    /// </summary>
    /// <param name="pc">グローバル命令インデックス</param>
    /// <param name="isKernelMode">カーネルモードか</param>
    /// <returns>命令</returns>
    public IInstruction GetInstruction(InstructionIndex pc, bool isKernelMode) {
        int[] cumulativeLengths = isKernelMode ? this._kernelTextCumulativeLengths : this._textCumulativeLengths;
        int programIdx = FindProgramIndex(cumulativeLengths, pc.Idx);
        int localIdx = 0 < programIdx ? pc.Idx - cumulativeLengths[programIdx - 1] : pc.Idx;

        return isKernelMode
            ? this._programs[programIdx].KernelTextSegment.Instructions[localIdx]
            : this._programs[programIdx].TextSegment.Instructions[localIdx];
    }

    /// <summary>
    /// 実行時にPCとカーネルモードに基づいて所属ファイルのシンボルテーブルからラベルを解決するデリゲートを生成する
    /// </summary>
    public Func<string, InstructionIndex, bool, Label?> CreateResolver() {
        return (name, pc, isKernel) => {
            int[] lengths = isKernel
                ? this._kernelTextCumulativeLengths
                : this._textCumulativeLengths;
            int progIdx = FindProgramIndex(lengths, pc.Idx);
            return this._programs[progIdx].SymbolTable.Resolve(name);
        };
    }

    /// <summary>
    /// グローバルインデックスから所属プログラムのファイルパスを取得する
    /// </summary>
    public string GetProgramPath(InstructionIndex pc, bool isKernelMode) {
        int[] cumulativeLengths = isKernelMode ? this._kernelTextCumulativeLengths : this._textCumulativeLengths;
        int programIdx = FindProgramIndex(cumulativeLengths, pc.Idx);
        return this._programs[programIdx].File.FullName;
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
