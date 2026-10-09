using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions;

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
    private readonly Stack<Address> _previousPCs = new();

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
    public ExecuteResult Execute(RuntimeContext context) {
        // Undoのために現在のPCを保存
        this._previousPCs.Push(context.PC);

        if(this.EvaluateCondition(context)) {
            // 不正なラベルでも，InstructionFetchで例外が発生するべき
            context.PC = context.ResolveLabelName(targetLabel)?.Addr ?? Address.InValid;
            context.Log($"{mnemonic}: branch taken to {targetLabel}");
        } else {
            // 分岐不成立時は次の命令へ
            context.PC += 4;
            context.Log($"{mnemonic}: branch not taken");
        }
        // 成立・不成立どちらでもPCはこの命令が設定する
        return ExecuteResult.PcSet;
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

    /// <summary>
    /// ゼロとの符号付き比較を行う条件分岐命令 (bgez, bgtz, blez, bltz) のパーサーを生成するファクトリ
    /// </summary>
    /// <remarks>rtには<see cref="RegisterID.Zero"/>を渡して既存の実装を再利用する</remarks>
    internal static Func<string, IInstructionParser> CreateZeroParser(Func<int, bool> condition) {
        return mnemonic => new Factories.FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return OperandParser.TryParseBranchZeroOperands(operands, out RegisterID rs, out string? label)
                ? new BranchInstruction(rs, RegisterID.Zero, label, lineIndex, mnemonic, (rsVal, _) => condition(unchecked((int)rsVal)))
                : (IInstruction?)null;
        });
    }
}
