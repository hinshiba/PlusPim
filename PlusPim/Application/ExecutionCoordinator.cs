namespace PlusPim.Application;

/// <summary>
/// 実行の操作 (Continue，Step など) をワーカースレッドで高々1つずつ実行し，取り消せるようにする
/// </summary>
/// <remarks>
/// ロックの順序は常にこのクラスのロック → <see cref="Application"/> のロックとする．
/// 操作は <see cref="Application"/> のロックを解放してから戻るので，停止の報告で逆順にはならない
/// </remarks>
internal sealed class ExecutionCoordinator {
    private readonly Lock _sync = new();

    /// <summary>
    /// 実行中の操作の取り消し．実行中でなければ<see langword="null"/>
    /// </summary>
    private CancellationTokenSource? _cts;

    private Task? _worker;
    private bool _isShutdown;

    /// <summary>
    /// 操作を実行中かどうか
    /// </summary>
    public bool IsRunning {
        get {
            lock(this._sync) {
                return this._cts is not null;
            }
        }
    }

    /// <summary>
    /// 実行中でなければ，操作をワーカースレッドで始める
    /// </summary>
    /// <param name="operation">実行する操作．取り消されたら<see cref="StopReason.Pause"/>を返すこと</param>
    /// <param name="beforeStart">ロックを持ったまま，ワーカーを始める前に呼ぶ (DAP の応答を停止イベントより先に送るため)</param>
    /// <param name="onStopped">操作が戻った後に，ロックを持ったまま呼ぶ．報告が終わるまで次の操作は始まらない</param>
    /// <param name="onFaulted">操作が例外を投げた場合に，<paramref name="onStopped"/>の代わりに呼ぶ</param>
    /// <returns>始めた場合は<see langword="true"/>．実行中または終了処理の後は<see langword="false"/></returns>
    public bool TryStart(Func<CancellationToken, StopReason> operation, Action beforeStart,
                         Action<StopReason> onStopped, Action<Exception> onFaulted) {
        lock(this._sync) {
            if(this._isShutdown || this._cts is not null) {
                return false;
            }

            CancellationTokenSource cts = new();
            this._cts = cts;
            try {
                beforeStart();
            } catch {
                this._cts = null;
                cts.Dispose();
                throw;
            }

            this._worker = Task.Factory.StartNew(
                () => this.Run(operation, cts, onStopped, onFaulted),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            return true;
        }
    }

    private void Run(Func<CancellationToken, StopReason> operation, CancellationTokenSource cts,
                     Action<StopReason> onStopped, Action<Exception> onFaulted) {
        StopReason reason = StopReason.Pause;
        Exception? fault = null;
        try {
            reason = operation(cts.Token);
        } catch(Exception ex) {
            fault = ex;
        }

        lock(this._sync) {
            try {
                // 終了処理の後は報告しない
                if(!this._isShutdown) {
                    if(fault is null) {
                        onStopped(reason);
                    } else {
                        onFaulted(fault);
                    }
                }
            } finally {
                this._cts = null;
                cts.Dispose();
            }
        }
    }

    /// <summary>
    /// 実行中の操作の取り消しを要求する
    /// </summary>
    /// <returns>実行中の操作があった場合は<see langword="true"/></returns>
    public bool RequestPause() {
        lock(this._sync) {
            if(this._cts is null) {
                return false;
            }
            this._cts.Cancel();
            return true;
        }
    }

    /// <summary>
    /// 実行中の操作を取り消して終了を待つ．以降は操作を始めない
    /// </summary>
    /// <param name="timeout">終了を待つ最大の時間</param>
    public void Shutdown(TimeSpan timeout) {
        Task? worker;
        lock(this._sync) {
            this._isShutdown = true;
            this._cts?.Cancel();
            worker = this._worker;
        }
        // ワーカーは報告にロックを使うので，ロックの外で待つ
        _ = worker?.Wait(timeout);
    }
}
