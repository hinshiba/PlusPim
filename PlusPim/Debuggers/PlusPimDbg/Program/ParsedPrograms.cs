using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// 複数の<see cref="ParsedProgram"/>を管理するクラス
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

    /// <summary>
    /// 各プログラムのローカル，グローバルの順にラベルを解決するリゾルバ
    /// </summary>
    private readonly ScopedSymbolResolver[] _resolvers;

    /// <summary>
    /// 全ファイルの<c>.globl</c>で宣言されたシンボル
    /// </summary>
    public SymbolTable GlobalSymbols { get; } = new();

    /// <summary>
    /// 全ファイルのパス1を行ってグローバルシンボルを集め，その後に各ファイルのパス2を行う
    /// </summary>
    /// <param name="files">すべての実行するファイル</param>
    /// <param name="logger">ロガー</param>
    /// <param name="strict">解析できない行と未対応の指令をエラーにするかどうか</param>
    /// <exception cref="AssemblyException">アセンブルに失敗した場合</exception>
    public ParsedPrograms(FileInfo[] files, ILogger logger, bool strict = false) {
        // フェーズ1: 全ファイルのパス1，データセグメント，ローカルのシンボルテーブル
        List<ParsedProgram> programList = [];
        Address textSegmentOffset = TextSegment.TextSegmentBase;
        Address kernelTextSegmentOffset = TextSegment.KernelTextSegmentBase;
        Address dataSegmentOffset = DataSegment.DataSegmentBase;
        List<int> textCumulativeLengths = [];
        List<int> kernelTextCumulativeLengths = [];
        int textTotal = 0;
        int kernelTextTotal = 0;
        foreach(FileInfo file in files) {
            ParsedProgram program = new(file, textSegmentOffset, dataSegmentOffset, kernelTextSegmentOffset, logger, strict);

            // 開始アドレスの調整
            textSegmentOffset += program.TextSegmentSize;
            kernelTextSegmentOffset += program.KernelTextSegmentSize;
            dataSegmentOffset += program.DataSegmentSize;

            // 累積命令数の記録
            textTotal += program.TextInstructionCount;
            textCumulativeLengths.Add(textTotal);
            kernelTextTotal += program.KernelTextInstructionCount;
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

        // グローバルシンボルの表を作る
        List<string> errors = this.BuildGlobalSymbols(programList, logger);

        // フェーズ2: 全ファイルのパス2
        foreach(ParsedProgram program in programList) {
            program.Assemble(this.GlobalSymbols);
        }

        // すべてのファイルを処理してからエラーをまとめて報告する
        errors.AddRange(programList.SelectMany(program => program.Errors));
        if(errors.Count != 0) {
            foreach(string error in errors) {
                logger.Error("ParsedPrograms", error);
            }
            throw new AssemblyException(errors);
        }

        this._programs = [.. programList];
        this.DataSegmentSize = (uint)programList.Sum(program => (long)program.DataSegmentSize.Addr);
        this._textCumulativeLengths = [.. textCumulativeLengths];
        this._kernelTextCumulativeLengths = [.. kernelTextCumulativeLengths];
        this._resolvers = [.. programList.Select(program => new ScopedSymbolResolver(program.SymbolTable, this.GlobalSymbols))];
    }

    /// <summary>
    /// <c>.globl</c>の宣言から<see cref="GlobalSymbols"/>を作る
    /// </summary>
    /// <remarks>宣言したファイルで定義されていない名前は警告を出して無視する</remarks>
    /// <returns>複数のファイルで同じグローバルシンボルを定義していた場合のエラー</returns>
    private List<string> BuildGlobalSymbols(List<ParsedProgram> programs, ILogger logger) {
        List<string> errors = [];
        Dictionary<string, ParsedProgram> owners = [];
        foreach(ParsedProgram program in programs) {
            foreach((string name, int lineNumber) in program.GlobalDeclarations) {
                if(program.SymbolTable.Resolve(name) is not { } label) {
                    logger.Warning("ParsedPrograms", $"{program.File.Name}:{lineNumber} Global symbol '{name}' is not defined in this file.");
                    continue;
                }

                if(owners.TryGetValue(name, out ParsedProgram? owner)) {
                    if(owner != program) {
                        errors.Add($"{program.File.Name}:{lineNumber} Duplicate global symbol '{name}' (already defined in {owner.File.Name})");
                    }
                    continue;
                }

                owners[name] = program;
                _ = this.GlobalSymbols.Add(label);
            }
        }
        return errors;
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
    /// 全ファイルのデータセグメントのバイト数の合計 (<c>.space</c>による空き領域を含む)
    /// </summary>
    /// <remarks>データセグメントは<see cref="DataSegment.DataSegmentBase"/>から連続して配置される</remarks>
    public uint DataSegmentSize { get; }

    /// <summary>
    /// 統合されたデータセグメントのメモリイメージ
    /// </summary>
    public Dictionary<Address, byte> MemoryImage { get; } = [];

    /// <summary>
    /// 命令アドレスから命令を取得する
    /// MIPS例外が発生しうる (RI, AdEL) が，例外の適用は呼び出し側が行う
    /// </summary>
    /// <param name="pc">命令アドレス</param>
    /// <param name="isKernelMode">カーネルモードかどうか</param>
    /// <param name="instruction">取得できた場合は命令</param>
    /// <param name="fault">取得できなかった場合は発生させる例外</param>
    /// <returns>取得できた場合は<see langword="true"/></returns>
    public bool TryGetInstruction(Address pc, bool isKernelMode, [NotNullWhen(true)] out IInstruction? instruction, out ExceptionRequest fault) {
        instruction = null;
        fault = default;

        // 有効なアドレスか確認
        if((pc.Addr & 0b11) != 0) {
            fault = new ExceptionRequest(ExcCode.AdEL, pc);
            return false;
        }

        int globalIdx = (int)((pc.Addr - (isKernelMode ? TextSegment.KernelTextSegmentBase.Addr : TextSegment.TextSegmentBase.Addr)) / 4);
        // 有効な範囲か確認
        if((isKernelMode ? this.KernelInstructionCount : this.UserInstructionCount) <= globalIdx) {
            // 書き込まれていない範囲は無効な命令で埋まっていると見なす．アドレス例外ではないので BadVAddr は変えない
            fault = new ExceptionRequest(ExcCode.RI, null);
            return false;
        }

        int[] cumulativeLengths = isKernelMode ? this._kernelTextCumulativeLengths : this._textCumulativeLengths;
        int programIdx = FindProgramIndex(cumulativeLengths, globalIdx);
        int localIdx = 0 < programIdx ? globalIdx - cumulativeLengths[programIdx - 1] : globalIdx;

        instruction = isKernelMode
            ? this._programs[programIdx].KernelTextSegment.Instructions[localIdx]
            : this._programs[programIdx].TextSegment.Instructions[localIdx];
        return true;
    }


    /// <summary>
    /// そのPCが指す命令のソースを返す
    /// </summary>
    /// <param name="pc">アドレス</param>
    /// <returns>ファイルと1-indexの行番号</returns>
    public (FileInfo? file, int lineNumber) GetSourceInfo(Address pc) {
        // 有効なアドレスか確認
        if((pc.Addr & 0b11) != 0) {
            return (null, 0);
        }

        bool isKernelMode = TextSegment.KernelTextSegmentBase <= pc;

        int globalIdx = (int)((pc.Addr - (isKernelMode ? TextSegment.KernelTextSegmentBase.Addr : TextSegment.TextSegmentBase.Addr)) / 4);
        // 有効な範囲か確認
        if((isKernelMode ? this.KernelInstructionCount : this.UserInstructionCount) <= globalIdx) {
            return (null, 0);
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
    /// 実行時にPCとカーネルモードに基づいて，所属ファイルのシンボルテーブル，グローバルシンボルの順にラベルを解決するデリゲートを生成する
    /// </summary>
    public Func<string, Address, bool, Label?> CreateResolver() {
        return (name, pc, isKernel) => {
            // 実行中pcなので，かならず有効な範囲である
            int globalIdx = (int)((pc.Addr - (isKernel ? TextSegment.KernelTextSegmentBase.Addr : TextSegment.TextSegmentBase.Addr)) / 4);
            int[] lengths = isKernel
                ? this._kernelTextCumulativeLengths
                : this._textCumulativeLengths;
            int progIdx = FindProgramIndex(lengths, globalIdx);
            return this._resolvers[progIdx].Resolve(name);
        };
    }


    /// <summary>
    /// 全プログラムからラベルを検索する
    /// </summary>
    /// <remarks>グローバルシンボルを優先し，なければ指定順で最初に見つかったファイルのラベルを返す</remarks>
    public Label? ResolveFromAll(string name) {
        if(this.GlobalSymbols.Resolve(name) is { } global) {
            return global;
        }
        foreach(ParsedProgram program in this._programs) {
            Label? label = program.SymbolTable.Resolve(name);
            if(label is not null) {
                return label;
            }
        }
        return null;
    }


    /// <summary>
    /// ファイル名でプログラム配列を検索し，一致するインデックスを返す
    /// </summary>
    private bool TryFindProgramIndexByFile(FileInfo file, out int fileIndex) {
        for(fileIndex = 0; fileIndex < this._programs.Length; fileIndex++) {
            if(string.Equals(this._programs[fileIndex].File.FullName, file.FullName, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }
        fileIndex = -1;
        return false;
    }

    /// <summary>
    /// 指定ファイル・行番号に対応する先頭の命令アドレスを返す
    /// </summary>
    /// <param name="file">ソースファイル</param>
    /// <param name="lineNumber">1-indexedの行番号</param>
    /// <returns>該当する命令のアドレス．見つからない場合はnull</returns>
    public Address? GetAddressForLine(FileInfo file, int lineNumber) {
        if(!this.TryFindProgramIndexByFile(file, out int fileIndex)) {
            return null;
        }

        // まずはユーザーテキストセグメントを検索
        ReadOnlySpan<IInstruction> instructions = this._programs[fileIndex].TextSegment.Instructions;
        for(int localIndex = 0; localIndex < instructions.Length; localIndex++) {
            if(instructions[localIndex].SourceLine == lineNumber) {
                return new Address((uint)((fileIndex == 0 ? 0 : this._textCumulativeLengths[fileIndex - 1]) + localIndex) * 4) + TextSegment.TextSegmentBase;
            }

        }
        // 次にカーネルテキストセグメントを検索
        instructions = this._programs[fileIndex].KernelTextSegment.Instructions;
        for(int localIndex = 0; localIndex < instructions.Length; localIndex++) {
            if(instructions[localIndex].SourceLine == lineNumber) {
                return new Address((uint)((fileIndex == 0 ? 0 : this._kernelTextCumulativeLengths[fileIndex - 1]) + localIndex) * 4) + TextSegment.KernelTextSegmentBase;
            }

        }

        return null;
    }

    /// <summary>
    /// 指定ファイルの疑似命令の行と，展開先の命令のアドレスと表記を返す
    /// </summary>
    /// <param name="file">ソースファイル</param>
    /// <returns>行の順の配列．読み込んでいないファイルなら空</returns>
    public PseudoExpansionInfo[] GetPseudoExpansions(FileInfo file) {
        if(!this.TryFindProgramIndexByFile(file, out int fileIndex)) {
            return [];
        }

        ParsedProgram program = this._programs[fileIndex];
        int textBase = fileIndex == 0 ? 0 : this._textCumulativeLengths[fileIndex - 1];
        int kernelBase = fileIndex == 0 ? 0 : this._kernelTextCumulativeLengths[fileIndex - 1];
        return [
            .. MapPseudoExpansions(program.TextSegment, TextSegment.TextSegmentBase, textBase),
            .. MapPseudoExpansions(program.KernelTextSegment, TextSegment.KernelTextSegmentBase, kernelBase)
        ];
    }

    /// <summary>
    /// セグメント内のインデックスをアドレスにして，展開先の命令の表記と組にする
    /// </summary>
    /// <param name="segment">ファイルのテキスト系セグメント</param>
    /// <param name="segmentBase">セグメントの開始アドレス</param>
    /// <param name="globalBase">このファイルより前のファイルの命令数</param>
    private static IEnumerable<PseudoExpansionInfo> MapPseudoExpansions(TextSegment segment, Address segmentBase, int globalBase) {
        foreach(PseudoExpansion expansion in segment.PseudoExpansions) {
            PseudoExpandedInstruction[] instructions = new PseudoExpandedInstruction[expansion.Count];
            for(int i = 0; i < expansion.Count; i++) {
                int localIndex = expansion.FirstIndex + i;
                Address address = new Address((uint)(globalBase + localIndex) * 4) + segmentBase;
                instructions[i] = new PseudoExpandedInstruction(address.Addr, segment.Instructions[localIndex].Disassembly ?? "?");
            }
            yield return new PseudoExpansionInfo(expansion.SourceLine, expansion.Mnemonic, instructions);
        }
    }

    /// <summary>
    /// 指定ファイルに属する全命令のアドレスを返す
    /// </summary>
    public HashSet<Address> GetAllAddressesForFile(FileInfo file) {
        HashSet<Address> addresses = [];
        if(!this.TryFindProgramIndexByFile(file, out int fileIndex)) {
            return addresses;
        }

        int textBase = fileIndex == 0 ? 0 : this._textCumulativeLengths[fileIndex - 1];
        for(int i = 0; i < this._programs[fileIndex].TextSegment.Instructions.Length; i++) {
            addresses.Add(new Address((uint)(textBase + i) * 4) + TextSegment.TextSegmentBase);
        }

        int kernelBase = fileIndex == 0 ? 0 : this._kernelTextCumulativeLengths[fileIndex - 1];
        for(int i = 0; i < this._programs[fileIndex].KernelTextSegment.Instructions.Length; i++) {
            addresses.Add(new Address((uint)(kernelBase + i) * 4) + TextSegment.KernelTextSegmentBase);
        }
        return addresses;
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
