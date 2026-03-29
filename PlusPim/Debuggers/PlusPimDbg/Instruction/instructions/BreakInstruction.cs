using PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;

/// <summary>
/// break命令: ブレークポイント例外 (ExcCode=9) を発生させる
/// </summary>
internal sealed class BreakInstruction(int sourceLine): IInstruction {
    public int SourceLine { get; } = sourceLine;

    public void Execute(RuntimeContext context) {
        context.RaiseException(ExcCode.Bp);
    }

    public void Undo(RuntimeContext context) {
        context.RetException();
    }

    internal static Func<string, IInstructionParser> CreateParser() {
        return mnemonic => new FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return new BreakInstruction(lineIndex);
        });
    }
}
