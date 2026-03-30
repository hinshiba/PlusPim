using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;

/// <summary>
/// MIPSにおいてメモリ操作命令を表すクラス
/// </summary>
/// <param name="rt">メモリ領域とやり取りをするレジスタ</param>
/// <param name="rs">アドレスを指すレジスタ</param>
/// <param name="offset"><paramref name="rs"/>からのオフセットを示す即値</param>
/// <param name="isWrite">書き込みかどうか</param>
/// <param name="isSign">符号拡張するかどうか <paramref name="isWrite"/>が<see langword="false"/>のときのみ意味を持つ</param>
/// <param name="byteNum">操作するバイト数</param>
/// <param name="sourceLine">行番号</param>
internal sealed class MemoryInstruction(
    RegisterID rt, RegisterID rs, Immediate offset,
    bool isWrite, bool isSign, int byteNum, int sourceLine
): IInstruction {

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = sourceLine;

    /// <summary>
    /// 逆操作のためのスタック．書き込み命令なら元のメモリの値，読み込み命令なら元のレジスタの値を保存する
    /// </summary>
    private readonly Stack<uint> _prevVal = new();

    /// <summary>
    /// 実効アドレスを計算する
    /// </summary>
    private Address ComputeAddress(RuntimeContext context) {
        return new Address(context.Registers[rs] + offset.ToUInt());
    }

    public void Execute(RuntimeContext context) {
        Address addr = this.ComputeAddress(context);

        // アライメントの確認
        if(addr % byteNum != 0) {
            // MIPS例外を発生させる
            if(isWrite) {
                context.RaiseException(ExcCode.AdES, addr);
            } else {
                context.RaiseException(ExcCode.AdEL, addr);
            }
            return;
        }

        if(isWrite) {
            this.ExecuteWrite(context, addr);
        } else {
            this.ExecuteRead(context, addr);
        }
    }

    private void ExecuteWrite(RuntimeContext context, Address addr) {
        context.Log($"Memory Write: {addr} <= {context.Registers[rt]} (ByteNum: {byteNum})");

        // Undoのために保存
        this._prevVal.Push(context.ReadMemoryBytes(addr, byteNum, false));

        // 書き込み
        context.WriteMemoryBytes(addr, context.Registers[rt], byteNum);
    }

    private void ExecuteRead(RuntimeContext context, Address addr) {
        context.Log($"Memory Read: {addr} => {rt} (ByteNum: {byteNum}, IsSign: {isSign})");

        // Undoのために保存
        this._prevVal.Push(context.Registers[rt]);

        // 読み込み
        context.Registers[rt] = context.ReadMemoryBytes(addr, byteNum, isSign);
    }

    public void Undo(RuntimeContext context) {
        Address addr = this.ComputeAddress(context);
        if(isWrite) {
            context.WriteMemoryBytes(addr, this._prevVal.Pop(), byteNum);
        } else {
            context.Registers[rt] = this._prevVal.Pop();
        }
    }

    /// <summary>
    /// メモリアクセス命令のパーサーを生成するファクトリ (lw, sw, lb, sb 等)
    /// </summary>
    internal static Func<string, IInstructionParser> CreateParser(int byteNum, bool isWrite, bool isSign = false) {
        return mnemonic => new Factories.FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return OperandParser.TryParseMemoryOperands(operands, out RegisterID rt, out RegisterID rs, out Immediate? offset)
                ? new MemoryInstruction(rt, rs, offset, isWrite, isSign, byteNum, lineIndex)
                : (IInstruction?)null;
        });
    }
}
