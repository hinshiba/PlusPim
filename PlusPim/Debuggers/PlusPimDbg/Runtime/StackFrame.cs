using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// 内部向けスタックフレームの情報
/// </summary>
internal sealed class StackFrame(InstructionIndex currentPC, Label label, RegisterFile registers, uint hi, uint lo) {
    /// <summary>
    /// 現時点でのライブPCか，jalによって凍結されたPC
    /// </summary>
    public InstructionIndex CurrentPC { get; } = currentPC;

    /// <summary>
    /// このスタックフレームが属すると考えられる関数のラベル
    /// </summary>
    public Label Label { get; } = label;

    /// <summary>
    /// 上記PCに対応する時点での汎用レジスタのスナップショット
    /// </summary>
    public RegisterFile Registers { get; } = registers;

    /// <summary>
    /// 上記PCに対応する時点でのHIレジスタのスナップショット
    /// </summary>
    public uint HISnapshot { get; } = hi;

    /// <summary>
    /// 上記PCに対応する時点でのLOレジスタのスナップショット
    /// </summary>
    public uint LOSnapshot { get; } = lo;
}
