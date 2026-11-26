using PlusPim.Instructions;
using System.Diagnostics;

namespace PlusPim.Image;

/// <summary>
/// セグメントの種類
/// </summary>
public enum SegmentKind: byte { Text, KText, Data }

/// <summary>
/// セグメントの既定の配置先と範囲
/// </summary>
/// <param name="Base">アドレスを指定しないセクションの配置先</param>
/// <param name="Start">範囲の先頭</param>
/// <param name="End">範囲の末尾の次</param>
public readonly record struct Segment(Address Base, Address Start, Address End) {
    /// <summary>
    /// <paramref name="size"/> の範囲がこのセグメントに含まれるか
    /// </summary>
    public bool Contains(Address addr, ulong size) {
        return this.Start <= addr && addr <= this.End && size <= this.End.Addr - addr.Addr;
    }
}

/// <summary>
/// 固定のメモリマップ
/// </summary>
public static class MemoryMap {
    /// <summary>
    /// ユーザのテキストセグメント
    /// </summary>
    public static readonly Segment Text = new(new(0x00400000), new(0x00400000), new(0x10000000));
    /// <summary>
    /// データセグメント
    /// </summary>
    public static readonly Segment Data = new(new(0x10000000), new(0x10000000), new(0x80000000));
    /// <summary>
    /// カーネルのテキストセグメント
    /// </summary>
    public static readonly Segment KText = new(new(0x80000180), new(0x80000000), new(0x90000000));
    /// <summary>
    /// 例外の発生時に制御が移るアドレス
    /// </summary>
    /// <remarks><see cref="KText"/> の配置先と同じ値なのは, アドレスなしの <c>.ktext</c> にハンドラを書く SPIM の慣習による</remarks>
    public static readonly Address ExceptionVector = new(0x80000180);

    /// <summary>
    /// アドレスがテキスト領域に含まれるか
    /// </summary>
    /// <remarks>これらの命令はデコード済みで保持し, メモリに置かない</remarks>
    public static bool IsText(Address addr) {
        return Text.Contains(addr, 1) || KText.Contains(addr, 1);
    }

    /// <summary>
    /// <paramref name="kind"/> のセグメント
    /// </summary>
    public static Segment Of(SegmentKind kind) {
        return kind switch {
            SegmentKind.Text => Text,
            SegmentKind.KText => KText,
            SegmentKind.Data => Data,
            _ => throw new UnreachableException(),
        };
    }
}
