using PlusPim.Instructions;

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
    public static readonly Segment Text = new(new(0x00400000), new(0x00400000), new(0x10000000));
    public static readonly Segment Data = new(new(0x10000000), new(0x10000000), new(0x80000000));
    public static readonly Segment KText = new(new(0x80000180), new(0x80000000), new(0x90000000));

    public static Segment Of(SegmentKind kind) {
        return kind switch {
            SegmentKind.Text => Text,
            SegmentKind.KText => KText,
            SegmentKind.Data => Data,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    /// <summary>
    /// <paramref name="addr"/> を含むセグメント. どこにも属さなければ <see langword="null"/>
    /// </summary>
    public static SegmentKind? KindOf(Address addr) {
        return Text.Contains(addr, 1) ? SegmentKind.Text
            : KText.Contains(addr, 1) ? SegmentKind.KText
            : Data.Contains(addr, 1) ? SegmentKind.Data
            : null;
    }
}
