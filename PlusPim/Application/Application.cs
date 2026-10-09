using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;
using System.Diagnostics;

namespace PlusPim.Application;

/// <summary>
/// アプリケーションの主要な機能を提供するクラス
/// </summary>
internal class Application: IApplication {
    /// <summary>
    /// 長い実行でロックを解放するまでのステップ数．実行中の要求 (スタックトレースなど) は最大でこの分だけ待つ
    /// </summary>
    private const int BatchSize = 4096;

    /// <summary>
    /// デバッガの状態を守るロック．公開するメンバーはすべてこのロックの中でデバッガを操作する
    /// </summary>
    /// <remarks>ランタイムコールの入出力も同期されていないので，ステップ実行は必ずこのロックの中で行う</remarks>
    private readonly Lock _gate = new();

    private IDebugger? _debugger;
    private IDebugger Debugger => this._debugger ?? throw new InvalidOperationException("Debugger is not initialized");
    private readonly ILogger _logger;
    private readonly FileInfo[] _files;
    private readonly bool _strict;

    /// 報告すべき例外の集合
    private HashSet<ExcCode> _filters = [];

    /// 二重例外を例外として報告するかどうか
    private bool _reportDoubleExceptions = true;

    /// <summary>
    /// アプリケーションのコンストラクタ
    /// </summary>
    /// <param name="files">すべての実行するファイル</param>
    /// <param name="logger">ロガー</param>
    /// <param name="strict">解析できない行と未対応の指令をエラーにするかどうか</param>
    public Application(FileInfo[] files, ILogger logger, bool strict = false) {
        this._files = files;
        this._logger = logger;
        this._strict = strict;
    }

    /// <summary>
    /// プログラムをロードする．
    /// </summary>
    /// <returns>成功した場合<see langword="true"/></returns>
    /// <exception cref="Debuggers.PlusPimDbg.Program.AssemblyException">アセンブルに失敗した場合</exception>
    public bool Load() {
        PlusPimDbg debugger = new(this._files, this._logger, this._strict);
        lock(this._gate) {
            this._debugger = debugger;
        }
        // メソッドで操作されるのを待つ
        this._logger.Info("Application", "Load success");
        return true;
    }

    public bool IsLoaded {
        get {
            lock(this._gate) {
                return this._debugger is not null;
            }
        }
    }

    public StackFrameInfo[] GetCallStack() {
        lock(this._gate) {
            return this._debugger?.GetCallStack() ?? [];
        }
    }

    public StackFrameInfo? GetStackFrame(int frameId) {
        StackFrameInfo[] callStack = this.GetCallStack();
        foreach(StackFrameInfo frame in callStack) {
            if(frame.FrameId == frameId) {
                return frame;
            }
        }
        return null;
    }

    public ExceptionInfo? GetLastException() {
        lock(this._gate) {
            return this._debugger?.GetLastException();
        }
    }

    public RuntimeErrorInfo? GetRuntimeError() {
        lock(this._gate) {
            return this._debugger?.GetRuntimeError();
        }
    }

    // 順方向実行

    public StopReason StepOut(CancellationToken ct = default) {
        int startDepth = this.GetCallStackDepth();
        // 呼び出し元のフレームに戻ったら完了
        return this.RunForward(ct, () => this.Debugger.CallStackDepth < startDepth);
    }

    public StopReason StepOver(CancellationToken ct = default) {
        int startDepth = this.GetCallStackDepth();
        // サブルーチン呼出しでなければ1ステップで完了．サブルーチン呼出しであれば戻ってきたら完了
        return this.RunForward(ct, () => this.Debugger.CallStackDepth <= startDepth);
    }

    public StopReason StepIn(CancellationToken ct = default) {
        return this.RunForward(ct, () => true);
    }

    public StopReason Continue(CancellationToken ct = default) {
        // どこかで停止するまでデバッガに実行させる
        return this.RunForward(ct, () => false);
    }

    private int GetCallStackDepth() {
        lock(this._gate) {
            return this.Debugger.CallStackDepth;
        }
    }

    /// <summary>
    /// 停止する理由が起きるか，<paramref name="isDone"/>が成り立つまで1ステップずつ実行する
    /// </summary>
    /// <remarks><see cref="BatchSize"/>ステップごとにロックを解放し，実行中も他の要求に応えられるようにする</remarks>
    /// <param name="ct">取り消されたら<see cref="StopReason.Pause"/>で停止する</param>
    /// <param name="isDone">各ステップの後に呼ぶ．成り立てば<see cref="StopReason.Step"/>で停止する</param>
    private StopReason RunForward(CancellationToken ct, Func<bool> isDone) {
        while(true) {
            lock(this._gate) {
                IDebugger debugger = this.Debugger;
                for(int i = 0; i < BatchSize; i++) {
                    if(ct.IsCancellationRequested) {
                        return StopReason.Pause;
                    }

                    StopReason reason = debugger.Step();
                    // ブレークポイント，キャッチする例外，終了，ランタイムエラーは停止する
                    if(!this.CanContinue(reason)) {
                        return reason;
                    }
                    if(isDone()) {
                        return StopReason.Step;
                    }
                }
            }
        }
    }

    private bool CanContinue(StopReason reason) {
        // ブレークポイント，キャッチする例外，終了，ランタイムエラーは停止する
        return reason switch {
            StopReason.Step => true,
            StopReason.Breakpoint => false,
            StopReason.Terminated => false,
            StopReason.Exception => !this.IsBreakException(this.Debugger.GetLastException() ?? throw new InvalidOperationException("Debugger reported an exception but GetLastException() returned null.")),
            // ランタイムエラーは続行できないので，例外フィルタによらず常に停止する
            StopReason.RuntimeError => false,
            _ => throw new UnreachableException("StopReason val is not defined."),
        };
    }

    // 逆方向実行

    public bool StepBack() {
        lock(this._gate) {
            return this.Debugger.Back();
        }
    }

    /// <remarks>
    /// ブレークポイントでのみ止まり，例外では止まらない．
    /// <see cref="BatchSize"/>ステップごとにロックを解放する
    /// </remarks>
    public StopReason ReverseContinue(CancellationToken ct = default) {
        while(true) {
            lock(this._gate) {
                IDebugger debugger = this.Debugger;
                for(int i = 0; i < BatchSize; i++) {
                    if(ct.IsCancellationRequested) {
                        return StopReason.Pause;
                    }
                    if(!debugger.Back()) {
                        return StopReason.HistoryStart;
                    }
                    if(debugger.IsAtBreakpoint) {
                        return StopReason.Breakpoint;
                    }
                }
            }
        }
    }

    // ブレークポイント

    public BreakpointResult[] SetBreakpoints(FileInfo file, int[] lines) {
        lock(this._gate) {
            return this.Debugger.SetBreakpoints(file, lines);
        }
    }

    // 例外系

    public void SetExceptionFilters(List<ExceptionFilter> filters) {
        bool reportDoubleExceptions = false;
        HashSet<ExcCode> newFilters = [];
        foreach(ExceptionFilter filter in filters) {
            switch(filter) {
                case ExceptionFilter.Double:
                    reportDoubleExceptions = true;
                    break;
                case ExceptionFilter.Fatal:
                    _ = newFilters.Add(ExcCode.AdEL);
                    _ = newFilters.Add(ExcCode.AdES);
                    _ = newFilters.Add(ExcCode.RI);
                    _ = newFilters.Add(ExcCode.CpU);
                    _ = newFilters.Add(ExcCode.Ov);
                    break;
                case ExceptionFilter.Break:
                    _ = newFilters.Add(ExcCode.Bp);
                    break;
                case ExceptionFilter.Syscall:
                    _ = newFilters.Add(ExcCode.Sys);
                    break;
                default:
                    throw new UnreachableException("ExceptionFilter val is not defined.");
            }
        }
        // 実行中のループも参照するので，ロックの中で置き換える
        lock(this._gate) {
            this._reportDoubleExceptions = reportDoubleExceptions;
            this._filters = newFilters;
        }
    }

    private bool IsBreakException(ExceptionInfo exception) {
        return (exception.IsDouble && this._reportDoubleExceptions) || this._filters.Contains(exception.Reason);
    }

}
