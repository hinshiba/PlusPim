namespace PlusPim.Debuggers.PlusPimDbg;

/// <summary>
/// 内部向けスタックフレームの情報
/// </summary>
internal sealed class CallStackFrame(ProgramCounter returnPC, string subroutineLabel, RegisterFile registerSnapshot, int hi, int lo) {
    public ProgramCounter ReturnPC { get; } = returnPC;
    public string SubroutineLabel { get; } = subroutineLabel;
    public RegisterFile RegisterSnapshot { get; } = registerSnapshot;
    public int HISnapshot { get; } = hi;
    public int LOSnapshot { get; } = lo;
}
