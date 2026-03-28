using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;

/// <summary>
/// MIPSにおいてメモリ操作命令を表すクラス
/// </summary>
/// <remarks>lw, sw, lb, sb, lh, sh 等の命令を表す</remarks>
internal sealed class MemoryInstruction(
    RegisterID rt, RegisterID rs, Immediate offset,
    bool isWrite, bool isSign, int byteNum, int sourceLine
): IInstruction {
    public int SourceLine { get; } = sourceLine;

    private RegisterID Rt { get; } = rt;

    private RegisterID Rs { get; } = rs;

    private Immediate Offset { get; } = offset;

    private bool IsWrite { get; } = isWrite;

    /// <summary>
    /// 符号拡張するかどうか <see cref="IsWrite"/>が<see langword="false"/>のときのみ意味を持つ
    /// </summary>
    private bool IsSign { get; } = isSign;

    /// <summary>
    /// 操作するバイト数
    /// </summary>
    private int ByteNum { get; } = byteNum;

    /// <summary>
    /// 逆操作のためのスタック．書き込み命令なら元のメモリの値，読み込み命令なら元のレジスタの値を保存する
    /// </summary>
    private readonly Stack<uint> _prevVal = new();

    /// <summary>
    /// 実効アドレスを計算する
    /// </summary>
    private Address ComputeAddress(RuntimeContext context) {
        return new Address(context.Registers[this.Rs] + this.Offset.ToUInt());
    }

    public void Execute(RuntimeContext context) {
        Address addr = this.ComputeAddress(context);

        // アライメントの確認
        if(addr % this.ByteNum != 0) {
            // TODO: MIPSの例外にする
            throw new InvalidOperationException($"Memory Access: {addr} does not meet {this.ByteNum}Byte alignment.");
        }

        if(this.IsWrite) {
            this.ExecuteWrite(context, addr);
        } else {
            this.ExecuteRead(context, addr);
        }
    }

    private void ExecuteWrite(RuntimeContext context, Address addr) {
        context.Log($"Memory Write: {addr} <= {context.Registers[this.Rt]} (ByteNum: {this.ByteNum})");

        // Undoのために保存
        this._prevVal.Push(context.ReadMemoryBytes(addr, this.ByteNum, false));

        // 書き込み
        context.WriteMemoryBytes(addr, context.Registers[this.Rt], this.ByteNum);
    }

    private void ExecuteRead(RuntimeContext context, Address addr) {
        context.Log($"Memory Read: {addr} => {this.Rt} (ByteNum: {this.ByteNum}, IsSign: {this.IsSign})");

        // Undoのために保存
        this._prevVal.Push(context.Registers[this.Rt]);

        // 読み込み
        context.Registers[this.Rt] = context.ReadMemoryBytes(addr, this.ByteNum, this.IsSign);
    }

    public void Undo(RuntimeContext context) {
        Address addr = this.ComputeAddress(context);
        if(this.IsWrite) {
            context.WriteMemoryBytes(addr, this._prevVal.Pop(), this.ByteNum);
        } else {
            context.Registers[this.Rt] = this._prevVal.Pop();
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
