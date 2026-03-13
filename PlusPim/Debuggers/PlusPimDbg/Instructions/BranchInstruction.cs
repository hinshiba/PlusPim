using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Debuggers.PlusPimDbg.Runtime.Exceptions;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

/// <summary>
/// MIPSにおいてブランチ命令を表す抽象基底クラス
/// </summary>
/// <remarks>この派生クラスはPCの自動インクリメントは行われない．条件失敗時のインクリメントはこのクラス側に責任がある</remarks>
internal abstract partial class BranchInstruction(RegisterID rs, RegisterID rt, string targetLabel, int sourceLine): IInstruction {
    [GeneratedRegex(@"^\$(?<rs>\w+),\s*\$(?<rt>\w+),\s*(?<label>(\w|\$)+)$")]
    private static partial Regex BranchOperandsPattern();

    protected RegisterID Rs { get; } = rs;
    protected RegisterID Rt { get; } = rt;
    protected string TargetLabel { get; } = targetLabel;

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine => sourceLine;

    /// <summary>
    /// Undo用に前のPCをスタックで管理
    /// </summary>
    private readonly Stack<InstructionIndex> _previousPCs = new();

    /// <summary>
    /// 分岐条件を評価する
    /// </summary>
    protected abstract bool EvaluateCondition(ExecuteContext context);

    /// <summary>
    /// 分岐条件が真のときにラベル先にジャンプする
    /// </summary>
    /// <exception cref="InvalidOperationException">ラベルが解決できない場合</exception>
    public void Execute(ExecuteContext context) {
        // Undoのために現在のPCを保存
        this._previousPCs.Push(context.PC);

        if(this.EvaluateCondition(context)) {
            Label executionIndex = context.ResolveLabelName(this.TargetLabel) ?? throw new InvalidOperationException($"Label '{this.TargetLabel}' not found.");
            context.PC = InstructionIndex.FromAddress(executionIndex.Addr) ?? throw new AlignmentException($"Attempted branch to {executionIndex} but address is not aligned");
            context.Log($"{this.GetType().Name}: branch taken to {this.TargetLabel}");
        } else {
            // 分岐不成立時は次の命令へ
            context.PC++;
            context.Log($"{this.GetType().Name}: branch not taken");
        }
    }

    public void Undo(ExecuteContext context) {
        if(this._previousPCs.Count == 0) {
            throw new InvalidOperationException("No previous PC to undo.");
        }
        context.PC = this._previousPCs.Pop();
    }

    /// <summary>
    /// オペランドを $rs, $rt, label 形式から解析する
    /// </summary>
    internal static bool TryParseBranchOperands(
        string operands,
        [MaybeNullWhen(false)] out RegisterID rs,
        [MaybeNullWhen(false)] out RegisterID rt,
        [MaybeNullWhen(false)] out string label) {

        rs = default;
        rt = default;
        label = null;

        Match match = BranchOperandsPattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(Enum.TryParse<RegisterID>(match.Groups["rs"].Value, true, out RegisterID rsParsed)
            && Enum.TryParse<RegisterID>(match.Groups["rt"].Value, true, out RegisterID rtParsed)) {
            rs = rsParsed;
            rt = rtParsed;
            label = match.Groups["label"].Value;
            return true;
        }
        return false;
    }
}
