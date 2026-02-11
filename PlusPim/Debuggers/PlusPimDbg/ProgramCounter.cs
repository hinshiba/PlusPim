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
    public int Address => (this.Index * 4) + TextSegmentBase;

    private ProgramCounter(int index) {
        this.Index = index;
    }

    /// <summary>
    /// 命令インデックスから<see cref="ProgramCounter"/>を生成する
    /// </summary>
    /// <param name="index">インデックス</param>
    /// <returns>作成された<see cref="ProgramCounter"/></returns>
    public static ProgramCounter FromIndex(int index) {
        return new ProgramCounter(index);
    }

    /// <summary>
    /// 命令アドレスから<see cref="ProgramCounter"/>を生成する
    /// </summary>
    /// <param name="address">アドレス</param>
    /// <returns>作成された<see cref="ProgramCounter"/></returns>
    /// <remarks>アドレスは4バイト境界にあるものとする</remarks>
    public static ProgramCounter FromAddress(int address) {
        return new ProgramCounter((address - TextSegmentBase) / 4);
    }

    /// <summary>
    /// 命令インデックスを1進めた新しい<see cref="ProgramCounter"/>を取得する
    /// </summary>
    public ProgramCounter Next => new(this.Index + 1);

    /// <summary>
    /// 命令インデックスを1戻した新しい<see cref="ProgramCounter"/>を取得する
    /// </summary>
    public ProgramCounter Previous => new(this.Index - 1);
}
