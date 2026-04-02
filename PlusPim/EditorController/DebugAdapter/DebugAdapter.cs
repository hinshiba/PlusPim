using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using PlusPim.Application;
using PlusPim.Logging;
using System.Diagnostics;
using StackFrame = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.StackFrame;

namespace PlusPim.EditorController.DebugAdapter;

internal class DebugAdapter: DebugAdapterBase {
    private const int SCOPE_REGISTERS = 1;
    private const int SCOPE_SPECIAL_REGISTERS = 2;
    private const int SCOPE_CP0_REGISTERS = 3;

    private static readonly string[] RegisterNames = [
        "$zero ($0)", "$at ($1)", "$v0 ($2)", "$v1 ($3)",
        "$a0 ($4)", "$a1 ($5)", "$a2 ($6)", "$a3 ($7)",
        "$t0 ($8)", "$t1 ($9)", "$t2 ($10)", "$t3 ($11)",
        "$t4 ($12)", "$t5 ($13)", "$t6 ($14)", "$t7 ($15)",
        "$s0 ($16)", "$s1 ($17)", "$s2 ($18)", "$s3 ($19)",
        "$s4 ($20)", "$s5 ($21)", "$s6 ($22)", "$s7 ($23)",
        "$t8 ($24)", "$t9 ($25)", "$k0 ($26)", "$k1 ($27)",
        "$gp ($28)", "$sp ($29)", "$fp ($30)", "$ra ($31)"
    ];

    private readonly IApplication _app;
    private readonly ILogger _logger;
    private readonly TaskCompletionSource _sessionEnded = new();
    private bool _isInit = false;

    internal DebugAdapter(Stream input, Stream output, IApplication app, ILogger logger) {
        this._app = app;
        this._logger = logger;
        this.InitializeProtocolClient(input, output);
        this.Protocol.Run();
        this._logger.Debug("DebugAdapter", "Protocol client initialized and running.");
        // エラー時の終了処理の登録
        this.Protocol.DispatcherError += (_, _) => {
            _ = this._sessionEnded.TrySetResult();
            this._isInit = false;
        };

        this._logger.AddSink((LogLevel level, string source, string msg) => {
            if(!this._isInit) {
                // 初期化前や終了後に送信しない
                return;
            }
            this.Protocol.SendEvent(new OutputEvent {
                Output = $"[{level}][{source}] {msg}\n",
                Category = OutputEvent.CategoryValue.Console
            });
        });
    }

    internal Task WaitForSessionEnd() {
        return this._sessionEnded.Task;
    }

    protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments) {
        this._logger.Debug("DebugAdapter", "InitializeRequest.");
        // InitializeRequestに対してResponseを返す前は，イベントを送信してはならない
        // 返さないといけないレスポンスに，戻り値の型が設定されているので便利
        return new InitializeResponse {
            SupportsStepBack = true,
            SupportsExceptionInfoRequest = true,
            ExceptionBreakpointFilters = [
                new ExceptionBreakpointsFilter("double", "Double Exceptions") {
                    Description = "Break when a second exception occurs in kernel mode (fatal crash)",
                    Default = true
                },
                new ExceptionBreakpointsFilter("fatal", "Fatal Exceptions") {
                    Description = "Break when an (commonly) unrecoverable exception occurs (AdEL, AdES, RI, CpU, Ov)",
                    Default = true
                },
                new ExceptionBreakpointsFilter("break", "Break Exceptions") {
                    Description = "Break on break instruction (Bp)",
                    Default = true
                },
                new ExceptionBreakpointsFilter("syscall", "Syscall Exceptions") {
                    Description = "Break on syscall instruction (Sys)",
                    Default = false
                }
            ]
        };
    }

    protected override LaunchResponse HandleLaunchRequest(LaunchArguments args) {
        this._isInit = true;
        this._logger.Debug("DebugAdapter", "LaunchRequest.");

        _ = this._app.Load();

        // StoppedEventを送信してVariablesペインを有効化
        this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Entry) {
            ThreadId = 1,
            AllThreadsStopped = true
        });
        this.Protocol.SendEvent(new InitializedEvent());
        return new LaunchResponse();
    }

    protected override DisconnectResponse HandleDisconnectRequest(DisconnectArguments args) {
        this._logger.Debug("DebugAdapter", "DisconnectRequest.");
        _ = this._sessionEnded.TrySetResult();

        this._isInit = false;

        return new DisconnectResponse();
    }

    protected override SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments args) {
        this._logger.Debug("DebugAdapter", "SetBreakpointsRequest.");

        string filePath = args.Source.Path ?? "";
        int[] lines = args.Breakpoints?.Select(bp => bp.Line).ToArray() ?? [];
        BreakpointResult[] results = this._app.SetBreakpoints(filePath, lines);

        return new SetBreakpointsResponse {
            Breakpoints = results.Select(r => new Breakpoint {
                Verified = r.Verified,
                Line = r.Line
            }).ToList()
        };
    }

    protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments args) {
        this._logger.Debug("DebugAdapter", "SetExceptionBreakpointsRequest.");

        List<ExceptionFilter> filters = new(args.Filters.Count);
        foreach(string filter in args.Filters) {
            if(Enum.TryParse<ExceptionFilter>(filter, ignoreCase: true, out ExceptionFilter exceptionFilter)) {
                filters.Add(exceptionFilter);
            }
        }
        this._app.SetExceptionFilters(filters);
        return new SetExceptionBreakpointsResponse();
    }

    protected override ExceptionInfoResponse HandleExceptionInfoRequest(ExceptionInfoArguments args) {
        this._logger.Debug("DebugAdapter", "ExceptionInfoRequest.");
        ExceptionInfo? exInfo = this._app.GetLastException();
        return exInfo is null
            ? new ExceptionInfoResponse("unknown", ExceptionBreakMode.Always)
            : new ExceptionInfoResponse(
            exInfo.ExceptionId,
            exInfo.IsDouble
                ? ExceptionBreakMode.Unhandled
                : ExceptionBreakMode.Always
        ) {
                Description = exInfo.Description
            };
    }

    protected override ThreadsResponse HandleThreadsRequest(ThreadsArguments args) {
        this._logger.Debug("DebugAdapter", "ThreadsRequest.");
        return new ThreadsResponse {
            Threads = [new Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Thread(1, "Main Thread")]
        };
    }


    protected override StackTraceResponse HandleStackTraceRequest(StackTraceArguments args) {
        this._logger.Debug("DebugAdapter", "StackTraceRequest.");

        StackFrameInfo[] callStack = this._app.GetCallStack();
        List<StackFrame> dapFrames = [];
        foreach(StackFrameInfo frame in callStack) {
            dapFrames.Add(new StackFrame(frame.FrameId, frame.Name, frame.Line, 0) {
                Source = new Source { Path = frame.SrcFile?.FullName ?? "" }
            });
        }

        return new StackTraceResponse {
            StackFrames = dapFrames,
            TotalFrames = dapFrames.Count
        };
    }

    protected override ScopesResponse HandleScopesRequest(ScopesArguments args) {
        this._logger.Debug("DebugAdapter", "ScopesRequest.");

        int frameId = args.FrameId;
        // 安全なエンコード範囲の確認
        // これが発生するのはほぼないのでAssert
        System.Diagnostics.Debug.Assert(frameId is >= 0 and < 0x8000, $"frameId {frameId} exceeds safe encoding range");
        // エンコード: (frameId << 16) | scopeType
        int registersRef = (frameId << 16) | SCOPE_REGISTERS;
        int specialRegistersRef = (frameId << 16) | SCOPE_SPECIAL_REGISTERS;
        int cp0RegistersRef = (frameId << 16) | SCOPE_CP0_REGISTERS;

        return new ScopesResponse {
            Scopes = [
                new Scope("Registers", registersRef, false) {
                    PresentationHint = Scope.PresentationHintValue.Registers
                },
                new Scope("Special Registers", specialRegistersRef, false) {
                    PresentationHint = Scope.PresentationHintValue.Registers
                },
                new Scope("CP0 Registers", cp0RegistersRef, false) {
                    PresentationHint = Scope.PresentationHintValue.Registers
                }
            ]
        };
    }

    protected override VariablesResponse HandleVariablesRequest(VariablesArguments args) {
        this._logger.Debug("DebugAdapter", "VariablesRequest.");

        List<Variable> variables = [];

        // variablesReference をデコード: (frameId << 16) | scopeType
        int frameId = args.VariablesReference >> 16;
        int scopeType = args.VariablesReference & 0xFFFF;

        // 該当フレームのレジスタを取得
        StackFrameInfo? targetFrame = this._app.GetStackFrame(frameId);

        if(targetFrame != null) {
            if(scopeType == SCOPE_REGISTERS) {
                for(int i = 0; i < 32 && i < targetFrame.Registers.Length; i++) {
                    variables.Add(new Variable(RegisterNames[i], $"0x{targetFrame.Registers[i]:X8}", 0));
                }
            } else if(scopeType == SCOPE_SPECIAL_REGISTERS) {
                variables.Add(new Variable("PC", $"0x{targetFrame.PC:X8}", 0));
                variables.Add(new Variable("HI", $"0x{targetFrame.HI:X8}", 0));
                variables.Add(new Variable("LO", $"0x{targetFrame.LO:X8}", 0));
            } else if(scopeType == SCOPE_CP0_REGISTERS && targetFrame.CP0BadVAddr.HasValue) {
                variables.Add(new Variable("BadVAddr ($8)", $"0x{targetFrame.CP0BadVAddr.Value:X8}", 0));
                variables.Add(new Variable("Status ($12)", $"0x{targetFrame.CP0Status!.Value:X8}", 0));
                variables.Add(new Variable("Cause ($13)", $"0x{targetFrame.CP0Cause!.Value:X8}", 0));
                variables.Add(new Variable("EPC ($14)", $"0x{targetFrame.CP0EPC!.Value:X8}", 0));
            }
        }

        return new VariablesResponse {
            Variables = variables
        };
    }


    protected override ContinueResponse HandleContinueRequest(ContinueArguments args) {
        this._logger.Debug("DebugAdapter", "ContinueRequest.");

        this.SendExecuteEvent(this._app.Continue());

        return new ContinueResponse();
    }

    protected override NextResponse HandleNextRequest(NextArguments args) {
        this._logger.Debug("DebugAdapter", "NextRequest.");

        this.SendExecuteEvent(this._app.StepOver());

        return new NextResponse();
    }

    protected override StepInResponse HandleStepInRequest(StepInArguments args) {
        this._logger.Debug("DebugAdapter", "StepInRequest.");

        this.SendExecuteEvent(this._app.StepIn());

        return new StepInResponse();
    }

    protected override StepOutResponse HandleStepOutRequest(StepOutArguments args) {
        this._logger.Debug("DebugAdapter", "StepOutRequest.");

        this.SendExecuteEvent(this._app.StepOut());

        return new StepOutResponse();
    }

    protected override StepBackResponse HandleStepBackRequest(StepBackArguments args) {
        this._logger.Debug("DebugAdapter", "StepBackRequest.");

        if(!this._app.StepBack()) {
            this.Protocol.SendEvent(new OutputEvent {
                Output = "Already at the beginning of the program.\n",
                Category = OutputEvent.CategoryValue.Console
            });

        }
        this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Step) {
            ThreadId = 1,
            AllThreadsStopped = true
        });

        return new StepBackResponse();
    }

    protected override ReverseContinueResponse HandleReverseContinueRequest(ReverseContinueArguments args) {
        this._logger.Debug("DebugAdapter", "ReverseContinueRequest.");

        _ = this._app.ReverseContinue();

        this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Step) {
            ThreadId = 1,
            AllThreadsStopped = true
        });

        return new ReverseContinueResponse();
    }

    private void SendExecuteEvent(StopReason reason) {
        switch(reason) {
            case StopReason.Step:
                this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Step) {
                    ThreadId = 1,
                    AllThreadsStopped = true
                });
                break;
            case StopReason.Breakpoint:
                this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Breakpoint) {
                    ThreadId = 1,
                    AllThreadsStopped = true
                });
                break;
            case StopReason.Terminated:
                this.Protocol.SendEvent(new TerminatedEvent());
                break;
            // フィルタはアプリケーション側で適用されるので，例外情報がある場合は常にExceptionで止める
            case StopReason.Exception:
                ExceptionInfo exInfo = this._app.GetLastException() ?? throw new InvalidOperationException("PlusPim Dbg report stop by Exception. But ExceptionInfo is not set");
                this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Exception) {
                    ThreadId = 1,
                    AllThreadsStopped = true,
                    Description = exInfo.Description,
                    Text = exInfo.ExceptionId
                });

                break;
            default:
                // 到達不能であるはず
                throw new UnreachableException($"Unknown stop reason: {reason}");
        }
    }

}
