using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Jump;

internal sealed class JrInstruction(RegisterID rs, int lineIndex): JumpInstruction(null, lineIndex) {
    private RegisterID Rs { get; } = rs;
    private readonly Stack<(Label, StackFrame?, bool)> _poppedFrames = new();

    public override void Execute(RuntimeContext context) {
        Label prevLabel = context.CurrentLabel;
        Address target = new(context.Registers[this.Rs]);
        this.JumpTo(context, target);

        // コールスタックからpopを試み，Undo用にフレームを保存しておく
        StackFrame? poppedFrame = context.TryPopCallStack(target);

        // mainからのjr $raでプログラムを終了する
        bool terminatedByThis = this.Rs == RegisterID.Ra
            && context.CallStack.Count == 0
            && poppedFrame is null;
        if(terminatedByThis) {
            context.IsTerminated = true;
        }

        this._poppedFrames.Push((prevLabel, poppedFrame, terminatedByThis));

        context.Log($"jr ${this.Rs}: jump to {target}");
    }

    public override void Undo(RuntimeContext context) {
        this.UndoJump(context);

        // popしたフレームを復元
        if(this._poppedFrames.Count > 0) {
            (Label prevLabel, StackFrame? frame, bool terminatedByThis) = this._poppedFrames.Pop();
            context.UndoTryPopCallStack(prevLabel, frame);
            if(terminatedByThis) {
                context.IsTerminated = false;
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
