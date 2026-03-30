using PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;

/// <summary>
/// CP0レジスタとの転送命令 (mfc0/mtc0)
/// </summary>
internal sealed class CP0RegisterInstruction(
    RegisterID rt, int cp0RegNum, bool isFrom, int sourceLine
): IInstruction {
    public int SourceLine { get; } = sourceLine;

    private readonly Stack<uint> _prevRegValues = new();
    private readonly Stack<CP0RegisterFile> _prevCP0 = new();

    public void Execute(RuntimeContext context) {
        if(isFrom) {
            // mfc0: GPR[rt] = CP0[cp0RegNum]
            this._prevRegValues.Push(context.Registers[rt]);
            context.Registers[rt] = context.ReadCP0Register(cp0RegNum);
        } else {
            // mtc0: CP0[cp0RegNum] = GPR[rt]
            this._prevCP0.Push(context.GetCP0Snapshot());
            context.WriteCP0Register(cp0RegNum, context.Registers[rt]);
        }
    }

    public void Undo(RuntimeContext context) {
        if(isFrom) {
            context.Registers[rt] = this._prevRegValues.Pop();
        } else {
            context.RestoreCP0(this._prevCP0.Pop());
        }
    }

    internal static Func<string, IInstructionParser> CreateParser(bool isFrom) {
        return mnemonic => new FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return OperandParser.TryParse2RegOperands(operands, out RegisterID rt, out RegisterID rd)
            // $nでもC#のenumの仕様としてパースされるので，既存のパーサーを使いまわしてintにキャストする
                ? new CP0RegisterInstruction(rt, (int)rd, isFrom, lineIndex)
                : (IInstruction?)null;
        });
    }
}
