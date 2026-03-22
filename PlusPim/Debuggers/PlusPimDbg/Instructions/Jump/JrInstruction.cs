using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Jump;

internal sealed class JrInstruction(RegisterID rs, int lineIndex): JumpInstruction(null, lineIndex) {
    private RegisterID Rs { get; } = rs;
    private readonly Stack<(Label, StackFrame?)> _poppedFrames = new();

    public override void Execute(RuntimeContext context) {
        Label prevLabel = context.CurrentLabel;
        Address targetAddress = new(context.Registers[this.Rs]);
        InstructionIndex? target_ = InstructionIndex.FromAddress(targetAddress, context);
        if(target_ != null) {
            // nullなら例外が発生
            return;
        }
        InstructionIndex target = (InstructionIndex)target_!;
        this.JumpTo(context, target);

        // コールスタックからpopを試み，Undo用にフレームを保存しておく
        this._poppedFrames.Push((prevLabel, context.TryPopCallStack(target)));

        context.Log($"jr ${this.Rs}: jump to 0x{targetAddress:X8} (index {target.Idx})");
    }

    public override void Undo(RuntimeContext context) {
        this.UndoJump(context);

        // popしたフレームを復元
        if(this._poppedFrames.Count > 0) {
            (Label prevLabel, StackFrame? frame) = this._poppedFrames.Pop();
            context.UndoTryPopCallStack(prevLabel, frame);
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
