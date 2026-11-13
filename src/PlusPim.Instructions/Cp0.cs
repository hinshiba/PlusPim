namespace PlusPim.Instructions;

/// <summary>
/// CP0レジスタの番号とStatus のビットの解釈
/// </summary>
public static class Cp0 {
    /// <summary>Status のEXLビット. 立っている間はカーネルモード</summary>
    public const uint Exl = 1u << 1;

    /// <summary>Statusの値がカーネルモードを表すか</summary>
    public static bool IsKernel(uint status) {
        return (status & Exl) != 0;
    }

    /// <summary>
    /// <c>mfc0</c> / <c>mtc0</c> のオペランド <c>$n</c> をCP0レジスタへ解決する. 未対応の番号なら <see langword="null"/>
    /// </summary>
    /// <remarks>アセンブラは <c>$n</c> を汎用レジスタとして読むので, その番号をCP0の番号とみなす</remarks>
    public static RName? FromNumber(GprId n) {
        return (byte)n switch {
            8 => RName.BadVAddr,
            12 => RName.Status,
            13 => RName.Cause,
            14 => RName.Epc,
            _ => null,
        };
    }
}
