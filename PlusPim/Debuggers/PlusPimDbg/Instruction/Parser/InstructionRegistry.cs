using PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions.Jump;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Pseudo;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;

internal sealed partial class InstructionRegistry {
    [GeneratedRegex(@"^(?<op>\w+!?)(\s+(?<operands>.+))?$")]
    private static partial Regex AssemblyLinePattern();

    public static InstructionRegistry Default => field ??= CreateDefault();

    private readonly Dictionary<string, IInstructionParser> _parsers;
    private readonly Dictionary<string, IPseudoInstructionParser> _pseudoParsers;

    private InstructionRegistry(
        Dictionary<string, IInstructionParser> parsers,
        Dictionary<string, IPseudoInstructionParser> pseudoParsers) {
        this._parsers = parsers;
        this._pseudoParsers = pseudoParsers;
    }

    public static InstructionRegistry CreateDefault() {
        Dictionary<string, IInstructionParser> parsers = new(StringComparer.OrdinalIgnoreCase);
        RegisterParser(parsers, new JInstructionParser());
        RegisterParser(parsers, new JalInstructionParser());
        RegisterParser(parsers, new JrInstructionParser());

        Dictionary<string, IPseudoInstructionParser> pseudoParsers = new(StringComparer.OrdinalIgnoreCase);
        RegisterPseudoParser(pseudoParsers, new NopInstructionParser());
        RegisterPseudoParser(pseudoParsers, new MoveInstructionParser());
        RegisterPseudoParser(pseudoParsers, new LiInstructionParser());
        RegisterPseudoParser(pseudoParsers, new LaInstructionParser());

        InstructionRegistry registry = new(parsers, pseudoParsers);
        registry.RegisterLambdaInstructions();
        return registry;
    }

    private static void RegisterParser(Dictionary<string, IInstructionParser> parsers, IInstructionParser parser) {
        parsers[parser.Mnemonic] = parser;
    }

    private static void RegisterPseudoParser(Dictionary<string, IPseudoInstructionParser> parsers, IPseudoInstructionParser parser) {
        parsers[parser.Mnemonic] = parser;
    }

    /// <summary>
    /// ラムダベースの命令をファクトリから登録する
    /// </summary>
    private void Register(string mnemonic, Func<string, IInstructionParser> factory) {
        this._parsers[mnemonic] = factory(mnemonic);
    }

    /// <summary>
    /// 符号付き加算．結果が32bit符号付きの範囲を超えた場合のみ<see cref="OverflowException"/>を投げる
    /// </summary>
    /// <remarks>
    /// <see langword="uint"/>から<see langword="int"/>への変換で例外が発生するのを防ぐため，変換は<c>unchecked</c>で行う
    /// </remarks>
    private static uint AddSigned(uint lhs, uint rhs) {
        int l = unchecked((int)lhs);
        int r = unchecked((int)rhs);
        return unchecked((uint)checked(l + r));
    }

    /// <summary>
    /// 符号付き減算．結果が32bit符号付きの範囲を超えた場合のみ<see cref="OverflowException"/>を投げる
    /// </summary>
    /// <remarks><see cref="AddSigned"/>と同様の理由</remarks>
    private static uint SubSigned(uint lhs, uint rhs) {
        int l = unchecked((int)lhs);
        int r = unchecked((int)rhs);
        return unchecked((uint)checked(l - r));
    }

    private void RegisterLambdaInstructions() {
        // R-Type 3レジスタ
        this.Register("add", RType3RegInstruction.CreateParser(AddSigned));
        this.Register("addu", RType3RegInstruction.CreateParser((rs, rt) => unchecked(rs + rt)));
        this.Register("sub", RType3RegInstruction.CreateParser(SubSigned));
        this.Register("subu", RType3RegInstruction.CreateParser((rs, rt) => unchecked(rs - rt)));
        this.Register("and", RType3RegInstruction.CreateParser((rs, rt) => rs & rt));
        this.Register("or", RType3RegInstruction.CreateParser((rs, rt) => rs | rt));
        this.Register("xor", RType3RegInstruction.CreateParser((rs, rt) => rs ^ rt));
        this.Register("nor", RType3RegInstruction.CreateParser((rs, rt) => ~(rs | rt)));
        this.Register("slt", RType3RegInstruction.CreateParser((rs, rt) => unchecked((int)rs) < unchecked((int)rt) ? 1u : 0u));
        this.Register("sltu", RType3RegInstruction.CreateParser((rs, rt) => rs < rt ? 1u : 0u));

        // R-Type シフト即値
        this.Register("sll", RTypeShiftImmInstruction.CreateParser((rt, shamt) => rt << shamt));
        this.Register("srl", RTypeShiftImmInstruction.CreateParser((rt, shamt) => rt >> shamt));
        this.Register("sra", RTypeShiftImmInstruction.CreateParser((rt, shamt) => unchecked((uint)((int)rt >> shamt))));

        // R-Type シフト可変
        this.Register("sllv", RTypeShiftVarInstruction.CreateParser((rt, shift) => rt << shift));
        this.Register("srlv", RTypeShiftVarInstruction.CreateParser((rt, shift) => rt >> shift));
        this.Register("srav", RTypeShiftVarInstruction.CreateParser((rt, shift) => unchecked((uint)((int)rt >> shift))));

        // I-Type
        this.Register("addi", ITypeInstruction.CreateParser((rs, imm) => AddSigned(rs, unchecked((uint)imm.ToSInt()))));
        this.Register("addiu", ITypeInstruction.CreateParser((rs, imm) => unchecked((uint)((int)rs + imm.ToSInt()))));
        this.Register("andi", ITypeInstruction.CreateParser((rs, imm) => rs & imm.ToUInt()));
        this.Register("ori", ITypeInstruction.CreateParser((rs, imm) => rs | imm.ToUInt()));
        this.Register("xori", ITypeInstruction.CreateParser((rs, imm) => rs ^ imm.ToUInt()));
        this.Register("slti", ITypeInstruction.CreateParser((rs, imm) => unchecked((int)rs) < imm.ToSInt() ? 1u : 0u));
        this.Register("sltiu", ITypeInstruction.CreateParser((rs, imm) => rs < unchecked((uint)imm.ToSInt()) ? 1u : 0u));
        this.Register("lui", ITypeInstruction.CreateRegImmParser(imm => unchecked(imm.ToUInt() << 16)));

        // Branch
        this.Register("beq", BranchInstruction.CreateParser((rs, rt) => rs == rt));
        this.Register("bne", BranchInstruction.CreateParser((rs, rt) => rs != rt));
        this.Register("bgez", BranchInstruction.CreateZeroParser(rs => rs >= 0));
        this.Register("bgtz", BranchInstruction.CreateZeroParser(rs => rs > 0));
        this.Register("blez", BranchInstruction.CreateZeroParser(rs => rs <= 0));
        this.Register("bltz", BranchInstruction.CreateZeroParser(rs => rs < 0));

        // MulDiv
        this.Register("mult", MulDivInstruction.CreateParser((rs, rt) => {
            long result = unchecked((long)(int)rs * (int)rt);
            return unchecked(((uint)(result >> 32), (uint)(result & 0xFFFFFFFF)));
        }));
        this.Register("multu", MulDivInstruction.CreateParser((rs, rt) => {
            ulong result = unchecked((ulong)rs * rt);
            return unchecked(((uint)(result >> 32), (uint)(result & 0xFFFFFFFF)));
        }));

        // ゼロ除算と0x80000000 / -1 は計算前にランタイムエラーとする
        this.Register("div", MulDivInstruction.CreateParser((rs, rt) => unchecked(
            ((uint)((int)rs % (int)rt), (uint)((int)rs / (int)rt))),
            (rs, rt) => rt == 0 ? RuntimeErrorKind.DivisionByZero
                : rs == 0x80000000 && rt == 0xFFFFFFFF ? RuntimeErrorKind.DivisionOverflow
                : null));
        this.Register("divu", MulDivInstruction.CreateParser((rs, rt) => unchecked(
            (rs % rt, rs / rt)),
            (rs, rt) => rt == 0 ? RuntimeErrorKind.DivisionByZero : null));

        // LoHi
        this.Register("mfhi", LoHiRegisterInstruction.CreateParser(true, true));
        this.Register("mflo", LoHiRegisterInstruction.CreateParser(false, true));

        this.Register("mthi", LoHiRegisterInstruction.CreateParser(true, false));
        this.Register("mtlo", LoHiRegisterInstruction.CreateParser(false, false));

        // Memory
        this.Register("lb", MemoryInstruction.CreateParser(byteNum: 1, isWrite: false, isSign: true));
        this.Register("lbu", MemoryInstruction.CreateParser(byteNum: 1, isWrite: false));
        this.Register("lh", MemoryInstruction.CreateParser(byteNum: 2, isWrite: false, isSign: true));
        this.Register("lhu", MemoryInstruction.CreateParser(byteNum: 2, isWrite: false));
        this.Register("lw", MemoryInstruction.CreateParser(byteNum: 4, isWrite: false));
        this.Register("sb", MemoryInstruction.CreateParser(byteNum: 1, isWrite: true));
        this.Register("sh", MemoryInstruction.CreateParser(byteNum: 2, isWrite: true));
        this.Register("sw", MemoryInstruction.CreateParser(byteNum: 4, isWrite: true));
        this.Register("lwl", UnalignedMemoryInstruction.CreateParser(isWrite: false, isLeft: true));
        this.Register("lwr", UnalignedMemoryInstruction.CreateParser(isWrite: false, isLeft: false));
        this.Register("swl", UnalignedMemoryInstruction.CreateParser(isWrite: true, isLeft: true));
        this.Register("swr", UnalignedMemoryInstruction.CreateParser(isWrite: true, isLeft: false));

        // Syscall等
        this.Register("syscall", SyscallInstruction.CreateParser());
        this.Register("runtime_call!", RuntimeCall.CreateParser());

        // 例外系
        this.Register("mfc0", CP0RegisterInstruction.CreateParser(isFrom: true));
        this.Register("mtc0", CP0RegisterInstruction.CreateParser(isFrom: false));
        this.Register("eret", EretInstruction.CreateParser());
        this.Register("break", BreakInstruction.CreateParser());
    }

    /// <summary>
    /// 行が疑似命令ならそのニーモニックを返す
    /// </summary>
    /// <param name="assemblyLine">アセンブリ行</param>
    /// <returns>疑似命令のニーモニック (小文字)．疑似命令でなければ<see langword="null"/></returns>
    /// <remarks>オペランドは解析しない</remarks>
    public string? GetPseudoMnemonic(string assemblyLine) {
        Match match = AssemblyLinePattern().Match(assemblyLine);
        return match.Success && this._pseudoParsers.TryGetValue(match.Groups["op"].Value, out IPseudoInstructionParser? pseudo)
            ? pseudo.Mnemonic
            : null;
    }

    /// <summary>
    /// 指定された行の展開後の命令数を返す
    /// </summary>
    /// <param name="assemblyLine">アセンブリ行</param>
    /// <returns>命令数．解析不能な場合は0</returns>
    public int GetInstructionCount(string assemblyLine) {
        return this.TryParseLine(assemblyLine, 0, out ParsedLine? line) ? line.Size : 0;
    }

    /// <summary>
    /// 指定された行を<see cref="IInstruction"/>への解析を試みる
    /// </summary>
    /// <param name="assemblyLine">行</param>
    /// <param name="lineNumber">行番号(1-based)</param>
    /// <param name="instruction">成功の場合は<see cref="IInstruction"/>が返却される</param>
    /// <returns>成功なら<see langword="true"/></returns>
    public bool TryParse(string assemblyLine, int lineNumber, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;

        // アセンブリの行にマッチするか探索
        Match match = AssemblyLinePattern().Match(assemblyLine);
        if(!match.Success) {
            return false;
        }

        // オペコードとオペランドを分割
        string op = match.Groups["op"].Value;
        string operands = match.Groups["operands"].Value;

        // オペコードに対応するパーサーを探して，あればそれでオペランドを解析する
        return this._parsers.TryGetValue(op, out IInstructionParser? parser) && parser.TryParse(operands, lineNumber, out instruction);
    }

    /// <summary>
    /// 指定された行を，展開後の命令数が確定した<see cref="ParsedLine"/>に解析する(疑似命令を含む)
    /// </summary>
    /// <param name="assemblyLine">行</param>
    /// <param name="lineNumber">行番号(1-based)</param>
    /// <param name="line">成功の場合は解析済みの行が返却される</param>
    /// <returns>成功なら<see langword="true"/></returns>
    public bool TryParseLine(string assemblyLine, int lineNumber, [MaybeNullWhen(false)] out ParsedLine line) {
        line = null;

        Match match = AssemblyLinePattern().Match(assemblyLine);
        if(!match.Success) {
            return false;
        }

        string op = match.Groups["op"].Value;
        string operands = match.Groups["operands"].Value;

        // 疑似命令を先に試す
        if(this._pseudoParsers.TryGetValue(op, out IPseudoInstructionParser? pseudo)) {
            return pseudo.TryParse(operands, lineNumber, out line);
        }

        // 通常の命令
        if(this._parsers.TryGetValue(op, out IInstructionParser? parser)
            && parser.TryParse(operands, lineNumber, out IInstruction? instruction)) {
            line = ParsedLine.Fixed(instruction);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 指定された行を実命令列に解析する(疑似命令の展開を含む)
    /// </summary>
    /// <param name="assemblyLine">行</param>
    /// <param name="lineNumber">行番号(1-based)</param>
    /// <param name="symbols">シンボルの解決に使う</param>
    /// <param name="instructions">成功の場合は命令列が返却される</param>
    /// <returns>成功なら<see langword="true"/>．未定義のシンボルを参照する場合は<see langword="false"/></returns>
    public bool TryParseAll(string assemblyLine, int lineNumber, ISymbolResolver symbols,
                            [MaybeNullWhen(false)] out IInstruction[] instructions) {
        instructions = null;

        if(!this.TryParseLine(assemblyLine, lineNumber, out ParsedLine? line)) {
            return false;
        }

        IInstruction[] materialized = line.Materialize(symbols, out string? unresolved);
        if(unresolved is not null) {
            return false;
        }

        instructions = materialized;
        return true;
    }
}
