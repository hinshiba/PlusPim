using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Jump;

internal sealed class JrInstruction(RegisterID rs): JumpInstruction(null) {
    private RegisterID Rs { get; } = rs;
    private readonly Stack<CallStackFrame?> _poppedFrames = new();

    public override void Execute(ExecuteContext context) {
        int targetAddress = context.Registers[this.Rs];
        ProgramCounter target = ProgramCounter.FromAddress(targetAddress);
        this.JumpTo(context, target);

        // コールスタックから pop
        if(context.CallStack.Count > 0) {
            this._poppedFrames.Push(context.CallStack.Pop());
        } else {
            this._poppedFrames.Push(null);
        }

        context.Log($"jr ${this.Rs}: jump to 0x{targetAddress:X8} (index {target.Index})");
    }

    public override void Undo(ExecuteContext context) {
        this.UndoJump(context);

        // popしたフレームを復元
        if(this._poppedFrames.Count > 0) {
            CallStackFrame? frame = this._poppedFrames.Pop();
            if(frame != null) {
                context.CallStack.Push(frame);
            }
        }
    }
}

internal sealed class JrInstructionParser: IInstructionParser {
    public string Mnemonic => "jr";

    public bool TryParse(string operands, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(OperandParser.TryParseSingleRegOperand(operands, out RegisterID rs)) {
            instruction = new JrInstruction(rs);
            return true;
        }
        return false;
    }
}
