using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

/// <summary>
/// 即値を表すクラス
/// </summary>
internal class Immediate(int value): IParsable<Immediate> {

    /// <summary>
    /// プライマリコンストラクタで初期化される即値の値
    /// </summary>
    public int Value { get; } = value;

    /// <summary>
    /// intへの暗黙的型変換を提供
    /// </summary>
    public static implicit operator int(Immediate imm) {
        return imm.Value;
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
        // null または 空文字のチェック
        if(string.IsNullOrWhiteSpace(s)) {
            result = null;
            return false;
        }

        int parseResult;
        bool parseIsSuccess;
        // 0x で始まる場合は16進数として処理
        if(s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            result = null;
            // 2文字目以降を渡す
            // NumberStyles.HexNumber を指定
            parseIsSuccess = int.TryParse(
                s[2..],
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out parseResult
            );
        } else {
            result = null;
            // それ以外は通常の10進数として処理
            parseIsSuccess = int.TryParse(
                s,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parseResult
            );
        }
        if(parseIsSuccess) {
            result = new Immediate(parseResult);
        }
        return parseIsSuccess;

    }

    public override string ToString() {
        // 2バイト即値なので4桁
        return $"0x{this.Value:X4}";
    }

}
