using PlusPim.Image;
using PlusPim.Instructions;
using System.Diagnostics;

namespace PlusPim.Asm;

/// <summary>
/// セクションを配置し, シンボルを解決してプログラムを作る
/// </summary>
internal static class Linker {
    private const string EntryName = "main";

    /// <summary>
    /// セクションを配置し, シンボルを解決して1つのプログラムにする
    /// </summary>
    /// <param name="units">ファイルごとのアセンブル結果. アドレスが指定されていな場合はこの順に配置される.</param>
    /// <param name="paths"><see cref="SourceId"/> の順のソースのパス</param>
    /// <param name="diag">診断の報告先</param>
    /// <returns>エラーがあれば <see langword="null"/></returns>
    public static AssembledProgram? Link(List<ObjectUnit> units, List<SourcePath> paths, Diagnostics diag) {
        Address[][] bases = Place(units, diag);

        Dictionary<string, Address> globals = [];
        for(int u = 0; u < units.Count; u++) {
            foreach((string name, SourceLocation loc) in units[u].Exports) {
                if(globals.ContainsKey(name)) {
                    diag.Report(WarningCode.DuplicateGlobal, loc, name);
                }
                (int sec, uint off) = units[u].Locals[name];
                globals[name] = bases[u][sec] + off;
            }
        }

        List<(Address, IExecute[])> text = [];
        List<(Address, ReadOnlyMemory<byte>)> data = [];
        List<LineSpan> spans = [];
        List<Symbol> symbols = [];
        for(int u = 0; u < units.Count; u++) {
            ObjectUnit unit = units[u];
            Address[] b = bases[u];
            foreach((string name, (int sec, uint off)) in unit.Locals) {
                symbols.Add(new Symbol(name, b[sec] + off, unit.Exports.ContainsKey(name)));
            }
            for(int s = 0; s < unit.Sections.Count; s++) {
                Section sec = unit.Sections[s];
                if(sec.Kind == SegmentKind.Data) {
                    data.Add((b[s], sec.Bytes.ToArray()));
                    continue;
                }
                List<IExecute> code = [];
                foreach(CodeLine line in sec.Code) {
                    Address at = b[s] + line.Offset;
                    spans.Add(new LineSpan(line.Location, at, line.Templates.Length, line.Pseudo));
                    HashSet<string> reported = [];
                    Address Resolve(string name) {
                        if(unit.Locals.TryGetValue(name, out (int Sec, uint Off) l)) {
                            return b[l.Sec] + l.Off;
                        }
                        if(globals.TryGetValue(name, out Address g)) {
                            return g;
                        }
                        if(reported.Add(name)) {
                            diag.Report(WarningCode.UndefinedSymbol, line.Location, name);
                        }
                        return default;
                    }
                    // パーサが仮のアドレスで検査済みなので成功する
                    code.AddRange(line.Templates.Select(t => t.Resolve(Resolve) ?? throw new UnreachableException()));
                }
                text.Add((b[s], [.. code]));
            }
        }

        if(!globals.TryGetValue(EntryName, out Address entry)) {
            diag.Report(WarningCode.MissingEntry, null, EntryName);
            entry = units.SelectMany((u, i) => u.Sections.Select((s, j) => (s.Kind, Base: bases[i][j])))
                .Where(s => s.Kind == SegmentKind.Text)
                .Select(s => s.Base)
                .DefaultIfEmpty(MemoryMap.Text.Base)
                .MinBy(a => a.Addr);
        }

        return diag.HasError
            ? null
            : new AssembledProgram(ProgramImage.Create(entry, text, data), DebugInfo.Create(SourceMap.Create(spans), paths, symbols));
    }

    /// <summary>
    /// ファイル順, 出現順にたどり, セグメントごとにセクションの先頭を決める. その過程で範囲と重なりを検査する.
    /// </summary>
    private static Address[][] Place(List<ObjectUnit> units, Diagnostics diag) {
        // 範囲外のセクションでも検査を続けるため, 32bitを超えうる値で数える
        Dictionary<SegmentKind, ulong> counters = Enum.GetValues<SegmentKind>().ToDictionary(k => k, k => (ulong)MemoryMap.Of(k).Base.Addr);
        List<(ulong Base, Section Sec)> placed = [];
        Address[][] bases = new Address[units.Count][];
        for(int u = 0; u < units.Count; u++) {
            bases[u] = new Address[units[u].Sections.Count];
            for(int s = 0; s < units[u].Sections.Count; s++) {
                Section sec = units[u].Sections[s];
                ulong b = sec.Fixed?.Addr ?? ((counters[sec.Kind] + sec.Alignment - 1) / sec.Alignment * sec.Alignment);
                counters[sec.Kind] = b + sec.Size;
                if(uint.MaxValue < b || !MemoryMap.Of(sec.Kind).Contains(new Address((uint)b), sec.Size)) {
                    diag.Report(ErrorCode.SectionOutOfRange, sec.Location, $"0x{b:X}", sec.Size.ToString());
                }
                bases[u][s] = new Address(unchecked((uint)b));
                if(0 < sec.Size) {
                    placed.Add((b, sec));
                }
            }
        }

        // 開始アドレス順に, それまでで最も後ろまで届くセクションと比べる
        placed.Sort((x, y) => x.Base.CompareTo(y.Base));
        (ulong end, Section? last) = (0, null);
        foreach((ulong b, Section sec) in placed) {
            if(b < end) {
                diag.Report(ErrorCode.SectionOverlap, sec.Location, $"0x{b:X}", last!.Location.Sid.Index.ToString(), last.Location.Line.ToString());
            }
            if(end < b + sec.Size) {
                (end, last) = (b + sec.Size, sec);
            }
        }
        return bases;
    }
}
