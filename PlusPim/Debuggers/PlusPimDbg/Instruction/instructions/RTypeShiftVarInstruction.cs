using PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;

/// <summary>
/// シフト可変R形式命令の汎用実装（sllv, srlv, srav）
/// </summary>
/// <remarks>
/// MIPSの構文は $rd, $rt, $rs だが、TryParse3RegOperandsは $rd, $rs, $rt の順で解析するため
/// パーサー側でrs/rtを入れ替えて渡す。ここではRsがシフト量、Rtがシフト対象。
/// </remarks>
internal sealed class RTypeShiftVarInstruction(
    RegisterID rd, RegisterID rs, RegisterID rt, int lineIndex,
    string mnemonic, Func<uint, int, uint> compute
): IInstruction {
    private RegisterID Rd { get; } = rd;
    private RegisterID Rs { get; } = rs;
    private RegisterID Rt { get; } = rt;
    public int SourceLine { get; } = lineIndex;

    private readonly Stack<uint> _previousRdValues = new();

    public void Execute(RuntimeContext context) {
        uint rsVal = context.Registers[this.Rs];
        uint rtVal = context.Registers[this.Rt];
        uint result = compute(rtVal, (int)(rsVal & 0x1F));
        this.WriteRd(context, result);
        context.Log($"{mnemonic} ${this.Rd}, ${this.Rt}, ${this.Rs}: 0x{rtVal:X8}, {rsVal & 0x1F} => 0x{result:X8}");
    }

    public void Undo(RuntimeContext context) {
        if(this.Rd == RegisterID.Zero) {
            return;
        }
        if(this._previousRdValues.Count == 0) {
            throw new InvalidOperationException("No previous value to undo.");
        }
        context.Registers[this.Rd] = this._previousRdValues.Pop();
    }

    private void WriteRd(RuntimeContext context, uint value) {
        if(this.Rd == RegisterID.Zero) {
            return;
        }
        this._previousRdValues.Push(context.Registers[this.Rd]);
        context.Registers[this.Rd] = value;
    }

    /// <summary>
    /// シフト可変R形式命令のパーサーを生成するファクトリ
    /// </summary>
    /// <remarks>
    /// MIPSの構文は $rd, $rt, $rs だが TryParse3RegOperands は $rd, $rs, $rt 順で解析するため
    /// rs/rt を入れ替えて渡す
    /// </remarks>
    internal static Func<string, IInstructionParser> CreateParser(Func<uint, int, uint> compute) {
        return mnemonic => new FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return OperandParser.TryParse3RegOperands(operands, out RegisterID rd, out RegisterID parsedRs, out RegisterID parsedRt)
                ? new RTypeShiftVarInstruction(rd, parsedRt, parsedRs, lineIndex, mnemonic, compute)
                : (IInstruction?)null;
        });
    }
}
