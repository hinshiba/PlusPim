namespace PlusPim.Logging;

internal interface ILogger {
    void Log(LogLevel level, string source, string message);
    void Debug(string source, string message);
    void Info(string source, string message);
    void Warning(string source, string message);
    void Error(string source, string message);
    void AddSink(Action<LogLevel, string, string> sink);

    /// <summary>
    /// <see cref="Action{String}"/>ブリッジを返す．指定されたソース名でDebugレベルのログを出力する．
    /// </summary>
    Action<string> ToAction(string source);
}

internal sealed class Logger(LogLevel minLevel): ILogger {
    private readonly List<Action<LogLevel, string, string>> _sinks = [];

    public void Log(LogLevel level, string source, string message) {
        if(level < minLevel) {
            return;
        }
        foreach(Action<LogLevel, string, string> sink in this._sinks) {
            sink(level, source, message);
        }
    }

    public void Debug(string source, string message) {
        this.Log(LogLevel.Debug, source, message);
    }

    public void Info(string source, string message) {
        this.Log(LogLevel.Info, source, message);
    }

    public void Warning(string source, string message) {
        this.Log(LogLevel.Warning, source, message);
    }

    public void Error(string source, string message) {
        this.Log(LogLevel.Error, source, message);
    }

    public void AddSink(Action<LogLevel, string, string> sink) {
        this._sinks.Add(sink);
    }

    public Action<string> ToAction(string source) {
        return message => this.Debug(source, message);
    }

    /// <summary>
    /// テスト用のサイレントロガー
    /// </summary>
    public static ILogger Null { get; } = new NullLogger();

    private sealed class NullLogger: ILogger {
        public void Log(LogLevel level, string source, string message) { }
        public void Debug(string source, string message) { }
        public void Info(string source, string message) { }
        public void Warning(string source, string message) { }
        public void Error(string source, string message) { }
        public void AddSink(Action<LogLevel, string, string> sink) { }
        public Action<string> ToAction(string source) {
            return _ => { };
        }
    }
}
