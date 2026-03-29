using PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;

/// <summary>
/// eret命令: 例外からの復帰 (PC = EPC, EXL = 0)
/// </summary>
internal sealed class EretInstruction(int sourceLine): IInstruction {
    public int SourceLine { get; } = sourceLine;

    private readonly Stack<(InstructionIndex PrevPC, CP0RegisterFile PrevCP0)> _prevState = new();

    public void Execute(RuntimeContext context) {
        this._prevState.Push((context.PC, context.GetCP0Snapshot()));

        // EPC (InstructionIndex) を直接PCに代入
        context.PC = context.GetCP0Snapshot().Epc;

        // EXLクリア (カーネルモード脱出)
        context.WriteCP0Register(12, 0);
    }

    public void Undo(RuntimeContext context) {
        (InstructionIndex prevPC, CP0RegisterFile prevCP0) = this._prevState.Pop();
        context.PC = prevPC;
        context.RestoreCP0(prevCP0);
    }

    internal static Func<string, IInstructionParser> CreateParser() {
        return mnemonic => new FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return new EretInstruction(lineIndex);
        });
    }
}
