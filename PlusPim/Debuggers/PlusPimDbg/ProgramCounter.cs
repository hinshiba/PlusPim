namespace PlusPim.Debuggers.PlusPimDbg;

/// <summary>
/// プログラムカウンタを表す値型
/// </summary>
/// <remarks>内部インデックスとMIPSアドレスの変換を一元管理する</remarks>
internal readonly record struct ProgramCounter {
    private const int TextSegmentBase = 0x00400000;

    /// <summary>
    /// 命令配列上のインデックス（0始まり）
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// MIPSアドレス空間上のPC値
    /// </summary>
    public int Address => this.Index + TextSegmentBase;

    private ProgramCounter(int index) {
        this.Index = index;
    }

    public static ProgramCounter FromIndex(int index) {
        return new ProgramCounter(index);
    }

    public static ProgramCounter FromAddress(int address) {
        return new ProgramCounter(address - TextSegmentBase);
    }

    public ProgramCounter Next => new(this.Index + 1);
    public ProgramCounter Previous => new(this.Index - 1);
}
