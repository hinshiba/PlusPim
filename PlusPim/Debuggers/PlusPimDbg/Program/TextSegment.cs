using PlusPim.Debuggers.PlusPimDbg.Instructions;
using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

internal sealed class TextSegment {
    public static readonly Address TextSegmentBase = new(0x400000);

    private readonly IInstruction[] _instructions;
    private readonly int[] _sourceLines;

    private readonly SymbolTable _symbolTable;

    public TextSegment(List<IInstruction> instructions, List<int> _sourceLines, SymbolTable symbolTable) {
        this._instructions = instructions.ToArray();
        this._sourceLines = _sourceLines.ToArray();
        this._symbolTable = symbolTable;
    }
}
