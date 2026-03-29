using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;

/// <summary>
/// 2バイト即値を表すクラス
/// </summary>
internal class Immediate: IParsable<Immediate> {

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

    public static Immediate Parse(string s, IFormatProvider? provider) {
        return TryParse(s, provider, out Immediate? result) ? result : throw new FormatException();
    }


    /// <summary>
    /// 0xから始まる16進数か10進数文字列から即値への変換
    /// </summary>
    /// <remarks>
    /// 正規表現によってマッチした値を処理する前提であるので，前後の空白は取り除かれていることを想定している
    /// </remarks>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out Immediate result) {
        result = null;
        // null または 空文字のチェック
        if(string.IsNullOrWhiteSpace(s)) {
            return false;
        }

        ushort parseResult;
        bool isSuccess;
        // 0x で始まる場合は16進数として処理
        if(s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            // 2文字目以降を渡す
            // NumberStyles.HexNumberは空白を許可するが，trimしている前提なのでAllowHexSpecifierを使用
            isSuccess = ushort.TryParse(
                s[2..],
                NumberStyles.AllowHexSpecifier,
                provider,
                out parseResult
            );
        } else if(s.StartsWith('-')) {
            // 符号あり
            isSuccess = short.TryParse(
                s,
                NumberStyles.Integer,
                provider,
                out short signedResult
            );
            // 符号ありで成功した場合は，符号なしの値に変換して格納
            // フラグに依存させないために，uncheckedを用いる
            parseResult = unchecked((ushort)signedResult);
        } else {
            // それ以外は通常の10進数として処理
            isSuccess = ushort.TryParse(
                s,
                NumberStyles.Integer,
                provider,
                out parseResult
            );
        }

        if(isSuccess) {
            result = new Immediate(parseResult);
        }
        return isSuccess;
    }

    public override string ToString() {
        // 2バイト即値なので4桁
        return $"0x{this._value:X4}";
    }

}
