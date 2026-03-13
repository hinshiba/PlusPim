using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

internal abstract class MemoryInstruction: IInstruction {
    public int SourceLine { get; }

    protected RegisterID Rt { get; }

    protected Address Addr { get; }

    protected bool IsWrite { get; }

    /// <summary>
    /// 操作するバイト数
    /// </summary>
    protected abstract int ByteNum { get; }

    protected MemoryInstruction(RegisterID rt, Address addr, bool isWrite, int sourceLine) {
        this.Rt = rt;
        this.Addr = addr;
        this.IsWrite = isWrite;
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
        context.Log($"Memory Write: 0x{this.Addr:X8} <= {context.Registers[this.Rt]} (ByteNum: {this.ByteNum})");

        // Undoのために保存


        // 書き込み
        context.WriteMemoryByte(this.Addr, (byte)(context.Registers[this.Rt] & 0xFF));

    }

    private void ExecuteRead(ExecuteContext context) {

    }


    public void Undo(ExecuteContext context) {
        throw new NotImplementedException();
    }
}
