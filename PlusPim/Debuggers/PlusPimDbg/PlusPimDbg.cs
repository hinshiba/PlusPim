using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg.Instructions;

namespace PlusPim.Debuggers.PlusPimDbg;

internal class PlusPimDbg: IDebugger {
    private ExecuteContext? _context;
    private ParsedProgram? _program;
    private bool _isTerminated;
    private Action<string>? _log;
    private readonly Stack<(Mnemonic Mnemonic, bool WasTerminated)> _history = new();

    public void SetLogger(Action<string> log) {
        this._log = log;
    }

    /// <summary>
    /// プログラムの読み込みと各種初期化
    /// </summary>
    /// <param name="programPath">プログラムのパス</param>
    /// <returns>成功したとき<see langword="true"/></returns>
    public bool Load(string programPath) {
        // プログラムの解析
        this._program = new ParsedProgram(programPath, this._log);
        // 実行コンテキストの初期化
        this._context = new ExecuteContext(this._log);
        this._context.SetSymbolTable(this._program.SymbolTable);
        this._context.Registers[(int)RegisterID.T1] = 0xcafe; // テスト用初期値
        this._context.ExecutionIndex = this._program.GetLabelAddress("main") ?? 0;
        this._isTerminated = false;
        this._history.Clear();
        return true;
    }

    public int GetPC() {

        return this._context?.PC ?? -1;
    }

    public int[] GetRegisters() {
        return this._context?.Registers ?? [];
    }

    public int GetHI() {
        return this._context?.HI ?? -1;
    }

    public int GetLO() {
        return this._context?.LO ?? -1;
    }

    /// <summary>
    /// 命令を1ステップ実行する
    /// </summary>
    /// <remarks>初期化されていなかったり，終了状態である場合は何もしません</remarks>
    public void Step() {
        // プログラムの終了か未初期化チェック
        if(this._isTerminated || this._program == null || this._context == null) {
            return;
        }

        if(this._program.MnemonicCount <= this._context.ExecutionIndex) {
            this._isTerminated = true;
            return;
        }

        // 命令を取得
        Mnemonic mnemonic = this._program.GetMnemonic(this._context.ExecutionIndex);
        // ブランチやジャンプならPCの変更(条件未成立時の+1を含む)は命令側の責任
        bool modifiesPC = mnemonic.Instruction is JumpInstruction or BranchInstruction;
        // インスタンスを履歴に保存
        this._history.Push((mnemonic, this._isTerminated));
        mnemonic.Execute(this._context);
        if(!modifiesPC) {
            this._context.ExecutionIndex++;
        }

        if(this._program.MnemonicCount <= this._context.ExecutionIndex) {
            this._isTerminated = true;
        }
    }

    /// <summary>
    /// 命令を1ステップ戻す
    /// </summary>
    /// <returns>成功したとき<see langword="true"/></returns>
    public bool StepBack() {
        if(this._history.Count == 0 || this._context == null) {
            return false;
        }

        // popして逆操作しているだけ
        (Mnemonic? mnemonic, bool wasTerminated) = this._history.Pop();
        bool modifiesPC = mnemonic.Instruction is JumpInstruction or BranchInstruction;
        mnemonic.Undo(this._context);
        if(!modifiesPC) {
            this._context.ExecutionIndex--;
        }
        this._isTerminated = wasTerminated;
        return true;
    }

    public int GetCurrentLine() {
        return this._context == null || this._program == null
            ? 0
            : this._context.ExecutionIndex >= this._program.MnemonicCount ? 0 : this._program.GetSourceLine(this._context.ExecutionIndex);
    }

    public string GetProgramPath() {
        return this._program?.ProgramPath ?? "";
    }

    public bool IsTerminated() {
        return this._isTerminated;
    }

    public StackFrameInfo[] GetCallStack() {
        if(this._context == null || this._program == null) {
            return [];
        }

        List<StackFrameInfo> frames = [];

        // フレーム1: 現在のフレーム（ライブレジスタ）
        string currentLabel = this._program.GetLabelForExecutionIndex(this._context.ExecutionIndex) ?? "<unknown>";
        int currentLine = this._context.ExecutionIndex < this._program.MnemonicCount
            ? this._program.GetSourceLine(this._context.ExecutionIndex)
            : 0;
        frames.Add(new StackFrameInfo {
            FrameId = 1,
            Name = currentLabel,
            Line = currentLine,
            Registers = (int[])this._context.Registers.Clone(),
            PC = this._context.PC,
            HI = this._context.HI,
            LO = this._context.LO
        });

        // フレーム2以降: CallStackの各フレーム（上から順）
        int frameId = 2;
        foreach(CallStackFrame csFrame in this._context.CallStack) {
            int line = csFrame.ExecutionIndex < this._program.MnemonicCount
                ? this._program.GetSourceLine(csFrame.ExecutionIndex)
                : 0;
            frames.Add(new StackFrameInfo {
                FrameId = frameId,
                Name = csFrame.SubroutineLabel,
                Line = line,
                Registers = csFrame.RegisterSnapshot,
                PC = csFrame.ExecutionIndex + ExecuteContext.TextSegmentBase,
                HI = csFrame.HISnapshot,
                LO = csFrame.LOSnapshot
            });
            frameId++;
        }

        return frames.ToArray();
    }
}
