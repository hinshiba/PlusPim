using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

/// <summary>
/// MIPSにおいてLo Hiレジスタ間の転送を行なう命令の基底クラス
/// </summary>
internal abstract partial class LoHiRegisterInstruction: IInstruction {
    [GeneratedRegex(@"^\$(?<reg>\w+)$")]
    private static partial Regex OperandsLoHiPattern();


    protected RegisterID Reg { get; }

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; }

    protected LoHiRegisterInstruction(RegisterID reg, int sourceLine) {
        this.Reg = reg;
        this.SourceLine = sourceLine;
    }

    // 逆操作のためのレジスタの以前の値
    // ループ内では複数回書き込まれる可能性があるためスタックで管理
    protected readonly Stack<int> _prevRegValues = new();

    public abstract void Execute(ExecuteContext context);

    /// <summary>
    /// 命令の逆操作．Hi, Loへの書き込みか読み込みかなので，派生クラスが実装する
    /// </summary>
    public abstract void Undo(ExecuteContext context);

    /// <summary>
    /// オペランドのレジスタを指定している文字列からRegisterIDを得る
    /// </summary>
    /// <param name="operands">対象の文字列</param>
    /// <param name="reg"><see langword="true"/>ならばRegsiterIDが代入される．<see langword="false"/>のときの値は未定義．</param>
    /// <returns><see langword="true"/>ならば解析成功</returns>
    internal static bool TryParseLoHiOperands(
        string operands,
        [MaybeNullWhen(false)] out RegisterID reg) {

        reg = default;

        Match match = OperandsLoHiPattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(Enum.TryParse<RegisterID>(match.Groups["reg"].Value, true, out RegisterID regParsed)) {
            reg = regParsed;
            return true;
        }
        return false;
    }
}
