using PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions;

/// <summary>
/// CP0レジスタとの転送命令 (mfc0/mtc0)
/// </summary>
internal sealed class CP0RegisterInstruction(
    RegisterID rt, int cp0RegNum, bool isFrom, int sourceLine
): IInstruction {
    public int SourceLine { get; } = sourceLine;

    private readonly Stack<uint> _prevRegValues = new();
    private readonly Stack<CP0RegisterFile> _prevCP0 = new();

    public ExecuteResult Execute(RuntimeContext context) {
        if(!context.IsKernelMode) {
            // カーネル空間でないならコプロセッサ例外．番号の検査より優先し，Undo用の情報も積まない
            return ExecuteResult.Raise(ExcCode.CpU);
        }

        if(!CP0RegisterFile.IsSupported(cp0RegNum)) {
            // 未対応の番号はランタイムエラー．rt も CP0 も変更せず，Undo用の情報も積まない
            string mnemonic = isFrom ? "mfc0" : "mtc0";
            return ExecuteResult.Fail(
                RuntimeErrorKind.UnsupportedCP0Register,
                $"{mnemonic} ${rt.ToString().ToLowerInvariant()}, ${cp0RegNum}: unsupported CP0 register number {cp0RegNum} (supported: 8, 12, 13, 14)"
            );
        }

        if(isFrom) {
            // mfc0: GPR[rt] = CP0[cp0RegNum]
            this._prevRegValues.Push(context.Registers[rt]);
            context.Registers[rt] = context.ReadCP0Register(cp0RegNum);
        } else {
            // mtc0: CP0[cp0RegNum] = GPR[rt]
            this._prevCP0.Push(context.GetCP0Snapshot());
            context.WriteCP0Register(cp0RegNum, context.Registers[rt]);
        }
        return ExecuteResult.Next;
    }

    public void Undo(RuntimeContext context) {
        if(isFrom) {
            context.Registers[rt] = this._prevRegValues.Pop();
        } else {
            context.RestoreCP0(this._prevCP0.Pop());
        }
    }

    internal static Func<string, IInstructionParser> CreateParser(bool isFrom) {
        return mnemonic => new FuncInstructionParser(mnemonic, (operands, lineNumber) => {
            return OperandParser.TryParseCp0Operands(operands, out RegisterID rt, out int cp0Reg)
                ? new CP0RegisterInstruction(rt, cp0Reg, isFrom, lineNumber)
                : (IInstruction?)null;
        });
    }
}
