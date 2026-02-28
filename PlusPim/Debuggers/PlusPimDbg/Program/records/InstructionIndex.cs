namespace PlusPim.Debuggers.PlusPimDbg.Program.records;

/// <summary>
/// 命令インデックスを表す値型
/// </summary>
/// <param name="Idx">命令インデックス</param>
internal record struct InstructionIndex(int Idx) {

    /// <summary>
    /// アドレスから命令インデックスを生成する
    /// </summary>
    /// <param name="Addr">アドレス</param>
    /// <returns>4バイトアライメントでない場合<see langword="null"/>，または命令インデックス</returns>
    public static InstructionIndex? FromAddress(Address Addr) {
        return (Addr.Addr & 0b11) != 0 ? null : new InstructionIndex((Addr.Addr - TextSegment.TextSegmentBase.Addr) / 4);
    }

    public static InstructionIndex operator +(InstructionIndex lhs, int rhs) {
        lhs.Idx += rhs;
        return lhs;
    }

    public static InstructionIndex operator ++(InstructionIndex val) {
        val.Idx++;
        return val;
    }

    public static InstructionIndex operator --(InstructionIndex val) {
        val.Idx--;
        return val;
    }
}
