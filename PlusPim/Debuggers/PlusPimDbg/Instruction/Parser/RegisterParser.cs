using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Collections.Frozen;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;

/// <summary>
/// レジスタ指定を解析する
/// </summary>
/// <remarks>
/// 先頭に0を付けない <c>0</c>-<c>31</c> と，<see cref="RegisterID"/> の小文字だけ受け付ける．
/// </remarks>
internal static class RegisterParser {
    private const int RegisterCount = 32;

    private static readonly FrozenDictionary<string, RegisterID> Names =
        Enum.GetValues<RegisterID>().ToFrozenDictionary(id => id.ToString().ToLowerInvariant(), StringComparer.Ordinal);

    /// <summary>
    /// レジスタ名か番号を解析する
    /// </summary>
    /// <param name="nameWithoutDollar"><c>$</c> を除いたレジスタ指定</param>
    /// <param name="register">成功した場合はレジスタ番号</param>
    /// <returns>成功なら<see langword="true"/></returns>
    public static bool TryParse(string nameWithoutDollar, out RegisterID register) {
        if(TryParseNumber(nameWithoutDollar, out int number)) {
            register = (RegisterID)number;
            return true;
        }
        return Names.TryGetValue(nameWithoutDollar, out register);
    }

    /// <summary>
    /// レジスタ番号 (<c>0</c>-<c>31</c>，先頭の0は不可) を解析する
    /// </summary>
    /// <param name="nameWithoutDollar"><c>$</c> を除いたレジスタ指定</param>
    /// <param name="number">成功した場合は番号</param>
    /// <returns>成功なら<see langword="true"/></returns>
    public static bool TryParseNumber(string nameWithoutDollar, out int number) {
        number = 0;
        if(nameWithoutDollar.Length is 0 or > 2 || (nameWithoutDollar.Length == 2 && nameWithoutDollar[0] == '0')) {
            return false;
        }
        foreach(char c in nameWithoutDollar) {
            if(!char.IsAsciiDigit(c)) {
                return false;
            }
            number = (number * 10) + (c - '0');
        }
        return number < RegisterCount;
    }
}
