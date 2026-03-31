using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;
using System.Diagnostics;

namespace PlusPim.Application;

/// <summary>
/// アプリケーションの主要な機能を提供するクラス
/// </summary>
internal class Application: IApplication {
    private IDebugger? _debugger_;
    private IDebugger Debugger => this._debugger_ ?? throw new InvalidOperationException("Debugger is not initialized");
    private readonly ILogger _logger;
    private readonly bool _isDebug;
    private readonly FileInfo[] _files;

    /// 報告すべき例外の集合
    private HashSet<ExcCode> _filters = [];

    /// 二重例外を例外として報告するかどうか
    private bool _reportDoubleExceptions = true;

    /// <summary>
    /// アプリケーションのコンストラクタ
    /// </summary>
    /// <param name="isDebug">デバッグ起動かどうか</param>
    /// <param name="files">すべての実行するファイル</param>
    /// <param name="logger">ロガー</param>
    public Application(bool isDebug, FileInfo[] files, ILogger logger) {
        this._isDebug = isDebug;
        this._files = files;
        this._logger = logger;
    }

    /// <summary>
    /// プログラムをロードする．ランタイムモードの場合はContinue()する．
    /// </summary>
    /// <returns>成功した場合<see langword="true"/></returns>
    public bool Load() {
        this._debugger_ = new PlusPimDbg(this._files, this._logger);

        if(!this._isDebug) {
            // デバッガモードでない場合はすぐに実行する
            // ここで無限ループする可能性がある
            _ = this.Continue();
        }
        // デバッガモードではメソッドで操作されるのを待つ
        this._logger.Info("Application", "Load success");
        return true;
    }

    public StackFrameInfo[] GetCallStack() {
        return this._debugger_?.GetCallStack() ?? [];
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
        return this._debugger_?.GetLastException();
    }

    // 順方向実行

    public StopReason StepOut() {
        int startFrameNum = this.Debugger.GetCallStack().Length;
        do {
            // デバッガにステップ実行させる
            StopReason reason = this.Debugger.Step();
            // ブレークポイント，キャッチする例外，終了は停止する
            if(this.CanContinue(reason)) {
                continue;
            } else {
                return reason;
            }
        } while(this.Debugger.GetCallStack().Length < startFrameNum);
        // ステップアウトが完了したので停止する
        return StopReason.Step;
    }


    public StopReason StepOver() {
        int startFrameNum = this.Debugger.GetCallStack().Length;
        do {
            // デバッガにステップ実行させる
            StopReason reason = this.Debugger.Step();

            if(this.CanContinue(reason)) {
                continue;
            } else {
                return reason;
            }
            // ステップアウトとの違いは条件が以下になっているだけ
            // サブルーチン呼出しでなければ==で．サブルーチン呼出しであればステップアウトと同じ挙動
        } while(this.Debugger.GetCallStack().Length <= startFrameNum);
        // ステップオーバーが完了したので停止する
        return StopReason.Step;
    }

    private bool CanContinue(StopReason reason) {
        // ブレークポイント，キャッチする例外，終了は停止する
        return reason switch {
            StopReason.Step => true,
            StopReason.Breakpoint => false,
            StopReason.Terminated => false,
            StopReason.Exception => !this.IsBreakException(this.GetLastException() ?? throw new InvalidOperationException("Debugger reported an exception but GetLastException() returned null.")),
            _ => throw new UnreachableException("StopReason val is not defined."),
        };
    }


    public StopReason StepIn() {
        // デバッガにステップ実行させる
        StopReason reason = this.Debugger.Step();
        // どのような結果でも停止する
        return reason switch {
            StopReason.Step => reason,
            StopReason.Breakpoint => reason,
            StopReason.Terminated => reason,
            StopReason.Exception => this.IsBreakException(this.GetLastException() ?? throw new InvalidOperationException("Debugger reported an exception but GetLastException() returned null."))
                                ? reason
                                : StopReason.Step,
            _ => throw new UnreachableException("StopReason val is not defined."),
        };
    }

    public StopReason Continue() {
        // どこかで停止するまでデバッガに実行させる
        while(true) {
            StopReason reason = this.Debugger.Step();
            if(!this.CanContinue(reason)) {
                return reason;
            }

        }
    }


    public bool StepBack() {
        return this.Debugger.StepBack();
    }

    public bool ReverseContinue() {
        // 一回でも成功すればtrueなので
        bool result = this.Debugger.StepBack();
        while(this.Debugger.StepBack()) { }
        return result;
    }

    // 例外系

    public void SetExceptionFilters(List<ExceptionFilter> filters) {
        this._reportDoubleExceptions = false;
        HashSet<ExcCode> newFilters = [];
        foreach(ExceptionFilter filter in filters) {
            switch(filter) {
                case ExceptionFilter.Double:
                    this._reportDoubleExceptions = true;
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
        this._filters = newFilters;
    }

    private bool IsBreakException(ExceptionInfo exception) {
        return (exception.IsDouble && this._reportDoubleExceptions) || this._filters.Contains(exception.reason);
    }
}
