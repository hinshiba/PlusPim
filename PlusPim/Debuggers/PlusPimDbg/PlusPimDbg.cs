using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;

namespace PlusPim.Debuggers.PlusPimDbg;

internal class PlusPimDbg: IDebugger {
    /// <summary>
    /// 1ステップの履歴
    /// </summary>
    private abstract record HistoryEntry;

    /// <summary>
    /// 命令の実行 (命令フェッチでの例外を含む)
    /// </summary>
    /// <param name="Record">処理系による実行の記録</param>
    /// <param name="PcAdvanced">PCを自動インクリメントしたか</param>
    private sealed record Executed(ExecutionRecord Record, bool PcAdvanced): HistoryEntry;

    /// <summary>
    /// 例外ハンドラへの遷移
    /// </summary>
    /// <param name="PcBefore">遷移前のPC</param>
    /// <param name="Acked">遷移時に消去した例外</param>
    private sealed record EnteredHandler(Address PcBefore, ExceptionEvent Acked): HistoryEntry;

    private readonly RuntimeContext _context;
    private readonly ParsedPrograms _programs;
    private readonly Stack<HistoryEntry> _history = new();
    private readonly HashSet<Address> _breakpoints = [];

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
    /// <remarks>終了状態，またはランタイムエラーが発生している場合は何もせず，履歴にも積まない</remarks>
    public StopReason Step() {
        if(this._context.IsTerminated) {
            return StopReason.Terminated;
        }

        if(this._context.RuntimeError is not null) {
            // ランタイムエラーの後は続行できない．Backでのみ抜けられる
            return StopReason.RuntimeError;
        }

        if(this._context.LastException is ExceptionEvent acked) {
            // 例外を消去してktextにジャンプする
            Address pcBefore = this._context.PC;
            _ = this._context.AckException();
            this._context.PC = TextSegment.KernelTextSegmentBase;
            this._history.Push(new EnteredHandler(pcBefore, acked));

            return StopReason.Step;
        }

        ExecutionRecord record;
        bool pcAdvanced = false;
        // 命令を取得
        if(this._programs.TryGetInstruction(this._context.PC, this._context.IsKernelMode, out IInstruction? inst, out ExceptionRequest fault)) {
            // 実行
            record = Processor.Execute(this._context, inst);
            // 例外・ランタイムエラーは完了させずに停止するので，命令の実行が完了し，命令自身がPCを設定しなかったときのみ自動incrementする
            if(record.Completed && !record.Result.PcWritten) {
                this._context.PC += 4;
                pcAdvanced = true;
            }
        } else {
            record = Processor.RaiseFetchFault(this._context, fault);
        }

        // 履歴に保存
        this._history.Push(new Executed(record, pcAdvanced));


        // 戻り値を決定する

        // exceptionより前にないと，二重例外のときに終了できない
        if(this._context.IsTerminated) {
            return StopReason.Terminated;
        }

        if(this._context.RuntimeError is not null) {
            return StopReason.RuntimeError;
        }

        if(this._context.LastException is not null) {
            return StopReason.Exception;
        }

        // 次の命令がブレークポイント
        if(this._breakpoints.Contains(this._context.PC)) {
            return StopReason.Breakpoint;
        }


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
        switch(this._history.Pop()) {
            case EnteredHandler(Address pcBefore, ExceptionEvent acked):
                // 遷移前のPCと，消去した例外を戻す
                this._context.PC = pcBefore;
                this._context.RestoreExceptionState(this._context.CaptureExceptionState() with { LastException = acked });
                break;

            case Executed(ExecutionRecord record, bool pcAdvanced):
                // 命令フェッチでの例外も含め，例外に関わる状態は処理系が戻す
                Processor.Undo(this._context, record);
                if(pcAdvanced) {
                    this._context.PC -= 4;
                }
                break;

            default:
                throw new InvalidOperationException("Unknown history entry.");
        }
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

    public RuntimeErrorInfo? GetRuntimeError() {
        if(this._context.RuntimeError is RuntimeError error) {
            // ランタイムエラーではPCを進めないので，PCが発生した命令のアドレスである
            return new RuntimeErrorInfo {
                Kind = error.Kind,
                Description = error.Message,
                Address = this._context.PC.Addr
            };
        }
        return null;
    }

    /// <summary>
    /// ブレークポイントを設定する
    /// </summary>
    public BreakpointResult[] SetBreakpoints(FileInfo file, int[] lines) {
        // 該当ファイルの既存ブレークポイントをクリアしてから再設定する
        // DAP の setBreakpoints はファイル単位で全ブレークポイントを送ってくるため
        HashSet<Address> oldAddresses = this._programs.GetAllAddressesForFile(file);
        this._breakpoints.ExceptWith(oldAddresses);

        BreakpointResult[] result = new BreakpointResult[lines.Length];
        for(int i = 0; i < lines.Length; i++) {
            Address? addr_ = this._programs.GetAddressForLine(file, lines[i]);
            if(addr_ is Address addr) {
                _ = this._breakpoints.Add(addr);
                result[i] = new BreakpointResult { Line = lines[i], Verified = true };
            } else {
                result[i] = new BreakpointResult { Line = lines[i], Verified = false };
            }
        }
        return result;
    }

    /// <summary>
    /// コールスタックの状態を返す
    /// </summary>
    public StackFrameInfo[] GetCallStack() {
        List<StackFrameInfo> frames = [];

        (uint badVAddr, uint status, uint cause, uint epc) = this._context.GetCP0DisplayValues();
        // 例外発生なら次の命令ではなく，例外発生の命令の情報にする
        (FileInfo? file, int lineNumber) = this._programs.GetSourceInfo((this._context.LastException is null) ? this._context.PC : new Address(epc));
        frames.Add(new StackFrameInfo {
            FrameId = 1,
            Name = this._context.CurrentLabel.Name,
            Line = lineNumber,
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
            (file, lineNumber) = this._programs.GetSourceInfo(frame.CurrentPC);
            frames.Add(new StackFrameInfo {
                FrameId = frameId,
                Name = frame.Label.Name,
                Line = lineNumber,
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
