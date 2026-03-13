namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// MIPSの汎用レジスタを識別する列挙型
/// </summary>
internal enum RegisterID {
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
