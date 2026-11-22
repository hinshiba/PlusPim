using System.Collections.Frozen;
using System.Globalization;

namespace PlusPim.Instructions;

/// <summary>
/// MIPSの汎用レジスタを識別する列挙型
/// </summary>
public enum GprId: byte {
    Zero,
    At,
    // $v
    V0,
    V1,
    // $a
    A0,
    A1,
    A2,
    A3,
    // $t
    T0,
    T1,
    T2,
    T3,
    T4,
    T5,
    T6,
    T7,
    // $s
    S0,
    S1,
    S2,
    S3,
    S4,
    S5,
    S6,
    S7,
    // $t8 ~
    T8,
    T9,
    // $k
    K0,
    K1,
    // other
    Gp,
    Sp,
    Fp,
    Ra
}

/// <summary>
/// <see cref="GprId"/> のアセンブリ上の表記
/// </summary>
public static class GprIds {
    private static readonly string[] Names = [
        "$zero", "$at", "$v0", "$v1",
        "$a0", "$a1", "$a2", "$a3",
        "$t0", "$t1", "$t2", "$t3", "$t4", "$t5", "$t6", "$t7",
        "$s0", "$s1", "$s2", "$s3", "$s4", "$s5", "$s6", "$s7",
        "$t8", "$t9", "$k0", "$k1",
        "$gp", "$sp", "$fp", "$ra",
    ];

    private static readonly FrozenDictionary<string, GprId> ByName = Enum.GetValues<GprId>()
        .SelectMany(r => new[] { (Key: Names[(int)r], r), (Key: "$" + ((int)r).ToString(CultureInfo.InvariantCulture), r) })
        .ToFrozenDictionary(p => p.Key, p => p.r, StringComparer.Ordinal);

    /// <summary>
    /// 名前か番号の表記を解析する
    /// </summary>
    /// <param name="s"><c>$</c> を含む表記</param>
    /// <returns>レジスタの表記でなければ <see langword="false"/></returns>
    public static bool TryParse(string s, out GprId id) {
        return ByName.TryGetValue(s, out id);
    }

    /// <summary>
    /// 正規の表記 (<c>$sp</c> など) を返す
    /// </summary>
    public static string AsmName(this GprId id) {
        return Names[(int)id];
    }
}
