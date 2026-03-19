namespace PlusPim.Debuggers.PlusPimDbg.Program.records;

/// <summary>
/// 命令インデックスを表す値型
/// </summary>
/// <param name="Idx">命令インデックス</param>
internal record struct InstructionIndex(int Idx) {

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
