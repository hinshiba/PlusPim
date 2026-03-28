using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;

/// <summary>
/// MIPSにおいてブランチ命令を表すクラス
/// </summary>
/// <remarks>PCの自動インクリメントは行われない．条件失敗時のインクリメントはこのクラス側に責任がある</remarks>
internal sealed class BranchInstruction(
    RegisterID rs, RegisterID rt, string targetLabel, int sourceLine,
    string mnemonic, Func<uint, uint, bool> condition
): IInstruction {

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = sourceLine;

    /// <summary>
    /// Undo用に前のPCをスタックで管理
    /// </summary>
    private readonly Stack<InstructionIndex> _previousPCs = new();

    /// <summary>
    /// 分岐条件を評価する
    /// </summary>
    private bool EvaluateCondition(RuntimeContext context) {
        uint rsVal = context.Registers[rs];
        uint rtVal = context.Registers[rt];
        bool result = condition(rsVal, rtVal);
        context.Log($"{mnemonic} ${rs}, ${rt}, {targetLabel}: 0x{rsVal:X8}, 0x{rtVal:X8} => {result}");
        return result;
    }

    /// <summary>
    /// 分岐条件が真のときにラベル先にジャンプする
    /// </summary>
    /// <remarks>
    /// ラベルが解決できなくても例外は発生しない．その場合は-1にジャンプする．
    /// </remarks>
    public void Execute(RuntimeContext context) {
        // Undoのために現在のPCを保存
        this._previousPCs.Push(context.PC);

        if(this.EvaluateCondition(context)) {
            // 不正なラベルでも，InstructionFetchで例外が発生するべき
            context.PC = context.ResolveLabelIndex(targetLabel) ?? InstructionIndex.Invalid;
            context.Log($"{mnemonic}: branch taken to {targetLabel}");
        } else {
            // 分岐不成立時は次の命令へ
            context.PC++;
            context.Log($"{mnemonic}: branch not taken");
        }
    }

    public void Undo(RuntimeContext context) {
        if(this._previousPCs.Count == 0) {
            throw new InvalidOperationException("No previous PC to undo.");
        }
        context.PC = this._previousPCs.Pop();
    }

    /// <summary>
    /// 条件分岐命令のパーサーを生成するファクトリ
    /// </summary>
    internal static Func<string, IInstructionParser> CreateParser(Func<uint, uint, bool> condition) {
        return mnemonic => new Factories.FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return OperandParser.TryParseBranchOperands(operands, out RegisterID rs, out RegisterID rt, out string? label)
                ? new BranchInstruction(rs, rt, label, lineIndex, mnemonic, condition)
                : (IInstruction?)null;
        });
    }
}
