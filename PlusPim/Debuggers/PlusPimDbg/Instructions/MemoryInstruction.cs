using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

/// <summary>
/// MIPSにおいてメモリ操作命令の基底クラス
/// </summary>
/// <remarks>lw, sw, lb, sb, lh, sh 等の命令を表す</remarks>
internal abstract partial class MemoryInstruction: IInstruction {
    [GeneratedRegex(@"^\$(?<rt>\w+),\s*(?<offset>\S+)\(\$(?<rs>\w+)\)$")]
    private static partial Regex MemoryOperandPattern();

    public int SourceLine { get; }

    protected RegisterID Rt { get; }

    protected RegisterID Rs { get; }

    protected Immediate Offset { get; }

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
    private readonly Stack<uint> _prevVal = new();

    protected MemoryInstruction(RegisterID rt, RegisterID rs, Immediate offset, bool isWrite, bool isSign, int sourceLine) {
        this.Rt = rt;
        this.Rs = rs;
        this.Offset = offset;
        this.IsWrite = isWrite;
        this.IsSign = isSign;
        this.SourceLine = sourceLine;
    }

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
        context.WriteMemoryBytes(addr, (uint)context.Registers[this.Rt], this.ByteNum);
    }

    private void ExecuteRead(RuntimeContext context, Address addr) {
        context.Log($"Memory Read: {addr} => {this.Rt} (ByteNum: {this.ByteNum}, IsSign: {this.IsSign})");

        // Undoのために保存
        this._prevVal.Push((uint)context.Registers[this.Rt]);

        // 読み込み
        context.Registers[this.Rt] = (int)context.ReadMemoryBytes(addr, this.ByteNum, this.IsSign);
    }

    public void Undo(RuntimeContext context) {
        Address addr = this.ComputeAddress(context);
        if(this.IsWrite) {
            context.WriteMemoryBytes(addr, this._prevVal.Pop(), this.ByteNum);
        } else {
            context.Registers[this.Rt] = (int)this._prevVal.Pop();
        }
    }

    /// <summary>
    /// メモリ命令のオペランド ($rt, offset($rs)) を解析する
    /// </summary>
    internal static bool TryParseMemoryOperands(
        string operands,
        [MaybeNullWhen(false)] out RegisterID rt,
        [MaybeNullWhen(false)] out RegisterID rs,
        [MaybeNullWhen(false)] out Immediate offset) {

        rt = default;
        rs = default;
        offset = null;

        Match match = MemoryOperandPattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(Enum.TryParse<RegisterID>(match.Groups["rt"].Value, true, out RegisterID rtParsed)
            && Enum.TryParse<RegisterID>(match.Groups["rs"].Value, true, out RegisterID rsParsed)
            && Immediate.TryParse(match.Groups["offset"].Value, null, out Immediate? offsetParsed)) {
            rt = rtParsed;
            rs = rsParsed;
            offset = offsetParsed;
            return true;
        }

        return false;
    }
}
