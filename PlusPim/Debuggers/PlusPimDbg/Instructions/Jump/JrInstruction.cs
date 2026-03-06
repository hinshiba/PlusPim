using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Debuggers.PlusPimDbg.Runtime.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Jump;

internal sealed class JrInstruction(RegisterID rs, int lineIndex): JumpInstruction(null, lineIndex) {
    private RegisterID Rs { get; } = rs;
    private readonly Stack<StackFrame?> _poppedFrames = new();

    public override void Execute(ExecuteContext context) {
        Address targetAddress = new(context.Registers[this.Rs]);
        InstructionIndex target = InstructionIndex.FromAddress(targetAddress) ?? throw new AlignmentException($"Try jr to {context.Registers[this.Rs]} but not align");
        this.JumpTo(context, target);

        // コールスタックからpopを試み，Undo用にフレームを保存しておく
        this._poppedFrames.Push(context.TryPopCallStack(target));


        context.Log($"jr ${this.Rs}: jump to 0x{targetAddress:X8} (index {target.Idx})");
    }

    public override void Undo(ExecuteContext context) {
        this.UndoJump(context);

        // popしたフレームを復元
        if(this._poppedFrames.Count > 0) {
            StackFrame? frame = this._poppedFrames.Pop();
            if(frame != null) {
                context.CallStack.Push(frame);
            }
        }
    }
}

internal sealed class JrInstructionParser: IInstructionParser {
    public string Mnemonic => "jr";

    public bool TryParse(string operands, int lineIndex, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(OperandParser.TryParseSingleRegOperand(operands, out RegisterID rs)) {
            instruction = new JrInstruction(rs, lineIndex);
            return true;
        }
        return false;
    }
}
