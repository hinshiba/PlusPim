namespace PlusPim.Application;

internal class Application: IApplication {
    private readonly IDebugger _debugger;

    internal Application(IDebugger debugger) {
        this._debugger = debugger;
    }

    public void SetLogger(Action<string> log) {
        this._debugger.SetLogger(log);
    }

    public bool Load(string programPath) {
        return this._debugger.Load(programPath);
    }

    public (int[] Registers, int PC, int HI, int LO) GetRegisters() {
        return (this._debugger.GetRegisters(), this._debugger.GetPC(), this._debugger.GetHI(), this._debugger.GetLO());
    }

    public void Step() {
        this._debugger.Step();
    }


    public bool StepBack() {
        return this._debugger.StepBack();
    }

    public int GetCurrentLine() {
        return this._debugger.GetCurrentLine();
    }

    public string GetProgramPath() {
        return this._debugger.GetProgramPath();
    }

    public bool IsTerminated() {
        return this._debugger.IsTerminated();
    }

    public StackFrameInfo[] GetCallStack() {
        return this._debugger.GetCallStack();
    }
}
