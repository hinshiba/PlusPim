using PlusPim.Debuggers.PlusPimDbg.Instructions;
using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

internal sealed partial class TextSegment {
    public static readonly Address TextSegmentBase = new(0x400000);

    private readonly IInstruction[] _instructions;
    private readonly int[] _sourceLines;

    public TextSegment(List<IInstruction> instructions, List<int> _sourceLines) {
        this._instructions = instructions.ToArray();
        this._sourceLines = _sourceLines.ToArray();
    }
}
