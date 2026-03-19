using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

/// <summary>
/// MIPSにおいて乗除算の命令を表す抽象基底クラス
/// </summary>
internal abstract partial class MulDivInstruction: IInstruction {
    [GeneratedRegex(@"^\$(?<rs>\w+),\s*\$(?<rt>\w+)$")]
    private static partial Regex OperandsMulDivPattern();


    protected RegisterID Rs { get; }
    protected RegisterID Rt { get; }

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; }

    protected MulDivInstruction(RegisterID rs, RegisterID rt, int sourceLine) {
        this.Rs = rs;
        this.Rt = rt;
        this.SourceLine = sourceLine;
    }

    // 逆操作のためのHiLoレジスタの以前の値
    // ループ内では複数回書き込まれる可能性があるためスタックで管理
    // (Hi, Lo)である．
    private readonly Stack<(int, int)> _prevHiLoValues = new();

    public abstract void Execute(RuntimeContext context);

    /// <summary>
    /// 命令の逆操作だが，乗除算命令ではHi Loに書き込んだ値を元に戻すだけで良い
    /// </summary>
    public void Undo(RuntimeContext context) {
        (int prevHi, int prevLo) = this._prevHiLoValues.Pop();
        context.HI = prevHi;
        context.LO = prevLo;
    }

    /// <summary>
    /// オペランドの2レジスタを指定している文字列からRegisterIDを得る
    /// </summary>
    /// <param name="operands">対象の文字列</param>
    /// <param name="rs"><see langword="true"/>ならばRegsiterIDが代入される．<see langword="false"/>のときの値は未定義．</param>
    /// <param name="rt">rsと同様</param>
    /// <returns><see langword="true"/>ならば解析成功</returns>
    internal static bool TryParseMulDivOperands(
        string operands,
        [MaybeNullWhen(false)] out RegisterID rs,
        [MaybeNullWhen(false)] out RegisterID rt) {

        rs = default;
        rt = default;

        Match match = OperandsMulDivPattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(Enum.TryParse<RegisterID>(match.Groups["rs"].Value, true, out RegisterID rsParsed)
            && Enum.TryParse<RegisterID>(match.Groups["rt"].Value, true, out RegisterID rtParsed)) {
            rs = rsParsed;
            rt = rtParsed;
            return true;
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
    protected void WriteHiLo(RuntimeContext context, int hi, int lo) {
        // 逆操作のために保存
        this._prevHiLoValues.Push((context.HI, context.LO));
        context.HI = hi;
        context.LO = lo;
    }
}
