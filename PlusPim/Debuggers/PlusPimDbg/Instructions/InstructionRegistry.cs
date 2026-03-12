using PlusPim.Debuggers.PlusPimDbg.Program;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

internal sealed partial class InstructionRegistry {
    [GeneratedRegex(@"^(?<op>\w+)(\s+(?<operands>.+))?$")]
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

        IEnumerable<Type> parserTypes = typeof(InstructionRegistry).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                && t.IsAssignableTo(typeof(IInstructionParser)));

        foreach(Type type in parserTypes) {
            IInstructionParser parser = (IInstructionParser)Activator.CreateInstance(type)!;
            parsers[parser.Mnemonic] = parser;
        }

        Dictionary<string, IPseudoInstructionParser> pseudoParsers = new(StringComparer.OrdinalIgnoreCase);

        IEnumerable<Type> pseudoParserTypes = typeof(InstructionRegistry).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                && t.IsAssignableTo(typeof(IPseudoInstructionParser)));

        foreach(Type type in pseudoParserTypes) {
            IPseudoInstructionParser pseudoParser = (IPseudoInstructionParser)Activator.CreateInstance(type)!;
            pseudoParsers[pseudoParser.Mnemonic] = pseudoParser;
        }

        return new InstructionRegistry(parsers, pseudoParsers);
    }

    /// <summary>
    /// 指定された行の展開後の命令数を返す
    /// </summary>
    /// <param name="assemblyLine">アセンブリ行</param>
    /// <returns>命令数．解析不能な場合は0</returns>
    public int GetInstructionCount(string assemblyLine) {
        if(SyscallInstructionParser.Mnemonic.Equals(assemblyLine, StringComparison.OrdinalIgnoreCase)) {
            return 1;
        }

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
            // syscallの場合がある
            if(SyscallInstructionParser.Mnemonic.Equals(assemblyLine)) {
                instruction = new SyscallInstruction(lineIndex);
                return true;
            }
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
            if(SyscallInstructionParser.Mnemonic.Equals(assemblyLine, StringComparison.OrdinalIgnoreCase)) {
                instructions = [new SyscallInstruction(lineIndex)];
                return true;
            }

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
