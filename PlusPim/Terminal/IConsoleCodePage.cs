namespace PlusPim.Terminal;

/// <summary>
/// Windows のコンソールのコードページと制御イベントをテスト可能にするためのインターフェース
/// </summary>
internal interface IConsoleCodePage {
    /// <summary>
    /// コンソールが接続されているか．リダイレクトのみやコンソールなしでは false
    /// </summary>
    bool IsConsoleAttached { get; }

    uint GetOutputCodePage();

    uint GetInputCodePage();

    bool SetOutputCodePage(uint codePage);

    bool SetInputCodePage(uint codePage);

    /// <summary>
    /// Ctrl+C，Ctrl+Break，コンソールを閉じる，ログオフ，シャットダウンのときに handler を呼ぶ
    /// </summary>
    void RegisterControlHandler(Action handler);
}
