using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

/// <summary>
/// MIPSにおいてブランチ命令を表す抽象基底クラス
/// </summary>
/// <remarks>この派生クラスはPCの自動インクリメントは行われない．条件失敗時のインクリメントはこのクラス側に責任がある</remarks>
internal abstract partial class BranchInstruction: IInstruction {
    [GeneratedRegex(@"^\$(?<rs>\w+),\s*\$(?<rt>\w+),\s*(?<label>\w+)$")]
    private static partial Regex BranchOperandsPattern();

    protected RegisterID Rs { get; }
    protected RegisterID Rt { get; }
    protected string TargetLabel { get; }

    /// <summary>
    /// Undo用に前のExecutionIndexをスタックで管理
    /// </summary>
    private readonly Stack<int> _previousExecutionIndices = new();

    protected BranchInstruction(RegisterID rs, RegisterID rt, string targetLabel) {
        this.Rs = rs;
        this.Rt = rt;
        this.TargetLabel = targetLabel;
    }

    /// <summary>
    /// 分岐条件を評価する
    /// </summary>
    protected abstract bool EvaluateCondition(IExecutionContext context);

    /// <summary>
    /// 分岐条件が真のときにラベル先にジャンプする
    /// </summary>
    /// <exception cref="InvalidOperationException">ラベルが解決できない場合</exception>
    public void Execute(IExecutionContext context) {
        // Undoのために現在のExecutionIndexを保存
        this._previousExecutionIndices.Push(context.ExecutionIndex);

        if(this.EvaluateCondition(context)) {
            int? executionIndex = context.GetLabelExecutionIndex(this.TargetLabel) ?? throw new InvalidOperationException($"Label '{this.TargetLabel}' not found.");
            context.ExecutionIndex = executionIndex.Value;
            context.Log($"{this.GetType().Name}: branch taken to {this.TargetLabel}");
        } else {
            // 分岐不成立時は次の命令へ
            context.ExecutionIndex++;
            context.Log($"{this.GetType().Name}: branch not taken");
        }
    }

    public void Undo(IExecutionContext context) {
        if(this._previousExecutionIndices.Count == 0) {
            throw new InvalidOperationException("No previous ExecutionIndex to undo.");
        }
        context.ExecutionIndex = this._previousExecutionIndices.Pop();
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

    /// <summary>
    /// Sourceレジスタの値をコンテキストから読み込む
    /// </summary>
    /// <param name="context">レジスタを読み込むコンテキスト</param>
    /// <returns>Sourceレジスタの値</returns>
    protected int ReadRs(IExecutionContext context) {
        return context.Registers[(int)this.Rs];
    }

    /// <summary>
    /// Targetレジスタの値をコンテキストから読み込む
    /// </summary>
    /// <param name="context">レジスタを読み込むコンテキスト</param>
    /// <returns>Targetレジスタの値</returns>
    protected int ReadRt(IExecutionContext context) {
        return context.Registers[(int)this.Rt];
    }
}
