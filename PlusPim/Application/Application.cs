using PlusPim.Debuggers.PlusPimDbg;

namespace PlusPim.Application;

/// <summary>
/// アプリケーションの主要な機能を提供するクラス
/// </summary>
internal class Application: IApplication {
    private IDebugger? _debugger;
    private Action<string>? _log;
    private readonly bool _isDebug;
    private readonly FileInfo[] _files;

    /// <summary>
    /// アプリケーションのコンストラクタ
    /// </summary>
    /// <param name="isDebug">デバッグ起動かどうか</param>
    /// <param name="files">すべての実行するファイル</param>
    public Application(bool isDebug, FileInfo[] files) {
        this._isDebug = isDebug;
        this._files = files;
    }


    public void SetLogger(Action<string> log) {
        this._log = log;
    }

    /// <summary>
    /// プログラムをロードする．ランタイムモードの場合はContinue()する．
    /// </summary>
    /// <returns>成功した場合<see langword="true"/></returns>
    public bool Load() {

        if(this._log == null) {
            Console.WriteLine("Load not success");
            throw new InvalidOperationException("Logger is not set. Please call SetLogger before Load.");
        }
        this._debugger = new PlusPimDbg(this._files[0].FullName, this._log);

        if(!this._isDebug) {
            // デバッガモードでない場合はすぐに実行する
            // ここで無限ループする可能性がある
            this.Continue();
        }
        // デバッガモードではメソッドで操作されるのを待つ
        Console.WriteLine("Load success");
        return true;
    }

    public (int[] Registers, int PC, int HI, int LO) GetRegisters() {
        return this._debugger?.GetRegisters() ?? ([], -1, -1, -1);
    }

    public void Step() {
        this._debugger?.Step();
    }

    public bool StepBack() {
        return this._debugger?.StepBack() ?? false;
    }

    public int GetCurrentLine() {
        return this._debugger?.GetCurrentLine() ?? 0;
    }

    public string GetProgramPath() {
        return this._debugger?.GetProgramPath() ?? "";
    }

    public bool IsTerminated() {
        return this._debugger?.IsTerminated() ?? true;
    }

    public void Continue() {
        while(!this.IsTerminated()) {
            this.Step();
        }
    }

    public void ReverseContinue() {
        while(this.StepBack()) {
        }
    }

    public StackFrameInfo[] GetCallStack() {
        return this._debugger?.GetCallStack() ?? [];
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
}
