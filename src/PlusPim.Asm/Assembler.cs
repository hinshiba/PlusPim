using PlusPim.Image;
using System.Collections.Immutable;

namespace PlusPim.Asm;

/// <summary>
/// ソースの読み込みを委任する
/// </summary>
public interface ISourceReader {
    /// <summary><paramref name="path"/> の内容. 読めなければ <see langword="null"/></summary>
    string? Read(SourcePath path);
}

/// <summary>
/// リンク済みのプログラム
/// </summary>
/// <param name="Image">実行で必要なプログラムイメージ</param>
/// <param name="DebugInfo">デバッガへの追加情報</param>
public sealed record AssembledProgram(ProgramImage Image, DebugInfo DebugInfo);

/// <summary>
/// アセンブルの結果
/// </summary>
/// <param name="Program">エラーがあれば <see langword="null"/></param>
/// <param name="Diagnostics">出現順の診断</param>
public sealed record AssemblyResult(AssembledProgram? Program, ImmutableArray<Diagnostic> Diagnostics);

/// <summary>
/// ソースの列をプログラムと診断の列へ変換する
/// </summary>
public static class Assembler {
    /// <summary>
    /// すべてのソースをアセンブルしてリンクする.
    /// </summary>
    /// <param name="paths">配置する順のソース</param>
    /// <param name="reader">ソースを読む方法</param>
    public static AssemblyResult Assemble(IEnumerable<SourcePath> paths, ISourceReader reader) {
        Diagnostics diag = new();
        List<SourcePath> sources = [];
        List<ObjectUnit> units = [];
        foreach(SourcePath path in paths) {
            if(sources.Contains(path)) {
                diag.Report(WarningCode.DuplicateSource, null, path.Value);
                continue;
            }
            if(reader.Read(path) is not string text) {
                diag.Report(ErrorCode.SourceNotFound, null, path.Value);
                continue;
            }
            units.Add(AssembleFile(new SourceId(sources.Count), text, diag));
            sources.Add(path);
        }
        return new AssemblyResult(Linker.Link(units, sources, diag), diag.ToImmutable());
    }

    /// <summary>1ファイルをアセンブルする</summary>
    private static ObjectUnit AssembleFile(SourcePath path, string text, Diagnostics diag) {
        Layout layout = new(diag);
        List<LayoutEffect> effects = [];
        int line = 0;
        foreach(ReadOnlySpan<char> s in text.AsSpan().EnumerateLines()) {
            SourceLocation loc = new(path, ++line);
            effects.Clear();
            Parser.Parse(Lexer.Lex(s), new Reporter(diag, loc), effects);
            foreach(LayoutEffect e in effects) {
                layout.Apply(loc, e);
            }
        }
        return layout.Finish();
    }
}
