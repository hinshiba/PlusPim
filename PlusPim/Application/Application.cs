using PlusPim.Debuggers.PlusPimDbg;

namespace PlusPim.Application;

/// <summary>
/// アプリケーションの主要な機能を提供するクラス
/// </summary>
internal class Application: IApplication {
    private IDebugger? _debugger;
    private Action<string>? _log;

    public void SetLogger(Action<string> log) {
        this._log = log;
    }

    public bool Load(string programPath) {
        if(this._log == null) {
            return false;
        }
        this._debugger = new PlusPimDbg(programPath, this._log);
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
