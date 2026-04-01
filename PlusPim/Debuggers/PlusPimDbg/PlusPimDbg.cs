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
    private readonly Stack<(IInstruction? Instruction, bool WasTerminated, bool PcAutoIncremented)> _history = new();

    internal PlusPimDbg(FileInfo[] files, ILogger logger) {
        this._programs = new ParsedPrograms(files, logger);

        // mainがなければ暫定でテキストセグメントの先頭から開始する
        Address startAddr = TextSegment.TextSegmentBase;
        Label? mainLabel = this._programs.ResolveFromAll("main");
        if(mainLabel is null) {
            logger.Warning("PlusPimDbg", "'main' label not found. Starting execution at index 0.");
            mainLabel = new Label { Name = "<unk>", Addr = new(0) };
        } else {
            startAddr = ((Label)mainLabel).Addr;
        }

        // コンテキスト設定
        this._context = new RuntimeContext(logger.ToAction("Instruction"), this._programs.CreateResolver(), startAddr, (Label)mainLabel);
        this._context.LoadMemoryImage(this._programs.MemoryImage);
    }

    public (uint[] Registers, uint PC, uint HI, uint LO) GetRegisters() {
        return (this._context.Registers.ToArray(), this._context.PC.Addr, this._context.HI, this._context.LO);
    }

    /// <summary>
    /// 命令を1ステップ実行する
    /// </summary>
    /// <remarks>終了状態である場合は何もしない</remarks>
    public StopReason Step() {
        if(this._context.AckException()) {
            // ktextにジャンプ
            this._context.PC = TextSegment.KernelTextSegmentBase;

            return StopReason.Step;
        }

        // 実行前のPCを保存
        Address pcBeforeExec = this._context.PC;
        bool pcAutoIncremented = false;
        // 命令を取得
        IInstruction? inst_ = this._programs.GetInstruction(this._context.PC, this._context);
        if(inst_ is IInstruction inst) {
            // 実行
            inst.Execute(this._context);
            // 命令がPCを変更しなかった場合のみ自動increment
            pcAutoIncremented = this._context.PC == pcBeforeExec;

            if(pcAutoIncremented) {
                this._context.PC += 4;
            }
        }

        // 履歴に保存
        this._history.Push((inst_, this._context.IsTerminated, pcAutoIncremented));


        // 戻り値を決定する

        // exceptionより前にないと，二重例外のときに終了できない
        if(this._context.IsTerminated) {
            return StopReason.Terminated;
        }

        if(this._context.LastException is not null) {
            return StopReason.Exception;
        }

        // 次の行がブレークポイント
        // todo

        // それ以外は通常のステップ
        return StopReason.Step;

    }



    /// <summary>
    /// 命令を1ステップ戻す
    /// </summary>
    /// <returns>成功したとき<see langword="true"/></returns>
    public bool Back() {
        if(this._history.Count == 0) {
            return false;
        }

        // popして逆操作しているだけ
        (IInstruction? instruction, bool wasTerminated, bool pcAutoIncremented) = this._history.Pop();
        if(instruction is null) {
            // 命令フェッチに失敗している状態を巻き戻す
            // 何もしない
            return true;
        }

        instruction.Undo(this._context);
        if(pcAutoIncremented) {
            this._context.PC -= 4;
        }
        this._context.IsTerminated = wasTerminated;
        return true;
    }

    public ExceptionInfo? GetLastException() {
        ExceptionEvent? exc_ = this._context.LastException;
        if(exc_ is ExceptionEvent exc) {
            string desc = exc.IsDouble
            ? $"Double exception: {exc.Code} (program will terminate)"
            : $"MIPS exception: {exc.Code}";

            return new ExceptionInfo {
                Reason = exc.Code,
                ExceptionId = exc.Code.ToString(),
                Description = desc,
                IsDouble = exc.IsDouble
            };
        }
        return null;

    }

    /// <summary>
    /// コールスタックの状態を返す
    /// </summary>
    public StackFrameInfo[] GetCallStack() {
        List<StackFrameInfo> frames = [];

        (uint badVAddr, uint status, uint cause, uint epc) = this._context.GetCP0DisplayValues();
        (FileInfo? file, int lineIndex) = this._programs.GetSourceInfo(this._context.PC) ?? (null, 0);
        frames.Add(new StackFrameInfo {
            FrameId = 1,
            Name = this._context.CurrentLabel.Name,
            Line = lineIndex,
            SrcFile = file,
            Registers = this._context.Registers.ToArray(),
            PC = this._context.PC.Addr,
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
            (file, lineIndex) = this._programs.GetSourceInfo(this._context.PC) ?? (null, 0);
            frames.Add(new StackFrameInfo {
                FrameId = frameId,
                Name = frame.Label.Name,
                Line = lineIndex,
                SrcFile = file,
                Registers = frame.Registers.ToArray(),
                PC = frame.CurrentPC.Addr,
                HI = frame.HISnapshot,
                LO = frame.LOSnapshot
            });
            frameId++;
        }

        return [.. frames];
    }
}
