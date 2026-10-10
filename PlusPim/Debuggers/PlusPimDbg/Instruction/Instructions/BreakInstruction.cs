using PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions;

/// <summary>
/// break命令: ブレークポイント例外 (ExcCode=9) を発生させる
/// </summary>
internal sealed class BreakInstruction(int sourceLine): IInstruction {
    public int SourceLine { get; } = sourceLine;

    public ExecuteResult Execute(RuntimeContext context) {
        return ExecuteResult.Raise(ExcCode.Bp);
    }

    public void Undo(RuntimeContext context) {
        // 常に例外を要求するので，Undo が呼ばれることはない
        throw new InvalidOperationException("break always raises an exception, so it has nothing to undo.");
    }

    internal static Func<string, IInstructionParser> CreateParser() {
        return mnemonic => new FuncInstructionParser(mnemonic, (operands, lineNumber) => {
            return OperandParser.TryParseNoOperand(operands) ? new BreakInstruction(lineNumber) : null;
        });
    }
}
