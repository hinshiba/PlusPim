using PlusPim.Instructions;
using System.Collections.Frozen;

namespace PlusPim.Image;

/// <summary>
/// リンク後のラベル
/// </summary>
/// <param name="Name">名前</param>
/// <param name="Addr">アドレス</param>
/// <param name="Global">グローバルシンボルか</param>
public readonly record struct Symbol(string Name, Address Addr, bool Global);

/// <summary>
/// デバッガのみが必要な情報
/// </summary>
public sealed class DebugInfo {
    private readonly FrozenDictionary<Address, string> _names;

    /// <summary>
    /// アドレスとソース上の行の対応
    /// </summary>
    public SourceMap SourceMap { get; }

    private DebugInfo(SourceMap sourceMap, FrozenDictionary<Address, string> names) {
        this.SourceMap = sourceMap;
        this._names = names;
    }

    /// <summary>
    /// デバッグ情報を作る
    /// </summary>
    /// <param name="sourceMap">アドレスと行の対応</param>
    /// <param name="symbols">定義順のラベル</param>
    public static DebugInfo Create(SourceMap sourceMap, IEnumerable<Symbol> symbols) {
        // 同じアドレスでは
        // 1. グローバルを優先
        // 2. 同じスコープならラベルは先に出たものが優先
        Dictionary<Address, Symbol> names = [];
        foreach(Symbol s in symbols) {
            if(!names.TryGetValue(s.Addr, out Symbol prev) || (s.Global && !prev.Global)) {
                names[s.Addr] = s;
            }
        }

        return new DebugInfo(sourceMap, names.ToFrozenDictionary(kv => kv.Key, kv => kv.Value.Name));
    }

    /// <summary>
    /// <paramref name="addr"/> に解決するシンボル. ない場合は <see langword="null"/>
    /// </summary>
    public string? SymbolAt(Address addr) {
        return this._names.GetValueOrDefault(addr);
    }
}
