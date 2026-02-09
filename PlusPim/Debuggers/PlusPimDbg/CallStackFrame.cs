namespace PlusPim.Debuggers.PlusPimDbg;

/// <summary>
/// 内部向けスタッフフレームの情報
/// </summary>
internal sealed class CallStackFrame(int executionIndex, string subroutineLabel, int[] registerSnapshot, int hi, int lo) {
    public int ExecutionIndex { get; } = executionIndex;
    public string SubroutineLabel { get; } = subroutineLabel;
    public int[] RegisterSnapshot { get; } = registerSnapshot;
    public int HISnapshot { get; } = hi;
    public int LOSnapshot { get; } = lo;
}
