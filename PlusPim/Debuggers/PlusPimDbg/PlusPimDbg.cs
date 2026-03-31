using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;

namespace PlusPim.Debuggers.PlusPimDbg;

internal class PlusPimDbg: IDebugger {
    private readonly RuntimeContext _context;
    private readonly ParsedPrograms _programs;
    private readonly Stack<(IInstruction Instruction, bool WasTerminated, bool PcAutoIncremented)> _history = new();

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

    public (uint[] Registers, uint PC, uint HI, uint LO) GetRegisters() {
        return (this._context.Registers.ToArray(), Address.FromInstructionIndex(this._context.PC, this._context.IsKernelMode()).Addr, this._context.HI, this._context.LO);
    }

    /// <summary>
    /// 命令を1ステップ実行する
    /// </summary>
    /// <remarks>終了状態である場合は何もしない</remarks>
    public void Step() {
        if(this._context.IsTerminated) {
            return;
        }
        this._context.ClearLastException();

        // 有効なPCでないなら例外を発生させて例外ハンドラにジャンプ
        if(this._context.PC == InstructionIndex.Invalid) {
            // jumpで指定されていないラベルへジャンプした場合にセットされる
            this._context.RaiseException(ExcCode.RI, Address.FromInstructionIndex(this._context.PC, this._context.IsKernelMode()));
        } else {
            // 次のPCに命令があるか確認
            if(this._context.IsKernelMode()) {
                if(this._programs.KernelInstructionCount <= this._context.PC.Idx) {
                    // 例外ハンドラにジャンプするが，カーネルモード中での例外であるので，double exceptionとなり終了する
                    this._context.RaiseException(ExcCode.RI, Address.FromInstructionIndex(this._context.PC, this._context.IsKernelMode()));
                    // terminatedになるので，続行する意味がないので早期リターン
                    return;
                }
            } else {
                if(this._programs.UserInstructionCount <= this._context.PC.Idx) {
                    // 例外ハンドラにジャンプ
                    this._context.RaiseException(ExcCode.RI, Address.FromInstructionIndex(this._context.PC, this._context.IsKernelMode()));
                    // ここでカーネル空間に入るが，カーネル空間にハンドラがあるか不明なので
                    return;
                }
            }
        }

        // 命令を取得
        IInstruction instruction = this._programs.GetInstruction(this._context.PC, this._context.IsKernelMode());
        // 実行前のPCを保存
        InstructionIndex pcBeforeExec = this._context.PC;

        // 実行
        instruction.Execute(this._context);

        // 命令がPCを変更しなかった場合のみ自動increment
        bool pcAutoIncremented = this._context.PC == pcBeforeExec;
        if(pcAutoIncremented) {
            this._context.PC++;
        }
        // 履歴に保存
        this._history.Push((instruction, this._context.IsTerminated, pcAutoIncremented));
    }

    /// <summary>
    /// 命令を1ステップ戻す
    /// </summary>
    /// <returns>成功したとき<see langword="true"/></returns>
    public bool StepBack() {
        if(this._history.Count == 0) {
            return false;
        }
        this._context.ClearLastException();

        // popして逆操作しているだけ
        (IInstruction instruction, bool wasTerminated, bool pcAutoIncremented) = this._history.Pop();
        instruction.Undo(this._context);
        if(pcAutoIncremented) {
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

    public ExceptionInfo? GetLastException() {
        ExceptionEvent? exc = this._context.LastException;
        if(exc is null) {
            return null;
        }

        string desc = exc.IsDouble
            ? $"Double exception: {exc.Code} (program will terminate)"
            : $"MIPS exception: {exc.Code}";

        return new ExceptionInfo {
            ExceptionId = exc.Code.ToString(),
            Description = desc,
            IsDouble = exc.IsDouble
        };
    }

    /// <summary>
    /// コールスタックの状態を返す
    /// </summary>
    public StackFrameInfo[] GetCallStack() {
        List<StackFrameInfo> frames = [];

        (uint badVAddr, uint status, uint cause, uint epc) = this._context.GetCP0DisplayValues();
        frames.Add(new StackFrameInfo {
            FrameId = 1,
            Name = this._context.CurrentLabel.Name,
            Line = this.GetCurrentLine(),
            Registers = this._context.Registers.ToArray(),
            PC = Address.FromInstructionIndex(this._context.PC, this._context.IsKernelMode()).Addr,
            HI = this._context.HI,
            LO = this._context.LO,
            CP0BadVAddr = badVAddr,
            CP0Status = status,
            CP0Cause = cause,
            CP0EPC = epc
        });

        // CallStackの各フレーム
        int frameId = 2;
        foreach(StackFrame frame in this._context.CallStack) {
            frames.Add(new StackFrameInfo {
                FrameId = frameId,
                Name = frame.Label.Name,
                Line = this._programs.GetInstruction(frame.CurrentPC, false).SourceLine,
                Registers = frame.Registers.ToArray(),
                PC = Address.FromInstructionIndex(frame.CurrentPC, false).Addr,
                HI = frame.HISnapshot,
                LO = frame.LOSnapshot
            });
            frameId++;
        }

        return [.. frames];
    }
}
