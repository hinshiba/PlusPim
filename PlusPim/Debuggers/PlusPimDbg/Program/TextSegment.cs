using PlusPim.Debuggers.PlusPimDbg.Instructions;
using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// テキストセグメントを表現する
/// </summary>
/// <param name="instructions">命令列</param>
/// <remarks>
/// カーネルテキストセグメントもこのクラスで表現する
/// </remarks>
internal sealed class TextSegment(List<IInstruction> instructions, Address addr) {
    /// <summary>
    /// 1ファイル名の(ユーザー)テキストセグメントの開始アドレス
    /// </summary>
    public static readonly Address TextSegmentBase = new(0x400000);

    /// <summary>
    /// 1ファイル名のカーネルテキストセグメントの開始アドレス
    /// </summary>
    public static readonly Address KernelTextSegmentBase = new(0x80000180);

    /// <summary>
    /// このインスタンスのテキストセグメントの開始アドレス
    /// </summary>
    public readonly Address BaseAddr = addr;

    public ReadOnlySpan<IInstruction> Instructions => this._instructions.AsSpan();
    private readonly IInstruction[] _instructions = [.. instructions];
}
