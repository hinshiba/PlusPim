using PlusPim.Image;
using PlusPim.Instructions;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace PlusPim.Asm;

/// <summary>
/// アセンブラ指令のパースと <see cref="LayoutEffect"/> の生成.
/// </summary>
internal static class DirectiveParsers {
    /// <summary>省略可能な配置先を取り, セクションを始める</summary>
    public static Directives.Parse Segment(SegmentKind kind) {
        return (args, r) => {
            if(args.IsEmpty) {
                return [new SwitchSegment(kind, null)];
            }
            if(args is not [long v]) {
                return null;
            }
            if(v is < 0 or > uint.MaxValue) {
                r.Report(ErrorCode.ValueOutOfRange, v.ToString(CultureInfo.InvariantCulture));
                return [];
            }
            Address addr = new((uint)v);
            if(kind != SegmentKind.Data && addr % 4 != 0) {
                r.Report(ErrorCode.MisalignedText, addr.ToString());
            }
            return [new SwitchSegment(kind, addr)];
        };
    }

    /// <summary>リトルエンディアンで <paramref name="width"/> バイトずつ配置する. 整数でない値は捨てる</summary>
    public static Directives.Parse Ints(int width, long min, long max) {
        return (args, r) => {
            if(args.IsEmpty) {
                return null;
            }
            List<byte> bytes = [];
            Span<byte> buf = stackalloc byte[4];
            foreach(Arg a in args) {
                if(a is not long v) {
                    r.Report(WarningCode.InvalidOperand, a.Display());
                } else if(v < min || max < v) {
                    r.Report(ErrorCode.ValueOutOfRange, v.ToString(CultureInfo.InvariantCulture));
                } else {
                    BinaryPrimitives.WriteUInt32LittleEndian(buf, unchecked((uint)v));
                    bytes.AddRange(buf[..width]);
                }
            }
            return [new Emit([.. bytes])];
        };
    }

    /// <summary>UTF-8で配置する</summary>
    /// <param name="ascii">ASCII以外の文字をエラーとする</param>
    /// <param name="nul">末尾にNULを足す</param>
    public static Directives.Parse String(bool ascii, bool nul) {
        return (args, r) => {
            if(args is not [Str s]) {
                return null;
            }
            if(ascii && !Ascii.IsValid(s.Value)) {
                r.Report(ErrorCode.NonAsciiString, s.Value);
                return [];
            }
            // 孤立サロゲートは U+FFFD になる
            return [new Emit(Encoding.UTF8.GetBytes(nul ? s.Value + '\0' : s.Value))];
        };
    }

    /// <summary>0で埋めた領域を確保する</summary>
    public static LayoutEffect[]? Space(ReadOnlySpan<Arg> args, Reporter r) {
        if(args is not [long n]) {
            return null;
        }
        if(n is < 0 or > int.MaxValue) {
            r.Report(WarningCode.InvalidOperand, n.ToString(CultureInfo.InvariantCulture));
            return [];
        }
        return [new Reserve((uint)n)];
    }

    /// <summary>2の累乗の境界まで進める</summary>
    public static LayoutEffect[]? AlignTo(ReadOnlySpan<Arg> args, Reporter r) {
        if(args is not [long n]) {
            return null;
        }
        if(n is < 0 or > 30) {
            r.Report(WarningCode.InvalidOperand, n.ToString(CultureInfo.InvariantCulture));
            return [];
        }
        return [new Align((int)n)];
    }

    /// <summary>名前でない値は捨てる</summary>
    public static LayoutEffect[]? Global(ReadOnlySpan<Arg> args, Reporter r) {
        if(args.IsEmpty) {
            return null;
        }
        List<LayoutEffect> effects = [];
        foreach(Arg a in args) {
            if(a is SymbolRef s) {
                effects.Add(new DeclareGlobal(s.Name));
            } else {
                r.Report(WarningCode.InvalidOperand, a.Display());
            }
        }
        return [.. effects];
    }
}
