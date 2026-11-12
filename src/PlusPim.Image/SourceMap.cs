using PlusPim.Instructions;
using System.Collections.Frozen;

namespace PlusPim.Image;

/// <summary>
/// 命令の元となった1行と, その命令の連続した区間
/// </summary>
/// <param name="Location">ソース上の行</param>
/// <param name="Start">先頭の命令のアドレス</param>
/// <param name="Count">命令数</param>
/// <param name="Pseudo">疑似命令ならそのニーモニック</param>
public readonly record struct LineSpan(SourceLocation Location, Address Start, int Count, string? Pseudo) {
    public Address End => this.Start + ((uint)this.Count * 4);
}

/// <summary>
/// アドレスとソース上の行の対応
/// </summary>
/// <remarks>命令のみ</remarks>
public sealed class SourceMap {
    private readonly LineSpan[] _spans;
    private readonly uint[] _starts;
    private readonly FrozenDictionary<SourceLocation, Address> _byLocation;

    private SourceMap(LineSpan[] spans) {
        this._spans = spans;
        this._starts = [.. spans.Select(s => s.Start.Addr)];
        this._byLocation = spans.ToFrozenDictionary(s => s.Location, s => s.Start);
    }

    /// <exception cref="ArgumentException">区間に重なりや空の区間, 行の重複がある場合</exception>
    public static SourceMap Create(IEnumerable<LineSpan> spans) {
        LineSpan[] sorted = [.. spans.OrderBy(s => s.Start.Addr)];
        HashSet<SourceLocation> seen = [];
        for(int i = 0; i < sorted.Length; i++) {
            LineSpan s = sorted[i];
            if(s.Count <= 0) {
                throw new ArgumentException($"Line span at {s.Start} is empty.", nameof(spans));
            }
            if(i > 0 && s.Start < sorted[i - 1].End) {
                throw new ArgumentException($"Line span at {s.Start} overlaps the previous one.", nameof(spans));
            }
            if(!seen.Add(s.Location)) {
                throw new ArgumentException($"Line {s.Location} appears twice.", nameof(spans));
            }
        }
        return new SourceMap(sorted);
    }

    /// <summary>
    /// <paramref name="addr"/> の命令と対応するソース上の行
    /// </summary>
    public SourceLocation? LocationOf(Address addr) {
        int i = Sorted.Floor(this._starts, addr.Addr);
        return i >= 0 && addr < this._spans[i].End ? this._spans[i].Location : null;
    }

    /// <summary>
    /// <paramref name="loc"/> の行の命令. 命令のない行は <see langword="null"/>
    /// </summary>
    public Address? AddressOf(SourceLocation loc) {
        return this._byLocation.TryGetValue(loc, out Address a) ? a : null;
    }

    /// <summary>
    /// <paramref name="sid"/> の疑似命令の行をアドレス順に列挙する
    /// </summary>
    public IEnumerable<LineSpan> PseudosIn(SourceId sid) {
        return this._spans.Where(s => s.Location.Sid == sid && s.Pseudo is not null);
    }
}
