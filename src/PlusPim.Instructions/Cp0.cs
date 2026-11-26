using System.Collections.Frozen;

namespace PlusPim.Instructions;

/// <summary>
/// CP0レジスタの番号とStatusのビットの解釈
/// </summary>
public static class Cp0 {
    /// <summary>StatusのEXLビット. 立っている間はカーネルモード</summary>
    public const uint Exl = 1u << 1;

    /// <summary>CauseのBDビット. 分岐遅延スロットにある命令で例外が起きたことを表す</summary>
    public const uint Bd = 1u << 31;

    /// <summary>Statusの値がカーネルモードを表すか</summary>
    public static bool IsKernel(uint status) {
        return (status & Exl) != 0;
    }

    /// <summary>CP0レジスタの番号. 未対応の番号は含まない</summary>
    private static readonly FrozenDictionary<RName, byte> Numbers = new Dictionary<RName, byte> {
        [RName.BadVAddr] = 8,
        [RName.Status] = 12,
        [RName.Cause] = 13,
        [RName.Epc] = 14,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<byte, RName> ByNumber = Numbers.ToFrozenDictionary(p => p.Value, p => p.Key);

    /// <summary>
    /// <c>mfc0</c> / <c>mtc0</c> のオペランド <c>$n</c> をCP0レジスタへ解決する. 未対応の番号なら <see langword="null"/>
    /// </summary>
    /// <remarks>アセンブラは <c>$n</c> を汎用レジスタとして読むので, その番号をCP0の番号とみなす</remarks>
    public static RName? FromNumber(GprId n) {
        return ByNumber.TryGetValue((byte)n, out RName r) ? r : null;
    }

    /// <summary><see cref="FromNumber"/> の逆</summary>
    /// <exception cref="KeyNotFoundException">CP0レジスタでない場合</exception>
    public static byte ToNumber(RName r) {
        return Numbers[r];
    }
}
