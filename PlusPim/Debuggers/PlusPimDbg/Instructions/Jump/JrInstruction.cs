using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Jump;

internal sealed partial class JrInstruction(RegisterID rs): JumpInstruction(null) {
    // 一時的にここに正規表現配置
    [GeneratedRegex(@"^\$(?<rs>\w+)$")]
    private static partial Regex SingleRegPattern();

    private RegisterID Rs { get; } = rs;
    private readonly Stack<CallStackFrame?> _poppedFrames = new();

    public override void Execute(IExecutionContext context) {
        int targetAddress = context.Registers[(int)this.Rs];
        int executionIndex = targetAddress - ExecuteContext.TextSegmentBase;
        this.JumpTo(context, executionIndex);

        // コールスタックから pop
        if(context.CallStack.Count > 0) {
            this._poppedFrames.Push(context.CallStack.Pop());
        } else {
            this._poppedFrames.Push(null);
        }

        context.Log($"jr ${this.Rs}: jump to 0x{targetAddress:X8} (index {executionIndex})");
    }

    public override void Undo(IExecutionContext context) {
        this.UndoJump(context);

        // popしたフレームを復元
        if(this._poppedFrames.Count > 0) {
            CallStackFrame? frame = this._poppedFrames.Pop();
            if(frame != null) {
                context.CallStack.Push(frame);
            }
        }
    }

    internal static bool TryParseSingleRegOperand(string operands, [MaybeNullWhen(false)] out RegisterID rs) {
        rs = default;
        Match match = SingleRegPattern().Match(operands.Trim());
        return match.Success && Enum.TryParse<RegisterID>(match.Groups["rs"].Value, true, out rs);
    }
}

internal sealed class JrInstructionParser: IInstructionParser {
    public string Mnemonic => "jr";

    public bool TryParse(string operands, [MaybeNullWhen(false)] out IInstruction instruction) {
        instruction = null;
        if(JrInstruction.TryParseSingleRegOperand(operands, out RegisterID rs)) {
            instruction = new JrInstruction(rs);
            return true;
        }
        return false;
    }
}
