using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PlusPim.Conformance.Tests;

/// <summary>
/// SPIMのアセンブリをPlusPimのアセンブラが読める形へ行ごとに書き換える
/// </summary>
/// <remarks>
/// PlusPimにない構文だけをSPIMと同じ意味の命令列へ展開する. 展開には <c>$at</c> を使う.
/// SPIMは <c>.half</c> と <c>.word</c> を自動で揃えるが, PlusPimは揃えないので <c>.align</c> を補う.
/// データはすべて <c>.data</c> にあるものとする.
/// 書き換えない行はそのまま返すので, 未対応の命令はPlusPimの診断として現れる.
/// </remarks>
internal static partial class SpimDialect {
    [GeneratedRegex("""^(?<label>\s*(?:[A-Za-z_.$][\w.$]*:\s*)*)(?<op>\.?[a-z][a-z0-9.]*)(?=\s|$)(?<operands>[^#"]*?)(?<comment>\s*#.*)?$""")]
    private static partial Regex Line();

    [GeneratedRegex(@"[\s,]+")]
    private static partial Regex Separator();

    [GeneratedRegex(@"^-?(?:0x[0-9a-fA-F]+|\d+)$")]
    private static partial Regex Number();

    /// <summary><c>sym</c>, <c>sym+4</c>, <c>sym($r)</c>, <c>4($r)</c>, <c>10000000</c> などのメモリオペランド</summary>
    [GeneratedRegex(@"^(?<sym>[A-Za-z_.][\w.$]*)?(?<off>[+-]?(?:0x[0-9a-fA-F]+|\d+))?(?:\((?<base>\$\w+)\))?$")]
    private static partial Regex MemoryOperand();

    /// <summary>レジスタの3オペランド形をとり, SPIMでは最後を即値にできる命令</summary>
    private static readonly FrozenSet<string> ThreeReg = FrozenSet.Create(StringComparer.Ordinal,
        "add", "addu", "sub", "subu", "and", "or", "xor", "nor", "slt", "sltu", "sllv", "srlv", "srav",
        "mul", "div", "divu", "rem", "remu", "seq", "sne", "sge", "sgeu", "sgt", "sgtu", "sle", "sleu");

    /// <summary>即値の3オペランド形をとり, SPIMでは書き込み先と読むレジスタが同じなら1つにできる命令</summary>
    private static readonly FrozenSet<string> TwoImm = FrozenSet.Create(StringComparer.Ordinal,
        "addi", "addiu", "andi", "ori", "xori", "slti", "sltiu");

    /// <summary>シフト量を即値でとる命令. 符号化は下位5ビットだけを持つ</summary>
    private static readonly FrozenSet<string> Shift = FrozenSet.Create(StringComparer.Ordinal, "sll", "srl", "sra");

    /// <summary>2つのレジスタを比べる分岐で, SPIMでは2つ目を即値にできる命令</summary>
    private static readonly FrozenSet<string> CompareBranch = FrozenSet.Create(StringComparer.Ordinal,
        "beq", "bne", "blt", "bltu", "bgt", "bgtu", "ble", "bleu", "bge", "bgeu");

    private static readonly FrozenSet<string> MemoryAccess = FrozenSet.Create(StringComparer.Ordinal,
        "lb", "lbu", "lh", "lhu", "lw", "lwl", "lwr", "sb", "sh", "sw", "swl", "swr", "ulh", "ulhu", "ulw", "ush", "usw");

    /// <summary>空白で値を区切れるデータの指令と, SPIMが自動で揃える境界の2の指数</summary>
    private static readonly FrozenDictionary<string, int> DataList = new Dictionary<string, int> {
        [".byte"] = 0,
        [".half"] = 1,
        [".word"] = 2,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary><paramref name="line"/> をPlusPimの1行以上へ書き換える</summary>
    public static IEnumerable<string> Translate(string line) {
        Match m = Line().Match(line);
        if(!m.Success) {
            return [line];
        }
        string label = m.Groups["label"].Value;
        string op = m.Groups["op"].Value;
        string operandText = m.Groups["operands"].Value.Trim();
        string[] o = operandText.Length == 0 ? [] : Separator().Split(operandText);
        if(DataList.TryGetValue(op, out int align)) {
            // ラベルは揃えた後の位置を指す
            string data = label + Syntax(op, o) + m.Groups["comment"].Value;
            // PlusPimの.alignはセクションの先頭も揃えるので, 新しいセクションを始めて揃える位置をこの行に限る
            return align == 0 ? [data] : [".data", $".align {align}", data];
        }
        if(op.StartsWith('.')) {
            return [line];
        }
        string[] body = Expand(op, o) ?? [Syntax(op, o)];
        body[0] = label + body[0] + m.Groups["comment"].Value;
        return body;
    }

    private static string[]? Expand(string op, string[] o) {
        return o switch {
            [_, string imm] when ThreeReg.Contains(op) && IsNumber(imm) => WithImmediate(op, o[0], o[0], ParseNumber(imm)),
            [_, _, string imm] when ThreeReg.Contains(op) && IsNumber(imm) => WithImmediate(op, o[0], o[1], ParseNumber(imm)),
            [_, string imm] when TwoImm.Contains(op) && IsNumber(imm) => [Syntax(op, [o[0], o[0], imm])],
            [_, _, string n] when Shift.Contains(op) && IsNumber(n) && 31 < ParseNumber(n) => [Syntax(op, [o[0], o[1], (ParseNumber(n) & 31).ToString(CultureInfo.InvariantCulture)])],
            [string _] when op == "break" => ["break"],
            [_, string imm, _] when CompareBranch.Contains(op) && IsNumber(imm) => [LoadAt(ParseNumber(imm)), Syntax(op, [o[0], "$at", o[2]])],
            [_, string mem] when MemoryAccess.Contains(op) => ExpandMemory(op, o[0], mem),
            _ => null,
        };
    }

    /// <summary>
    /// 即値をとる3オペランド形. SPIMと同じく, 対応するI形式に収まればその1命令にし, 収まらなければ <c>$at</c> に読む
    /// </summary>
    /// <remarks>番地を直書きするテストがあるので, 命令数をSPIMに合わせる</remarks>
    private static string[] WithImmediate(string op, string rd, string rs, long imm) {
        bool signed = IsSigned16(imm);
        bool zero = IsUnsigned16(imm);
        string? i = op switch {
            "add" when signed => "addi",
            "addu" when signed => "addiu",
            "slt" when signed => "slti",
            "sltu" when signed => "sltiu",
            "and" when zero => "andi",
            "or" when zero => "ori",
            "xor" when zero => "xori",
            _ => null,
        };
        return i is null ? [LoadAt(imm), Syntax(op, [rd, rs, "$at"])] : [Syntax(i, [rd, rs, imm.ToString(CultureInfo.InvariantCulture)])];
    }

    /// <summary><c>$at</c> に即値を読む. SPIMと同じく16ビットに収まれば1命令にする</summary>
    private static string LoadAt(long imm) {
        return IsUnsigned16(imm) ? $"ori $at, $zero, {imm}"
            : IsSigned16(imm) ? $"addiu $at, $zero, {imm}"
            : $"li $at, {imm}";
    }

    private static bool IsSigned16(long v) {
        return short.MinValue <= v && v <= short.MaxValue;
    }

    private static bool IsUnsigned16(long v) {
        return 0 <= v && v <= ushort.MaxValue;
    }

    /// <summary>PlusPimが読めない形のメモリオペランドを <c>$at</c> を基底とする形にする</summary>
    private static string[]? ExpandMemory(string op, string rt, string mem) {
        Match m = MemoryOperand().Match(mem);
        if(!m.Success) {
            return null;
        }
        long off = m.Groups["off"].Success ? ParseNumber(m.Groups["off"].Value) : 0;
        string? sym = m.Groups["sym"].Success ? m.Groups["sym"].Value : null;
        string? b = m.Groups["base"].Success ? m.Groups["base"].Value : null;
        if(sym is null && b is not null && IsSigned16(off)) {
            return null;
        }
        List<string> lines = [sym is null ? LoadAt(off) : $"la $at, {sym}"];
        if(b is not null) {
            lines.Add($"addu $at, $at, {b}");
        }
        lines.Add(Syntax(op, [rt, $"{(sym is null ? 0 : off)}($at)"]));
        return [.. lines];
    }

    private static bool IsNumber(string s) {
        return Number().IsMatch(s);
    }

    private static long ParseNumber(string s) {
        bool neg = s.StartsWith('-');
        string t = s.TrimStart('+', '-');
        long v = t.StartsWith("0x", StringComparison.Ordinal) ? long.Parse(t[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : long.Parse(t, CultureInfo.InvariantCulture);
        return neg ? -v : v;
    }

    private static string Syntax(string op, string[] operands) {
        return operands.Length == 0 ? op : $"{op} {string.Join(", ", operands)}";
    }
}
