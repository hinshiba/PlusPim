using PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;
using PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Jump;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Pseudo;
using PlusPim.Debuggers.PlusPimDbg.Program;
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

    private void RegisterLambdaInstructions() {
        // R-Type 3レジスタ
        this.Register("add", RType3RegInstruction.CreateParser((rs, rt) => (uint)checked((int)rs + (int)rt)));
        this.Register("addu", RType3RegInstruction.CreateParser((rs, rt) => rs + rt));
        this.Register("sub", RType3RegInstruction.CreateParser((rs, rt) => (uint)checked((int)rs - (int)rt)));
        this.Register("subu", RType3RegInstruction.CreateParser((rs, rt) => rs - rt));
        this.Register("and", RType3RegInstruction.CreateParser((rs, rt) => rs & rt));
        this.Register("or", RType3RegInstruction.CreateParser((rs, rt) => rs | rt));
        this.Register("xor", RType3RegInstruction.CreateParser((rs, rt) => rs ^ rt));
        this.Register("nor", RType3RegInstruction.CreateParser((rs, rt) => ~(rs | rt)));
        this.Register("slt", RType3RegInstruction.CreateParser((rs, rt) => (int)rs < (int)rt ? 1u : 0u));
        this.Register("sltu", RType3RegInstruction.CreateParser((rs, rt) => rs < rt ? 1u : 0u));

        // R-Type シフト即値
        this.Register("sll", RTypeShiftImmInstruction.CreateParser((rt, shamt) => rt << shamt));
        this.Register("srl", RTypeShiftImmInstruction.CreateParser((rt, shamt) => rt >> shamt));
        this.Register("sra", RTypeShiftImmInstruction.CreateParser((rt, shamt) => (uint)((int)rt >> shamt)));

        // R-Type シフト可変
        this.Register("sllv", RTypeShiftVarInstruction.CreateParser((rt, shift) => rt << shift));
        this.Register("srlv", RTypeShiftVarInstruction.CreateParser((rt, shift) => rt >> shift));
        this.Register("srav", RTypeShiftVarInstruction.CreateParser((rt, shift) => (uint)((int)rt >> shift)));

        // I-Type
        this.Register("addi", ITypeInstruction.CreateParser((rs, imm) => (uint)checked((int)rs + imm.ToSInt())));
        this.Register("addiu", ITypeInstruction.CreateParser((rs, imm) => (uint)((int)rs + imm.ToSInt())));
        this.Register("andi", ITypeInstruction.CreateParser((rs, imm) => rs & imm.ToUInt()));
        this.Register("ori", ITypeInstruction.CreateParser((rs, imm) => rs | imm.ToUInt()));
        this.Register("xori", ITypeInstruction.CreateParser((rs, imm) => rs ^ imm.ToUInt()));
        this.Register("slti", ITypeInstruction.CreateParser((rs, imm) => (uint)((int)rs < imm.ToSInt() ? 1 : 0)));
        this.Register("sltiu", ITypeInstruction.CreateParser((rs, imm) => rs < (ushort)imm.ToSInt() ? 1u : 0u));
        this.Register("lui", ITypeInstruction.CreateRegImmParser(imm => unchecked(imm.ToUInt() << 16)));

        // Branch
        this.Register("beq", BranchInstruction.CreateParser((rs, rt) => rs == rt));
        this.Register("bne", BranchInstruction.CreateParser((rs, rt) => rs != rt));

        // MulDiv
        this.Register("mult", MulDivInstruction.CreateParser((rs, rt) => {
            long result = (long)(int)rs * (int)rt;
            return ((uint)(result >> 32), (uint)(result & 0xFFFFFFFF));
        }));
        this.Register("multu", MulDivInstruction.CreateParser((rs, rt) => {
            ulong result = (ulong)rs * rt;
            return ((uint)(result >> 32), (uint)(result & 0xFFFFFFFF));
        }));

        this.Register("div", MulDivInstruction.CreateParser((rs, rt) =>
            ((uint)((int)rs % (int)rt), (uint)((int)rs / (int)rt))));
        this.Register("divu", MulDivInstruction.CreateParser((rs, rt) =>
            (rs % rt, rs / rt)));

        // LoHi
        this.Register("mfhi", LoHiRegisterInstruction.CreateParser(true, true));
        this.Register("mflo", LoHiRegisterInstruction.CreateParser(false, true));

        this.Register("mthi", LoHiRegisterInstruction.CreateParser(true, false));
        this.Register("mtlo", LoHiRegisterInstruction.CreateParser(false, false));

        // Memory
        this.Register("lw", MemoryInstruction.CreateParser(byteNum: 4, isWrite: false));
        this.Register("sw", MemoryInstruction.CreateParser(byteNum: 4, isWrite: true));

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
    /// 指定された行の展開後の命令数を返す
    /// </summary>
    /// <param name="assemblyLine">アセンブリ行</param>
    /// <returns>命令数．解析不能な場合は0</returns>
    public int GetInstructionCount(string assemblyLine) {
        Match match = AssemblyLinePattern().Match(assemblyLine);
        if(!match.Success) {
            return 0;
        }

        string op = match.Groups["op"].Value;

        return this._pseudoParsers.TryGetValue(op, out IPseudoInstructionParser? pseudo)
            ? pseudo.GetExpansionSize(match.Groups["operands"].Value)
            : this._parsers.ContainsKey(op) ? 1 : 0;
    }

    /// <summary>
    /// 指定された行を<see cref="IInstruction"/>への解析を試みる
    /// </summary>
    /// <param name="assemblyLine">行</param>
    /// <param name="lineIndex">行番号(1-based)</param>
    /// <param name="instruction">成功の場合は<see cref="IInstruction"/>が返却される</param>
    /// <returns>成功なら<see langword="true"/></returns>
    public bool TryParse(string assemblyLine, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
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
        return this._parsers.TryGetValue(op, out IInstructionParser? parser) && parser.TryParse(operands, lineIndex, out instruction);
    }

    /// <summary>
    /// 指定された行を実命令列に解析する(疑似命令の展開を含む)
    /// </summary>
    /// <param name="assemblyLine">行</param>
    /// <param name="lineIndex">行番号(1-based)</param>
    /// <param name="symbolTable">シンボルテーブル</param>
    /// <param name="instructions">成功の場合は命令列が返却される</param>
    /// <returns>成功なら<see langword="true"/></returns>
    public bool TryParseAll(string assemblyLine, int lineIndex, SymbolTable symbolTable,
                            [MaybeNullWhen(false)] out IInstruction[] instructions) {
        instructions = null;

        Match match = AssemblyLinePattern().Match(assemblyLine);
        if(!match.Success) {
            return false;
        }

        string op = match.Groups["op"].Value;
        string operands = match.Groups["operands"].Value;

        // 疑似命令を先に試す
        if(this._pseudoParsers.TryGetValue(op, out IPseudoInstructionParser? pseudo)) {
            return pseudo.TryExpand(operands, lineIndex, symbolTable, out instructions);
        }

        // 通常の命令
        if(this._parsers.TryGetValue(op, out IInstructionParser? parser)
            && parser.TryParse(operands, lineIndex, out IInstruction? instruction)) {
            instructions = [instruction];
            return true;
        }

        return false;
    }
}
