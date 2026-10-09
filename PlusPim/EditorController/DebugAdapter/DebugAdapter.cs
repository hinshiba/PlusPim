using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json.Linq;
using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Logging;
using System.Diagnostics;
using System.Globalization;
using StackFrame = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.StackFrame;

namespace PlusPim.EditorController.DebugAdapter;

internal class DebugAdapter: DebugAdapterBase {
    private const int SCOPE_REGISTERS = 1;
    private const int SCOPE_SPECIAL_REGISTERS = 2;
    private const int SCOPE_CP0_REGISTERS = 3;
    private const int SCOPE_MEMORY = 4;

    /// <summary>
    /// readMemory で一度に読む最大のバイト数
    /// </summary>
    private const int MaxReadMemoryBytes = 64 * 1024;

    /// <summary>
    /// <c>$sp</c> の番号
    /// </summary>
    private const int StackPointerIndex = 29;

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
    private readonly ExecutionCoordinator _execution = new();
    private readonly TaskCompletionSource _sessionEnded = new();
    private volatile bool _isInit = false;

    /// <summary>
    /// launch の <c>stopOnEntry</c>．<see langword="false"/> なら configurationDone の後に実行を始める
    /// </summary>
    private volatile bool _stopOnEntry = true;

    internal DebugAdapter(Stream input, Stream output, IApplication app, ILogger logger) {
        this._app = app;
        this._logger = logger;
        this.InitializeProtocolClient(input, output);
        // エラー時の終了処理の登録．Run より前に登録しないと，直後のエラーを取りこぼす
        this.Protocol.DispatcherError += (_, _) => {
            _ = this._sessionEnded.TrySetResult();
            this._isInit = false;
        };
        // カスタム要求も Run より前に登録する
        this.Protocol.RegisterRequestType<PseudoExpansionsRequest, PseudoExpansionsArguments, PseudoExpansionsResponse>(this.HandlePseudoExpansionsRequest);
        this.Protocol.Run();
        this._logger.Debug("DebugAdapter", "Protocol client initialized and running.");

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

    /// <summary>
    /// デバッギの標準出力をDAPのOutputEventとしてエディタへ送る
    /// </summary>
    internal void SendDebuggeeOutput(string text) {
        if(!this._isInit) {
            // Launch前や終了後は破棄
            return;
        }
        this.Protocol.SendEvent(new OutputEvent {
            Output = text,
            Category = OutputEvent.CategoryValue.Stdout
        });
    }

    protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments) {
        this._logger.Debug("DebugAdapter", "InitializeRequest.");
        // InitializeRequestに対してResponseを返す前は，イベントを送信してはならない
        // 返さないといけないレスポンスに，戻り値の型が設定されているので便利
        return new InitializeResponse {
            SupportsConfigurationDoneRequest = true,
            SupportsReadMemoryRequest = true,
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

    /// <remarks>
    /// DAP の順序に従い，読み込みに成功したら launch に応答してから initialized を送る．
    /// 実行 (stopOnEntry の停止を含む) は configurationDone の後に始める
    /// </remarks>
    protected override void HandleLaunchRequestAsync(IRequestResponder<LaunchArguments> responder) {
        this._isInit = true;
        this._logger.Debug("DebugAdapter", "LaunchRequest.");

        this._stopOnEntry = ReadStopOnEntry(responder.Arguments);

        try {
            _ = this._app.Load();
        } catch(AssemblyException ex) {
            responder.SetError(new ProtocolException($"Failed to load program:\n{string.Join("\n", ex.Errors)}"));
            return;
        } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {
            responder.SetError(new ProtocolException($"Failed to load program: {ex.Message}"));
            return;
        }

        responder.SetResponse(new LaunchResponse());
        // 応答の後でないと，クライアントが構成要求を送る前提が崩れる
        this.Protocol.SendEvent(new InitializedEvent());
    }

    /// <summary>
    /// launch の構成から <c>stopOnEntry</c> を読む．指定がなければ <see langword="true"/>
    /// </summary>
    private static bool ReadStopOnEntry(LaunchArguments args) {
        return args.ConfigurationProperties is null
            || !args.ConfigurationProperties.TryGetValue("stopOnEntry", out JToken? token)
            || token.Type != JTokenType.Boolean
            || token.Value<bool>();
    }

    protected override void HandleConfigurationDoneRequestAsync(IRequestResponder<ConfigurationDoneArguments> responder) {
        this._logger.Debug("DebugAdapter", "ConfigurationDoneRequest.");

        if(!this._app.IsLoaded) {
            responder.SetError(new ProtocolException("Program is not loaded."));
            return;
        }

        if(this._stopOnEntry) {
            responder.SetResponse(new ConfigurationDoneResponse());
            // StoppedEventを送信してVariablesペインを有効化
            this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Entry) {
                ThreadId = 1,
                AllThreadsStopped = true
            });
        } else {
            this.StartExecution(responder, new ConfigurationDoneResponse(), this._app.Continue);
        }
    }

    protected override void HandleDisconnectRequestAsync(IRequestResponder<DisconnectArguments> responder) {
        this._logger.Debug("DebugAdapter", "DisconnectRequest.");

        // 実行中なら止めてから終える
        this._execution.Shutdown(TimeSpan.FromSeconds(2));
        this._isInit = false;

        // 応答を書いてからセッションを終える．先に終えると Main がストリームを閉じ，応答が届かない
        // 応答は別のスレッドで書かれるので，Stop で送信キューが空になるのを待つ
        responder.SetResponse(new DisconnectResponse());
        this.Protocol.Stop(2000);
        _ = this._sessionEnded.TrySetResult();
    }

    protected override SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments args) {
        this._logger.Debug("DebugAdapter", "SetBreakpointsRequest.");

        FileInfo file = new(args.Source.Path);
        int[] lines = [.. args.Breakpoints.Select(bp => bp.Line)];
        BreakpointResult[] results = this._app.SetBreakpoints(file, lines);

        return new SetBreakpointsResponse {
            Breakpoints = [.. results.Select(r => new Breakpoint {
                Verified = r.Verified,
                Line = r.Line
            })]
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
        // ランタイムエラーは続行できないので，未処理の例外として報告する
        RuntimeErrorInfo? runtimeError = this._app.GetRuntimeError();
        if(runtimeError is not null) {
            return new ExceptionInfoResponse(RuntimeErrorDisplay.ExceptionId(runtimeError), ExceptionBreakMode.Unhandled) {
                Description = RuntimeErrorDisplay.WidgetDescription(runtimeError)
            };
        }

        ExceptionInfo? exInfo = this._app.GetLastException();
        return exInfo is null
            ? new ExceptionInfoResponse("unknown", ExceptionBreakMode.Always)
            : new ExceptionInfoResponse(
            RuntimeErrorDisplay.MipsExceptionId(exInfo),
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
                Source = frame.SrcFile is not null ? new Source { Path = frame.SrcFile.FullName } : null,
                InstructionPointerReference = MemoryReference(frame.PC)
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
        int memoryRef = (frameId << 16) | SCOPE_MEMORY;

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
                },
                // メモリビュー (View Binary Data) を開くための変数
                new Scope("Memory", memoryRef, false)
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
                    // 値をアドレスとしてメモリビューを開けるようにする
                    variables.Add(new Variable(RegisterNames[i], $"0x{targetFrame.Registers[i]:X8}", 0) {
                        MemoryReference = MemoryReference(targetFrame.Registers[i])
                    });
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
            } else if(scopeType == SCOPE_MEMORY) {
                (uint dataStart, uint dataSize) = this._app.DataSegmentRange;
                variables.Add(new Variable(".data", $"0x{dataStart:X8} ({dataSize} bytes)", 0) {
                    MemoryReference = MemoryReference(dataStart)
                });
                uint sp = targetFrame.Registers[StackPointerIndex];
                variables.Add(new Variable("stack ($sp)", $"0x{sp:X8}", 0) {
                    MemoryReference = MemoryReference(sp)
                });
            }
        }

        return new VariablesResponse {
            Variables = variables
        };
    }


    /// <remarks>
    /// <c>address</c> は実際に返す最初のバイトのアドレスとし，アドレス 0 より前の部分は <c>address</c> を進めて表す．
    /// <c>unreadableBytes</c> は <c>0xFFFFFFFF</c> を超える末尾の部分だけである
    /// </remarks>
    protected override ReadMemoryResponse HandleReadMemoryRequest(ReadMemoryArguments args) {
        this._logger.Debug("DebugAdapter", "ReadMemoryRequest.");

        if(!this._app.IsLoaded) {
            throw new ProtocolException("Program is not loaded.");
        }
        if(!TryParseMemoryReference(args.MemoryReference, out uint reference)) {
            throw new ProtocolException($"Invalid memory reference: {args.MemoryReference}");
        }

        const long AddressSpaceEnd = 0x1_0000_0000L;
        long start = reference + (long)(args.Offset ?? 0);
        long end = start + Math.Clamp(args.Count, 0, MaxReadMemoryBytes);
        // アドレス空間 [0, 2^32) と重なる部分だけを読む
        long readStart = Math.Clamp(start, 0, AddressSpaceEnd);
        long readEnd = Math.Clamp(end, readStart, AddressSpaceEnd);

        byte[] data = readStart < readEnd
            ? this._app.ReadMemory((uint)readStart, (int)(readEnd - readStart))
            : [];
        return new ReadMemoryResponse {
            Address = $"0x{readStart:X8}",
            Data = Convert.ToBase64String(data),
            UnreadableBytes = readEnd < end ? (int)(end - readEnd) : null
        };
    }

    /// <summary>
    /// カスタム要求 <c>pluspimPseudoExpansions</c>．ファイルの疑似命令の行と展開先の命令を返す
    /// </summary>
    private void HandlePseudoExpansionsRequest(IRequestResponder<PseudoExpansionsArguments, PseudoExpansionsResponse> responder) {
        this._logger.Debug("DebugAdapter", "PseudoExpansionsRequest.");

        if(!this._app.IsLoaded) {
            responder.SetError(new ProtocolException("Program is not loaded."));
            return;
        }

        string? path = responder.Arguments.Source?.Path;
        PseudoExpansionInfo[] expansions = string.IsNullOrEmpty(path) ? [] : this._app.GetPseudoExpansions(new FileInfo(path));
        responder.SetResponse(new PseudoExpansionsResponse {
            Lines = [.. expansions.Select(expansion => new PseudoExpansionLine {
                Line = expansion.Line,
                Mnemonic = expansion.Mnemonic,
                Instructions = [.. expansion.Instructions.Select(instruction => new PseudoExpansionInstruction {
                    Address = MemoryReference(instruction.Address),
                    Text = instruction.Text
                })]
            })]
        });
    }

    /// <summary>
    /// メモリ参照の文字列 (<c>0x</c> から始まる16進数か10進数) を解析する
    /// </summary>
    private static bool TryParseMemoryReference(string? text, out uint address) {
        address = 0;
        if(string.IsNullOrWhiteSpace(text)) {
            return false;
        }
        text = text.Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? uint.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out address)
            : uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out address);
    }

    /// <summary>
    /// アドレスを DAP のメモリ参照の文字列にする
    /// </summary>
    private static string MemoryReference(uint address) {
        return $"0x{address:X8}";
    }

    // 実行の要求は応答を先に送り，ワーカーで実行して停止したら stopped を送る
    // 実行中に届いた実行の要求はエラーにする (待たせない)

    protected override void HandleContinueRequestAsync(IRequestResponder<ContinueArguments, ContinueResponse> responder) {
        this._logger.Debug("DebugAdapter", "ContinueRequest.");
        this.StartExecution(responder, new ContinueResponse { AllThreadsContinued = true }, this._app.Continue);
    }

    protected override void HandleNextRequestAsync(IRequestResponder<NextArguments> responder) {
        this._logger.Debug("DebugAdapter", "NextRequest.");
        this.StartExecution(responder, new NextResponse(), this._app.StepOver);
    }

    protected override void HandleStepInRequestAsync(IRequestResponder<StepInArguments> responder) {
        this._logger.Debug("DebugAdapter", "StepInRequest.");
        this.StartExecution(responder, new StepInResponse(), this._app.StepIn);
    }

    protected override void HandleStepOutRequestAsync(IRequestResponder<StepOutArguments> responder) {
        this._logger.Debug("DebugAdapter", "StepOutRequest.");
        this.StartExecution(responder, new StepOutResponse(), this._app.StepOut);
    }

    protected override void HandleStepBackRequestAsync(IRequestResponder<StepBackArguments> responder) {
        this._logger.Debug("DebugAdapter", "StepBackRequest.");
        this.StartExecution(responder, new StepBackResponse(), _ => this._app.StepBack() ? StopReason.Step : StopReason.HistoryStart);
    }

    protected override void HandleReverseContinueRequestAsync(IRequestResponder<ReverseContinueArguments> responder) {
        this._logger.Debug("DebugAdapter", "ReverseContinueRequest.");
        this.StartExecution(responder, new ReverseContinueResponse(), this._app.ReverseContinue);
    }

    /// <remarks>停止中の pause は何もせずに成功する</remarks>
    protected override void HandlePauseRequestAsync(IRequestResponder<PauseArguments> responder) {
        this._logger.Debug("DebugAdapter", "PauseRequest.");
        responder.SetResponse(new PauseResponse());
        _ = this._execution.RequestPause();
    }

    /// <summary>
    /// 実行の操作をワーカーで始める．始められなければ要求をエラーにする
    /// </summary>
    /// <param name="responder">実行の要求</param>
    /// <param name="response">始める前に送る応答</param>
    /// <param name="operation">実行の操作</param>
    private void StartExecution(IRequestResponder responder, ResponseBody response, Func<CancellationToken, StopReason> operation) {
        if(!this._app.IsLoaded) {
            responder.SetError(new ProtocolException("Program is not loaded."));
            return;
        }
        if(!this._execution.TryStart(operation, () => responder.SetResponse(response), this.SendExecuteEvent, this.OnExecutionFaulted)) {
            responder.SetError(new ProtocolException("The debuggee is running. Pause it first."));
        }
    }

    /// <summary>
    /// 実行の操作が例外を投げたときに，UIが実行中のままにならないよう例外で停止したことにする
    /// </summary>
    private void OnExecutionFaulted(Exception ex) {
        this._logger.Error("DebugAdapter", $"Execution failed: {ex}");
        this.Protocol.SendEvent(new OutputEvent {
            Output = $"[PlusPim internal error] {ex.GetType().Name}: {ex.Message}\n",
            Category = OutputEvent.CategoryValue.Stderr
        });
        this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Exception) {
            ThreadId = 1,
            AllThreadsStopped = true,
            Text = ex.GetType().Name
        });
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
            case StopReason.Pause:
                this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Pause) {
                    ThreadId = 1,
                    AllThreadsStopped = true
                });
                break;
            case StopReason.HistoryStart:
                this.Protocol.SendEvent(new OutputEvent {
                    Output = "Reached the beginning of the execution history.\n",
                    Category = OutputEvent.CategoryValue.Console
                });
                this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Entry) {
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
                    Description = RuntimeErrorDisplay.MipsExceptionStateLabel(exInfo),
                    Text = RuntimeErrorDisplay.MipsExceptionId(exInfo)
                });

                break;
            // ランタイムエラーはセッションを終了させずに停止する (StepBack で戻れるようにするため)
            // 例外ウィジェットを表示するために停止理由は exception とし，表示する文字列で MIPS の例外と区別する
            case StopReason.RuntimeError:
                RuntimeErrorInfo errorInfo = this._app.GetRuntimeError() ?? throw new InvalidOperationException("PlusPim Dbg report stop by RuntimeError. But RuntimeErrorInfo is not set");
                this.Protocol.SendEvent(new OutputEvent {
                    Output = RuntimeErrorDisplay.ConsoleLine(errorInfo),
                    Category = OutputEvent.CategoryValue.Stderr,
                    Source = errorInfo.SourceFile is not null ? new Source { Path = errorInfo.SourceFile.FullName } : null,
                    Line = 0 < errorInfo.Line ? errorInfo.Line : null
                });
                this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Exception) {
                    ThreadId = 1,
                    AllThreadsStopped = true,
                    Description = RuntimeErrorDisplay.StateLabel,
                    Text = RuntimeErrorDisplay.ExceptionId(errorInfo)
                });
                break;
            default:
                // 到達不能であるはず
                throw new UnreachableException($"Unknown stop reason: {reason}");
        }
    }

}
