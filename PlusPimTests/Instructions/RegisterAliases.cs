using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPimTests.Instructions;

/// <summary>
/// 2レジスタ命令 (<c>op r1, r2</c>) のレジスタエイリアスパターン (doc/tests/instructions/values.md)
/// </summary>
public enum Alias2Reg {
    /// <summary>エイリアスなし: r1=$t0, r2=$t1</summary>
    None,
    /// <summary><c>r1==r2</c>: r1=r2=$t0</summary>
    Same,
}

/// <summary>
/// 3レジスタ命令 (<c>op r1, r2, r3</c>) のレジスタエイリアスパターン (doc/tests/instructions/values.md)
/// </summary>
/// <remarks>名前は <c>op $rd, $rs, $rt</c> の並びに由来し，アセンブリ上のオペランド位置 r1, r2, r3 を指す</remarks>
public enum Alias3Reg {
    /// <summary>エイリアスなし: $t0, $t1, $t2</summary>
    None,
    /// <summary><c>r1==r2</c>: $t0, $t0, $t1</summary>
    RdRs,
    /// <summary><c>r1==r3</c>: $t0, $t1, $t0</summary>
    RdRt,
    /// <summary><c>r2==r3</c>: $t0, $t1, $t1</summary>
    RsRt,
    /// <summary><c>r1==r2==r3</c>: $t0, $t0, $t0</summary>
    All,
}

/// <summary>
/// エイリアスパターンから実際のレジスタとアセンブリ上の名前を得る
/// </summary>
internal static class RegisterAliases {
    /// <summary>
    /// 1レジスタ命令で使うレジスタ
    /// </summary>
    public const RegisterID Single = RegisterID.T0;

    /// <summary>
    /// オペランド位置 (r1, r2) に置くレジスタ
    /// </summary>
    public static (RegisterID R1, RegisterID R2) Registers(Alias2Reg alias) {
        return alias switch {
            Alias2Reg.None => (RegisterID.T0, RegisterID.T1),
            Alias2Reg.Same => (RegisterID.T0, RegisterID.T0),
            _ => throw new ArgumentOutOfRangeException(nameof(alias)),
        };
    }

    /// <summary>
    /// オペランド位置 (r1, r2, r3) に置くレジスタ
    /// </summary>
    public static (RegisterID R1, RegisterID R2, RegisterID R3) Registers(Alias3Reg alias) {
        return alias switch {
            Alias3Reg.None => (RegisterID.T0, RegisterID.T1, RegisterID.T2),
            Alias3Reg.RdRs => (RegisterID.T0, RegisterID.T0, RegisterID.T1),
            Alias3Reg.RdRt => (RegisterID.T0, RegisterID.T1, RegisterID.T0),
            Alias3Reg.RsRt => (RegisterID.T0, RegisterID.T1, RegisterID.T1),
            Alias3Reg.All => (RegisterID.T0, RegisterID.T0, RegisterID.T0),
            _ => throw new ArgumentOutOfRangeException(nameof(alias)),
        };
    }

    /// <summary>
    /// r1 と r2 が同じレジスタか
    /// </summary>
    public static bool IsAliased(this Alias2Reg alias) {
        return alias == Alias2Reg.Same;
    }

    /// <summary>
    /// r2 と r3 (3レジスタ命令の2つのソース) が同じレジスタか
    /// </summary>
    public static bool SourcesAliased(this Alias3Reg alias) {
        return alias is Alias3Reg.RsRt or Alias3Reg.All;
    }

    /// <summary>
    /// アセンブリ上のレジスタ名 (例: <c>$t0</c>)
    /// </summary>
    public static string Name(RegisterID id) {
        return $"${id.ToString().ToLowerInvariant()}";
    }
}
