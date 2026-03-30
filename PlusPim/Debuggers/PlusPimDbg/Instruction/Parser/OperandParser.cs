using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;

/// <summary>
/// 命令オペランドの共通パースユーティリティ
/// </summary>
internal static partial class OperandParser {
    [GeneratedRegex(@"^\$(?<rs>\w+)$")]
    private static partial Regex SingleRegPattern();

    [GeneratedRegex(@"^\$(?<r1>\w+),\s*\$(?<r2>\w+)$")]
    private static partial Regex Operands2RegPattern();

    [GeneratedRegex(@"^\$(?<rd>\w+),\s*\$(?<rs>\w+),\s*\$(?<rt>\w+)$")]
    private static partial Regex Operands3RegPattern();

    [GeneratedRegex(@"^\$(?<rd>\w+),\s*\$(?<rt>\w+),\s*(?<shamt>\S+)$")]
    private static partial Regex Operands2RegShamtPattern();

    [GeneratedRegex(@"^\$(?<rt>\w+),\s*(?<imm>\S+)$")]
    private static partial Regex RegImmPattern();

    [GeneratedRegex(@"^\$(?<rt>\w+),\s*\$(?<rs>\w+),\s*(?<imm>\S+)$")]
    private static partial Regex OperandsItypePattern();

    [GeneratedRegex(@"^\$(?<rs>\w+),\s*\$(?<rt>\w+),\s*(?<label>(\w|\$)+)$")]
    private static partial Regex BranchOperandsPattern();

    [GeneratedRegex(@"^\$(?<rt>\w+),\s*(?<offset>\S+)\(\$(?<rs>\w+)\)$")]
    private static partial Regex MemoryOperandPattern();

    /// <summary>
    /// 単一レジスタオペランド ($rs) を解析する
    /// </summary>
    internal static bool TryParseSingleRegOperand(string operands, [MaybeNullWhen(false)] out RegisterID rs) {
        rs = default;
        Match match = SingleRegPattern().Match(operands);
        return match.Success && Enum.TryParse<RegisterID>(match.Groups["rs"].Value, true, out rs);
    }

    /// <summary>
    /// 2レジスタオペランド ($r1, $r2) を解析する (mult, div, move等)
    /// </summary>
    internal static bool TryParse2RegOperands(
        string operands,
        [MaybeNullWhen(false)] out RegisterID r1,
        [MaybeNullWhen(false)] out RegisterID r2) {

        r1 = default;
        r2 = default;

        Match match = Operands2RegPattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(Enum.TryParse<RegisterID>(match.Groups["r1"].Value, true, out RegisterID r1Parsed)
            && Enum.TryParse<RegisterID>(match.Groups["r2"].Value, true, out RegisterID r2Parsed)) {
            r1 = r1Parsed;
            r2 = r2Parsed;
            return true;
        }

        return false;
    }

    /// <summary>
    /// オペランドを3レジスタを指定している文字列から解析してRegisterIDに変換する
    /// </summary>
    /// <param name="operands">対象の文字列</param>
    /// <param name="rd"><see langword="true"/>ならばRegisterIDが代入される．<see langword="false"/>のときの値は未定義．</param>
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
    /// <param name="rd"><see langword="true"/>ならばRegisterIDが代入される．<see langword="false"/>のときの値は未定義．</param>
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
            return true;
        }

        return false;
    }

    /// <summary>
    /// レジスタ+即値のみのオペランド ($rt, imm) を解析する (lui等)
    /// </summary>
    /// <param name="operands">対象の文字列</param>
    /// <param name="rt"><see langword="true"/>ならばRegisterIDが代入される．<see langword="false"/>のときの値は未定義．</param>
    /// <param name="imm">即値が代入される</param>
    /// <returns><see langword="true"/>ならば解析成功</returns>
    internal static bool TryParseRegImmOperands(
        string operands,
        [MaybeNullWhen(false)] out RegisterID rt,
        [MaybeNullWhen(false)] out Immediate imm) {

        rt = default;
        imm = null;

        Match match = RegImmPattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(Enum.TryParse<RegisterID>(match.Groups["rt"].Value, true, out RegisterID rtParsed)
            && Immediate.TryParse(match.Groups["imm"].Value, null, out Immediate? immParsed)) {
            rt = rtParsed;
            imm = immParsed;
            return true;
        }

        return false;
    }

    /// <summary>
    /// I形式命令のオペランド ($rt, $rs, imm) を解析する (addiu, andi等)
    /// </summary>
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
            return true;
        }

        return false;
    }

    /// <summary>
    /// 分岐命令のオペランド ($rs, $rt, label) を解析する (beq, bne等)
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
    /// メモリ命令のオペランド ($rt, offset($rs)) を解析する (lw, sw等)
    /// </summary>
    internal static bool TryParseMemoryOperands(
        string operands,
        [MaybeNullWhen(false)] out RegisterID rt,
        [MaybeNullWhen(false)] out RegisterID rs,
        [MaybeNullWhen(false)] out Immediate offset) {

        rt = default;
        rs = default;
        offset = null;

        Match match = MemoryOperandPattern().Match(operands);
        if(!match.Success) {
            return false;
        }

        if(Enum.TryParse<RegisterID>(match.Groups["rt"].Value, true, out RegisterID rtParsed)
            && Enum.TryParse<RegisterID>(match.Groups["rs"].Value, true, out RegisterID rsParsed)
            && Immediate.TryParse(match.Groups["offset"].Value, null, out Immediate? offsetParsed)) {
            rt = rtParsed;
            rs = rsParsed;
            offset = offsetParsed;
            return true;
        }

        return false;
    }

    /// <summary>
    /// ラベルオペランドを解析する (j, jal等)
    /// </summary>
    internal static bool TryParseLabelOperand(string operands, [MaybeNullWhen(false)] out string label) {
        if(string.IsNullOrEmpty(operands) || operands.Contains(' ') || operands.Contains(',')) {
            label = null;
            return false;
        }
        label = operands;
        return true;
    }

    /// <summary>
    /// オペランドがないことを確認する
    /// </summary>
    internal static bool TryParseNoOperand(string operands) {
        return string.IsNullOrWhiteSpace(operands);
    }
}
