using PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;

/// <summary>
/// シフト即値R形式命令の汎用実装（sll, srl, sra）
/// </summary>
internal sealed class RTypeShiftImmInstruction(
    RegisterID rd, RegisterID rt, Immediate shamt, int lineIndex,
    string mnemonic, Func<uint, int, uint> compute
): IInstruction {
    private RegisterID Rd { get; } = rd;
    private RegisterID Rt { get; } = rt;
    private Immediate Shamt { get; } = shamt;
    public int SourceLine { get; } = lineIndex;

    private readonly Stack<uint> _previousRdValues = new();

    public void Execute(RuntimeContext context) {
        uint rtVal = context.Registers[this.Rt];
        int shamtVal = this.Shamt.ToSInt();
        uint result = compute(rtVal, shamtVal);
        this.WriteRd(context, result);
        context.Log($"{mnemonic} ${this.Rd}, ${this.Rt}, {this.Shamt}: 0x{rtVal:X8}, {shamtVal} => 0x{result:X8}");
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
    /// シフト即値R形式命令のパーサーを生成するファクトリ
    /// </summary>
    internal static Func<string, IInstructionParser> CreateParser(Func<uint, int, uint> compute) {
        return mnemonic => new FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return OperandParser.TryParse2RegShamtOperands(operands, out RegisterID rd, out RegisterID rt, out Immediate? shamt)
                ? 31 < shamt.ToUInt() ? null : (IInstruction)new RTypeShiftImmInstruction(rd, rt, shamt, lineIndex, mnemonic, compute)
                : null;
        });
    }
}
