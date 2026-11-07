namespace PlusPim.EditorController.Transport;

/// <summary>
/// DAPの通信路を確立する
/// </summary>
internal interface IDapTransport {
    /// <summary>
    /// 接続を確立し，DAPが読み書きする双方向ストリームを返す
    /// </summary>
    /// <exception cref="DapTransportException">接続や待ち受けに失敗した</exception>
    Task<Stream> OpenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// DAPの通信路を確立できなかったことを表す．メッセージはそのまま利用者に表示できる
/// </summary>
internal sealed class DapTransportException(string message, Exception? inner = null): Exception(message, inner);
