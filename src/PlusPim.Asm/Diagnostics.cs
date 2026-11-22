using PlusPim.Image;
using System.Collections.Immutable;

namespace PlusPim.Asm;

/// <summary>
/// 診断を出現順に集める
/// </summary>
internal sealed class Diagnostics {
    private readonly ImmutableArray<Diagnostic>.Builder _items = ImmutableArray.CreateBuilder<Diagnostic>();

    /// <summary>エラーを1つ以上報告した</summary>
    public bool HasError { get; private set; }

    /// <summary>診断を追加する</summary>
    /// <param name="code">診断の番号</param>
    /// <param name="loc">診断が発生した場所. 行に結びつかない診断では <see langword="null"/>.</param>
    /// <param name="args">表示に埋め込む値</param>
    public void Report(DiagnosticCode code, SourceLocation? loc, params ReadOnlySpan<string> args) {
        this.HasError |= code is ErrorCode;
        this._items.Add(new Diagnostic(code, loc, [.. args]));
    }

    /// <summary>出現順の診断</summary>
    public ImmutableArray<Diagnostic> ToImmutable() {
        return this._items.ToImmutable();
    }
}

/// <summary>
/// 診断を報告する
/// </summary>
internal readonly record struct Reporter(Diagnostics Diagnostics, SourceLocation Location) {
    /// <summary><see cref="Location"/> の診断を追加する</summary>
    public void Report(DiagnosticCode code, params ReadOnlySpan<string> args) {
        this.Diagnostics.Report(code, this.Location, args);
    }
}
