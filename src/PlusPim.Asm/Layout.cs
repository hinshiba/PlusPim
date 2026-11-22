using PlusPim.Image;
using PlusPim.Instructions;

namespace PlusPim.Asm;

/// <summary>セクション内の相対オフセットに置いた1行分の命令</summary>
internal readonly record struct CodeLine(SourceLocation Location, uint Offset, Template[] Templates, string? Pseudo);

/// <summary>
/// 連続して配置する区間
/// </summary>
/// <param name="kind">配置先のセグメント</param>
/// <param name="origin">指定された先頭. なければその種類の前のセクションに続ける</param>
/// <param name="location">始めた行</param>
internal abstract class Section(SegmentKind kind, Address? origin, SourceLocation location) {
    /// <summary>配置先のセグメント</summary>
    public SegmentKind Kind => kind;
    /// <summary>指定された先頭</summary>
    public Address? Origin => origin;
    /// <summary>始めた行</summary>
    public SourceLocation Location => location;
    /// <summary>バイト数</summary>
    public uint Size { get; private set; }

    /// <summary>
    /// 先頭が揃うべき境界. <see cref="Origin"/> がなければリンカが先頭をこれに揃える
    /// </summary>
    /// <remarks><c>.align</c> を相対オフセットで解決するために, 先頭の整列を保証する</remarks>
    public abstract uint Alignment { get; }

    /// <summary>セグメント指令から種類に合うセクションを始める</summary>
    public static Section Open(SwitchSegment s, SourceLocation loc) {
        return s.Kind == SegmentKind.Data ? new DataSection(s.Origin, loc) : new TextSection(s.Kind, s.Origin, loc);
    }

    /// <summary>
    /// 末尾を <paramref name="by"/> だけ進め, 進める前の末尾を返す. 32bitを超えたら <see cref="uint.MaxValue"/> で止める
    /// </summary>
    /// <remarks>範囲外はリンカが報告するので, 0に戻って範囲内に見えることだけを防ぐ</remarks>
    protected uint Advance(ulong by) {
        uint at = this.Size;
        this.Size = (uint)Math.Min(at + by, uint.MaxValue);
        return at;
    }
}

/// <summary>命令を置くセクション. <see cref="SegmentKind.Text"/> か <see cref="SegmentKind.KText"/></summary>
internal sealed class TextSection(SegmentKind kind, Address? origin, SourceLocation location): Section(kind, origin, location) {
    /// <summary>出現順の命令の行</summary>
    public List<CodeLine> Code { get; } = [];

    /// <inheritdoc/>
    public override uint Alignment => 4;

    /// <summary><paramref name="loc"/> の行の命令を末尾に置く</summary>
    public void Place(SourceLocation loc, PlaceCode c) {
        this.Code.Add(new CodeLine(loc, this.Advance((ulong)c.Templates.Length * 4), c.Templates, c.Pseudo));
    }
}

/// <summary>データを置くセクション. 初期化しない領域は <see cref="Initialized"/> の間の穴になる</summary>
internal sealed class DataSection(Address? origin, SourceLocation location): Section(SegmentKind.Data, origin, location) {
    private uint _alignment = 1;

    /// <summary>初期化済みの連続したバイト列と相対オフセット</summary>
    public List<(uint Offset, List<byte> Bytes)> Initialized { get; } = [];

    /// <inheritdoc/>
    public override uint Alignment => this._alignment;

    /// <summary>バイト列を末尾に置く. 直前のバイト列に続くなら連結する</summary>
    public void Emit(byte[] bytes) {
        uint at = this.Advance((ulong)bytes.Length);
        if(this.Initialized is [.., (uint o, List<byte> tail)] && o + tail.Count == at) {
            tail.AddRange(bytes);
        } else {
            this.Initialized.Add((at, [.. bytes]));
        }
    }

    /// <summary>初期化しない領域を末尾に確保する</summary>
    public void Reserve(uint size) {
        _ = this.Advance(size);
    }

    /// <summary>末尾を2の <paramref name="log2"/> 乗の倍数まで進める</summary>
    /// <remarks>指定アドレスのセクションは絶対アドレスで, それ以外は先頭からの相対で揃える</remarks>
    public void Align(int log2) {
        uint mask = (1u << log2) - 1;
        _ = this.Advance((0u - ((this.Origin?.Addr ?? 0) + this.Size)) & mask);
        this._alignment = Math.Max(this._alignment, mask + 1);
    }
}

/// <summary>
/// 1ファイルのアセンブル結果. アセンブルとリンクの間だけの約束である
/// </summary>
/// <param name="Sections">出現順のセクション</param>
/// <param name="Labels">定義順のラベル. 値はセクションの位置と相対オフセット</param>
/// <param name="Globals">公開する名前と宣言した行</param>
internal sealed record ObjectUnit(
    List<Section> Sections,
    Dictionary<string, (int Section, uint Offset)> Labels,
    Dictionary<string, SourceLocation> Globals);

/// <summary>
/// <see cref="LayoutEffect"/> を順に適用して, セクションとラベルの相対オフセットを決める
/// </summary>
internal sealed class Layout(Diagnostics diag) {
    private readonly List<Section> _sections = [];
    private readonly Dictionary<string, (int, uint)> _labels = [];
    private readonly Dictionary<string, SourceLocation> _globals = [];

    /// <summary>次に配置するものの先頭で確定するラベル. <c>.align</c> の後の位置を指すため遅らせる</summary>
    private readonly List<string> _pendingLabels = [];

    /// <summary>
    /// <paramref name="loc"/> の行の効果を1つ適用する. 置けない効果は報告して捨てる
    /// </summary>
    public void Apply(SourceLocation loc, LayoutEffect effect) {
        switch(effect) {
            case SwitchSegment s:
                this.Flush();
                this._sections.Add(Section.Open(s, loc));
                return;
            case DeclareGlobal g:
                this._globals[g.Name] = loc;
                return;
            case DefineLabel l:
                if(this._labels.ContainsKey(l.Name) || this._pendingLabels.Contains(l.Name)) {
                    diag.Report(WarningCode.DuplicateLabel, loc, l.Name);
                }
                _ = this.EnsureSection(loc);
                this._pendingLabels.Add(l.Name);
                return;
        }

        switch(effect, this.EnsureSection(loc)) {
            case (PlaceCode c, TextSection t):
                this.Flush();
                t.Place(loc, c);
                break;
            case (PlaceCode, _):
                diag.Report(WarningCode.MisplacedInstruction, loc);
                break;
            case (Emit e, DataSection d):
                this.Flush();
                d.Emit(e.Bytes);
                break;
            case (Reserve r, DataSection d):
                this.Flush();
                d.Reserve(r.Size);
                break;
            case (AlignTo a, DataSection d):
                d.Align(a.Log2);
                break;
            default:
                diag.Report(WarningCode.MisplacedDirective, loc);
                break;
        }
    }

    /// <summary>
    /// 未確定のラベルを確定し, 定義のない公開を捨てて結果を返す
    /// </summary>
    public ObjectUnit Finish() {
        this.Flush();
        foreach((string name, SourceLocation loc) in this._globals.Where(e => !this._labels.ContainsKey(e.Key)).ToList()) {
            diag.Report(WarningCode.UndefinedGlobal, loc, name);
            _ = this._globals.Remove(name);
        }
        return new ObjectUnit(this._sections, this._labels, this._globals);
    }

    /// <summary>現在のセクション. セグメント指令より前ならテキストセクションを始める</summary>
    private Section EnsureSection(SourceLocation loc) {
        if(this._sections.Count == 0) {
            diag.Report(WarningCode.MissingSegment, loc);
            this._sections.Add(new TextSection(SegmentKind.Text, null, loc));
        }
        return this._sections[^1];
    }

    /// <summary>未確定のラベルを現在のセクションの末尾に確定する</summary>
    private void Flush() {
        if(this._sections.Count == 0) {
            return;
        }
        foreach(string name in this._pendingLabels) {
            this._labels[name] = (this._sections.Count - 1, this._sections[^1].Size);
        }
        this._pendingLabels.Clear();
    }
}
