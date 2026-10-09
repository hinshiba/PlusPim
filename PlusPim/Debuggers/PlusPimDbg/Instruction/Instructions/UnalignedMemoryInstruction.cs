using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions;

/// <summary>
/// MIPSにおけるアライメントされていないメモリ操作命令 (lwl, lwr, swl, swr) を表すクラス
/// </summary>
/// <remarks>
/// リトルエンディアン．アドレスエラー例外は発生しない．
/// off = 実効アドレス &amp; 3 として，
/// lwl は rt の上位 off+1 バイトにアラインされたアドレスから実効アドレスまでを読み込み，
/// lwr は rt の下位 4-off バイトに実効アドレスからワード末尾までを読み込む．
/// swl は rt の上位 off+1 バイトをアラインされたアドレスから実効アドレスまでに書き込み，
/// swr は rt の下位 4-off バイトを実効アドレスからワード末尾までに書き込む
/// </remarks>
/// <param name="rt">メモリ領域とやり取りをするレジスタ</param>
/// <param name="rs">アドレスを指すレジスタ</param>
/// <param name="offset"><paramref name="rs"/>からのオフセットを示す即値</param>
/// <param name="isWrite">書き込みかどうか</param>
/// <param name="isLeft">左(lwl, swl)かどうか．<see langword="false"/>なら右(lwr, swr)</param>
/// <param name="sourceLine">行番号</param>
internal sealed class UnalignedMemoryInstruction(
    RegisterID rt, RegisterID rs, Immediate offset,
    bool isWrite, bool isLeft, int sourceLine
): IInstruction {

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = sourceLine;

    /// <summary>
    /// 逆操作のためのスタック．読み込み命令なら元のレジスタの値，書き込み命令なら書き換わる範囲の元のバイト列とその先頭アドレス・バイト数を保存する
    /// </summary>
    private readonly Stack<(uint Value, Address Addr, int Num)> _prev = new();

    public ExecuteResult Execute(RuntimeContext context) {
        Address addr = MemoryInstruction.ComputeEffectiveAddress(context, rs, offset);
        int off = (int)(addr.Addr & 3);

        // 影響を受けるバイト範囲 (先頭アドレスとバイト数)
        Address start = isLeft ? new Address(addr.Addr - (uint)off) : addr;
        int num = isLeft ? off + 1 : 4 - off;

        uint rtVal = context.Registers[rt];
        if(isWrite) {
            context.Log($"Memory Write (unaligned): {addr} <= {rtVal} (ByteNum: {num})");
            this._prev.Push((context.ReadMemoryBytes(start, num, false), start, num));
            // 左は rt の上位 num バイト，右は rt の下位 num バイトを書く
            context.WriteMemoryBytes(start, isLeft ? rtVal >> (8 * (4 - num)) : rtVal, num);
        } else {
            context.Log($"Memory Read (unaligned): {addr} => {rt} (ByteNum: {num})");
            this._prev.Push((rtVal, start, num));
            uint loaded = context.ReadMemoryBytes(start, num, false);
            // 左は rt の上位 num バイトを置き換え，右は rt の下位 num バイトを置き換える
            context.Registers[rt] = isLeft
                ? (loaded << (8 * (4 - num))) | (rtVal & (uint)(0xFFFFFFFFul >> (8 * num)))
                : loaded | (rtVal & ~(0xFFFFFFFFu >> (8 * (4 - num))));
        }
        return ExecuteResult.Next;
    }

    public void Undo(RuntimeContext context) {
        (uint value, Address addr, int num) = this._prev.Pop();
        if(isWrite) {
            context.WriteMemoryBytes(addr, value, num);
        } else {
            context.Registers[rt] = value;
        }
    }

    /// <summary>
    /// lwl, lwr, swl, swr のパーサーを生成するファクトリ
    /// </summary>
    internal static Func<string, IInstructionParser> CreateParser(bool isWrite, bool isLeft) {
        return mnemonic => new Factories.FuncInstructionParser(mnemonic, (operands, lineNumber) => {
            return OperandParser.TryParseMemoryOperands(operands, out RegisterID rt, out RegisterID rs, out Immediate? offset)
                ? new UnalignedMemoryInstruction(rt, rs, offset, isWrite, isLeft, lineNumber)
                : (IInstruction?)null;
        });
    }
}
