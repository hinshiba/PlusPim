using PlusPim.Instructions;
using System.Collections.Frozen;

namespace PlusPim.Image;

/// <summary>
/// ラベルの定義
/// </summary>
/// <param name="Name">名前</param>
/// <param name="Addr">アドレス</param>
/// <param name="Global">グローバルシンボルか</param>
public readonly record struct Symbol(string Name, Address Addr, bool Global);

/// <summary>
/// デバッガのみが必要な情報
/// </summary>
public sealed class DebugInfo {
    private readonly SourcePath[] _paths;
    private readonly FrozenDictionary<SourcePath, SourceId> _ids;
    private readonly FrozenDictionary<Address, string> _names;

    public SourceMap SourceMap { get; }

    private DebugInfo(SourceMap sourceMap, SourcePath[] paths, FrozenDictionary<SourcePath, SourceId> ids, FrozenDictionary<Address, string> names) {
        this.SourceMap = sourceMap;
        this._paths = paths;
        this._ids = ids;
        this._names = names;
    }

    /// <param name="sourceMap">アドレスと行の対応</param>
    /// <param name="paths"><see cref="SourceId"/> の順のソースのパス</param>
    /// <param name="symbols">定義順のラベル</param>
    /// <exception cref="ArgumentException">同じパスの重複</exception>
    public static DebugInfo Create(SourceMap sourceMap, IEnumerable<SourcePath> paths, IEnumerable<Symbol> symbols) {
        SourcePath[] p = [.. paths];
        Dictionary<SourcePath, SourceId> ids = [];
        for(int i = 0; i < p.Length; i++) {
            if(!ids.TryAdd(p[i], new SourceId(i))) {
                throw new ArgumentException($"Path {p[i]} appears twice.", nameof(paths));
            }
        }

        // 同じアドレスでは
        // 1. グローバルを優先
        // 2. 同じスコープならラベルは後に出たものが優先
        Dictionary<Address, Symbol> names = [];
        foreach(Symbol s in symbols) {
            if(!names.TryGetValue(s.Addr, out Symbol prev) || (s.Global && !prev.Global)) {
                names[s.Addr] = s;
            }
        }

        return new DebugInfo(sourceMap, p, ids.ToFrozenDictionary(), names.ToFrozenDictionary(kv => kv.Key, kv => kv.Value.Name));
    }

    /// <summary>
    /// <paramref name="addr"/> に解決するシンボル. ない場合は <see langword="null"/>
    /// </summary>
    public string? SymbolAt(Address addr) {
        return this._names.GetValueOrDefault(addr);
    }

    public SourcePath PathOf(SourceId sid) {
        return this._paths[sid.Index];
    }

    public SourceId? IdOf(SourcePath path) {
        return this._ids.TryGetValue(path, out SourceId id) ? id : null;
    }
}
