using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

internal abstract class MemoryInstruction: IInstruction {
    public int SourceLine { get; }

    protected RegisterID Rt { get; }

    protected Address Addr { get; }

    protected bool IsWrite { get; }

    /// <summary>
    /// 符号拡張するかどうか <see cref="this.IsWrite"/>が<see langword="false"/>のときのみ意味を持つ
    /// </summary>
    protected bool IsSign { get; }

    /// <summary>
    /// 操作するバイト数
    /// </summary>
    protected abstract int ByteNum { get; }

    /// <summary>
    /// 逆操作のためのスタック．書き込み命令なら元のメモリの値，読み込み命令なら元のレジスタの値を保存する
    /// </summary>
    protected Stack<uint> PrevNum = new();

    protected MemoryInstruction(RegisterID rt, Address addr, bool isWrite, bool isSign, int sourceLine) {
        this.Rt = rt;
        this.Addr = addr;
        this.IsWrite = isWrite;
        this.IsSign = isSign;
        this.SourceLine = sourceLine;
    }

    public void Execute(ExecuteContext context) {
        // アライメントの確認
        if(this.Addr % this.ByteNum != 0) {
            // TODO: MIPSの例外にする
            throw new InvalidOperationException($"Memory Access: {this.Addr} does not meet {this.ByteNum}Byte alignment.");
        }

        if(this.IsWrite) {
            this.ExecuteWrite(context);
        } else {
            this.ExecuteRead(context);
        }
    }

    private void ExecuteWrite(ExecuteContext context) {
        context.Log($"Memory Write: {this.Addr} <= {context.Registers[this.Rt]} (ByteNum: {this.ByteNum})");

        // Undoのために保存
        this.PrevNum.Push(context.ReadMemoryBytes(this.Addr, this.ByteNum, false));

        // 書き込み
        context.WriteMemoryBytes(this.Addr, (uint)context.Registers[this.Rt], this.ByteNum);

    }

    private void ExecuteRead(ExecuteContext context) {
        context.Log($"Memory Read: {this.Addr} => {this.Rt} (ByteNum: {this.ByteNum}, IsSign: {this.IsSign})");

        // Undoのために保存
        this.PrevNum.Push((uint)context.Registers[this.Rt]);

        // 読み込み
        context.Registers[this.Rt] = (int)context.ReadMemoryBytes(this.Addr, this.ByteNum, this.IsSign);
    }


    public void Undo(ExecuteContext context) {
        if(this.IsWrite) {
            context.WriteMemoryBytes(this.Addr, this.PrevNum.Pop(), this.ByteNum);
        } else {
            context.Registers[this.Rt] = (int)this.PrevNum.Pop();
        }
    }
}
