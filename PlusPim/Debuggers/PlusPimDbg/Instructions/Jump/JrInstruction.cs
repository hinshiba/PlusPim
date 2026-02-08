using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions.Jump;

internal sealed partial class JrInstruction(RegisterID rs): JumpInstruction(null) {
    // 一時的にここに正規表現配置
    [GeneratedRegex(@"^\$(?<rs>\w+)$")]
    private static partial Regex SingleRegPattern();

    private RegisterID Rs { get; } = rs;

    public override void Execute(IExecutionContext context) {
        int targetAddress = context.Registers[(int)this.Rs];
        int executionIndex = targetAddress - ExecuteContext.TextSegmentBase;
        this.JumpTo(context, executionIndex);

        // コールスタックから pop
        // raだけ等の条件が必要かも
        if(context.CallStack.Count > 0) {
            _ = context.CallStack.Pop();
        }

        context.Log($"jr ${this.Rs}: jump to 0x{targetAddress:X8} (index {executionIndex})");
    }

    public override void Undo(IExecutionContext context) {
        // コールスタックに push して戻す
        // UndoJump で復元される ExecutionIndex + 1 が jal の次の命令
        // ただし正確には jal 時の callstack の値を復元する必要がある
        this.UndoJump(context);

        // jal の Undo で CallStack を pop するので、jr の Undo では push して戻す
        context.CallStack.Push(context.ExecutionIndex + 1);
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
