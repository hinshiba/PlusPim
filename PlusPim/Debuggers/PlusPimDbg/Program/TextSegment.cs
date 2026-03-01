using PlusPim.Debuggers.PlusPimDbg.Instructions;
using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// テキストセグメントを表現する
/// </summary>
/// <param name="instructions">命令列</param>
internal sealed class TextSegment(List<IInstruction> instructions) {
    public static readonly Address TextSegmentBase = new(0x400000);

    public ReadOnlySpan<IInstruction> Instructions => this._instructions.AsSpan();
    private readonly IInstruction[] _instructions = [.. instructions];
}
