# Plan: debugger-dap (debugger core, Application layer, DAP server)

Base: branch `agent/temp` at `148383b`. Line numbers below refer to that commit.

## Resolved decisions (user answers, applied)

- Reverse Continue stops at breakpoints only. It does not stop at exceptions, even ones matching the exception filters.
- The default kernel handler task (#3) is dropped. PlusPim will not bundle a handler; users supply their own `.ktext` file as today. Everything about `useDefaultKernel`, `--no-default-kernel`, hidden-code stepping, the deemphasized frame and the `default_kernel.asm` resource was removed from this plan.
- Pseudo-instruction hints are needed only during a debug session. No CLI or LSP front end is planned for this.

## Summary

All six items depend on one structural change: execution moves off the DAP dispatch thread. Today every handler runs synchronously on the protocol library's dispatcher (`DebugAdapter.cs:263-324`). So `continue` on an infinite loop never returns, and the client's next request, including `pause`, is never dispatched.

The plan:

- Add an `ExecutionCoordinator` (Application layer). It runs at most one execution operation at a time on a worker thread and cancels it with a `CancellationToken`.
- Make `Application` thread-safe with one lock (`_gate`). Long loops release the lock every 4096 steps, so `setBreakpoints`, `stackTrace` and `readMemory` are served while the program runs.
- Change the DAP execution handlers to the library's `Handle*RequestAsync(IRequestResponder)` overloads. Each handler sends its response first and starts the worker, which sends `stopped` when it finishes. A request that arrives while the program runs gets an error response.
- Build the other items on top of this:
  - DAP-compliant init sequence (`configurationDone`)
  - `pause`
  - reverse continue that stops at breakpoints only
  - `readMemory` with memory references on registers plus a "Memory" scope
  - a custom DAP request `pluspimPseudoExpansions` for the inlay hints

A throwaway prototype in the worktree implemented all of this. Five end-to-end DAP scenarios over in-memory pipes pass, and so do the existing 25839 tests.

## Task breakdown

### initでDAPに準拠しない問題 (init sequence not DAP-compliant)

**Problem.** The adapter sends `stopped(entry)` and `initialized` from inside the `launch` handler, before the `launch` response. It never supports `configurationDone`.

**Root cause.**

- `DebugAdapter.cs:108-113`: `StoppedEvent(Entry)` is sent first, then `InitializedEvent`, then `LaunchResponse` is returned. DAP requires a different order: the adapter sends `initialized` when it can accept configuration requests. The client then sends `setBreakpoints`, `setExceptionBreakpoints` and `configurationDone`. Execution, including stop-on-entry, starts only after `configurationDone`. With the current order the client can query `stackTrace` before breakpoints exist, and the entry stop comes before the launch response.
- `DebugAdapter.cs:77-98`: `SupportsConfigurationDoneRequest` is not set, so clients never send `configurationDone`.
- `DebugAdapter.cs:35-41`: `Protocol.Run()` is called before the `DispatcherError` handler is registered. An early dispatcher error is lost (small race).
- `Application.cs:13`: if `setBreakpoints` arrives before `Load`, `Debugger` throws. This is safe today only because `initialized` is sent after `Load`. The new sequence keeps that ordering on purpose.

**Chosen design.** Sequence: `initialize` → response (capabilities incl. `supportsConfigurationDoneRequest`) → `launch` → `Load()` → launch response → `initialized` event → configuration requests → `configurationDone` → response → `stopped(entry)`. If the launch config has `stopOnEntry: false`, the adapter instead starts Continue on the worker.

Alternative rejected: loading in `initialize`. The file list comes from the CLI, so this is possible. But a load failure can only be reported cleanly as a failed `launch` response, which VS Code shows to the user.

**Interfaces.**

- `HandleInitializeRequest`: add `SupportsConfigurationDoneRequest = true` (and `SupportsReadMemoryRequest = true`, see the memory view item).
- `override void HandleLaunchRequestAsync(IRequestResponder<LaunchArguments> r)`:
  - read `r.Arguments.ConfigurationProperties["stopOnEntry"]` (a `JToken`, default `true`)
  - call `Load()`; on an exception, `r.SetError(new ProtocolException("Failed to load program: ..."))`
  - otherwise `r.SetResponse(new LaunchResponse())`, then `SendEvent(new InitializedEvent())`
- `override void HandleConfigurationDoneRequestAsync(IRequestResponder<ConfigurationDoneArguments> r)`:
  - `stopOnEntry`: `SetResponse`, then `SendEvent(StoppedEvent(Entry))`
  - otherwise `StartExecution(r, new ConfigurationDoneResponse(), ct => app.Continue(ct))`
- Constructor: register `DispatcherError` and custom request types before `Protocol.Run()`.

**Steps.**

1. Capabilities.
2. Async launch.
3. Async configurationDone.
4. Reorder the constructor.
5. Make `_isInit` volatile, because it is read from the worker.

**Risks.** The sync `Handle*Request` overloads send their events before the response. Any handler that must emit an event after its response must use the `Async` overload. The prototype confirmed the order `configurationDone` response → `stopped(entry)`.

**Verify.** Trace `initialize` through `stopped(entry)` and check the order: launch response, initialized, config, configurationDone response, stopped. The prototype harness checks exactly this.

### Pause

**Problem.** An infinite loop makes the session unresponsive. The only way out today is killing the terminal.

**Root cause.**

- `DebugAdapter.cs:263-269` calls `Application.Continue()` synchronously on the dispatcher. `Continue` (`Application.cs:142-151`) loops with no exit other than a stop reason.
- `StepOver` and `StepOut` (`Application.cs:77-110`) can hang the same way, for example a `jal` into an infinite loop.
- `ReverseContinue` (`Application.cs:158-163`) can run for a long time on a long history.

Note: DAP has no `supportsPause` capability. `pause` is a mandatory request, so nothing needs advertising. The todo item's `SupportsPause` does not exist.

**Chosen design.**

*Worker.* `ExecutionCoordinator` (new, `PlusPim/Application/ExecutionCoordinator.cs`):

```csharp
internal sealed class ExecutionCoordinator {
    bool IsRunning { get; }
    // beforeStart runs under the coordinator lock before the worker starts (used to send the DAP response first).
    // onStopped runs under the lock after the operation returns, so no new run can start before the stop is reported.
    bool TryStart(Func<CancellationToken, StopReason> operation, Action beforeStart,
                  Action<StopReason> onStopped, Action<Exception> onFaulted);
    bool RequestPause();            // cancels the token; true if a run was in progress
    void Shutdown(TimeSpan timeout); // cancel + wait (disconnect)
}
```

The worker uses `Task.Factory.StartNew(..., TaskCreationOptions.LongRunning)`, so it gets a dedicated thread.

*Cancellation.* `IApplication` execution methods take `CancellationToken ct = default`. The default value keeps the existing tests (`ApplicationTests.cs:61` etc.) compiling unchanged.

- `StopReason StepIn(ct)`, `StepOver(ct)`, `StepOut(ct)`, `Continue(ct)`
- `StopReason ReverseContinue(ct)`, changed from `bool`
- `bool StepBack()` stays as it is; it is one step.

New values in `StopReason`:

- `Pause`, mapped to `stopped` reason `pause`
- `HistoryStart`, mapped to `stopped` reason `entry` plus a console line "Reached the beginning of the execution history." It replaces the `bool` from `ReverseContinue` and `StepBack`.

*One forward loop.* `Application.RunForward(ct, Func<bool> isDone)` replaces the four loops:

```
while(true) lock(_gate) for 4096 times {
    if ct cancelled → Pause
    r = Step(); if !CanContinue(r) → r
    if isDone() → Step
}
```

`isDone` is the only difference between the operations:

| Operation | `isDone` |
| --- | --- |
| StepIn | `true` |
| StepOver | `depth <= start` |
| StepOut | `depth < start` |
| Continue | `false` |

The prototype kept the exact existing semantics: all existing `ApplicationTests` pass.

*Locking.* Every public `Application` member takes `_gate`. Lock order is always coordinator `_sync` → `Application._gate`. Nothing takes them in reverse: the worker drops `_gate` before it enters the coordinator to report a stop.

*Adapter handlers.* `continue`, `next`, `stepIn`, `stepOut`, `stepBack` and `reverseContinue` override the `Async` variants and call one helper:

```csharp
void StartExecution(IRequestResponder r, ResponseBody response, Func<CancellationToken, StopReason> op) {
    if(!app.IsLoaded) { r.SetError(new ProtocolException("Program is not loaded.")); return; }
    if(!exec.TryStart(op, () => r.SetResponse(response), SendExecuteEvent, OnExecutionFaulted))
        r.SetError(new ProtocolException("The debuggee is running. Pause it first."));
}
```

- `ContinueResponse` sets `AllThreadsContinued = true`.
- `pause`: `HandlePauseRequestAsync` sends the response first, then calls `exec.RequestPause()`. A pause while stopped is a no-op that still succeeds.
- `disconnect` calls `exec.Shutdown(2s)` before it ends the session.
- `OnExecutionFaulted` logs the exception, sends a stderr `output` and a `stopped(exception)` with text set to the exception type name, so the UI never stays "running" forever.

*Mutual exclusion.* An execution request that arrives while the program runs gets an error response. It is not queued. VS Code disables step buttons while running, so only races (double F10) hit this path, and rejecting is what DAP adapters usually do.

**Steps.**

1. Add `StopReason.Pause` and `StopReason.HistoryStart`.
2. Add `ExecutionCoordinator`.
3. Refactor `Application` to use `_gate` and `RunForward` with cancellation.
4. Change the `IApplication` signatures.
5. Rewrite the adapter execution handlers with `StartExecution`, plus `pause` and `disconnect`.
6. Extend `SendExecuteEvent` with the new reasons.
7. Add `IDebugger.CallStackDepth` (an `int`). `RunForward` calls `isDone` on every step, and `GetCallStack()` (`PlusPimDbg.cs:214-253`) allocates every frame with register copies each time. This existing cost becomes visible now that runs can be long.

**Risks.**

- **Pause cannot interrupt a blocking read.** `read_int`, `read_string` and `read_char` block inside `Console.In.Read()` (`RuntimeCall.cs:239`). Pause takes effect only after the input line arrives. This should be documented. Fixing it belongs to the runtime area: the read would have to become cancellable or yield to the worker loop.
- **Thread-safety assumptions.**
  - `Protocol.SendEvent` now runs on the worker: stopped events, instruction logs through the logger sink (`DebugAdapter.cs:43-52`) and `DebuggeeOutputWriter` in stdio mode. The prototype saw no interleaving problems, but it was not stress-tested.
  - `Logger._sinks` is only mutated at startup, which is fine.
- **Batch size 4096.** A reader such as `stackTrace` waits at most one batch (well under 1 ms at the observed rate of about 1M steps/s). A larger batch only helps throughput.

**Verify.** Infinite loop → continue → `threads` answered while running → `next` rejected → pause → `stopped(pause)` → `stackTrace` line is inside the loop → `$t0` is large → stepIn works. The prototype passed this (`$t0 = 277420` after 300 ms).

### Reverse Continue をブレークポイントで止める (#5)

**Problem.** `reverseContinue` always rewinds to the very beginning. It also always reports reason `step`.

**Root cause.** `Application.cs:158-163` calls `Back()` until it fails, with no stop check. `DebugAdapter.cs:318` hard-codes `ReasonValue.Step`.

**Chosen design.** After each `Back()`, stop when `IDebugger.IsAtBreakpoint` is true. It is implemented in `PlusPimDbg` as:

```csharp
public bool IsAtBreakpoint => _context.LastException is null && _breakpoints.Contains(_context.PC);
```

The `LastException is null` condition matters. Undoing an `EnteredHandler` entry (`PlusPimDbg.cs:139-143`) leaves PC on the faulting instruction with the exception still pending. That instruction has already executed, so it is not the "about to execute" state that a forward breakpoint stop shows (`PlusPimDbg.cs:116-118`). The stop must happen one `Back()` later.

**Exception filters (decided by the user): do not stop.** Reverse continue stops only at breakpoints. A caller who wants to reach an exception can place a breakpoint, or use `stepBack`. Rejected: symmetric stopping at filter-matching exceptions (simple to add later by checking `GetLastException()` against `IsBreakException` after each `Back()`).

- Reaching the start of history returns `HistoryStart`, reported as reason `entry`.
- Cancellation works as in `RunForward`.
- A single `stepBack` keeps reason `step`.

**Interfaces.**

- `IDebugger.IsAtBreakpoint` (get-only `bool`).
- `StopReason ReverseContinue(CancellationToken ct = default)`.

**Steps.**

1. Add the property to `PlusPimDbg`.
2. Write the reverse loop with batching and cancellation.
3. Report the stop with `SendExecuteEvent`.

**Risks.** Breakpoint addresses can shift while the assembler-frontend #7 bug (failed parse lines) is open. That is not specific to reverse execution.

**Verify.** Breakpoint inside a loop. Continue three times; `$t0` is 2. Then reverse continue three times: stops at the breakpoint with `$t0` = 1, at the breakpoint with `$t0` = 0, then at `entry`. The prototype passed this.

### 既定のカーネルハンドラを同梱する (#3): DROPPED

Dropped by the user's decision: PlusPim does not bundle a kernel handler. Without a `.ktext` file a `syscall` still ends in a double exception, as today. The README (documentation task, out of scope here) should keep telling users to add `kseg.asm`.

Because nothing is bundled, none of the following is needed: `DefaultKernelHandler`, the embedded resource, `useDefaultKernel` constructor parameters, `--no-default-kernel`, `IDebugger.IsInHiddenCode`, `StackFrameInfo.IsHiddenCode`, the `isDone` hook in `RunForward`, or the `StepBack` loop over hidden code. Pause, reverse continue and memory view do not depend on them.

Open design note kept for later: a runtime error raised inside a user `.ktext` handler still names the frame after the stale `CurrentLabel` (`PlusPimDbg.cs:222`, shows "main"). Consider a separate fix that adds a frame for the user's `syscall` line at EPC.

### メモリビュー機能 (memory view)

**Problem.** Memory cannot be inspected at all.

**Root cause.**

- The adapter has no `readMemory` handler, and `SupportsReadMemoryRequest` is not set (`DebugAdapter.cs:77-98`).
- No variable carries a `memoryReference` (`DebugAdapter.cs:243`).
- No layer exposes memory: `RuntimeContext._memory` (`RuntimeContext.cs:34`) is reachable only through `ReadMemoryByte` (`RuntimeContext.cs:147-149`).

**Chosen design.** Use the standard DAP `readMemory`. VS Code then offers "View Binary Data" on any variable that has a `memoryReference`, and opens it in the Hex Editor's memory view. Rejected: a custom memory webview in the extension. More work, and a duplicate of what VS Code provides.

- Memory references are formatted `"0x%08X"`.
- `readMemory`:
  - parse the reference (hex or decimal) and add `offset`, which may be negative
  - clamp `count` to 64 KiB
  - read the part that lies in `[0, 2^32)`
  - `address` = the first byte actually returned. Leading out-of-range bytes are expressed by moving `address` forward.
  - `unreadableBytes` = trailing bytes beyond `0xFFFFFFFF` only
  - unwritten memory reads as 0, the same as the runtime
- Every GPR variable gets `MemoryReference` = its value, so `$sp`, `$gp`, `$a0` and pointers can each be opened directly.
- A new non-register scope "Memory" (reference id 4 in the existing `(frameId << 16) | scope` encoding) holds two variables:
  - `.data`, with reference `0x10000000` and value "0x10000000 (N bytes)"
  - `stack ($sp)`, with the current `$sp` as its reference

  This scope covers the todo's ".data と $sp 周辺".
- `StackFrame.InstructionPointerReference = MemoryReference(PC)`. Cheap, and needed later for a DAP `disassemble`.

**Interfaces.**

- `IDebugger` / `IApplication`:
  - `byte[] ReadMemory(uint address, int count)`
  - `(uint Start, uint Size) DataSegmentRange { get; }`
- `ParsedPrograms.DataSegmentSize` (a `uint`, sum of the per-file sizes)
- DAP shapes:
  - request `readMemory {memoryReference, offset?, count}`
  - response `{address: "0x...", data: base64, unreadableBytes?}`

**Steps.**

1. Data segment size.
2. Debugger `ReadMemory`.
3. Application wrappers under `_gate`.
4. Capability and handler.
5. Memory references on variables.
6. "Memory" scope.

**Risks.**

- `$sp` is 0 until the runtime area gives it an initial value (todo "MIPS準拠: $sp, $gp に適切な初期値"). Until then, `stack ($sp)` points at address 0.
- `writeMemory` is out of scope.
- The Hex Editor extension must be installed. VS Code prompts for it.

**Verify.**

- `.word 0x11223344` at `0x10000000` returns `RDMiEQAAAAA=`, that is `44 33 22 11` followed by zeros (little endian).
- A read crossing `0xFFFFFFFF` returns 4 bytes with `unreadableBytes: 4`.

Both passed in the prototype.

### 擬似命令の展開先をインレイヒントで表示する (#11), protocol and C# side

**Problem.** The extension cannot see which machine instructions a pseudo-instruction became. The todo also says stepping is per machine instruction, so a `la` line takes two steps.

**Root cause.**

- Expansion happens in `InstructionRegistry.TryParseAll` (`InstructionRegistry.cs:224-249`), and its result is flattened into the segment (`TextSegmentBuilder.cs:25-26`). Which lines were pseudo-instructions is not recorded.
- `IInstruction` (`IInstruction.cs:8-31`) has no textual form.

**Chosen transport: a custom DAP request.**

Reasons:

1. An expansion depends on the whole loaded program: `la` resolves label addresses that depend on every file's segment layout. The debug session already has exactly that program.
2. The hint mainly explains debug-time behavior: why one line takes several steps, and which address each step executes.
3. No new process or transport is needed. A CLI call per keystroke would pay PlusPim's startup cost, which is already a todo ("起動に時間がかかる").

Rejected:

- CLI `--dump-expansions`: hints without a debug session, but one process per request, unsaved buffers need stdin, and multi-file layouts are wrong for a single file.
- LSP: the right long-term home for edit-time features, but a whole new server for one feature.

The C# expansion service is transport-agnostic, so a CLI or LSP front end can reuse it later.

**Protocol contract (for the vscode-extension agent).**

- Command `pluspimPseudoExpansions`, sent via `session.customRequest`. It is valid after the `initialized` event; before that the response fails with "Program is not loaded."
- Arguments: `{ "source": { "path": "<absolute path>" } }`
- Response body:

```json
{ "lines": [
  { "line": 6, "mnemonic": "la",
    "instructions": [ { "address": "0x00400000", "text": "lui $t0, 0x1000" },
                      { "address": "0x00400004", "text": "ori $t0, $t0, 0x0000" } ] },
  { "line": 8, "mnemonic": "move", "instructions": [ { "address": "0x00400008", "text": "addu $t2, $t1, $zero" } ] }
] }
```

- `line` is 1-based.
- Every successfully parsed pseudo-instruction line is listed, including single-instruction ones (`move`, `nop`, small `li`).
- Lines that failed to parse are absent.
- An unknown path returns `lines: []`.
- The program does not change during a session, so the client may cache the result per path for the session.

**C# interfaces.**

- `IInstruction`: `string? Disassembly => null;`, a default interface member, so the change is non-breaking. Implemented in `ITypeInstruction` (`lui`/`ori` forms), `RType3RegInstruction` and `RTypeShiftImmInstruction`. Today's pseudo-instructions only expand to these (`InstructionFactory.cs:10-25`).
- `InstructionRegistry.GetPseudoMnemonic(string line)` returns a `string?`.
- `TextSegmentBuilder`: `AddLine` records `PseudoExpansion(int SourceLine, string Mnemonic, int FirstIndex, int Count)` when the mnemonic is a pseudo-instruction. Exposed as `TextSegment.PseudoExpansions` (an `IReadOnlyList<PseudoExpansion>`).
- `ParsedPrograms.GetPseudoExpansions(FileInfo)` maps segment-local indices to global addresses using the cumulative lengths (same scheme as `GetAddressForLine`, `ParsedPrograms.cs:201-224`).
- `IDebugger` / `IApplication`:
  - `PseudoExpansionInfo[] GetPseudoExpansions(FileInfo)`
  - `record PseudoExpansionInfo(int Line, string Mnemonic, PseudoExpandedInstruction[] Instructions)`
  - `record PseudoExpandedInstruction(uint Address, string Text)`
- Adapter:
  - `PseudoExpansionsRequest : DebugRequestWithResponse<PseudoExpansionsArguments, PseudoExpansionsResponse>`, with Newtonsoft `[JsonProperty]` names as in the contract
  - registered with `Protocol.RegisterRequestType<...>(handler)` before `Run()`

**Steps.**

1. `Disassembly` on the three instruction classes.
2. `GetPseudoMnemonic`.
3. Recording in `TextSegmentBuilder` and `TextSegment`.
4. `ParsedPrograms` mapping.
5. Debugger and Application pass-through.
6. Request types and handler.

**Risks.**

- Results are only as correct as the assembler: `li $t1, 0x12345` is currently dropped (the `li` hex bug), and #7 can shift addresses.
- A full disassembler, needed for the DAP `disassemble` request, is a separate and larger task. Instruction classes other than the three return `null`, shown as `?`.

**Verify.** A file with `la`, `move`, `nop` returns three lines with correct addresses and text. The prototype passed this. `li` with hex is missing, as expected.

## Ordering and dependencies

1. **Init sequence.** Small. It introduces the `Async` handler pattern and `configurationDone`, which Pause reuses for `stopOnEntry: false`.
2. **Pause / threading.** The foundation. It changes the `StopReason`, `IApplication` and `Application` loops and every execution handler. Everything after it builds on `RunForward`, `_gate` and `StartExecution`.
3. **Reverse Continue at breakpoints.** Needs the batched, cancellable reverse loop from step 2.
4. **Memory view.** Independent apart from `_gate` (step 2). Can run in parallel with 3.
5. **Pseudo-expansion request.** Independent of 2-5 on the C# side. It depends on cross-area coordination for the `TextSegmentBuilder` and instruction class edits (see below). The extension work starts once the contract is merged.

## Files expected to change

- `PlusPim/Application/Application.cs`: lock, `RunForward`, reverse loop, new pass-throughs
- `PlusPim/Application/IApplication.cs`: `CancellationToken` parameters, `ReverseContinue` returns `StopReason`, `IsLoaded`, `ReadMemory`, `DataSegmentRange`, `GetPseudoExpansions`
- `PlusPim/Application/IDebugger.cs`: `IsAtBreakpoint`, `CallStackDepth`, `ReadMemory`, `DataSegmentRange`, `GetPseudoExpansions`, expansion records
- `PlusPim/Application/StopReason.cs`: `Pause`, `HistoryStart`
- `PlusPim/Application/ExecutionCoordinator.cs` (new)
- `PlusPim/EditorController/DebugAdapter/DebugAdapter.cs`: most handlers
- `PlusPim/EditorController/DebugAdapter/PseudoExpansionsRequest.cs` (new)
- `PlusPim/Debuggers/PlusPimDbg/PlusPimDbg.cs`: new members
- `PlusPim/Program.cs`: none for this plan beyond coordination (see vscode-extension handshake)
- `PlusPim/Debuggers/PlusPimDbg/Program/ParsedPrograms.cs`: `DataSegmentSize`, `GetPseudoExpansions`
- `PlusPim/Debuggers/PlusPimDbg/Program/TextSegment.cs`: `PseudoExpansion`, `PseudoExpansions`
- `PlusPim/Debuggers/PlusPimDbg/Program/TextSegmentBuilder.cs`: about 3 lines in `AddLine`
- `PlusPim/Debuggers/PlusPimDbg/Instruction/IInstruction.cs`: `Disassembly`
- `PlusPim/Debuggers/PlusPimDbg/Instruction/Parser/InstructionRegistry.cs`: `GetPseudoMnemonic`
- `PlusPim/Debuggers/PlusPimDbg/Instruction/instructions/ITypeInstruction.cs`, `RType3RegInstruction.cs`, `RTypeShiftImmInstruction.cs`: `Disassembly`

## Cross-area dependencies

**vscode-extension.**

- `package.json` launch schema: add `stopOnEntry` (boolean, default `true`).
- InlayHintsProvider for `mips`:
  - after the session's `initialized` event (for example via the existing `DebugAdapterTracker` in `extension.ts:26-35`), call `session.customRequest("pluspimPseudoExpansions", { source: { path } })` for each visible MIPS document
  - render the `text` values joined by `; ` at the end of `line`
  - clear the hints when the session ends
  - contract as defined above
- Decide whether to recommend the Hex Editor extension (`ms-vscode.hexeditor`) for the memory view, for example in the README.

**assembler-frontend.**

- The `TextSegmentBuilder.AddLine` hook overlaps with the #7 fix at `TextSegmentBuilder.cs:25-30`. Either they own the hook or it lands after their fix.
- The `li` hex fix (`LiInstructionParser.cs:51`) makes `li` hints appear.
- `GetPseudoMnemonic` lives in `InstructionRegistry`, which they also touch for register parsing.

**code-quality.**

- Their planned dedup of `Stack<uint>`/`WriteRd`/`Undo` touches exactly `RType3RegInstruction`, `RTypeShiftImmInstruction` and `ITypeInstruction`, where `Disassembly` is added. Coordinate the order.
- Removing `Application._isDebug`, the runtime-mode path and the `Program.cs:148-150` dead code conflicts textually with the `Application` constructor and `Program.cs` edits here.
- `PlusPimDbg.GetRegisters` cleanup touches `PlusPimDbg.cs`.

**runtime.**

- Initial `$sp`/`$gp` values, needed for a meaningful `stack ($sp)` view.
- A blocking read cannot be interrupted by Pause (`RuntimeCall.cs:239`). If wanted, the read must become cancellable or yield to the worker.

## Prototype findings

**What was tried.** Everything above, in the worktree, throwaway and uncommitted. Plus an xUnit harness that drives the real `DebugAdapter` over `System.IO.Pipelines` pipes with raw DAP JSON.

- `dotnet build`: 0 errors.
- `dotnet test`: all 25839 existing tests pass unchanged, plus the 5 harness scenarios.

**What happened, and what changed in the plan.**

- **Pause works as designed.**
  - `threads` was answered while running.
  - `next` while running got the error response.
  - pause → `stopped(pause)` with PC inside the loop and `$t0 = 277420`.
  - A long reverse continue was paused the same way.
  - `setBreakpoints` sent while running was applied by the running loop.
  - `disconnect` while running returned in about 1 ms.
- **Event order is not automatic.** A sync `Handle*Request` that calls `SendEvent` emits the event before its own response. My first `configurationDone` sent `stopped(entry)` too early. **Plan change:** every handler that emits events uses the `Async` overload and calls `SetResponse` before sending the event. The coordinator's `beforeStart` callback guarantees response-before-stopped for execution requests.
- **DAP has no `supportsPause` capability.** The todo wording was corrected in this plan; nothing needs advertising.
- **`readMemory` semantics.** My first version reported leading out-of-range bytes in `unreadableBytes`. The DAP spec defines that field as bytes after the last byte read; a leading gap is expressed by `address`. **Plan change:** the rule is now explicit.
- **Kernel handler findings are moot.** The prototype's bundled-handler experiments (hidden-code stepping, deemphasized frame, opt-in constructor flag) were dropped together with the task.
- **Assembler limits observed while testing:**
  - `label: .word` on one line is ignored, which silently gave zeroed data until the test used a separate label line
  - `li` with a hex immediate is dropped

  Both are existing assembler-frontend todos. They affect the memory view and hint output but need no change here.

## Open questions for the user

None remaining. All three earlier questions were answered (see Resolved decisions).

## Reconciliation with other plans (added after review)

- Inlay hint contract: this plan's `pluspimPseudoExpansions` request and `lines` response are canonical. vscode-extension.md was aligned to them.
- assembler-frontend.md introduces `AssemblyException` (duplicate globals, see its `.globl` section). `Application.Load` / `HandleLaunchRequest` must catch it and fail the `launch` response with the collected error messages. Add this to the init-sequence step.
- vscode-extension.md reports that the `disconnect` response is never delivered: `HandleDisconnectRequest` completes `_sessionEnded` (`DebugAdapter.cs:118`) before the response is written, and `Main` then disposes the socket (`Program.cs:139-144`). Fix it together with the `disconnect` / `exec.Shutdown` change above: respond first, then end the session.
- vscode-extension.md adds a stdout handshake line (`PLUSPIM_DAP_LISTENING`) and a single-accept server in `Program.cs`. It also requires reading `program` from the `launch` arguments for other editors. Coordinate the `Program.cs` edits (handshake, dead-code removal).
- runtime.md edits `HandleExceptionInfoRequest` and `SendExecuteEvent` through a new `RuntimeErrorDisplay` class. Keep calling it when `SendExecuteEvent` is restructured here.
