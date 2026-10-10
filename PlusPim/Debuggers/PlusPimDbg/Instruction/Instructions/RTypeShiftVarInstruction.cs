using PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions;

/// <summary>
/// シフト可変R形式命令の汎用実装（sllv, srlv, srav）
/// </summary>
/// <remarks>
/// MIPSの構文は $rd, $rt, $rs だが、TryParse3RegOperandsは $rd, $rs, $rt の順で解析するため
/// パーサー側でrs/rtを入れ替えて渡す。ここではRsがシフト量、Rtがシフト対象。
/// </remarks>
internal sealed class RTypeShiftVarInstruction(
    RegisterID rd, RegisterID rs, RegisterID rt, int lineNumber,
    string mnemonic, Func<uint, int, uint> compute
): IInstruction {

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = lineNumber;

    private readonly RegisterWriteHistory _rd = new(rd);

    public ExecuteResult Execute(RuntimeContext context) {
        uint rsVal = context.Registers[rs];
        uint rtVal = context.Registers[rt];
        uint result = compute(rtVal, (int)(rsVal & 0x1F));
        this._rd.Write(context, result);
        context.Log($"{mnemonic} ${rd}, ${rt}, ${rs}: 0x{rtVal:X8}, {rsVal & 0x1F} => 0x{result:X8}");
        return ExecuteResult.Next;
    }

    public void Undo(RuntimeContext context) {
        this._rd.Undo(context);
    }

    /// <summary>
    /// シフト可変R形式命令のパーサーを生成するファクトリ
    /// </summary>
    /// <remarks>
    /// MIPSの構文は $rd, $rt, $rs だが TryParse3RegOperands は $rd, $rs, $rt 順で解析するため
    /// rs/rt を入れ替えて渡す
    /// </remarks>
    internal static Func<string, IInstructionParser> CreateParser(Func<uint, int, uint> compute) {
        return mnemonic => new FuncInstructionParser(mnemonic, (operands, lineNumber) => {
            return OperandParser.TryParse3RegOperands(operands, out RegisterID rd, out RegisterID parsedRs, out RegisterID parsedRt)
                ? new RTypeShiftVarInstruction(rd, parsedRt, parsedRs, lineNumber, mnemonic, compute)
                : (IInstruction?)null;
        });
    }
}
