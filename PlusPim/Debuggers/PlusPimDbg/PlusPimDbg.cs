using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg.Instructions;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;

namespace PlusPim.Debuggers.PlusPimDbg;

internal class PlusPimDbg: IDebugger {
    private readonly ExecuteContext _context;
    private readonly ParsedProgram _program;
    private readonly Stack<(IInstruction Instruction, bool WasTerminated)> _history = new();

    internal PlusPimDbg(string programPath, ILogger logger) {
        this._program = new ParsedProgram(programPath, logger);

        // mainがなければ暫定で0スタート
        InstructionIndex startIndex = new(0);
        Label? mainLabel = this._program.SymbolTable.Resolve("main");
        if(mainLabel is null) {
            logger.Warning("PlusPimDbg", "'main' label not found. Starting execution at index 0.");
            mainLabel = new Label { Name = "<unk>", Addr = new(0) };
        } else {
            startIndex = InstructionIndex.FromAddress(((Label)mainLabel).Addr) ?? new(0);
        }

        // コンテキスト設定
        this._context = new ExecuteContext(logger.ToAction("Instruction"), this._program.SymbolTable, startIndex, (Label)mainLabel);
        this._context.LoadDataSegment(this._program.DataSegment);
    }

    public (int[] Registers, int PC, int HI, int LO) GetRegisters() {
        return (this._context.Registers.ToArray(), this._context.PC.Idx, this._context.HI, this._context.LO);
    }

    /// <summary>
    /// 命令を1ステップ実行する
    /// </summary>
    /// <remarks>終了状態である場合は何もしません</remarks>
    public void Step() {
        if(this._context.IsTerminated) {
            return;
        }

        if(this._program.InstructionCount <= this._context.PC.Idx) {
            this._context.IsTerminated = true;
            return;
        }

        // 命令を取得
        IInstruction instruction = this._program.GetInstruction(this._context.PC);
        // ブランチやジャンプならPCの変更(条件未成立時の+1を含む)は命令側の責任
        bool modifiesPC = instruction is JumpInstruction or BranchInstruction;
        // インスタンスを履歴に保存
        this._history.Push((instruction, this._context.IsTerminated));
        instruction.Execute(this._context);

        // PC変更
        if(this._context.IsException()) {
            // 例外検知
            // カーネルセグメントの0番目の命令に飛ばす
            this._context.PC = new(0);
        } else if(!modifiesPC) {
            // JumpやBranchでないならPCを次に進める
            this._context.PC++;
        }

        if(this._program.InstructionCount <= this._context.PC.Idx) {
            this._context.IsTerminated = true;
        }
    }

    /// <summary>
    /// 命令を1ステップ戻す
    /// </summary>
    /// <returns>成功したとき<see langword="true"/></returns>
    public bool StepBack() {
        if(this._history.Count == 0) {
            return false;
        }

        // popして逆操作しているだけ
        (IInstruction instruction, bool wasTerminated) = this._history.Pop();
        bool modifiesPC = instruction is JumpInstruction or BranchInstruction;
        instruction.Undo(this._context);
        if(!modifiesPC) {
            this._context.PC--;
        }
        this._context.IsTerminated = wasTerminated;
        return true;
    }

    public int GetCurrentLine() {
        return this._context.PC.Idx >= this._program.InstructionCount ? 0 : this._program.GetInstruction(this._context.PC).SourceLine;
    }

    public string GetProgramPath() {
        return this._program.ProgramPath;
    }

    public bool IsTerminated() {
        return this._context.IsTerminated;
    }

    /// <summary>
    /// コールスタックの状態を返す
    /// </summary>
    public StackFrameInfo[] GetCallStack() {
        List<StackFrameInfo> frames = [];

        frames.Add(new StackFrameInfo {
            FrameId = 1,
            Name = this._context.CurrentLabel.Name,
            Line = this.GetCurrentLine(),
            Registers = this._context.Registers.ToArray(),
            PC = Address.FromInstructionIndex(this._context.PC).Addr,
            HI = this._context.HI,
            LO = this._context.LO
        });

        // CallStackの各フレーム
        int frameId = 2;
        foreach(StackFrame frame in this._context.CallStack) {
            frames.Add(new StackFrameInfo {
                FrameId = frameId,
                Name = frame.Label.Name,
                Line = this._program.GetInstruction(frame.CurrentPC).SourceLine,
                Registers = frame.Registers.ToArray(),
                PC = Address.FromInstructionIndex(frame.CurrentPC).Addr,
                HI = frame.HISnapshot,
                LO = frame.LOSnapshot
            });
            frameId++;
        }

        return [.. frames];
    }
}
