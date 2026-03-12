using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

/// <summary>
/// MIPSにおいてR形式の命令のほとんどを表す抽象基底クラス
/// </summary>
/// <remarks>ジャンプ命令, syscall, break, トラップ命令, HiLoレジスタ関連を含まない</remarks>
internal abstract partial class RTypeInstruction: IInstruction {
    [GeneratedRegex(@"^\$(?<rd>\w+),\s*\$(?<rs>\w+),\s*\$(?<rt>\w+)$")]
    private static partial Regex Operands3RegPattern();

    [GeneratedRegex(@"^\$(?<rd>\w+),\s*\$(?<rt>\w+),\s*(?<shamt>\S+)$")]
    private static partial Regex Operands2RegShamtPattern();

    // オペランドのレジスタID
    protected RegisterID Rd { get; }
    protected RegisterID Rs { get; }
    protected RegisterID Rt { get; }

    // シフト量の即値
    protected Immediate Shamt { get; }

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; }

    protected RTypeInstruction(RegisterID rd, RegisterID rs, RegisterID rt, int sourceLine) {
        this.Rd = rd;
        this.Rs = rs;
        this.Rt = rt;
        this.Shamt = new Immediate(-1); // 使用しない
        this.SourceLine = sourceLine;
    }

    protected RTypeInstruction(RegisterID rd, RegisterID rt, Immediate shamt, int sourceLine) {
        this.Rd = rd;
        this.Rs = RegisterID.Zero; // 使用しない
        this.Rt = rt;
        this.Shamt = shamt;
        this.SourceLine = sourceLine;
    }

    // 逆操作のためのRdの以前の値
    // ループ内では複数回書き込まれる可能性があるためスタックで管理
    private readonly Stack<int> _previousRdValues = new();

    public abstract void Execute(ExecuteContext context);

    /// <summary>
    /// 命令の逆操作だが，ほとんどのR形式命令ではRdに書き込んだ値を元に戻すだけで良い
    /// </summary>
    public void Undo(ExecuteContext context) {
        if(this.Rd == RegisterID.Zero) {
            return;
        }
        if(this._previousRdValues.Count == 0) {
            throw new InvalidOperationException("No previous value to undo.");
        }
        context.Registers[this.Rd] = this._previousRdValues.Pop();
    }

    /// <summary>
    /// オペランドを3レジスタを指定している文字列から解析してRegisterIDに変換する
    /// </summary>
    /// <param name="operands">対象の文字列</param>
    /// <param name="rd"><see langword="true"/>ならばRegsiterIDが代入される．<see langword="false"/>のときの値は未定義．</param>
    /// <param name="rs">rdと同様</param>
    /// <param name="rt">rdと同様</param>
    /// <returns><see langword="true"/>ならば解析成功</returns>
    internal static bool TryParse3RegOperands(
        string operands,
        [MaybeNullWhen(false)] out RegisterID rd,
        [MaybeNullWhen(false)] out RegisterID rs,
        [MaybeNullWhen(false)] out RegisterID rt) {

        rd = default;
        rs = default;
        rt = default;

        Match match = Operands3RegPattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(Enum.TryParse<RegisterID>(match.Groups["rd"].Value, true, out RegisterID rdParsed)
            && Enum.TryParse<RegisterID>(match.Groups["rs"].Value, true, out RegisterID rsParsed)
            && Enum.TryParse<RegisterID>(match.Groups["rt"].Value, true, out RegisterID rtParsed)) {
            rd = rdParsed;
            rs = rsParsed;
            rt = rtParsed;
            return true;
        }

        return false;
    }

    /// <summary>
    /// オペランドを2レジスタとシフト量を指定している文字列から解析してRegisterIDと即値に変換する
    /// </summary>
    /// <param name="operands">対象の文字列</param>
    /// <param name="rd"><see langword="true"/>ならばRegsiterIDが代入される．<see langword="false"/>のときの値は未定義．</param>
    /// <param name="rt">rdと同様</param>
    /// <param name="shamt">シフト量が代入される</param>
    /// <returns><see langword="true"/>ならば解析成功</returns>
    internal static bool TryParse2RegShamtOperands(
        string operands,
        [MaybeNullWhen(false)] out RegisterID rd,
        [MaybeNullWhen(false)] out RegisterID rt,
        [MaybeNullWhen(false)] out Immediate shamt) {

        rd = default;
        rt = default;
        shamt = default;

        Match match = Operands2RegShamtPattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(Enum.TryParse<RegisterID>(match.Groups["rd"].Value, true, out RegisterID rdParsed)
            && Enum.TryParse<RegisterID>(match.Groups["rt"].Value, true, out RegisterID rtParsed)
            && Immediate.TryParse(match.Groups["shamt"].Value, null, out Immediate? shamtParsed)) {
            rd = rdParsed;
            rt = rtParsed;
            shamt = shamtParsed;
            return rd != RegisterID.Zero;
        }

        return false;
    }

    /// <summary>
    /// コンテキスト内のDestinationレジスタに値を書き込むと同時に，逆操作のために以前の値を保存する
    /// </summary>
    /// <remarks>
    /// これを呼び出すと逆操作のためにDestinationレジスタの値は保存される
    /// </remarks>
    /// <param name="context">レジスタを含むコンテキスト</param>
    /// <param name="value">書き込む値</param>
    protected void WriteRd(ExecuteContext context, int value) {
        // $zero保護
        if(this.Rd == RegisterID.Zero) {
            return;
        }
        // 逆操作のために保存
        this._previousRdValues.Push(context.Registers[this.Rd]);
        context.Registers[this.Rd] = value;
    }
}
