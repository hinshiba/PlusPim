using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;
using PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Jump;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;

namespace PlusPim.Debuggers.PlusPimDbg;

internal class PlusPimDbg: IDebugger {
    private readonly RuntimeContext _context;
    private readonly ParsedPrograms _programs;
    private readonly Stack<(IInstruction Instruction, bool WasTerminated)> _history = new();

    internal PlusPimDbg(FileInfo[] files, ILogger logger) {
        this._programs = new ParsedPrograms(files, logger);

        // mainがなければ暫定で0スタート
        InstructionIndex startIndex = new(0);
        Label? mainLabel = this._programs.ResolveFromAll("main");
        if(mainLabel is null) {
            logger.Warning("PlusPimDbg", "'main' label not found. Starting execution at index 0.");
            mainLabel = new Label { Name = "<unk>", Addr = new(0) };
        } else {
            startIndex = InstructionIndex.FromAddress(((Label)mainLabel).Addr, false) ?? new(0);
        }

        // コンテキスト設定
        this._context = new RuntimeContext(logger.ToAction("Instruction"), this._programs.CreateResolver(), startIndex, (Label)mainLabel);
        this._context.LoadMemoryImage(this._programs.MemoryImage);
    }

    public (uint[] Registers, int PC, uint HI, uint LO) GetRegisters() {
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

        // 命令を取得
        IInstruction instruction = this._programs.GetInstruction(this._context.PC, this._context.IsKernelMode());
        // ブランチやジャンプならPCの変更(条件未成立時の+1を含む)は命令側の責任
        bool modifiesPC = instruction is JumpInstruction or BranchInstruction;
        // インスタンスを履歴に保存
        this._history.Push((instruction, this._context.IsTerminated));

        // 実行
        instruction.Execute(this._context);
        if(!modifiesPC) {
            // JumpやBranchはPCを変化させない
        } else {
            // それ以外はPC++
            this._context.PC++;
        }

        // 次のPCに命令があるか確認
        if(this._context.IsKernelMode()) {
            if(this._programs.KernelInstructionCount <= this._context.PC.Idx) {
                // todo 例外が発生すべき
                this._context.IsTerminated = true;
                return;
            }
        } else {
            if(this._programs.UserInstructionCount <= this._context.PC.Idx) {
                this._context.IsTerminated = true;
                return;
            }
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
        int totalCount = this._context.IsKernelMode() ? this._programs.KernelInstructionCount : this._programs.UserInstructionCount;
        return this._context.PC.Idx >= totalCount ? 0 : this._programs.GetInstruction(this._context.PC, this._context.IsKernelMode()).SourceLine;
    }

    public string GetProgramPath() {
        return this._programs.GetProgramPath(this._context.PC, this._context.IsKernelMode());
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
            PC = (int)Address.FromInstructionIndex(this._context.PC, this._context.IsKernelMode()).Addr,
            HI = this._context.HI,
            LO = this._context.LO
        });

        // CallStackの各フレーム
        int frameId = 2;
        foreach(StackFrame frame in this._context.CallStack) {
            frames.Add(new StackFrameInfo {
                FrameId = frameId,
                Name = frame.Label.Name,
                Line = this._programs.GetInstruction(frame.CurrentPC, false).SourceLine,
                Registers = frame.Registers.ToArray(),
                PC = (int)Address.FromInstructionIndex(frame.CurrentPC, this._context.IsKernelMode()).Addr,
                HI = frame.HISnapshot,
                LO = frame.LOSnapshot
            });
            frameId++;
        }

        return [.. frames];
    }
}
