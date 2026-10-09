using PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions;

/// <summary>
/// 3レジスタR形式命令の汎用実装（add, sub, and, or 等）
/// </summary>
/// <remarks>
/// ラムダ内で <c>checked</c> を使えば算術オーバーフロー時に
/// <see cref="OverflowException"/> が発生し，MIPS例外 <see cref="ExcCode.Ov"/> として処理される。
/// </remarks>
internal sealed class RType3RegInstruction(
    RegisterID rd, RegisterID rs, RegisterID rt, int lineNumber,
    string mnemonic, Func<uint, uint, uint> compute
): IInstruction {

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = lineNumber;

    private readonly RegisterWriteHistory _rd = new(rd);

    public string Disassembly => $"{mnemonic} {RegisterParser.Format(rd)}, {RegisterParser.Format(rs)}, {RegisterParser.Format(rt)}";

    public ExecuteResult Execute(RuntimeContext context) {
        uint rsVal = context.Registers[rs];
        uint rtVal = context.Registers[rt];
        uint result;
        try {
            result = compute(rsVal, rtVal);
        } catch(OverflowException) {
            // Rdは変更せず，Undo用の情報も積まない
            return ExecuteResult.Raise(ExcCode.Ov);
        }
        this._rd.Write(context, result);
        context.Log($"{mnemonic} ${rd}, ${rs}, ${rt}: 0x{rsVal:X8}, 0x{rtVal:X8} => 0x{result:X8}");
        return ExecuteResult.Next;
    }

    public void Undo(RuntimeContext context) {
        this._rd.Undo(context);
    }

    /// <summary>
    /// 3レジスタR形式命令のパーサーを生成するファクトリ
    /// </summary>
    internal static Func<string, IInstructionParser> CreateParser(Func<uint, uint, uint> compute) {
        return mnemonic => new FuncInstructionParser(mnemonic, (operands, lineNumber) => {
            return OperandParser.TryParse3RegOperands(operands, out RegisterID rd, out RegisterID rs, out RegisterID rt)
                ? new RType3RegInstruction(rd, rs, rt, lineNumber, mnemonic, compute)
                : (IInstruction?)null;
        });
    }
}
