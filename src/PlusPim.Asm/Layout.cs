using PlusPim.Image;
using PlusPim.Instructions;

namespace PlusPim.Asm;

/// <summary>セクション内の相対オフセットに置いた1行分の命令</summary>
internal readonly record struct CodeLine(SourceLocation Location, uint Offset, Template[] Templates, string? Pseudo);

/// <summary>
/// 連続して配置する区間. テキスト系は <see cref="Code"/> を, データは <see cref="Bytes"/> を持つ
/// </summary>
/// <param name="kind">配置先のセグメント</param>
/// <param name="fixed">指定された配置先. なければその種類の前のセクションに続ける</param>
/// <param name="location">始めた行</param>
internal sealed class Section(SegmentKind kind, Address? @fixed, SourceLocation location) {
    /// <summary>配置先のセグメント</summary>
    public SegmentKind Kind => kind;
    /// <summary>指定された配置先</summary>
    public Address? Fixed => @fixed;
    /// <summary>始めた行</summary>
    public SourceLocation Location => location;
    /// <summary>出現順の命令の行</summary>
    public List<CodeLine> Code { get; } = [];
    /// <summary>データの内容</summary>
    public List<byte> Bytes { get; } = [];
    /// <summary>バイト数</summary>
    public uint Size { get; set; }

    /// <summary>
    /// 先頭が揃うべき境界. <see cref="Fixed"/> がなければリンカが先頭をこれに揃える
    /// </summary>
    /// <remarks><c>.align</c> を相対オフセットで解決するために, 先頭の整列を保証する</remarks>
    public uint Alignment { get; set; } = kind == SegmentKind.Data ? 1u : 4u;
}

/// <summary>
/// 1ファイルのアセンブル結果. アセンブルとリンクの間だけの約束である
/// </summary>
/// <param name="Sections">出現順のセクション</param>
/// <param name="Locals">定義順のラベル. 値はセクションの位置と相対オフセット</param>
/// <param name="Exports">公開する名前と宣言した行</param>
internal sealed record ObjectUnit(
    List<Section> Sections,
    Dictionary<string, (int Section, uint Offset)> Locals,
    Dictionary<string, SourceLocation> Exports);

/// <summary>
/// <see cref="LayoutEffect"/> を順に適用して, セクションとラベルの相対オフセットを決める
/// </summary>
internal sealed class Layout(Diagnostics diag) {
    private readonly List<Section> _sections = [];
    private readonly Dictionary<string, (int, uint)> _locals = [];
    private readonly Dictionary<string, SourceLocation> _exports = [];

    /// <summary>次に配置するものの先頭で確定するラベル. <c>.align</c> の後の位置を指すため遅らせる</summary>
    private readonly List<string> _pending = [];

    /// <summary>
    /// <paramref name="loc"/> の行の効果を1つ適用する. 置けない効果は報告して捨てる
    /// </summary>
    public void Apply(SourceLocation loc, LayoutEffect effect) {
        switch(effect) {
            case SwitchSegment s:
                this.Flush();
                this._sections.Add(new Section(s.Kind, s.Fixed, loc));
                return;
            case DeclareGlobal g:
                this._exports[g.Name] = loc;
                return;
            case DefineLabel l:
                if(this._locals.ContainsKey(l.Name) || this._pending.Contains(l.Name)) {
                    diag.Report(WarningCode.DuplicateLabel, loc, l.Name);
                }
                this.Current(loc);
                this._pending.Add(l.Name);
                return;
        }

        Section sec = this.Current(loc);
        if(effect is PlaceCode c) {
            if(sec.Kind == SegmentKind.Data) {
                diag.Report(WarningCode.MisplacedInstruction, loc);
                return;
            }
            this.Flush();
            sec.Code.Add(new CodeLine(loc, sec.Size, c.Templates, c.Pseudo));
            sec.Size += (uint)c.Templates.Length * 4;
            return;
        }
        if(sec.Kind != SegmentKind.Data) {
            diag.Report(WarningCode.MisplacedDirective, loc);
            return;
        }
        switch(effect) {
            case Emit e:
                this.Flush();
                sec.Bytes.AddRange(e.Bytes);
                break;
            case Reserve r:
                this.Flush();
                sec.Bytes.AddRange(new byte[r.Size]);
                break;
            case Align a:
                // 指定アドレスのセクションは絶対アドレスで, それ以外は先頭からの相対で揃える
                uint mask = (1u << a.Log2) - 1;
                uint pad = (0u - ((sec.Fixed?.Addr ?? 0) + sec.Size)) & mask;
                sec.Bytes.AddRange(new byte[pad]);
                sec.Alignment = Math.Max(sec.Alignment, mask + 1);
                break;
        }
        sec.Size = (uint)sec.Bytes.Count;
    }

    /// <summary>
    /// 未確定のラベルを確定し, 定義のない公開を捨てて結果を返す
    /// </summary>
    public ObjectUnit Finish() {
        this.Flush();
        foreach((string name, SourceLocation loc) in this._exports.Where(e => !this._locals.ContainsKey(e.Key)).ToList()) {
            diag.Report(WarningCode.UndefinedGlobal, loc, name);
            this._exports.Remove(name);
        }
        return new ObjectUnit(this._sections, this._locals, this._exports);
    }

    /// <summary>現在のセクション. セグメント指令より前ならテキストセクションを始める</summary>
    private Section Current(SourceLocation loc) {
        if(this._sections.Count == 0) {
            diag.Report(WarningCode.MissingSegment, loc);
            this._sections.Add(new Section(SegmentKind.Text, null, loc));
        }
        return this._sections[^1];
    }

    /// <summary>未確定のラベルを現在のセクションの末尾に確定する</summary>
    private void Flush() {
        if(this._sections.Count == 0) {
            return;
        }
        foreach(string name in this._pending) {
            this._locals[name] = (this._sections.Count - 1, this._sections[^1].Size);
        }
        this._pending.Clear();
    }
}
