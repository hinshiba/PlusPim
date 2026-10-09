# Runtime plan

Area: runtime (todo.md, "バグ修正等 > 実行系"). Base: branch `agent/temp` (148383b). All file:line references are to that branch, which is what the main tree has checked out. `main` does not contain `Processor`, `RuntimeError` or `PendingInput` yet.

## Resolved decisions (user answers, applied)

- print_char writes raw UTF-8 bytes through `DebuggeeOutput` (UTF-8 chosen over Latin-1). `doc/runtime.md:55-60` must be updated to match (documentation task, tracked separately).
- read_char becomes byte-based as well, so `read_char -> print_char` round-trips non-ASCII text. It is now part of this plan (new subsection "read_char returns one UTF-8 byte"), not a follow-up.

## Summary

| Todo item | Decision |
| --- | --- |
| `$sp`, `$gp` initial values | The loader (`PlusPimDbg` ctor) sets `$sp = 0x7FFFEFFC` and `$gp = 0x10008000`, the SPIM/MARS values. The values are not part of the history, so StepBack keeps them. |
| print_char does not write raw bytes | Treat debuggee stdout as one UTF-8 byte stream (`DebuggeeOutput`). print_int, print_string and print_char all write bytes into it. A stateful decoder holds 0 to 3 pending bytes, and its state can be captured and restored for StepBack. Latin-1 is rejected (see below). This changes the spec in `doc/runtime.md:55-60` (confirmed by the user). |
| PendingInput hidden from snapshots | Drop the `ConditionalWeakTable`. `RuntimeContext` owns `PendingInput Input` (and `DebuggeeOutput Output`) explicitly. Add `Buffered` and `Restore`, plus `PlusPimDbg.GetPendingInput()` for snapshots. |
| read_char is one UTF-16 unit | Becomes one UTF-8 byte (confirmed by the user). `PendingInput` becomes byte-oriented. See the new subsection. |
| Runtime errors look like MIPS exceptions | Keep DAP stop reason `exception`, because VS Code only shows the exception widget for that reason. Distinguish runtime errors through `exceptionId`, the description and the state label ("PlusPim runtime error (DivisionByZero)" vs "MIPS exception (Ov)"). Add the source location to the stderr line. |

A prototype of all four items builds. The existing 25,839 tests pass unchanged, and 6 throwaway probe tests confirmed the behaviour. See "Prototype findings".

## Task breakdown

### `$sp`, `$gp` initial values

**Problem.** Every GPR starts at 0. A typical prologue `addi $sp, $sp, -4; sw $ra, 0($sp)` writes to `0xFFFFFFFC`. `$gp`-relative accesses hit address 0 and its neighbours. SPIM and MARS both start with `$sp = 0x7FFFEFFC` and `$gp = 0x10008000`.

**Root cause.**
- `PlusPim/Debuggers/PlusPimDbg/Runtime/RegisterFile.cs:7`: `new uint[32]`, all zero.
- `PlusPim/Debuggers/PlusPimDbg/PlusPimDbg.cs:48-50`: the ctor creates the context and loads the memory image, but never initialises any register.

**Design.**
- Define the ABI start state in a new `Runtime/InitialRegisters.cs` and apply it in the `PlusPimDbg` ctor, right after `LoadMemoryImage`.
- Do not apply it in the `RuntimeContext` ctor. Instruction-level tests build bare contexts through `TestHelpers.CreateRuntimeContext` (`PlusPimTests/TestHelpers.cs:45-53`), and those should stay all-zero. The start state is also the loader's job, not the CPU's. SPIM does the same thing in `initialize_registers` at load time.
- Values:
  - `$sp = 0x80000000 - 4 - 4096 = 0x7FFFEFFC`, the SPIM/MARS value. It is word-aligned, and there is no argc/argv area.
  - `$gp = 0x10008000`, which is `DataSegment.DataSegmentBase` (`Program/DataSegment.cs:13`, `0x10000000`) + `0x8000`.

**Interfaces.**
```csharp
internal static class InitialRegisters {
    public const uint StackPointer  = 0x7FFFEFFC;
    public const uint GlobalPointer = 0x10008000;
    public static void Apply(RegisterFile registers);  // sets Sp, Gp
}
```

**Steps.**
1. Add `InitialRegisters.cs`.
2. Call `InitialRegisters.Apply(this._context.Registers)` at the end of the `PlusPimDbg` ctor.
3. No change is needed in the DAP layer, because registers are already shown.

**Risks.**
- A program that assumes `$sp == 0` changes behaviour. That assumption is unlikely and was wrong anyway.
- There is no segment protection: `MemoryInstruction.cs:50-53` only checks alignment. So the stack and `.data` can only collide after about 1.8 GB of use.
- Verification: a debugger-level test checks the initial `$sp`/`$gp`, and checks that they survive `ReverseContinue`.

### print_char does not write raw bytes (UTF-8 vs Latin-1)

**Problem.**
- `Console.Write((char)(a0 & 0xFF))` writes U+00XX. Printing a UTF-8 string one byte at a time with print_char therefore produces mojibake. For example, `E3 81 82` becomes "ã\u0081\u0082" instead of "あ".
- print_string decodes every call on its own (`Encoding.UTF8.GetString`). A multibyte character split across calls becomes U+FFFD.

**Root cause.**
- `PlusPim/Debuggers/PlusPimDbg/Instruction/RuntimeCall.cs:132-137`: print_char.
- `RuntimeCall.cs:57-67`: print_string decodes per call.
- `RuntimeCall.cs:54`: print_int writes text directly.
- There is no shared output state. The current spec, `doc/runtime.md:55-60`, explicitly documents the Latin-1 behaviour.

**Chosen design: a single UTF-8 byte stream with a stateful decoder that can be captured and restored.**

Options considered:
- **Latin-1, as MARS does (document only).** Zero code change, and the doc already says it. Rejected:
  - Every other text path in PlusPim treats memory as UTF-8: print_string at `RuntimeCall.cs:66`, and read_string at `RuntimeCall.cs:102` and `doc/runtime.md:43-49`.
  - "Print a string byte by byte" is a standard exercise, and the users write Japanese text.
  - Latin-1 makes print_char the only syscall whose byte-to-text mapping differs.
- **Write raw bytes to `Console.OpenStandardOutput()`, as SPIM `putc` does.** Rejected:
  - In `--stdio` mode stdout is the DAP stream (`Program.cs:106-117`), so raw bytes would corrupt the protocol.
  - It would also bypass `DebuggeeOutputWriter` and the test `ConsoleRedirect`, and could reorder output against `Console.Out`'s buffer.
- **`Encoding.UTF8.GetDecoder()`.** Rejected: its internal state cannot be snapshotted or restored. See "Prototype findings" for why StepBack needs that.
- **Chosen: a custom decoder that keeps at most 3 pending bytes and decodes with `Rune.DecodeFromUtf8`.**
  - `Done`: write the rune.
  - `NeedMoreData`: keep the bytes.
  - `InvalidData`: write U+FFFD and drop the maximal invalid subpart.
  - print_int and print_string go through the same stream, so a sequence split across print_char and print_string decodes correctly. An ASCII byte after a dangling lead byte yields U+FFFD and then the ASCII character.
  - Completed text is written with `Console.Write(string)`. That keeps surrogate pairs (4-byte UTF-8) together in one `OutputEvent` on stdio transport.

**Interfaces.**
```csharp
// Runtime/DebuggeeOutput.cs (new)
internal sealed class DebuggeeOutput {
    public ReadOnlySpan<byte> Pending { get; }      // 0..3 bytes of an incomplete sequence
    public void Write(ReadOnlySpan<byte> bytes);    // decode what is complete, write it to Console.Out
    public void Flush();                            // pending bytes, if any -> one U+FFFD
    public byte[] CapturePending();                 // undo
    public void RestorePending(byte[] pending);     // undo (text already written stays written)
}
// RuntimeContext
public DebuggeeOutput Output { get; } = new();
```

**Steps.**
1. Add `DebuggeeOutput`, and add `RuntimeContext.Output` next to `RuntimeError` (`RuntimeContext.cs:51`).
2. In `RuntimeCall` add `Stack<byte[]> _prevOutputPending`. Then change each syscall:
   - print_int: `Output.Write(ASCII bytes of ((int)a0).ToString(InvariantCulture))`.
   - print_string: `Output.Write(bytes)` instead of `GetString`.
   - print_char: `Output.Write([(byte)(a0 & 0xFF)])`.
   - Before each of these three, push `Output.CapturePending()`.
3. exit (`RuntimeCall.cs:151-154`): push the pending bytes, then call `Output.Flush()`, so a dangling sequence shows up as U+FFFD instead of disappearing.
4. `Undo`: print_int, print_string, print_char and exit call `Output.RestorePending(pop)`. Today `RuntimeCall.cs:161-165` does nothing for prints.
5. Follow-ups for the docs and test owners:
   - Rewrite `doc/runtime.md:55-60` and the StepBack table at `doc/runtime.md:79` ("pending output bytes are restored").
   - Update `doc/tests/instructions/runtime_call_model.md:34-36`.
   - Add tests for bytes >= 0x80. The existing `Execute_PrintChar_WritesLowByte` only uses 0x41.

**Risks.**
- **Behaviour change:** print_char 0xE9 alone used to print "é" and now prints U+FFFD (when followed by a non-continuation byte or exit). This is the price of choosing UTF-8, and it is listed under open questions.
- **Undecodable characters on Windows:** in TCP mode `Console.OutputEncoding` is the console code page (`Program.cs` never sets it), so characters outside CP932 or CP1252 become `?`. This problem already exists for print_string. See cross-area dependencies.
- **Session ends while bytes are pending:** if the session ends without `exit` (disconnect, runtime error), the pending bytes are never shown. This is acceptable.
- Verification: write bytes one at a time ("aあ😀"), mix invalid bytes, split a sequence between print_char and print_string, and StepBack mid-sequence then re-step.

### Input buffer (PendingInput) not visible in MachineState or debugger snapshots

**Problem.**
- The pushed-back input (read_string leftovers and inputs returned by Undo) lives in a hidden side table.
- Neither `MachineState` nor `DebuggerSnapshot` sees it. A test cannot assert "StepBack restored the input" without draining it, which is what the workaround at `RuntimeCallInstructionTests.cs:585-592` does.

**Root cause.**
- `RuntimeCall.cs:225-267`: `PendingInput` is attached to the context with `ConditionalWeakTable<RuntimeContext, PendingInput>` (`:226`, `:230-232`).
- `RuntimeContext.cs` has no member for it.
- `PlusPimTests/Instructions/MachineState.cs:14-30` and `:52-75`, and `PlusPimTests/TestHelpers.cs:16-30` and `:77-96`, have no field for it.

**Design.**
- Move `PendingInput` to `Runtime/PendingInput.cs` and make it an explicit, get-only `RuntimeContext.Input` property.
- Expose the buffered text read-only, plus a `Restore` for test write-back (`MachineState.ApplyTo`, `MachineState.cs:213-238`).
- Document in a comment that it is not synchronised, because stepping is single-threaded.
- `Buffered` means "read from the console but not yet consumed". Text still sitting in `Console.In` is deliberately not included, because peeking it would block on a terminal.

**Interfaces.**
```csharp
// Runtime/PendingInput.cs (moved out of RuntimeCall.cs)
internal sealed class PendingInput {          // comment: not synchronised; stepping is single-threaded
    public string Buffered { get; }            // read but unconsumed
    public int ReadChar();                     // unchanged
    public string? ReadLine();                 // unchanged
    public void PushFront(string text);        // unchanged
    public void Restore(string buffered);      // test write-back only
}
// RuntimeContext
public PendingInput Input { get; } = new();
// PlusPimDbg (internal; tests have InternalsVisibleTo access, since they already use PlusPimDbg)
internal string GetPendingInput() => this._context.Input.Buffered;
```

**Steps.**
1. Create `PendingInput.cs` without the static table and `For`. Add `RuntimeContext.Input` (and `Output` from the previous task) with the thread-safety remark.
2. Replace the six `PendingInput.For(context)` calls in `RuntimeCall.cs` (`:76`, `:92`, `:146`, `:170`, `:178`, `:190`) with `context.Input`.
3. Add `PlusPimDbg.GetPendingInput()`.
4. Test-side follow-ups, which must be done with this change because the `For` removal breaks the build:
   - Change `RuntimeCallInstructionTests.cs:586` to `context.Input`.
   - Add `string PendingInput` and `byte[] PendingOutput` to `MachineState` (Capture, AssertEqual, ApplyTo via `Input.Restore` and `Output.RestorePending`).
   - Add `string PendingInput` to `DebuggerSnapshot`.

**Risks.**
- Adding the fields to `MachineState` makes every existing `AssertEqual` also compare input and output state. That is intended, since it catches leaks. It should not cause failures, because non-syscall instructions never touch either.
- Verification: after read_string with `$a1 = 3` on "abcdef\n", `Input.Buffered == "cdef\n"`, and Undo restores "abcdef\n". The prototype probe passed this.

### read_char returns one UTF-8 byte

**Problem.** `PendingInput.ReadChar` (`RuntimeCall.cs:139-149`) returns one UTF-16 code unit. Echoing "あ" (U+3042) with `read_char -> print_char` prints "B" (the low byte). With byte-based print_char, the matching input side must return the UTF-8 bytes `E3 81 82` one per call.

**Design.**
- `PendingInput` buffers text as UTF-8 bytes (`byte[]`/queue) instead of `string`. Console input arrives as lines of text; encode each line (including the newline) to UTF-8 when it is read.
- `ReadChar` removes and returns the first byte (0 to 255). End of input keeps today's behavior (the existing EOF value).
- `ReadLine`/`read_string`: decode up to the requested length in bytes. A multibyte character must not be split silently. Rule: `read_string` with `$a1 = n` copies at most `n - 1` bytes, as in SPIM, and pushes the remaining bytes back. A partial character left in the buffer is delivered to the next `read_char` or `read_string` call, so no bytes are lost.
- `PushFront` (used by Undo) takes bytes. `Buffered` becomes `byte[]` (hex in debugger display if shown); `Restore(byte[])` for test write-back.
- `MachineState.PendingInput` is therefore `byte[]`, not `string`.
- Undo of read_char pushes the single byte back to the front.

**Interfaces.**
```csharp
internal sealed class PendingInput {
    public ReadOnlySpan<byte> Buffered { get; }
    public int ReadChar();                    // 0..255, or the EOF value
    public string? ReadLine();                // line read from Console.In, encoded to UTF-8 into the buffer
    public byte[] TakeBytes(int max);         // used by read_string
    public void PushFront(ReadOnlySpan<byte> bytes);
    public void Restore(ReadOnlySpan<byte> buffered);
}
```

**Steps.** Do this together with the PendingInput task, since both rewrite `PendingInput` and the same `RuntimeCall.cs` lines.

**Risks.**
- The doc `doc/tests/instructions/runtime_call_model.md` and tests that assert string-based pending input change. Existing read_char tests that use ASCII keep working.
- A `read_string` limit that falls inside a multibyte character leaves the continuation bytes in the buffer; this is the intended behavior and must be stated in the runtime docs.
- The EOF value for read_char must stay distinguishable from byte 255. Keep the current EOF constant and check that it is outside 0..255; if it is not, choose one that is and update the doc.

### Make runtime errors visibly distinct from MIPS exceptions

**Problem.**
- Both stop with DAP reason `exception`.
- The exception widget shows "Exception has occurred: DivisionByZero" vs "Exception has occurred: Ov". Nothing tells the user that the first one has no handler and cannot be continued.
- The call-stack state label for a runtime error is the raw message, and for an exception it is "MIPS exception: Ov".

**Root cause.**
- `PlusPim/EditorController/DebugAdapter/DebugAdapter.cs:155-161`: `ExceptionInfoResponse(runtimeError.Id, ...)`, with the bare enum name as `exceptionId`.
- `DebugAdapter.cs:354-367`: the StoppedEvent has `Description = errorInfo.Description` and `Text = errorInfo.Id`. The stderr line has no "runtime error" marker in a fixed format and no source location.
- `DebugAdapter.cs:344-352`: MIPS exceptions use the bare code as Text.
- `RuntimeErrorInfo.cs:8-29` carries no source location.

**Design.**
- Keep `StoppedEvent.ReasonValue.Exception`. In VS Code (`debugModel.ts`, `Thread.exceptionInfo`), exceptionInfo is requested only when `stoppedDetails.reason === 'exception'`. A custom reason such as "runtime error" would lose the exception widget entirely.
- VS Code uses these fields:
  - `StoppedEvent.description` is the call-stack state label (`Thread.stateLabel`).
  - `exceptionId` is the widget title ("Exception has occurred: {id}").
  - The exceptionInfo `description` is the widget body.
  - `text` is unused when `supportsExceptionInfoRequest` is true.
- Put every user-facing string in one presentation helper in the DAP layer.

| Surface | Runtime error | MIPS exception |
| --- | --- | --- |
| State label (`StoppedEvent.Description`) | `Paused on PlusPim runtime error` | `Paused on MIPS exception` / `Paused on MIPS double exception` |
| Widget title (`exceptionId`) | `PlusPim runtime error (DivisionByZero)` | `MIPS exception (Ov)` / `MIPS double exception (Ov)` |
| Widget body | message + "This is a PlusPim runtime error, not a MIPS exception: no exception handler runs and execution cannot continue. Use Step Back to return to the state before this instruction." | unchanged (`PlusPimDbg.cs:159-175`) |
| Debug console (stderr) | `[PlusPim runtime error] DivisionByZero at 0x00400010 (main.asm:12): <message>. Execution cannot continue; use Step Back.` with `OutputEvent.Source`/`Line` set so the line links to the source | none (unchanged) |

**Interfaces.**
```csharp
// Application/RuntimeErrorInfo.cs: add
public FileInfo? SourceFile { get; init; }   // from ParsedPrograms.GetSourceInfo(PC)
public int Line { get; init; }               // 1-based, <= 0 if unknown
// EditorController/DebugAdapter/RuntimeErrorDisplay.cs (new, internal static)
const string StateLabel;
string ExceptionId(RuntimeErrorInfo);  string WidgetDescription(RuntimeErrorInfo);  string ConsoleLine(RuntimeErrorInfo);
string MipsExceptionId(ExceptionInfo); string MipsExceptionStateLabel(ExceptionInfo);
```

**Steps.**
1. In `PlusPimDbg.GetRuntimeError` (`PlusPimDbg.cs:177-187`), fill `SourceFile` and `Line` from `this._programs.GetSourceInfo(this._context.PC)`. The PC is the faulting instruction, because runtime errors do not advance it.
2. Add `RuntimeErrorDisplay`.
3. Use it in `HandleExceptionInfoRequest` (`:155-173`) and in `SendExecuteEvent` (`:344-367`).

**Risks.**
- **Changed MIPS exceptionId:** anything matching on the old bare code breaks. Nothing in `vscode_ext/pluspim` references exceptionId (checked with grep).
- **Repeated stderr line:** pressing Continue while stopped on a runtime error repeats the stderr line, because `Step` returns `RuntimeError` again (`PlusPimDbg.cs:66-69`). Acceptable, and arguably useful.
- **Non-debug mode:** this mode is not implemented (`Program.cs:146-151`), so it has no display. Out of scope.
- Verification: start a session on `div $t0, $zero` and check the three surfaces by hand. A DAP-level test is optional.

## Ordering and dependencies

1. **PendingInput explicit.** This establishes the "explicit context-owned I/O state" pattern and the `RuntimeContext` and `RuntimeCall` edits. It must ship together with the one-line test fix at `RuntimeCallInstructionTests.cs:586`.
2. **print_char / DebuggeeOutput, and read_char as UTF-8 bytes.** Same files and same pattern, so do it right after 1 (or merge 1 and the read_char work, since both rewrite `PendingInput`) to avoid two rounds of conflicts in `RuntimeCall.cs`.
3. **`$sp`/`$gp`.** Independent. Only touches `PlusPimDbg.cs` (ctor) and a new file.
4. **Runtime error display.** Independent of 1 to 3. It touches `PlusPimDbg.GetRuntimeError` and `DebugAdapter.cs`.

All four should land after, or on top of, the `inst` work under "検証中" (it introduced `Processor`, `RuntimeError` and `PendingInput`). Branch from `agent/temp` or its successor, not from `main`.

## Files expected to change

New:
- `PlusPim/Debuggers/PlusPimDbg/Runtime/PendingInput.cs`
- `PlusPim/Debuggers/PlusPimDbg/Runtime/DebuggeeOutput.cs`
- `PlusPim/Debuggers/PlusPimDbg/Runtime/InitialRegisters.cs`
- `PlusPim/EditorController/DebugAdapter/RuntimeErrorDisplay.cs`

Modified:
- `PlusPim/Debuggers/PlusPimDbg/Runtime/RuntimeContext.cs`: two properties near `:51`.
- `PlusPim/Debuggers/PlusPimDbg/Instruction/RuntimeCall.cs`: almost the whole `Execute`/`Undo`; the `PendingInput` class is removed.
- `PlusPim/Debuggers/PlusPimDbg/PlusPimDbg.cs`: the ctor `:35-51`, `GetRuntimeError` `:177-187`, and the new `GetPendingInput`.
- `PlusPim/Application/RuntimeErrorInfo.cs`: two properties.
- `PlusPim/EditorController/DebugAdapter/DebugAdapter.cs`: `HandleExceptionInfoRequest` `:153-174` and `SendExecuteEvent` `:343-367`.

Follow-ups owned by tests/docs, listed for conflict checking:
- `PlusPimTests/Instructions/RuntimeCallInstructionTests.cs:586` (required for the build)
- `PlusPimTests/Instructions/MachineState.cs`
- `PlusPimTests/TestHelpers.cs`
- `doc/runtime.md`
- `doc/tests/instructions/runtime_call_model.md`

## Cross-area dependencies

**debugger-dap**
- Both plans edit `DebugAdapter.cs`: `HandleExceptionInfoRequest` and `SendExecuteEvent`. That area's "init does not follow DAP" fix and the Pause work, which adds a `pause` reason to `SendExecuteEvent`, will touch the same methods. Whichever lands second must rebase. If the DAP plan restructures `SendExecuteEvent`, it should keep calling `RuntimeErrorDisplay`.
- Pause runs `Continue` on a worker thread (todo "新機能 > 実行系"). After that, `PendingInput`, `DebuggeeOutput` and every other `RuntimeContext` member are touched from two threads. The Pause plan must guarantee that DAP reads (stack trace, variables, a future pending-input view) only happen while execution is stopped, or under the same lock that serialises Step and Back.
- A syscall that is blocked in `Console.In.Read()` (`PendingInput.ReadChar`) cannot be interrupted by Pause. That needs a design decision in that plan.
- Optional: show `GetPendingInput()` (and pending output bytes) in a DAP "Runtime" scope. This would need an `IDebugger`/`IApplication` method. It is not planned here.

**vscode-extension / Program.cs**
- In TCP mode the debuggee writes to the integrated terminal with `Console.OutputEncoding`, which is the OEM code page on Windows. Characters outside that code page print as `?` for both print_string and print_char.
- Setting `Console.OutputEncoding = UTF8` (and possibly `InputEncoding`) in `Program.cs` debug mode is a small change, but it affects how the terminal launched by `extension.ts:101` renders. It belongs to whoever owns `Program.cs` and terminal launching.

**code-quality**
- Its dead-code list mentions `PlusPimDbg.GetRegisters` (test-only). This plan adds one more test-facing internal method, `GetPendingInput`, so that area should treat both the same way.

## Prototype findings

**Prototype setup**
- The worktree assigned to this agent was checked out at `main` (73fc28f), which predates `Processor`, `RuntimeError` and `PendingInput`.
- Fast-forwarding the worktree branch to `agent/temp`, and copying sources over it, were both refused by the permission policy.
- So the prototype was built in a scratchpad copy of the main tree's `agent/temp` sources: `PlusPim/`, `PlusPimTests/`, `PlusPim.slnx`, `.editorconfig`. The worktree is untouched.
- `dotnet build`: success, with only the pre-existing CS0162 warning at `Program.cs:149`.
- `dotnet test`: baseline 25,839 passed. With the prototype, 25,839 still pass, plus 6 throwaway probes.

**Findings that changed or confirmed the plan**
1. **StepBack forces a restorable decoder.** In a first sketch the decoder state was not part of undo. But stepping back over the second byte of "あ" and re-executing it would feed that byte twice and print U+FFFD. The probe `UndoMidSequenceThenRedo` passes only because print_* now record and restore the pending bytes. Plan changes:
   - print_int, print_string and print_char now push undo state. Previously they pushed nothing.
   - The StepBack row in `doc/runtime.md:79` must change.
   - `System.Text.Decoder` is ruled out.
2. **print_string must share the stream.** A sequence split as print_char(E3) followed by print_string("\x81\x82") only decodes correctly if print_string goes through the same decoder (probe `PendingAcrossPrintString`). So all three prints use one stream, not only print_char.
3. **VS Code UI constraints, checked in the VS Code source.**
   - `Thread.exceptionInfo` is fetched only when `reason === 'exception'`.
   - The widget title is built from `exceptionId` alone.
   - `Thread.stateLabel` uses `StoppedEvent.description`, and `text` is unused when exceptionInfo is supported.
   - A custom stop reason was therefore rejected. Differentiation is done through `exceptionId`, the description and the state label.
4. **Nothing guards these behaviour changes.** The full existing suite passed unchanged after:
   - changing print_char to UTF-8,
   - changing `$sp`/`$gp`,
   - moving `PendingInput`.

   No current test covers print_char with bytes >= 0x80, the initial `$sp`/`$gp`, or runtime-error display strings. The test owners need to add these. Otherwise the new behaviour is unguarded.
5. **What `Buffered` can show.** `PendingInput.Buffered` contains only text that PlusPim has already pulled from the console. That is what the snapshot can reasonably show, and it is documented as such instead of promising "all remaining input".
6. **Build break when `For` is removed.** Removing `PendingInput.For` breaks one test call site (`RuntimeCallInstructionTests.cs:586`). The prototype kept a temporary `For` shim. The real change should delete the shim and edit that line in the same commit.

## Open questions for the user

None remaining. Both earlier questions were answered (see Resolved decisions).

## Reconciliation with other plans (added after review)

- vscode-extension.md wraps redirected stdout in UTF-8 on the C# side and decodes with `stream: true` in the extension. The `DebuggeeOutput` byte stream decided here is compatible: it emits whole UTF-8 characters, and the lone-byte case (U+FFFD) is the only visible difference.
- vscode-extension.md notes that on Windows TCP mode the console code page can print non-ASCII as `?`; its `Program.cs` UTF-8 wrapping covers that.
- debugger-dap.md adds a worker thread for Continue. Reads of `DebuggeeOutput` / `Input.Buffered` from DAP handlers must happen while stopped or under the `Application` lock. Pause cannot interrupt a blocking `Console.In` read.
- debugger-dap.md restructures `SendExecuteEvent`; keep calling `RuntimeErrorDisplay` there.
- assembler-frontend.md: `.ascii` / `.asciiz` should encode literals as UTF-8 so `print_string` and `print_char` agree.
