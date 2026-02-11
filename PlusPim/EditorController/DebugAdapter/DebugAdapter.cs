using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json.Linq;
using PlusPim.Application;

namespace PlusPim.EditorController.DebugAdapter;

internal class DebugAdapter: DebugAdapterBase {
    private const int SCOPE_REGISTERS = 1;
    private const int SCOPE_SPECIAL_REGISTERS = 2;

    private static readonly string[] RegisterNames = [
        "$zero ($0)", "$at ($1)", "$v0 ($2)", "$v1 ($3)",
        "$a0 ($4)", "$a1 ($5)", "$a2 ($6)", "$a3 ($7)",
        "$t0 ($8)", "$t1 ($9)", "$t2 ($10)", "$t3 ($11)",
        "$t4 ($12)", "$t5 ($13)", "$t6 ($14)", "$t7 ($15)",
        "$s0 ($16)", "$s1 ($17)", "$s2 ($18)", "$s3 ($19)",
        "$s4 ($20)", "$s5 ($21)", "$s6 ($22)", "$s7 ($23)",
        "$t8 ($24)", "$t9 ($25)", "$k0 ($26)", "$k1 ($27)",
        "$gp ($28)", "$sp ($29)", "$s8 ($30)", "$ra ($31)"
    ];

    private readonly IApplication _app;

    internal DebugAdapter(Stream input, Stream output, IApplication app) {
        this._app = app;
        this.InitializeProtocolClient(input, output);
        this.Protocol.Run();
    }

    protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments) {
        // InitializeRequestに対してResponseを返す前は，イベントを送信してはならない
        // 返さないといけないレスポンスに，戻り値の型が設定されているので便利
        return new InitializeResponse {
            SupportsStepBack = true
        };
    }

    protected override LaunchResponse HandleLaunchRequest(LaunchArguments args) {

        this.Protocol.SendEvent(new OutputEvent {
            Output = "Handler: LaunchRequest.\n",
            Category = OutputEvent.CategoryValue.Console
        });

        this._app.SetLogger(msg => this.Protocol.SendEvent(new OutputEvent {
            Output = msg + "\n",
            Category = OutputEvent.CategoryValue.Console
        }));

        if(args.ConfigurationProperties.TryGetValue("program", out JToken? program)) {
            // エラーハンドリングはtodo
            _ = this._app.Load(program.Value<string>() ?? throw new InvalidOperationException("Program value is missing or null."));

            // StoppedEventを送信してVariablesペインを有効化
            this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Entry) {
                ThreadId = 1,
                AllThreadsStopped = true
            });
        } else {
            this.Protocol.SendEvent(new OutputEvent {
                Output = "program field not found in JSON\n",
                Category = OutputEvent.CategoryValue.Console
            });
            // プログラムが指定されていない場合はterminatedイベントを送信
            //this.Protocol.SendEvent(new TerminatedEvent());
        }


        // 即座にterminatedイベントを送信
        //this.Protocol.SendEvent(new TerminatedEvent());

        return new LaunchResponse();
    }

    protected override DisconnectResponse HandleDisconnectRequest(DisconnectArguments args) {

        this.Protocol.SendEvent(new OutputEvent {
            Output = "Handler: DisconnectRequest.\n",
            Category = OutputEvent.CategoryValue.Console
        });

        return new DisconnectResponse();
    }

    protected override ThreadsResponse HandleThreadsRequest(ThreadsArguments args) {

        this.Protocol.SendEvent(new OutputEvent {
            Output = "Handler: ThreadsRequest.\n",
            Category = OutputEvent.CategoryValue.Console
        });

        return new ThreadsResponse {
            Threads = [new Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Thread(1, "Main Thread")]
        };
    }

    protected override StackTraceResponse HandleStackTraceRequest(StackTraceArguments args) {

        this.Protocol.SendEvent(new OutputEvent {
            Output = "Handler: StackTraceRequest.\n",
            Category = OutputEvent.CategoryValue.Console
        });

        StackFrameInfo[] callStack = this._app.GetCallStack();
        List<StackFrame> dapFrames = [];
        foreach(StackFrameInfo frame in callStack) {
            dapFrames.Add(new StackFrame(frame.FrameId, frame.Name, frame.Line, 0) {
                Source = new Source { Path = this._app.GetProgramPath() }
            });
        }

        return new StackTraceResponse {
            StackFrames = dapFrames,
            TotalFrames = dapFrames.Count
        };
    }

    protected override ScopesResponse HandleScopesRequest(ScopesArguments args) {
        this.Protocol.SendEvent(new OutputEvent {
            Output = "Handler: ScopesRequest.\n",
            Category = OutputEvent.CategoryValue.Console
        });

        int frameId = args.FrameId;
        // エンコード: (frameId << 16) | scopeType
        int registersRef = (frameId << 16) | SCOPE_REGISTERS;
        int specialRegistersRef = (frameId << 16) | SCOPE_SPECIAL_REGISTERS;

        return new ScopesResponse {
            Scopes = [
                new Scope("Registers", registersRef, false) {
                    PresentationHint = Scope.PresentationHintValue.Registers
                },
                new Scope("Special Registers", specialRegistersRef, false) {
                    PresentationHint = Scope.PresentationHintValue.Registers
                }
            ]
        };
    }

    protected override VariablesResponse HandleVariablesRequest(VariablesArguments args) {
        this.Protocol.SendEvent(new OutputEvent {
            Output = "Handler: VariablesRequest.\n",
            Category = OutputEvent.CategoryValue.Console
        });

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
            }
        }

        return new VariablesResponse {
            Variables = variables
        };
    }

    protected override ContinueResponse HandleContinueRequest(ContinueArguments args) {

        this.Protocol.SendEvent(new OutputEvent {
            Output = "Handler: ContinueRequest.\n",
            Category = OutputEvent.CategoryValue.Console
        });

        this._app.Continue();
        this.Protocol.SendEvent(new TerminatedEvent());
        return new ContinueResponse { AllThreadsContinued = true };
    }

    protected override NextResponse HandleNextRequest(NextArguments args) {
        this.Protocol.SendEvent(new OutputEvent {
            Output = "Handler: NextRequest.\n",
            Category = OutputEvent.CategoryValue.Console
        });

        this.ExecuteSingleStep();
        return new NextResponse();
    }

    protected override StepInResponse HandleStepInRequest(StepInArguments args) {
        this.Protocol.SendEvent(new OutputEvent {
            Output = "Handler: StepInRequest.\n",
            Category = OutputEvent.CategoryValue.Console
        });

        this.ExecuteSingleStep();
        return new StepInResponse();
    }

    protected override StepOutResponse HandleStepOutRequest(StepOutArguments args) {
        this.Protocol.SendEvent(new OutputEvent {
            Output = "Handler: StepOutRequest.\n",
            Category = OutputEvent.CategoryValue.Console
        });

        this.ExecuteSingleStep();
        return new StepOutResponse();
    }

    protected override StepBackResponse HandleStepBackRequest(StepBackArguments args) {
        this.Protocol.SendEvent(new OutputEvent {
            Output = "Handler: StepBackRequest.\n",
            Category = OutputEvent.CategoryValue.Console
        });

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
        this.Protocol.SendEvent(new OutputEvent {
            Output = "Handler: ReverseContinueRequest.\n",
            Category = OutputEvent.CategoryValue.Console
        });

        this._app.ReverseContinue();

        this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Step) {
            ThreadId = 1,
            AllThreadsStopped = true
        });

        return new ReverseContinueResponse();
    }

    private void ExecuteSingleStep() {
        this._app.Step();
        if(this._app.IsTerminated()) {
            this.Protocol.SendEvent(new OutputEvent {
                Output = "debugee is terminated.\n",
                Category = OutputEvent.CategoryValue.Console
            });
            this.Protocol.SendEvent(new TerminatedEvent());
        } else {
            this.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Step) {
                ThreadId = 1,
                AllThreadsStopped = true
            });
        }
    }
}
