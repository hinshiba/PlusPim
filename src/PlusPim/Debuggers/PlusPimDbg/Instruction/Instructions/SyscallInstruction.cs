using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions;

internal sealed class SyscallInstruction(int sourceLine): IInstruction {
    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = sourceLine;


    public ExecuteResult Execute(RuntimeContext context) {
        return ExecuteResult.Raise(ExcCode.Sys);
    }

    public void Undo(RuntimeContext context) {
        // 常に例外を要求するので，Undo が呼ばれることはない
        throw new InvalidOperationException("syscall always raises an exception, so it has nothing to undo.");
    }

    /// <summary>
    /// 命令のパーサーを生成するファクトリ
    /// </summary>
    internal static Func<string, IInstructionParser> CreateParser() {
        return mnemonic => new Factories.FuncInstructionParser(mnemonic, (operands, lineNumber) => {
            return OperandParser.TryParseNoOperand(operands) ? new SyscallInstruction(lineNumber) : null;
        });
    }

}
