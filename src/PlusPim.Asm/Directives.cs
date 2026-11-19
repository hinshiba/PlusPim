using PlusPim.Image;
using System.Collections.Frozen;
using static PlusPim.Asm.DirectiveParsers;

namespace PlusPim.Asm;

/// <summary>
/// アセンブラ指令の一覧.
/// </summary>
internal static class Directives {
    /// <summary>
    /// アセンブラ指令のオペランドを配置の効果へ変換する
    /// </summary>
    /// <param name="args">アセンブラ指令のオペランド</param>
    /// <param name="r">エラーの報告先</param>
    /// <returns>オペランドの形が合わなければ <see langword="null"/></returns>
    public delegate LayoutEffect[]? Parse(ReadOnlySpan<Arg> args, Reporter r);

    /// <summary><c>.</c> を除いたアセンブラ指令名と解析関数の対応</summary>
    public static FrozenDictionary<string, Parse> ByName { get; } = new Dictionary<string, Parse> {
        ["text"] = Segment(SegmentKind.Text),
        ["ktext"] = Segment(SegmentKind.KText),
        ["data"] = Segment(SegmentKind.Data),
        ["byte"] = Ints(1, sbyte.MinValue, byte.MaxValue),
        ["half"] = Ints(2, short.MinValue, ushort.MaxValue),
        ["word"] = Ints(4, int.MinValue, uint.MaxValue),
        ["ascii"] = String(ascii: true, nul: false),
        ["asciiz"] = String(ascii: true, nul: true),
        ["utf-8z"] = String(ascii: false, nul: true),
        ["space"] = Space,
        ["align"] = AlignTo,
        ["globl"] = Global,
        ["global"] = Global,
    }.ToFrozenDictionary(StringComparer.Ordinal);
}
