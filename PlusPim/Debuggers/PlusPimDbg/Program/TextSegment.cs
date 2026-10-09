using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// テキストセグメントを表現する
/// </summary>
/// <param name="instructions">命令列</param>
/// <param name="pseudoExpansions">疑似命令の行の展開先</param>
/// <remarks>
/// カーネルテキストセグメントもこのクラスで表現する
/// </remarks>
internal sealed class TextSegment(List<IInstruction> instructions, List<PseudoExpansion> pseudoExpansions) {
    /// <summary>
    /// (ユーザー)テキストセグメントの開始アドレス
    /// </summary>
    public static readonly Address TextSegmentBase = new(0x400000);

    /// <summary>
    /// カーネルテキストセグメントの開始アドレス
    /// </summary>
    public static readonly Address KernelTextSegmentBase = new(0x80000180);

    public ReadOnlySpan<IInstruction> Instructions => this._instructions.AsSpan();
    private readonly IInstruction[] _instructions = [.. instructions];

    /// <summary>
    /// 疑似命令の行と，展開先の命令の範囲 (行の順)
    /// </summary>
    public IReadOnlyList<PseudoExpansion> PseudoExpansions { get; } = [.. pseudoExpansions];
}

/// <summary>
/// 疑似命令の1行の展開先
/// </summary>
/// <param name="SourceLine">1始まりの行番号</param>
/// <param name="Mnemonic">疑似命令のニーモニック</param>
/// <param name="FirstIndex">展開先の最初の命令の，セグメント内のインデックス</param>
/// <param name="Count">展開先の命令数</param>
internal readonly record struct PseudoExpansion(int SourceLine, string Mnemonic, int FirstIndex, int Count);
