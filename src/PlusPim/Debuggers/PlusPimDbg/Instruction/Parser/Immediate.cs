using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;

/// <summary>
/// 即値を表すクラス
/// </summary>
internal class Immediate {

    private readonly ushort _value;

    public Immediate(ushort value) {
        this._value = value;
    }

    public int ToSInt() {
        return (short)this._value;
    }

    public uint ToUInt() {
        return this._value;
    }

    /// <summary>
    /// 0xから始まる16進数か10進数文字列から16bit即値への変換
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out Immediate result) {
        result = null;
        if(!TryParseInteger(s, out long value) || value is < short.MinValue or > ushort.MaxValue) {
            return false;
        }

        // 負数は2の補数のビット列として格納する
        result = new Immediate(unchecked((ushort)value));
        return true;
    }

    /// <summary>
    /// 0xから始まる16進数か10進数文字列から32bit値への変換
    /// </summary>
    public static bool TryParse32([NotNullWhen(true)] string? s, out uint value) {
        value = 0;
        if(!TryParseInteger(s, out long parsed) || parsed is < int.MinValue or > uint.MaxValue) {
            return false;
        }

        value = unchecked((uint)parsed);
        return true;
    }

    /// <summary>
    /// 整数リテラルを解析する
    /// </summary>
    /// <remarks>
    /// 省略可能な符号 (<c>+</c>/<c>-</c>) に続けて，10進数 (10桁まで) か <c>0x</c>/<c>0X</c> で始まる16進数 (8桁まで) を受け付ける．
    /// 正規表現によってマッチした値を処理する前提であるので，前後の空白は取り除かれていることを想定している
    /// </remarks>
    internal static bool TryParseInteger([NotNullWhen(true)] string? s, out long value) {
        value = 0;
        if(string.IsNullOrEmpty(s)) {
            return false;
        }

        ReadOnlySpan<char> span = s;
        bool isNegative = span[0] == '-';
        if(span[0] is '+' or '-') {
            span = span[1..];
        }

        long magnitude;
        if(2 <= span.Length && span[0] == '0' && span[1] is 'x' or 'X') {
            // AllowHexSpecifierは16進数字のみを受け付ける
            span = span[2..];
            if(span.Length is 0 or > 8
                || !long.TryParse(span, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out magnitude)) {
                return false;
            }
        } else if(span.Length is 0 or > 10
            || !long.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out magnitude)) {
            // NumberStylesNoneは10進数字のみを受け付ける
            return false;
        }

        value = isNegative ? -magnitude : magnitude;
        return true;
    }

    public override string ToString() {
        // 2バイト即値なので4桁
        return $"0x{this._value:X4}";
    }

}
