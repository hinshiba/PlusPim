using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

internal sealed partial class InstructionRegistry {
    [GeneratedRegex(@"^(?<op>\w+)\s+(?<operands>.+)$")]
    private static partial Regex AssemblyLinePattern();

    public static InstructionRegistry Default => field ??= CreateDefault();

    private readonly Dictionary<string, IInstructionParser> _parsers;

    private InstructionRegistry(Dictionary<string, IInstructionParser> parsers) {
        this._parsers = parsers;
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

        return new InstructionRegistry(parsers);
    }

    /// <summary>
    /// 指定された行を<see cref="IInstruction"/>への解析を試みる
    /// </summary>
    /// <param name="assemblyLine">行</param>
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
}
