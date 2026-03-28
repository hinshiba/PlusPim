using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;

internal sealed class SyscallInstruction(int sourceLine): IInstruction {
    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = sourceLine;


    private readonly Stack<SyscallCode> _history = new();
    private readonly Stack<uint> _prevReadInt = new();
    private readonly Stack<(Address, byte[])> _prevReadString = new();


    public void Execute(RuntimeContext context) {
        // システムコール例外を発生させる
        context.RaiseException(ExcCode.Sys);
    }
    public void Undo(RuntimeContext context) {
        context.RetException();
    }

    /// <summary>
    /// 命令のパーサーを生成するファクトリ
    /// </summary>
    internal static Func<string, IInstructionParser> CreateParser() {
        return mnemonic => new Factories.FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return new SyscallInstruction(lineIndex);
        });
    }

}
