using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// 内部向けスタックフレームの情報
/// </summary>
internal sealed class CallStackFrame(InstructionIndex returnPC, string subroutineLabel, RegisterFile registerSnapshot, int hi, int lo) {
    public InstructionIndex ReturnPC { get; } = returnPC;
    public string SubroutineLabel { get; } = subroutineLabel;
    public RegisterFile RegisterSnapshot { get; } = registerSnapshot;
    public int HISnapshot { get; } = hi;
    public int LOSnapshot { get; } = lo;
}
