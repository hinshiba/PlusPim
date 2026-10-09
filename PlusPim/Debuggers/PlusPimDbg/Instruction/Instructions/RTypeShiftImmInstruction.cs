using PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions;

/// <summary>
/// シフト即値R形式命令の汎用実装（sll, srl, sra）
/// </summary>
internal sealed class RTypeShiftImmInstruction(
    RegisterID rd, RegisterID rt, Immediate shamt, int lineNumber,
    string mnemonic, Func<uint, int, uint> compute
): IInstruction {

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = lineNumber;

    private readonly RegisterWriteHistory _rd = new(rd);

    public ExecuteResult Execute(RuntimeContext context) {
        uint rtVal = context.Registers[rt];
        int shamtVal = shamt.ToSInt();
        uint result = compute(rtVal, shamtVal);
        this._rd.Write(context, result);
        context.Log($"{mnemonic} ${rd}, ${rt}, {shamt}: 0x{rtVal:X8}, {shamtVal} => 0x{result:X8}");
        return ExecuteResult.Next;
    }

    public void Undo(RuntimeContext context) {
        this._rd.Undo(context);
    }

    /// <summary>
    /// シフト即値R形式命令のパーサーを生成するファクトリ
    /// </summary>
    internal static Func<string, IInstructionParser> CreateParser(Func<uint, int, uint> compute) {
        return mnemonic => new FuncInstructionParser(mnemonic, (operands, lineNumber) => {
            return OperandParser.TryParse2RegShamtOperands(operands, out RegisterID rd, out RegisterID rt, out Immediate? shamt)
                ? 31 < shamt.ToUInt() ? null : (IInstruction)new RTypeShiftImmInstruction(rd, rt, shamt, lineNumber, mnemonic, compute)
                : null;
        });
    }
}
