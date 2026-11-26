namespace PlusPim.Conformance.Tests;

/// <summary>
/// SPIMのソースの除外する行の範囲
/// </summary>
/// <param name="First">元のファイルの最初の行. 1始まり</param>
/// <param name="Last">元のファイルの最後の行. 範囲に含む</param>
/// <param name="Reason">除外する理由</param>
/// <param name="Replacement">各行の代わりに置く行. 番地を直書きするテストで後続の配置を保つときに <c>nop</c> にする</param>
internal readonly record struct Excluded(int First, int Last, string Reason, string Replacement = "");

/// <summary>
/// 除外と方言の書き換えを済ませたSPIMのソース
/// </summary>
/// <param name="Text">PlusPimへ渡すソース</param>
/// <param name="Origin">書き換えた各行の元のファイルの行. 1始まり</param>
internal sealed record SpimSource(string Text, int[] Origin) {
    /// <summary>除外した行を置き換え, 残りを <see cref="SpimDialect.Translate"/> で書き換える</summary>
    public static SpimSource Of(string[] lines, IEnumerable<Excluded> excluded) {
        string?[] replaced = new string?[lines.Length + 1];
        foreach(Excluded e in excluded) {
            for(int i = e.First; i <= e.Last; i++) {
                replaced[i] = e.Replacement;
            }
        }
        List<string> text = [];
        List<int> origin = [];
        for(int i = 1; i <= lines.Length; i++) {
            foreach(string l in replaced[i] is string r ? [r] : SpimDialect.Translate(lines[i - 1])) {
                text.Add(l);
                origin.Add(i);
            }
        }
        return new(string.Join("\n", text), [.. origin]);
    }
}
