using System.Collections.Immutable;

namespace PlusPim.Adapter.Logging;

/// <summary>
/// ログの重要度
/// </summary>
public enum LogLevel: byte { Debug, Info, Warning, Error }

/// <summary>
/// 指定以上の重要度のログを, 登録したすべての出力先へ渡す
/// </summary>
/// <remarks>出力先の追加とログはどのスレッドからも呼べる</remarks>
/// <param name="minLevel">最低の重要度</param>
public sealed class Logger(LogLevel minLevel) {
    private ImmutableArray<Action<LogLevel, string, string>> _sinks = [];

    /// <summary>出力先を加える</summary>
    /// <param name="sink">重要度, 発生元, 本文を受け取る</param>
    public void AddSink(Action<LogLevel, string, string> sink) {
        _ = ImmutableInterlocked.Update(ref this._sinks, s => s.Add(sink));
    }

    /// <summary>ログを出す</summary>
    /// <param name="level">重要度</param>
    /// <param name="source">発生元</param>
    /// <param name="message">本文</param>
    public void Log(LogLevel level, string source, string message) {
        if(level < minLevel) {
            return;
        }
        foreach(Action<LogLevel, string, string> sink in this._sinks) {
            sink(level, source, message);
        }
    }
}
