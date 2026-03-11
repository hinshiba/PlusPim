using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

/// <summary>
/// MIPSにおいてI形式の命令のほとんどを表す抽象基底クラス
/// </summary>
/// <remarks>メモリアクセス命令, ブランチ命令, トラップ命令を含まない</remarks>
internal abstract partial class ITypeInstruction: IInstruction {
    [GeneratedRegex(@"^\$(?<rt>\w+),\s*\$(?<rs>\w+),\s*(?<imm>\d+)$")]
    private static partial Regex OperandsItypePattern();

    protected RegisterID Rs { get; }
    protected RegisterID Rt { get; }
    protected Immediate Imm { get; }

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; }

    protected ITypeInstruction(RegisterID rs, RegisterID rt, Immediate imm, int sourceLine) {
        this.Rs = rs;
        this.Rt = rt;
        this.Imm = imm;
        this.SourceLine = sourceLine;
    }

    // 逆操作のためのRtの以前の値
    // ループ内では複数回書き込まれる可能性があるためスタックで管理
    private readonly Stack<int> _prevRtValues = new();

    public abstract void Execute(ExecuteContext context);

    /// <summary>
    /// 命令の逆操作だが，ほとんどのI形式命令ではRtに書き込んだ値を元に戻すだけで良い
    /// </summary>
    public void Undo(ExecuteContext context) {
        if(this.Rt == RegisterID.Zero) {
            return;
        }
        if(this._prevRtValues.Count == 0) {
            throw new InvalidOperationException("No previous value to undo.");
        }
        context.Registers[this.Rt] = this._prevRtValues.Pop();
    }

    /// <summary>
    /// オペランドの2レジスタと即値を指定している文字列からRegisterIDと即値を得る
    /// </summary>
    /// <param name="operands">対象の文字列</param>
    /// <param name="rt"><see langword="true"/>ならばRegsiterIDが代入される．<see langword="false"/>のときの値は未定義．</param>
    /// <param name="rs">rtと同様</param>
    /// <param name="imm">即値が代入される</param>
    /// <returns><see langword="true"/>ならば解析成功</returns>
    internal static bool TryParseITypeOperands(
        string operands,
        [MaybeNullWhen(false)] out RegisterID rt,
        [MaybeNullWhen(false)] out RegisterID rs,
        [MaybeNullWhen(false)] out Immediate imm) {

        rt = default;
        rs = default;
        imm = null;

        Match match = OperandsItypePattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(Enum.TryParse<RegisterID>(match.Groups["rt"].Value, true, out RegisterID rtParsed)
            && Enum.TryParse<RegisterID>(match.Groups["rs"].Value, true, out RegisterID rsParsed)
            && Immediate.TryParse(match.Groups["imm"].Value, null, out Immediate? immParsed)) {
            rt = rtParsed;
            rs = rsParsed;
            imm = immParsed;
            return rt != RegisterID.Zero;
        }

        return false;
    }

    /// <summary>
    /// コンテキスト内のTargetレジスタに値を書き込むと同時に，逆操作のために以前の値を保存する
    /// </summary>
    /// <remarks>
    /// これを呼び出すと逆操作のためにTargetレジスタの値は保存される
    /// </remarks>
    /// <param name="context">レジスタを含むコンテキスト</param>
    /// <param name="value">書き込む値</param>
    protected void WriteRt(ExecuteContext context, int value) {
        // $zero保護
        if(this.Rt == RegisterID.Zero) {
            return;
        }
        // 逆操作のために保存
        this._prevRtValues.Push(context.Registers[this.Rt]);
        context.Registers[this.Rt] = value;
    }
}
