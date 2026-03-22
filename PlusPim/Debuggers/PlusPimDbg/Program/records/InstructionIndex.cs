using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Program.records;

/// <summary>
/// 命令インデックスを表す値型
/// </summary>
/// <param name="Idx">命令インデックス</param>
internal record struct InstructionIndex(int Idx) {

    /// <summary>
    /// アドレスから命令インデックスを生成する
    /// 不適切なアドレスであった場合はAdEL例外が発生する
    /// </summary>
    /// <param name="Addr">アドレス</param>
    /// <param name="context">コンテキスト</param>
    /// <returns>4バイトアライメントでない場合<see langword="null"/>，または命令インデックス</returns>
    public static InstructionIndex? FromAddress(Address Addr, RuntimeContext context) {
        if((Addr.Addr & 0b11) != 0) {
            context.RaiseException(ExcCode.AdEL, Addr);
            return null;
        }

        return context.IsKernelMode()
            ? new(unchecked((int)(Addr - TextSegment.TextSegmentBase).Addr) / 4)
            : new(unchecked((int)(Addr - TextSegment.TextSegmentBase).Addr) / 4);
    }

    /// <summary>
    /// アドレスから命令インデックスを生成する
    /// </summary>
    /// <param name="Addr">アドレス</param>
    /// <param name="IsKernelMode">カーネルモードかどうか</param>
    /// <returns>4バイトアライメントでない場合<see langword="null"/>，または命令インデックス</returns>
    public static InstructionIndex? FromAddress(Address Addr, bool IsKernelMode) {
        return IsKernelMode
            ? new(unchecked((int)(Addr - TextSegment.TextSegmentBase).Addr) / 4)
            : new(unchecked((int)(Addr - TextSegment.TextSegmentBase).Addr) / 4);
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
