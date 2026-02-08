using PlusPim.Debuggers.PlusPimDbg.Instructions.Branch;
using PlusPim.Debuggers.PlusPimDbg.Instructions.Jump;
using PlusPim.Debuggers.PlusPimDbg.Instructions.RType;
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
        // ファイル名順

        // ブランチ命令
        RegisterParser(parsers, new BeqInstructionParser());
        RegisterParser(parsers, new BneInstructionParser());

        // ジャンプ命令
        RegisterParser(parsers, new JInstructionParser());
        RegisterParser(parsers, new JalInstructionParser());
        RegisterParser(parsers, new JrInstructionParser());

        // Rタイプ命令
        RegisterParser(parsers, new AddInstructionParser());
        RegisterParser(parsers, new AdduInstructionParser());

        RegisterParser(parsers, new AndInstructionParser());
        RegisterParser(parsers, new OrInstructionParser());

        RegisterParser(parsers, new SllInstructionParser());

        RegisterParser(parsers, new SltInstructionParser());

        RegisterParser(parsers, new SubInstructionParser());


        return new InstructionRegistry(parsers);
    }

    private static void RegisterParser(Dictionary<string, IInstructionParser> parsers, IInstructionParser parser) {
        parsers[parser.Mnemonic] = parser;
    }

    /// <summary>
    /// 指定された行を<see cref="IInstruction"/>への解析を試みる
    /// </summary>
    /// <param name="assemblyLine">行</param>
    /// <param name="instruction">成功の場合は<see cref="IInstruction"/>が返却される</param>
    /// <returns>成功なら<see langword="true"/></returns>
    public bool TryParse(string assemblyLine, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;

        Match match = AssemblyLinePattern().Match(assemblyLine);
        if(!match.Success) {
            return false;
        }

        string op = match.Groups["op"].Value;
        string operands = match.Groups["operands"].Value;

        return this._parsers.TryGetValue(op, out IInstructionParser? parser) && parser.TryParse(operands, out instruction);
    }
}
