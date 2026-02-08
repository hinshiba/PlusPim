using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json.Linq;
using PlusPim.Application;

namespace PlusPim.EditorController.DebugAdapter;

internal class DebugAdapter: DebugAdapterBase {
    private const int REGISTERS_SCOPE_REF = 1000;
    private const int SPECIAL_REGISTERS_SCOPE_REF = 1001;

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

        return new StackTraceResponse {
            StackFrames = [
                new StackFrame(1, "main", this._app.GetCurrentLine(), 1) {
                    Source = new Source { Path = this._app.GetProgramPath() }
                }
            ],
            TotalFrames = 1
        };
    }

    protected override ScopesResponse HandleScopesRequest(ScopesArguments args) {
        this.Protocol.SendEvent(new OutputEvent {
            Output = "Handler: ScopesRequest.\n",
            Category = OutputEvent.CategoryValue.Console
        });

        return new ScopesResponse {
            Scopes = [
                new Scope("Registers", REGISTERS_SCOPE_REF, false) {
                    PresentationHint = Scope.PresentationHintValue.Registers
                },
                new Scope("Special Registers", SPECIAL_REGISTERS_SCOPE_REF, false) {
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
        (int[] registers, int pc, int hi, int lo) = this._app.GetRegisters();

        if(args.VariablesReference == REGISTERS_SCOPE_REF) {
            for(int i = 0; i < 32; i++) {
                variables.Add(new Variable(RegisterNames[i], $"0x{registers[i]:X8}", 0));
            }
        } else if(args.VariablesReference == SPECIAL_REGISTERS_SCOPE_REF) {
            variables.Add(new Variable("PC", $"0x{pc:X8}", 0));
            variables.Add(new Variable("HI", $"0x{hi:X8}", 0));
            variables.Add(new Variable("LO", $"0x{lo:X8}", 0));
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

        while(!this._app.IsTerminated()) {
            this._app.Step();
        }
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

        while(this._app.StepBack()) {
            // 先頭まで巻き戻す
        }

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
