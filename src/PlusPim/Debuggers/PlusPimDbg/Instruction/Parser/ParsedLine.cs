using PlusPim.Debuggers.PlusPimDbg.Program;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;

/// <summary>
/// 解析済みの1行．展開後の命令数はパス1の時点で確定している
/// </summary>
/// <remarks>
/// シンボルを参照しない行 (実命令，<c>li</c>，<c>move</c>，<c>nop</c>) は命令列を保持する．
/// シンボルを参照する行 (<c>la</c>) はパス2でシンボルを解決して命令列を作る
/// </remarks>
internal sealed class ParsedLine {
    /// <summary>
    /// シンボルを解決して命令列を作る
    /// </summary>
    /// <param name="symbols">シンボルの解決に使う</param>
    /// <param name="unresolved">解決できなかったシンボル名．すべて解決できた場合は<see langword="null"/></param>
    /// <returns>命令列．解決できなかった場合も<see cref="Size"/>個の命令を返す</returns>
    public delegate IInstruction[] SymbolExpansion(ISymbolResolver symbols, out string? unresolved);

    private readonly IInstruction[]? _instructions;
    private readonly SymbolExpansion? _expansion;

    /// 展開後の命令数
    public int Size { get; }

    private ParsedLine(int size, IInstruction[]? instructions, SymbolExpansion? expansion) {
        this.Size = size;
        this._instructions = instructions;
        this._expansion = expansion;
    }

    /// <summary>
    /// 命令列が確定している行
    /// </summary>
    public static ParsedLine Fixed(params IInstruction[] instructions) {
        return new ParsedLine(instructions.Length, instructions, null);
    }

    /// <summary>
    /// シンボルの解決を待つ行
    /// </summary>
    /// <param name="size">展開後の命令数．シンボルの解決結果によらない</param>
    /// <param name="expansion">命令列を作る関数</param>
    public static ParsedLine Deferred(int size, SymbolExpansion expansion) {
        return new ParsedLine(size, null, expansion);
    }

    /// <summary>
    /// 命令列を得る
    /// </summary>
    /// <param name="symbols">シンボルの解決に使う</param>
    /// <param name="unresolved">解決できなかったシンボル名．すべて解決できた場合は<see langword="null"/></param>
    /// <returns>長さが<see cref="Size"/>の命令列</returns>
    /// <exception cref="InvalidOperationException">命令数が<see cref="Size"/>と異なる場合</exception>
    public IInstruction[] Materialize(ISymbolResolver symbols, out string? unresolved) {
        if(this._instructions is not null) {
            unresolved = null;
            return this._instructions;
        }

        IInstruction[] instructions = this._expansion!(symbols, out unresolved);
        return instructions.Length == this.Size
            ? instructions
            : throw new InvalidOperationException($"Expansion produced {instructions.Length} instruction(s), but {this.Size} were reserved in pass 1.");
    }
}
