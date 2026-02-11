using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

/// <summary>
/// 命令オペランドの共通パースユーティリティ
/// </summary>
internal static partial class OperandParser {
    [GeneratedRegex(@"^\$(?<rs>\w+)$")]
    private static partial Regex SingleRegPattern();

    /// <summary>
    /// 単一レジスタオペランド ($rs) を解析する
    /// </summary>
    internal static bool TryParseSingleRegOperand(string operands, [MaybeNullWhen(false)] out RegisterID rs) {
        rs = default;
        Match match = SingleRegPattern().Match(operands.Trim());
        return match.Success && Enum.TryParse<RegisterID>(match.Groups["rs"].Value, true, out rs);
    }
}
