# Plan: code-quality

Owner area: dead code removal, de-duplication, naming consistency, instruction-execution performance (#13).

Baseline: all `file:line` references are against branch `agent/temp` at `148383b` (the branch the main tree is on). The `main` branch (`73fc28f`) is older and differs a lot in `PlusPim/` (Processor, ExecuteResult, RuntimeError), so do not implement against `main`. Two todo items are already partly done on `agent/temp`: `inst_` and `maxLength_` no longer exist (removed in `148383b` and `b069ddb`).

## Resolved decisions (user answers, applied)

- `PlusPimDbg.GetRegisters` stays as a supported `PlusPimDbg` API (it is a natural inspection API). It is removed from the dead-code list; no extension method is added to the test project.
- The `--debug` flag stays (no change to `extension.ts`).
- The renames are processed first, before the feature work. The full list with reasons is `doc/plans/renames.md`.

## Summary

- Dead code: every listed symbol is confirmed unused by repo-wide search, except two that the todo itself flags. `PlusPimDbg.GetRegisters` stays as a supported API (decided by the user). `ParsedProgram.InstructionCount` is deleted and its one test use is rewritten. Deleting `TextSegment.BaseAddress` and `TextSegmentBuilder.CurrentAddr` leaves constructor parameters unread, so the removal also simplifies the `TextSegmentBuilder`, `TextSegment` and `DataSegment` constructors.
- De-duplication: a composition helper `RegisterWriteHistory` (one register, one undo stack) replaces the four copies of `Stack<uint>` + `WriteRd`/`WriteRt` + `Undo`. It keeps the existing contract: write only on success, LIFO undo per instruction instance, and no push on exception or runtime error.
- Naming: rename `Address.InValid` to `Invalid`. Rename the namespaces and folders `instructions` to `Instructions` and `records` to `Records`. Use 1-based line numbers everywhere and call them `lineNumber`, so the name `lineIndex` disappears. Remove trailing underscores.
- Performance: an interpolated string handler on `RuntimeContext.Log` skips formatting when Debug logging is off, and needs no call-site changes. A BenchmarkDotNet project measures it. In the prototype, the non-verbose run drops from 10.0 ms / 4.27 MB to 1.4 ms / 1.83 MB per ~10k steps.
- The full prototype of all of the above built cleanly (0 warnings), passed all 25,839 existing tests and passed the CI `dotnet format --verify-no-changes` check.

## Task breakdown

### Dead code: unreachable code after `throw` in Program.cs (CS0162)

- Problem: `Program.cs:148` throws `NotImplementedException`, which makes `Program.cs:149-150` unreachable. This is the only build warning.
- Root cause: a placeholder for a non-debug run mode that was never built. `Program.cs:150` is the only caller of `new Application(false, ...)`.
- Chosen design: replace the else branch at `Program.cs:146-151` with `Console.Error.WriteLine("Non-debug mode is not implemented. Use --debug.")` and `return 1;`. Keep the `--debug` option (`Program.cs:33-40`), because the extension always passes `-d` (`vscode_ext/pluspim/src/extension.ts:100`). Removing the option would make System.CommandLine reject it and would need a matching extension change.
  - Rejected: removing `--debug` and always running in debug mode. That couples this change to the vscode-extension area for no functional gain (see open questions).
  - Rejected: keeping the `throw` and deleting only the dead lines. An unhandled exception is a worse user experience than an error message and exit code 1.
- Steps: edit the else branch, and change `Program.cs:104` to `new(files, logger)` together with the next item.
- Risks: running without `-d` now exits with code 1 and a message instead of crashing. Nothing depends on the old behavior.
- Verify: `dotnet build` shows 0 warnings.

### Dead code: `Application._isDebug` and the runtime-mode path

- Problem: `Application.cs:15` `_isDebug`, the constructor parameter at `Application.cs:27,30-31`, and the `if(!this._isDebug) Continue()` branch at `Application.cs:43-47` are reachable only through the dead code above.
- Chosen design: the constructor becomes `Application(FileInfo[] files, ILogger logger)`, and `Load()` only builds the debugger and logs.
- Steps: edit `Application.cs`, `Program.cs:104`, and `PlusPimTests/ApplicationTests.cs:35` (`new(true, [tempFile], ...)` becomes `new([tempFile], ...)`, the only test construction).
- Risks: the debugger-dap plan (Pause, worker-thread `Continue`) edits `Application.cs:135-156`. This change touches only lines 12-51, so land it first.

### Dead code: `InstructionIndex.FromAddress` (both overloads), `Address.FromInstructionIndex(..., bool)`

- Confirmed unused by search: `InstructionIndex.cs:18-27`, `InstructionIndex.cs:35-41`, `Address.cs:8-12`.
- Once these, `TextSegmentBuilder.CurrentInstructionIndex` and `ParsedProgram.GetInstruction` are gone, the operators `InstructionIndex.cs:44-57` and `InstructionIndex.Invalid` (`:59`) are also unused. The only remaining use of the type is `Address.FromInstructionIndex(InstructionIndex, Address)` at `ParsedProgram.cs:166`.
- Chosen design:
  - Step 1 (wave 1): delete the two `FromAddress` overloads, the bool overload and the unused operators, and drop the now-unused `using ...Runtime`. The type shrinks to `internal record struct InstructionIndex(int Idx);`.
  - Step 2 (wave 2, after assembler-frontend fixes #7): if `ParsedProgram.cs:166` is still the only use, inline it as `segmentBase + (uint)(instructionCount * 4)` and delete both `InstructionIndex` and `Address.FromInstructionIndex`. The #7 fix rewrites exactly that loop (`ParsedProgram.cs:161-175`), so doing step 2 earlier would only create a conflict.
- Verify: build, then run the tests.

### Dead code: `TextSegmentBuilder.CurrentInstructionIndex` / `CurrentAddr`

- Confirmed unused: `TextSegmentBuilder.cs:33-39`.
- Prototype finding: after removing `CurrentAddr` and `TextSegment.BaseAddress`, the primary constructor parameter `baseAddr` (`TextSegmentBuilder.cs:9`) is only forwarded to `new TextSegment(..., baseAddr)` (`:42`). So the cleanup cascades: `TextSegmentBuilder(ILogger logger)`, `TextSegment(List<IInstruction> instructions)`, and the call sites `ParsedProgram.cs:122,130` become `new(logger)`.
- Ordering: wave 2. The assembler-frontend fix for #7 (make pass 1 and pass 2 agree, `ParsedProgram.cs:172` vs `TextSegmentBuilder.cs:25-30`) may decide to compute label addresses from the builder's actual position, which is exactly what `CurrentAddr` provides. Tell that owner that the methods exist and are slated for deletion. Delete them only if the #7 fix does not use them.

### Dead code: `TextSegment.BaseAddress`, `DataSegment.BaseAddress`

- Confirmed unused: `TextSegment.cs:28`, `DataSegment.cs:18`. The static constants `TextSegment.TextSegmentBase`, `KernelTextSegmentBase` and `DataSegment.DataSegmentBase` are used and stay.
- Design: delete both fields. Delete the `addr` constructor parameters (`TextSegment.cs:14`, `DataSegment.cs:8`), and change `DataSegmentBuilder.cs:113` to `new DataSegment(this._memoryImage, size)`. `DataSegmentBuilder` keeps `_baseAddr` because it computes the size from it.
- Cross-check: the debugger-dap "memory view" item (`.data` and the area around `$sp`) can use the static `DataSegment.DataSegmentBase` plus `ParsedProgram.DataSegmentSize`, so it does not need the instance field.
- Ordering: delete `DataSegment.BaseAddress` in wave 1. Delete `TextSegment.BaseAddress` in wave 2 together with the builder change, because the two share the constructor.

### Dead code: `ParsedProgram.GetInstruction`

- Confirmed unused: `ParsedProgram.cs:192-194`. Runtime fetch goes through `ParsedPrograms.TryGetInstruction` (`ParsedPrograms.cs:93`).
- Design: delete it in wave 1. It is a separate hunk from the #7 area.

### Dead code: `Immediate.Parse`

- Confirmed unused: `Immediate.cs:25-27`. It exists only to satisfy `IParsable<Immediate>` (`Immediate.cs:9`), and nothing uses `Immediate` through `IParsable` (no generic `T : IParsable<T>` anywhere). All callers use `Immediate.TryParse(s, null, out ...)` (`OperandParser.cs:136,167,196,278`).
- Design: remove `IParsable<Immediate>` and `Parse`, and keep `TryParse` with its signature unchanged (including the unused `IFormatProvider?`, to avoid touching OperandParser, which assembler-frontend is about to rework).
- Rejected: keeping `IParsable` for "future generic parsing". assembler-frontend's new 32-bit immediate parser for `li` is a different type and does not need it.

### Dead code: `PlusPimDbg.GetRegisters` and `ParsedProgram.InstructionCount` (test-only)

- Usage: `GetRegisters` (`PlusPimDbg.cs:53-55`) is not part of `IDebugger` and has about 40 call sites, all in tests (`IntegrationTests.cs`, `TimeTravelTests.cs`, `DataSegmentTests.cs:178,281`, `TestHelpers.cs:146`). `InstructionCount` (`ParsedProgram.cs:199`) has one test use (`DataSegmentTests.cs:204`).
- Decision for `GetRegisters` (decided by the user): keep it as a supported `PlusPimDbg` API. No change in this plan. Optionally add it to `IDebugger` later if the DAP layer needs it.
- Decision for `InstructionCount`: delete it. Change `DataSegmentTests.cs:204` to `program.TextSegment.Instructions.Length`.
- Verify: the full test suite (the prototype ran it with 0 failures).

### De-duplication: `Stack<uint>` + `WriteRd` + `Undo`

- Problem: the same 12 to 20 lines appear in `RType3RegInstruction.cs:24,41-51`, `RTypeShiftImmInstruction.cs:20,31-41`, `RTypeShiftVarInstruction.cs:24,35-45` and `ITypeInstruction.cs:26,45-64` (as `WriteRt`).
- Semantics that must hold (checked against the code):
  - `IInstruction.Execute` must not change the context and must not push undo data when it returns `Raise` or `Fail` (`IInstruction.cs:13-16`, `ExecuteResult.cs:19-23`). `Processor.Undo` calls `Instruction.Undo` only for `record.Completed` (`Processor.cs:68-74`). So the push must happen only on the success path. Today it does: the `catch(OverflowException)` returns `Raise` before `WriteRd`, at `RType3RegInstruction.cs:32-35` and `ITypeInstruction.cs:33-36`.
  - One instruction instance runs many times in loops, and `PlusPimDbg.Back` pops a global history LIFO (`PlusPimDbg.cs:132-157`). So a per-instance LIFO stack matches.
  - Writes to `$zero` are dropped by `RegisterFile.cs:18-23`. Saving `0` and restoring `0` stays a no-op.
- Chosen design: composition.
  ```csharp
  // PlusPim/Debuggers/PlusPimDbg/Instruction/RegisterWriteHistory.cs
  internal sealed class RegisterWriteHistory(RegisterID register) {
      private readonly Stack<uint> _previousValues = new();
      public void Write(RuntimeContext context, uint value);  // push the current value, then write
      public void Undo(RuntimeContext context);               // pop and restore; InvalidOperationException if empty
  }
  ```
  Each of the four classes gets `private readonly RegisterWriteHistory _rd = new(rd);` (`_rt` in IType), calls `this._rd.Write(context, result)` on success, and implements `public void Undo(RuntimeContext context) { this._rd.Undo(context); }`.
- Rejected alternatives:
  - An abstract base class (`SingleRegisterWriteInstruction(RegisterID dest, int sourceLine)`). It would also absorb `SourceLine` and `Undo`, but derived classes that log `rd` would capture a primary-constructor parameter that is also passed to the base (warning CS9107), or would need to read a protected property. It also cannot serve classes whose undo mixes registers with HI/LO, CP0 or memory.
  - Merging the four classes into one generic "compute" instruction. It changes the log text and every parser factory in `InstructionRegistry.cs:83-112`, which is high churn in a file assembler-frontend also edits.
- Optional follow-up (not required by the todo): the same pattern appears in `CP0RegisterInstruction.cs:15,35,47` (mfc0), `JalInstruction.cs:9,21,38-41` ($ra), `LoHiRegisterInstruction.cs:18,31,54` (mfhi/mflo) and `MemoryInstruction.cs:30,77,88` (loads). Those classes share one stack between a register path and a non-register path. Adopting the helper there means splitting the stack, so do it only when a class is touched for another reason.
- Steps: add the helper, convert the four classes, and remove their private `WriteRd`/`WriteRt` and the stack fields.
- Risks: the error message on an empty-stack undo changes from "No previous value to undo." to "No previous value of $X to undo.". No test asserts it.
- Verify: the per-instruction tests cover execute, undo, repeated execution and overflow (`RepeatedExecution.Run`, `Processor.Undo` and `ExcCode.Ov` appear 108 times across 21 test files). Mutation check in the prototype: turning `Undo` into a no-op makes 4,104 tests fail, so the suite does guard this refactor.

### Naming: `Address.InValid` vs `Label.Invalid`

- Design: rename `Address.InValid` (`Address.cs:77`) to `Address.Invalid`, matching `Label.Invalid` (`Label.cs:13`) and `InstructionIndex.Invalid` (`InstructionIndex.cs:59`). Production call sites: `CP0RegisterFile.cs:30`, `Label.cs:13`, `BranchInstruction.cs:49`, `JumpInstruction.cs:37`. There are no test uses (tests only use `Label.Invalid`, at `JumpInstructionTests.cs:149`).
- Note: `CP0RegisterFile.cs:30` uses it as "EPC = 0". The value stays `new(0)`, so behavior is unchanged.

### Naming: namespace casing (`...Instruction.instructions`, `Program.records`)

- Design: rename the folders and namespaces `Instruction/instructions` to `Instruction/Instructions` (including the sub-namespaces `.Factories` and `.Jump`) and `Program/records` to `Program/Records`. `.editorconfig:59` sets `dotnet_style_namespace_match_folder = true`, so the folder and the namespace must change together.
- Steps:
  - Windows (`core.ignorecase=true`): a direct `git mv instructions Instructions` fails with "Invalid argument" (confirmed in the prototype). Use two steps: `git mv .../instructions .../instructions_tmp` and then `git mv .../instructions_tmp .../Instructions`, and the same for `records`. The index then records the case change, which matters because CI runs on case-sensitive Linux.
  - Replace the text `Instruction.instructions` with `Instruction.Instructions` and `Program.records` with `Program.Records` as whole words, in `PlusPim/`, `PlusPimTests/` and `PlusPimBenchmarks/`. In the prototype this was 73 replacements in 57 files.
  - Run `dotnet format PlusPim.slnx`. Prototype finding: the case change re-orders `using` directives (ordinal sort puts `I` before `P`, while lowercase `i` sorted after `P`), and CI's `dotnet format --verify-no-changes` failed on `Instruction/RuntimeCall.cs` until it was formatted.
- Risks: this touches almost every file, so its placement in the order matters most (see Ordering). The build found no name clash between the new `Instructions` namespace and the `TextSegment.Instructions` property.

### Naming: `lineIndex` 0-based in `TextSegmentBuilder`, 1-based in instruction constructors

- Current state:
  - `ParsedProgram.cs:54-56` counts lines from 0, and the value flows as `LineIndex` through `textLines`/`dataLines`/`kernelTextLines` (`:46-49`) into `DataSegmentBuilder.AddLabel` (`DataSegmentBuilder.cs:38-40`) and `TextSegmentBuilder.AddLine` (`TextSegmentBuilder.cs:16-18`).
  - `TextSegmentBuilder.cs:25` adds 1 before calling `InstructionRegistry.TryParseAll`. From there on the value is 1-based but still named `lineIndex` in `IInstructionParser`, `IPseudoInstructionParser`, `FuncInstructionParser`, `InstructionFactory`, every instruction constructor, `ParsedPrograms.GetSourceInfo`/`GetAddressForLine` and `PlusPimDbg.GetCallStack`.
  - The log messages add 1 by hand at `ParsedProgram.cs:87,114,116,168,170`.
  - `IInstruction.SourceLine` is documented as 1-based (`IInstruction.cs:28-31`), and so are `IDebugger.SetBreakpoints` (`IDebugger.cs:42`) and DAP stack frames (`DebugAdapter.cs:190`). `doc/assembler.md:21` also specifies 1-based.
- Decision: 1-based is canonical everywhere, and the name for it is `lineNumber`. The name `lineIndex` disappears. The only place where a 0-based value existed, `ParsedProgram`'s reader loop, now produces 1-based numbers at the source: `int lineNumber = 0; while(...) { lineNumber++; ... }`.
  - Rejected: making everything 0-based and converting at the DAP and log boundary. That would change `SourceLine` semantics, which tests check (`InstructionParseTests.cs:27-33`) and the docs specify, and it would create several conversion points instead of removing one.
  - The `IInstruction.SourceLine` property keeps its name to avoid an interface change.
- Safe migration:
  - Commit A (semantic, 3 files): in `ParsedProgram.cs`, `TextSegmentBuilder.cs` and `DataSegmentBuilder.cs`, replace `lineIndex + 1` with `lineNumber`, start the counter at 0 with pre-increment, rename the tuple field `LineIndex` to `LineNumber`, drop the `+ 1` in `TextSegmentBuilder.cs:25`, and update the doc comments ("0-indexed" becomes 1-based).
  - Commit B (mechanical): whole-word rename of `lineIndex` to `lineNumber` in `PlusPim/` and `PlusPimTests/`. That is 95 occurrences in 30 files; there are no named arguments `lineIndex:` to break.
  - Before commit A, add a characterization test that pins exact values: the `Line` of the stack frame after each step, and `SetBreakpoints` `Verified` for blank, label and pseudo-instruction lines. This is a test task for the test owner. The prototype used a throwaway version of it, and it passed both before and after the change.
- Risks: off-by-one errors in reported lines and breakpoints. The existing `IntegrationTests.Step_GetCurrentLine_MatchesSourceLine` (`IntegrationTests.cs:224-249`) only checks that lines increase, so it would not catch them, which is why the pinning test must exist first.

### Naming: trailing underscores (`_debugger_`, `inst_`, `maxLength_`)

- On `agent/temp`, `inst_` and `maxLength_` are already gone. What remains:
  - `Application.cs:12-13,41,54,68,72`: `_debugger_` becomes `_debugger`.
  - `PlusPimDbg.cs:160-161`: `exc_` becomes `lastException`.
  - `PlusPimDbg.cs:200-201`: `addr_` becomes `lineAddr`.
- After the change, a repo-wide search for `\b[a-z]\w*_\b` in `*.cs` returns nothing (checked in the prototype).

### Performance #13: don't build log strings when Debug is off

- Problem: about 22 `context.Log($"...")` calls on the execution path, such as `ITypeInstruction.cs:38`, `RType3RegInstruction.cs:37`, `MemoryInstruction.cs:64,74`, `BranchInstruction.cs:33,50,54`, `RuntimeCall.cs:51-152` and `RuntimeContext.cs:228,234,295`, format their strings on every step. `RuntimeContext.Log` (`RuntimeContext.cs:210-212`) forwards to `Logger.ToAction` -> `Logger.Debug` -> `Logger.Log`, which only then drops the message when `level < minLevel` (`Logger.cs:20-23,49-51`). `minLevel` is Debug only with `--verbose` (`Program.cs:83`) and is fixed for the logger's lifetime.
- Chosen design: a C# interpolated string handler.
  - `ILogger.IsEnabled(LogLevel level)`. `Logger` returns `minLevel <= level`, and `NullLogger` returns `false`.
  - `RuntimeContext` gets a trailing optional parameter `bool isLogEnabled = true` and a public `IsLogEnabled` property. `PlusPimDbg.cs:49` passes `logger.IsEnabled(LogLevel.Debug)`. Defaulting to `true` keeps `TestHelpers.CreateRuntimeContext` (`TestHelpers.cs:47`) unchanged and keeps the formatting path covered by tests.
  - New `Runtime/RuntimeLogHandler.cs`:
    ```csharp
    [InterpolatedStringHandler]
    internal ref struct RuntimeLogHandler {
        public RuntimeLogHandler(int literalLength, int formattedCount, RuntimeContext context, out bool isEnabled);
        public bool IsEnabled { get; }
        public void AppendLiteral(string value);
        public void AppendFormatted<T>(T value);
        public void AppendFormatted<T>(T value, string? format);
        public string ToStringAndClear();
    }
    ```
    It wraps `DefaultInterpolatedStringHandler`. It is created only when the flag is on.
  - `RuntimeContext` gains a second `Log` overload, `public void Log([InterpolatedStringHandlerArgument("")] ref RuntimeLogHandler handler)`. The existing `Log(string)` overload remains for constant strings and also checks `IsLogEnabled`.
  - Call sites do not change. C# binds any `$"..."` that has holes to the handler overload.
- Rejected alternatives:
  - An `if(context.IsLogEnabled)` guard at every call site. That is about 22 edits, and new instructions will forget it.
  - `Action<string>?` with null meaning disabled. It changes the `ILogger.ToAction` contract and spreads null checks.
  - `Func<bool>` evaluated per call. The logger's level never changes after construction, so this is not needed.
- Hazard: when logging is off, hole expressions are not evaluated at all. Every current hole is pure (register reads via `RegisterFile.cs:17`, and `ToString` on records and enums). The rule "no side effects in log holes" is written in the handler's `<remarks>`.
- Prototype checks (throwaway):
  - When disabled, `ctx.Log($"x={++n}")` leaves `n == 0`.
  - When enabled, the output is byte-identical to `string` interpolation.
  - `addu` logs nothing when disabled and exactly one line when enabled.
  - The full suite stays green.
- Not in scope: parse-time `logger.Debug($"...")` in `ParsedProgram`/`TextSegmentBuilder` runs once per source line, which is not hot. The same handler technique can be added to `ILogger` later if parsing ever shows up in a profile.

### Performance #13: benchmark

- Decision: BenchmarkDotNet, in a new project `PlusPimBenchmarks/`, listed in `PlusPim.slnx` under `/benchmarks/`.
  - Why: it handles warmup and JIT tiering, gives a statistical summary, and `[MemoryDiagnoser]` shows allocations, which is where most of the win is.
  - Cost: one package (0.15.2 restored fine in the prototype), one extra `InternalsVisibleTo` entry in `PlusPim.csproj` (all types are `internal`), and benchmark classes must be `public` with only public parameter types (the internal `LogLevel` cannot be a `[Params]` type, so use `bool Verbose`).
  - Rejected: a Stopwatch harness or a hidden CLI flag. It has no dependency, but it is noisy without warmup or tiering control, it reinvents statistics, and it does not show allocations.
- Benchmark design (`StepBenchmarks.cs`):
  - Workload: a 4-instruction loop (`addu`, `sll`, `addiu`, `bne`) run N times, then `jr $ra`, which terminates.
  - Parameter: `Verbose` (false/true), which builds `Logger(Info|Debug)` with a no-op sink, standing in for the DAP sink in `DebugAdapter.cs:43`.
  - `RunToEndAndBack`: the primary benchmark. It runs to the end and then calls `Back()` until the start, which exercises undo and restores the state exactly. So it can use `[GlobalSetup]` only and let BenchmarkDotNet pick the invocation count.
  - `RunToEnd`: needs a fresh debugger, so `[IterationSetup]`, which forces InvocationCount=1. Size N so that one call takes 100 ms or more. The prototype's N=2500 (~10k steps) triggered BenchmarkDotNet's MinIterationTime warning, so use N=50_000 or more.
- Run: `dotnet run -c Release --project PlusPimBenchmarks -- --filter '*StepBenchmarks*'`. BenchmarkDotNet refuses Debug builds. The project is Release-only by usage, so the `Debug` OutputPath override in `PlusPim.csproj:12-15` does not affect it.
- Prototype numbers (ShortRun, ~10k steps, indicative only):

  | Variant | RunToEnd | Allocated |
  |---|---:|---:|
  | Before (always formats), non-verbose | 10.0 ms | 4.27 MB |
  | After, non-verbose | 1.4 ms | 1.83 MB |
  | After, verbose | 10.8 ms | 4.27 MB |

  The remaining ~180 B per step comes from per-step history (`ExecutionRecord` and `Executed` records, `PlusPimDbg.cs:21,97`, `Processor.cs:12`). That is the next candidate if #13 continues; it is out of scope here.

## Ordering and dependencies

Each step is its own commit on an `agent/...` branch (AGENTS.md git rule), with `dotnet build` at 0 warnings, `dotnet test` green and `dotnet format --verify-no-changes PlusPim.slnx` clean.

- Wave 0, before any feature branch starts (mechanical, touches many files; landing them first means the other areas build on the final names and never rebase across them):
  - Namespace and folder casing rename, plus `dotnet format`.
  - The `lineIndex` migration, commit A then commit B, preceded by the pinning test from the test owner.
  - The `Address.Invalid` rename and the trailing-underscore renames.
  - Fallback: if feature branches already exist when this starts, do wave 0 last instead. All three are scripted (the `git mv` pairs and whole-word regex replacements above) and cheap to re-run on top of finished work. The worst choice is landing them in the middle.
- Wave 1, before the feature work (small, separate hunks):
  - Dead code: Program.cs, `Application._isDebug`, `InstructionIndex.FromAddress`, `Address.FromInstructionIndex(bool)`, `DataSegment.BaseAddress`, `ParsedProgram.GetInstruction`/`InstructionCount`, `Immediate.Parse`, `GetRegisters` moved to the tests.
  - The `RegisterWriteHistory` de-duplication. This makes later instruction edits smaller for runtime and assembler-frontend.
  - The log handler and the benchmark project. The benchmark gives runtime and debugger-dap a baseline to check their changes against.
- Wave 2, after assembler-frontend's #7 fix (pass 1 / pass 2 consistency):
  - `TextSegmentBuilder.CurrentInstructionIndex`/`CurrentAddr` (only if #7 does not use them), the `TextSegmentBuilder`/`TextSegment` constructor simplification and `TextSegment.BaseAddress`.
  - Inlining and deleting `InstructionIndex` and `Address.FromInstructionIndex` at `ParsedProgram.cs:166`.

## Files expected to change

Paths are relative to the repo root; `~` means `PlusPim/Debuggers/PlusPimDbg`.

- Wave 0 (wide):
  - Folders: `~/Instruction/instructions/**` renamed to `~/Instruction/Instructions/**` (all 19 files, including `Factories/` and `Jump/`), and `~/Program/records/**` renamed to `~/Program/Records/**` (`Address.cs`, `InstructionIndex.cs`, `Label.cs`).
  - `using` and `lineNumber` edits in: `~/PlusPimDbg.cs`, `~/Instruction/ExecuteResult.cs`, `~/Instruction/RuntimeCall.cs`, `~/Instruction/Parser/{IInstructionParser,IPseudoInstructionParser,InstructionRegistry}.cs`, `~/Instruction/Pseudo/{La,Li,Move,Nop}InstructionParser.cs`, `~/Program/{ParsedProgram,ParsedPrograms,SymbolTable,TextSegment,TextSegmentBuilder,DataSegment,DataSegmentBuilder}.cs`, `~/Runtime/{CP0RegisterFile,RuntimeContext,StackFrame}.cs`, `PlusPim/Application/Application.cs`.
  - Tests: `PlusPimTests/{TestHelpers,InstructionParseTests,DataSegmentTests}.cs` and `PlusPimTests/Instructions/{BranchInstructionTests,CP0InstructionTests,ITypeArithmeticInstructionTests,InstructionHarness,InstructionHarnessTests,JumpInstructionTests,MachineState,MemoryReference,ProcessorTests,RTypeArithmeticInstructionTests,RuntimeCallInstructionTests,SystemInstructionTests,UnalignedMemoryInstructionTests}.cs`.
- Wave 1:
  - `PlusPim/Program.cs`, `PlusPim/Application/Application.cs`, `~/PlusPimDbg.cs`.
  - `~/Program/Records/{InstructionIndex,Address}.cs`, `~/Program/{DataSegment,DataSegmentBuilder,ParsedProgram}.cs`, `~/Instruction/Parser/Immediate.cs`.
  - New `~/Instruction/RegisterWriteHistory.cs`, and `~/Instruction/Instructions/{RType3Reg,RTypeShiftImm,RTypeShiftVar,IType}Instruction.cs`.
  - `PlusPim/Logging/Logger.cs`, `~/Runtime/RuntimeContext.cs`, new `~/Runtime/RuntimeLogHandler.cs`.
  - `PlusPim/PlusPim.csproj` (InternalsVisibleTo), `PlusPim.slnx`, new `PlusPimBenchmarks/{PlusPimBenchmarks.csproj,StepBenchmarks.cs}`.
  - Tests: `PlusPimTests/{ApplicationTests,DataSegmentTests,TestHelpers}.cs`.
- Wave 2: `~/Program/{TextSegmentBuilder,TextSegment,ParsedProgram}.cs`, `~/Program/Records/{InstructionIndex,Address}.cs`.

## Cross-area dependencies

- assembler-frontend:
  - Shared files: `ParsedProgram.cs` (#7 at `:161-175`, same-line labels at `IsLabel` `:182`), `TextSegmentBuilder.cs` (#7 at `:25-30`), `OperandParser.cs` and every `CreateParser` lambda (register-name unification), `LiInstructionParser.cs` (hex and 32-bit `li`).
  - Wave 0 renames `lineIndex` inside all of those lambdas, so their branch should start after wave 0.
  - Ask them whether the #7 fix uses `TextSegmentBuilder.CurrentAddr`/`CurrentInstructionIndex`. This decides wave 2.
  - Side note for them: `InstructionFactory.cs:10-24` duplicates the `ori`, `lui`, `addu` and `sll` lambdas from `InstructionRegistry.cs:84,95,108,112`.
- runtime:
  - Shared files: `RuntimeContext.cs` (this plan adds a trailing constructor parameter and a `Log` overload; they may add `PendingInput` or initial `$sp`/`$gp` values), `RuntimeCall.cs` (print_char; wave 0 touches only `using`s), `PlusPimDbg.cs:35-51` (constructor).
  - Agree that new `RuntimeContext` constructor parameters go after `isLogEnabled`, or switch to named arguments.
  - New log calls in instructions must keep holes side-effect free.
  - New single-register writers should use `RegisterWriteHistory`.
- debugger-dap:
  - Shared files: `Application.cs` (Pause, worker-thread `Continue` at `:135-144`, Reverse Continue at `:151-156`; this plan edits only `:12-51`), `PlusPimDbg.cs` (`GetRegisters` removed; the `exc_`/`addr_` renames at `:160,200`).
  - Their tests should read state through `GetCallStack()`, or through the new test extension `GetRegisters`.
  - The memory view can use `DataSegment.DataSegmentBase`, not the deleted instance field.
- vscode-extension:
  - Shared file: `Program.cs` (port probe at `:126-137`; this plan edits `:104` and `:146-151`, different hunks).
  - The extension keeps passing `-d`, and this plan keeps the option.

## Prototype findings

The prototype is uncommitted in worktree `agent-a62bd5dce6b5a29bc`, whose working copy was restored from `148383b`, and will be discarded.

- Whole prototype: built with 0 warnings (the baseline had 2 x CS0162), passed all 25,839 existing tests plus 4 throwaway checks, and passed `dotnet format --verify-no-changes PlusPim.slnx` after one `dotnet format` run.
- Cascade from the dead code: removing `CurrentAddr` and `TextSegment.BaseAddress` leaves `baseAddr` unread in `TextSegmentBuilder` and `addr` unread in `TextSegment`/`DataSegment`. Removing `FromAddress`, `GetInstruction` and `CurrentInstructionIndex` leaves `InstructionIndex` with a single use. So the plan now simplifies the constructors and schedules deleting `InstructionIndex` into wave 2, alongside #7.
- Case-only folder renames: a direct `git mv` fails on Windows ("Invalid argument"), and the two-step rename works. The case change also re-sorts `using` directives, which fails the CI format check until `dotnet format` runs. Both points are now explicit steps.
- De-duplication: composition needed no hierarchy and produced no warnings. A base-class version would have hit CS9107, because derived classes log `rd` while also passing it to the base. The mutation check (no-op `Undo` makes 4,104 tests fail) confirmed that the suite is a real safety net for it.
- Log handler: overload resolution picked the handler for every existing `$"..."` call without edits. When disabled, the hole expressions were skipped (side-effect check), so the "pure holes" rule was added. Formatting was identical when enabled.
- Benchmark: BenchmarkDotNet restored and ran. A `LogLevel`-typed `[Params]` does not compile, because the type is internal. `[IterationSetup]` with a ~10k-step workload produced a MinIterationTime warning, so the plan uses a larger N and makes `RunToEndAndBack` (state-restoring, no IterationSetup) the primary benchmark. Measured result: about 7x faster and about 57% fewer allocations in the default non-verbose mode.
- Line numbers: switching `ParsedProgram` to produce 1-based numbers at the source, and then the mechanical rename, kept the pinned stack-frame lines `[3, 5, 5, 6]` and breakpoint verification `[false, true, false, true, true]` identical to the baseline.

## Open questions for the user

None remaining. All three earlier questions were answered (see Resolved decisions).

## Reconciliation with other plans (added after review)

- Wave 0 renames (`lineNumber`, namespace casing, `Invalid`) change many files that the other four plans also list. Land wave 0 before the feature plans start, or last if their branches already exist.
- assembler-frontend.md replaces `IPseudoInstructionParser.GetExpansionSize` / `TryExpand` and exposes pass-1 counts. It also needs `ParsedProgram.InstructionCount`-style counts, so the planned deletion of `InstructionCount` here must be re-checked against that plan.
- debugger-dap.md adds `IInstruction.Disassembly` to the same instruction classes that the `RegisterWriteHistory` refactor touches. Land the refactor first.
- vscode-extension.md and debugger-dap.md both edit `Program.cs`; the dead-code removal there is on different lines.
