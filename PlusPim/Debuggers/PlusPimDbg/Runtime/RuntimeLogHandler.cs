using System.Runtime.CompilerServices;

namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// <see cref="RuntimeContext.Log(ref RuntimeLogHandler)"/>のための補間文字列ハンドラ
/// ログが無効な場合は文字列を組み立てない
/// </summary>
/// <remarks>
/// ログが無効な場合は補間式そのものが評価されない．
/// そのため，ログの補間式に副作用のある式を書いてはならない．
/// </remarks>
[InterpolatedStringHandler]
internal ref struct RuntimeLogHandler {
    private DefaultInterpolatedStringHandler _inner;

    public RuntimeLogHandler(int literalLength, int formattedCount, RuntimeContext context, out bool isEnabled) {
        isEnabled = context.IsLogEnabled;
        this.IsEnabled = isEnabled;
        if(isEnabled) {
            this._inner = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
        }
    }

    /// <summary>
    /// ログが有効かどうか
    /// </summary>
    public bool IsEnabled { get; }

    public void AppendLiteral(string value) {
        this._inner.AppendLiteral(value);
    }

    public void AppendFormatted<T>(T value) {
        this._inner.AppendFormatted(value);
    }

    public void AppendFormatted<T>(T value, string? format) {
        this._inner.AppendFormatted(value, format);
    }

    public string ToStringAndClear() {
        return this._inner.ToStringAndClear();
    }
}
