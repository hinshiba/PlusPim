# Plan: vscode-extension area

Base: branch `agent/temp` (148383b) plus the uncommitted `doc/todo.md`. All `file:line` references are to that commit unless marked "prototype".

## Resolved decisions (user answers, applied)

- Terminals of finished sessions: the most natural behavior. A finished session's terminal stays open until a key is pressed or the user closes it; when a new session starts, leftover terminals of finished sessions are closed automatically (the prototype behavior), so they do not pile up.
- Inlay hints are shown only during a debug session. No CLI or LSP front end is planned for this.
- Display name: keep "PlusPim for VS Code", as written in the todo. The earlier trademark concern was only a precaution that was never verified, and it does not block the change.
- ReadyToRun: adopt it now (`-p:PublishReadyToRun=true` in `scripts/vsix.ts`), and still add the timing logs in VS Code to confirm the effect.
- The `--debug` flag stays (decided in the code-quality plan), so the extension keeps passing `-d`.

## Summary

- One design covers three todo items: the terminal disappearing (#12), the fixed port 4711 (#18), and the 50ms probe heuristic (#18). The extension spawns PlusPim itself with `child_process.spawn` and `--port 0`. PlusPim binds a port chosen by the OS and prints a single handshake line on stdout after `Listen()`, then accepts exactly one connection. The extension reads that line, connects VS Code with `DebugAdapterServer(port, "127.0.0.1")`, and shows the debuggee's stdio in a `Pseudoterminal` whose lifetime the extension controls. That terminal stays open after the session ends until the user dismisses it.
- The C# and TS halves must land together: an old extension's probe connection would be accepted as the DAP client by the new single-accept server.
- Startup: the binary itself is fast (about 220ms warm in the shipped configuration). The handshake removes about 60ms, and ReadyToRun removes about 70ms more. A large first-run penalty is plausible (antivirus scanning). Instrumentation in VS Code comes before any further change.
- "Debug build in VSIX" is already implemented on `agent/temp`. It only needs verifying and closing.
- Inlay hints: a provider written against an assumed DAP custom request, `pluspimPseudoExpansions`, behind an `ExpansionSource` interface. Hints are shown only during a session.
- Prototype finding that affects every other TS change: `.vscodeignore` ships only `out/extension.js`, so adding any new TS module breaks the VSIX unless the rule is changed.

## Task breakdown

### Terminal and program output disappear when the session ends (#12)

Problem: when a session ends, the terminal showing the program's output is closed.

Root cause:
- `extension.ts:14-20` calls `factory.dispose()` on every `onDidTerminateDebugSession` of type `pluspim`, and `extension.ts:114-117` disposes the terminal.
- Even without that, the terminal is created with `shellPath: binPath` (`extension.ts:101-105`). VS Code disposes such a terminal when its process exits, and `TerminalOptions` does not expose `waitOnExit`. PlusPim exits right after `disconnect` (`Program.cs:144`, `DebugAdapter.cs:116-123`). This behavior is believed, not verified here; it is the reason removing `dispose()` alone is not planned.
- Side bug: the factory holds one `terminal` field (`extension.ts:62`), so the end of any session disposes the latest session's terminal.

Chosen design: a `vscode.Pseudoterminal` (`DebuggeeTerminal`) per session. The extension owns the child process and its pipes.
- PlusPim's stdout and stderr are decoded as streaming UTF-8 (`TextDecoder` with `stream: true`), converted from LF to CRLF, and written to the terminal. Output is buffered until `open()` runs, because VS Code drops events fired before then.
- Keyboard input goes through a small line editor (`LineDiscipline`): echo, backspace with East Asian width, Enter sends `line + "\n"` to stdin, Ctrl+C kills the process, Ctrl+D on an empty line closes stdin, and escape sequences are dropped. PlusPim reads stdin line-wise (`RuntimeCall.cs:76,101,239`), so cooked mode is sufficient.
- When the process exits, the terminal prints a dim line `[PlusPim exited with code N. Press any key to close this terminal.]`. The next key closes it.
- If the user closes the terminal while the session runs, the process is killed, which ends the session through the socket.
- On session end, PlusPim is killed only if it has not exited within 3s.
- The factory keeps a `Map<sessionId, SessionRun>` instead of a single field. When a new session starts, terminals of finished sessions are disposed (see Open questions).
- Create the terminal with `isTransient: true`, because a pty terminal cannot be restored after a window reload.

Alternatives rejected:
- Removing `dispose()` only: the terminal still closes when the process exits.
- Wrapping in a shell with `pause` / `read`: platform- and shell-specific quoting, and still no handle on the process.
- Debug Console only (`--stdio` and `OutputEvent`): stdin is degraded to `TextReader.Null` (`Program.cs:117`), so `read_int` / `read_string` / `read_char` stop working.

Interfaces (prototype, `src/debuggeeTerminal.ts`, `src/lineDiscipline.ts`):

```ts
interface DebuggeeIo { writeStdin(text: string): void; endStdin(): void; kill(): void; }
class DebuggeeTerminal implements vscode.Pseudoterminal {
  attach(io: DebuggeeIo): void;
  write(text: string): void;              // LF -> CRLF, buffered until open()
  markExited(code: number | null): void;  // prints the footer, next key closes
  readonly hasExited: boolean;
  readonly onUserClosed: vscode.EventEmitter<void>;
  dispose(): void;
}
interface LineDisciplineActions { echo(t: string): void; submit(line: string): void; eof(): void; interrupt(): void; }
class LineDiscipline { input(data: string): void; }
function toTerminalNewlines(text: string): string;
```

Implementation steps (in order):
- Add `src/lineDiscipline.ts` (no `vscode` import) and `src/debuggeeTerminal.ts`.
- In `extension.ts`, replace the `terminal` field with the `runs` map. Replace the `onDidTerminateDebugSession` handler with `onSessionTerminated(session)`, which runs the grace-period kill. Register the factory itself as a disposable so `dispose()` kills live children on deactivate.
- Change `.vscodeignore:14-15` from `!out/extension.js` to `!out/*.js` (see Prototype findings).
- Verify manually in the Extension Development Host:
  - `syscall.asm` output stays visible after the session ends.
  - `read_int` works through the terminal.
  - Closing the terminal mid-session ends the session.

Risks:
- Not a real TTY. Arrow-key line editing and history are not supported, and output written while the user types interleaves with the echo. Acceptable for `read_*`.
- VS Code "Restart" may reuse the same `session.id` and call `createDebugAdapterDescriptor` again before the old process exits, which could leave two terminals. If this shows up in testing, key `runs` by a per-launch counter.
- The Pseudoterminal runtime behavior (open timing, close on key) was type-checked but not run inside VS Code (no extension host available to the prototype).

### Fixed port 4711, and the 50ms probe heuristic (#18)

These two items are planned as one design.

Problem: a session cannot start when 4711 is in use. The readiness check opens a probe connection, and the server tells it apart from the real client by waiting 50ms.

Root cause:
- Extension: `extension.ts:69` (`session.configuration.port ?? 4711`), `package.json:72-76` (`"default": 4711`), and `waitForPort` (`extension.ts:108`, `121-141`), which connects, closes, and retries every 100ms.
- C#: `Program.cs:128-137` accepts, sleeps 50ms, and drops a connection that is readable with 0 bytes. A real client that sends nothing within 50ms is dropped as a probe, and a probe that stays open longer than 50ms is taken as the client. An occupied port is an unhandled `SocketException` crash (`Program.cs:122`). Prototype run: exit code 3762504530, and the stack trace text arrives as CP932 bytes.

Chosen design: the OS assigns the port, and PlusPim announces it on stdout.
- CLI: `--port 0` binds an ephemeral port. The `--port` default stays 4711, so manual `debugServer` workflows keep working; only the extension passes 0.
- After `Bind` and `Listen(1)`, PlusPim writes exactly `PLUSPIM_DAP_LISTENING 127.0.0.1:<port>\n` to stdout and flushes. This is the only stdout output that can happen before a client connects: debuggee output starts after `launch`, and verbose logs go to stderr (`Program.cs:86-88`).
- Exactly one `AcceptAsync`, then the listener is closed. The probe loop and the 50ms delay are deleted.
- Bind failure with `AddressAlreadyInUse` prints `Port N is already in use. Use --port 0 to pick a free port.` to stderr and returns exit code 2.
- In TCP mode, redirected stdout, stderr and stdin are wrapped in UTF-8 `StreamWriter`/`StreamReader` (with `AutoFlush`). Without this, piped output uses the console code page (932 on this machine). The prototype measured `日本` arriving as U+FFFD. `Console.OutputEncoding` is not used, because `SetConsoleOutputCP` changes the code page of a shared cmd window. Wrapping only `IsXxxRedirected` streams leaves an interactive console untouched.
- Extension: `launchAdapter()` spawns with `stdio: "pipe", windowsHide: true` and runs stdout through `HandshakeScanner`. Non-handshake text before the marker is passed through, never dropped. It resolves with the port, and rejects on:
  - a timeout of 15s,
  - process exit before the handshake (the message includes the last 2000 chars of stderr),
  - a spawn error.
- The launch config `port` keeps working as an explicit override; it has no default.
- Return `new vscode.DebugAdapterServer(port, "127.0.0.1")`. The explicit host avoids `localhost` resolving to `::1`, since the listener is IPv4 only (`AddressFamily.InterNetwork`).

Alternatives rejected:
- Extension picks a free port (`listen(0)`, close, pass the port): a race window, and the probe heuristic remains.
- Keep the probe and detect "first bytes": still timing-based.
- Reverse connection (extension listens, PlusPim connects with `--connect`): no stdout parsing, but needs DAP framing in TS through `DebugAdapterInlineImplementation`. A reasonable fallback if stdout parsing ever proves fragile.
- `DebugAdapterExecutable` with `--stdio`: no port at all, but loses debuggee stdin and the terminal (see #12).
- Named pipe or port file: still needs a readiness signal, plus cleanup.

Interfaces:

```text
CLI:    PlusPim -d --port 0 [--verbose] <files...>
stdout: "PLUSPIM_DAP_LISTENING 127.0.0.1:<port>\n"   (first and only line before accept; LF)
exit:   2 = requested port in use
```

```ts
export const LISTENING_MARKER = "PLUSPIM_DAP_LISTENING 127.0.0.1:"; // must equal Program.ListeningMarker
class HandshakeScanner { feed(text: string): string /* passthrough */; readonly port: number | undefined; }
interface AdapterProcess { port: number; child: ChildProcessWithoutNullStreams; exited: Promise<number | null>; }
interface LaunchOptions { binPath: string; args: string[]; cwd?: string; timeoutMs: number;
  onStdout(text: string): void; onStderr(text: string): void; }
function launchAdapter(opts: LaunchOptions): Promise<AdapterProcess>; // rejects with AdapterLaunchError
```

Implementation steps (in order, one PR together with #12):
- `Program.cs`:
  - Add the `ListeningMarker` const and the UTF-8 wrapping of redirected streams.
  - Replace `Program.cs:121-137` with bind (with the `AddressAlreadyInUse` catch), `Listen(1)`, the announce line, a single accept, and disposing the listener.
  - Update the `--port` description at `Program.cs:46`.
- Add `src/adapterProcess.ts` (no `vscode` import) and delete `waitForPort`.
- `package.json`: drop `"default": 4711` from `port` and reword its description.
- Verify:
  - With 4711 occupied, three concurrent sessions start.
  - An explicit occupied port shows the exit-2 message in the terminal and in an error notification.

Risks:
- A user `args` entry such as `--help` makes PlusPim print to stdout and exit. This is handled: the text is passed through and the launch rejects with "exited before listening".
- Any local process could connect to the ephemeral port before VS Code does. This was also true before, and the window is now shorter. DAP has no authentication; out of scope.
- A stale `bin/debug` built before this change will not print the handshake, so the launch times out after 15s. Mention in the PR description.

### Slow startup (profiling)

Problem: the user perceives a slow start. There is no measurement yet.

Measured in the prototype (Windows, this machine, time from spawn to each DAP response, warm runs, median):

| Binary | to listen/handshake | to initialize response | to launch response |
| --- | --- | --- | --- |
| Debug build, old probe protocol | 70ms | +185ms | +27ms |
| Release self-contained (shipped), old protocol | 70ms | +185ms | +25ms |
| Release, new handshake | 73ms | +123ms | +24ms |
| Release + ReadyToRun, new handshake | 63ms | +76ms | +13ms |
| ReadyToRun + InvariantGlobalization | no measurable change (about 3ms) | | |
| ReadyToRun + PublishSingleFile | 63ms | +64ms | +9ms, but the first run took 2.8s |

Cold first runs added about 160 to 200ms in the multi-file layouts. ReadyToRun adds 1.7MB to the 78MB bundle.

Root cause candidates:
- Extension side: the 100ms retry granularity of `waitForPort` (`extension.ts:135`).
- Server side: the fixed 50ms probe wait (`Program.cs:130`).
- JIT of `Microsoft.VisualStudio.Shared.VSCodeDebugProtocol` / `Newtonsoft.Json` on the first message (most of the `initialize` time, which ReadyToRun reduces).
- First-run antivirus scanning of about 200 DLLs after each VSIX install.
- Unmeasured VS Code-side costs: activation, terminal creation.

Chosen plan:
- First, instrument instead of guessing:
  - Extension: a `vscode.window.createOutputChannel("PlusPim", { log: true })` with timestamps for descriptor entry, spawn, handshake and descriptor returned.
  - The tracker (already present, `extension.ts:23-35`) logs elapsed time for the `initialize` and `launch` responses and the first `stopped` event.
  - C#: verbose log lines carry ms since process start (`Environment.TickCount64 - Process.GetCurrentProcess().StartTime`).
  - Measure first launch after installing a VSIX, and a warm second launch.
- Then apply, in order of measured benefit and low risk:
  - The handshake (above): removes the 50ms and the polling delay.
  - `-p:PublishReadyToRun=true` in `scripts/vsix.ts:48-56` (the `dotnet publish` args): about 70ms warm.
  - Anything else only if the instrumentation shows a VS Code-side cost.

Rejected:
- PublishSingleFile: the 2.8s first run, and every version produces a new 70MB blob to scan.
- InvariantGlobalization: no gain.
- Trimming or NativeAOT: the DAP library and Newtonsoft rely on reflection, which is high risk.

Interfaces: none public. Log format `[+<ms>ms] <phase>`.

Risks: numbers on Linux and on other machines may differ. ReadyToRun must be produced per RID; `vsix.ts` already publishes per RID.

### Debug build included in the VSIX (#18)

Status: already implemented on `agent/temp`.
- `extension.ts:84-92` prefers `bin/debug` only when `context.extensionMode === vscode.ExtensionMode.Development`.
- `.vscodeignore:16-17` excludes `bin/debug/**`.
- The prototype ran `vsce ls --no-dependencies` with a populated `bin/debug`: 0 `bin/debug` entries.

Remaining steps:
- Re-run `bun x --no-install -p @vscode/vsce vsce ls --no-dependencies` after the `.vscodeignore` change and check the checkbox in `doc/todo.md`.
- Optional, small:
  - The warning at `extension.ts:90` suggests `bun run dotnet:debug:win` even on Linux. Mention `dotnet build` instead, since `PlusPim.csproj` Debug `OutputPath` already targets `bin/debug`.
  - `dotnet:debug:win` (`package.json:106`) publishes a self-contained Debug build into the same `bin/debug` folder that `dotnet build` writes framework-dependent output to; mixing the two leaves stray runtime files. Consider dropping the script in favor of `dotnet build`.

Risk: none significant.

### Display name: PlusPim for VS Code

- Change `package.json:3` `displayName` to `"PlusPim for VS Code"`. Do not change `name` (`pluspim`) or `publisher`, which would create a new extension ID and break updates.
- Keep the debugger `label` "PlusPim Debugger" (`package.json:44`), because it is what users see in the launch picker.
- Verify with `vsce ls` / the Extensions view in the dev host. README wording is a documentation item (out of scope).
- Decided by the user: keep this name. A possible conflict with Microsoft's naming guidance was never verified (`vsce` does not block it); re-check the current marketplace rules just before publishing.

### Pseudo-instruction expansion as inlay hints (#11, extension side)

Problem: `la`, `li`, `move` and similar expand to several machine instructions. Stepping is per machine instruction, so users cannot see why one line takes several steps.

Constraints:
- Expansions depend on the symbol table (`LaInstructionParser.cs:44-55` resolves the label address), so only an assembled program can produce them. A session-scoped source (DAP) is the natural first transport.
- No instruction can render itself as text today: `IInstruction` (`IInstruction.cs:8-33`) has no `ToString` or disassembly. The debugger-dap area must add one.

Chosen design:
- `PseudoExpansionHintsProvider` registered for `{ language: "mips" }`, backed by an `ExpansionSource`. `DapExpansionSource` calls `session.customRequest("pluspimPseudoExpansions", ...)` on the active `pluspim` session. After the first failure it stops asking that session, which degrades silently on older adapters.
- Cache per document URI, pinned to `document.version` at first fetch. Hide hints when the document is dirty or its version changed, since line numbers would be wrong.
- Invalidate (and fire `onDidChangeInlayHints`) when the tracker sees the `launch` response, because the program is assembled in `HandleLaunchRequest` (`DebugAdapter.cs:105`), and again on session end.
- Render at end of line: `=> lui $a0, 0x1000; ori $a0, $a0, 0x0000`, `paddingLeft`, and a tooltip with a `mips` code block.
- Setting: `pluspim.inlayHints.pseudoInstructions` (boolean, default true). VS Code's own `editor.inlayHints.enabled` still applies.

Alternatives:
- CLI (`PlusPim --expand file`): works without a session, but spawns a 200ms+ process per change, and multi-file label resolution needs the whole program list.
- LSP: right long-term (also serves other editors) but much larger.
- The `ExpansionSource` interface keeps both possible later.

Interfaces (prototype, `src/inlayHints.ts`): see Cross-area dependencies for the request shape, plus:

```ts
interface ExpansionSource { fetch(fsPath: string): Promise<PseudoExpansion[] | undefined>; }
class PseudoExpansionHintsProvider implements vscode.InlayHintsProvider { invalidate(): void; }
```

Implementation steps (after debugger-dap ships the request):
- Add `src/inlayHints.ts`.
- Register the provider in `activate`.
- Wrap the tracker so it always exists (the trace output stays conditional on `configuration.trace`) and calls `invalidate()` on the `launch` response.
- Add the `contributes.configuration` block.
- Verify: start a session on `syscall.asm`; line 7 (`la $a0, msg`) shows `lui`/`ori`; editing the file hides the hints.

Risks:
- No hints outside a debug session (documented limitation).
- Pinning to the first-fetch version is a heuristic; a file saved after launch but before the first fetch would show stale lines. The window is small.

### Other editors: Emacs, Vim, Neovim, Zed (feasibility only)

- The DAP server already exists, and `--stdio` (`Program.cs:106-119`) is the natural basis for generic clients:
  - Emacs: `dape` or `dap-mode`.
  - Neovim: `nvim-dap` (`type = "executable"`).
  - Vim: `vimspector`.
  - Zed: debugger extensions provide a DAP binary through the extension API.
- With the new single-accept server, `nvim-dap`'s `type = "server"` with `--port ${port}` would also work, without the probe problem.
- Blockers to fix first (outside this area):
  - PlusPim takes the program from the command line and ignores `LaunchArguments` (`DebugAdapter.cs:101-105`). Generic clients expect `program` in the `launch` request. Reading it there (debugger-dap) makes one adapter definition work in every editor.
  - In `--stdio` mode, debuggee stdin is `TextReader.Null` (`Program.cs:117`), so `read_*` syscalls get EOF. Other editors would need either the TCP mode with a terminal, or DAP `runInTerminal` support.
- Distribution: publish the per-RID self-contained binaries that `vsix.ts` already builds as GitHub release assets, then ship config snippets per editor (an Emacs/Vim/Neovim plugin is a thin wrapper). Zed additionally needs a tree-sitter grammar for syntax highlighting.
- An LSP (`PlusPim --lsp`) is only worth it for editor features: inlay hints, diagnostics for parse failures. If built, it should replace `DapExpansionSource` in VS Code too.

## Ordering and dependencies

- Independent, any time: display name; closing the Debug-build item. The `.vscodeignore` `!out/*.js` fix must land before or with the first PR that adds a TS module.
- PR A (one PR, C# and TS together): handshake + free port + Pseudoterminal (#12, #18 x2). It must not be split, because the new single-accept server breaks the old probe-based extension.
- Startup: add instrumentation in PR A or right after it, then the ReadyToRun change in `vsix.ts` as a separate small PR with before and after numbers.
- Inlay hints: after debugger-dap ships `pluspimPseudoExpansions` and instruction rendering. It touches `extension.ts` (activate and tracker), so rebase on PR A.
- Other editors: after debugger-dap reads `program` from `launch` arguments.

## Files expected to change

- `PlusPim/Program.cs` (TCP branch lines 120-145, `--port` description line 46, new const and `using System.Text`)
- `vscode_ext/pluspim/src/extension.ts` (factory, activate, tracker)
- `vscode_ext/pluspim/src/adapterProcess.ts` (new)
- `vscode_ext/pluspim/src/lineDiscipline.ts` (new)
- `vscode_ext/pluspim/src/debuggeeTerminal.ts` (new)
- `vscode_ext/pluspim/src/inlayHints.ts` (new)
- `vscode_ext/pluspim/package.json` (`displayName`, `port` attribute, `contributes.configuration`)
- `vscode_ext/pluspim/.vscodeignore` (`!out/*.js`)
- `vscode_ext/pluspim/scripts/vsix.ts` (`-p:PublishReadyToRun=true`)
- `doc/todo.md` (checkboxes only)

## Cross-area dependencies

- debugger-dap, the inlay hint contract assumed by the extension:
  - Request `pluspimPseudoExpansions` (canonical contract owned by debugger-dap.md, section "Protocol contract"), arguments `{ source: { path: string } }`, where the path is the absolute file system path as sent in `setBreakpoints`.
  - Response body `{ lines: Array<{ line: number; mnemonic: string; instructions: Array<{ address: string; text: string }> }> }`:
    - `line` is the 1-based source line of the pseudo-instruction.
    - `instructions[].text` holds the rendered machine instruction with resolved immediates, for example `lui $a0, 0x1000`; `address` is a hex string.
  - debugger-dap lists every parsed pseudo-instruction line, including single-instruction ones (`move`, `nop`). The extension decides what to show and may skip lines where it adds no information.
  - It must be valid after the `initialized` event (not only after the `launch` response). An unknown file returns `{ lines: [] }`, not an error.
  - If debugger-dap picks a different name or shape, only `DapExpansionSource` and the exported types in `inlayHints.ts` change.
- debugger-dap: the `disconnect` response is never delivered. `HandleDisconnectRequest` completes `_sessionEnded` (`DebugAdapter.cs:118`) before the response is written, `Main` returns and disposes the socket (`Program.cs:139-144`), and the prototype client never received the response. VS Code tolerates this by timing out, but it slows session end and violates DAP. Respond first, then end the session (for example, complete `_sessionEnded` after the protocol has flushed).
- debugger-dap: reading `program` from `LaunchArguments` (needed for other editors).
- debugger-dap / code-quality: `Program.cs` is also touched by the code-quality item "dead code after `throw` at `Program.cs:148-150`". Different lines, but coordinate the merge order.
- assembler-frontend: `.asciiz` / `.ascii` truncate each UTF-16 unit to a byte (`DataSegmentBuilder.cs:244,248`), so `こんにちは` assembles to garbage. Encode the literal as UTF-8. Found while testing the UTF-8 output path.
- runtime: `print_char` (`RuntimeCall.cs:136`). With the UTF-8 pipe writer from this plan, `Console.Write((char)b)` for `b >= 0x80` is encoded as two UTF-8 bytes. Writing raw bytes to `Console.OpenStandardOutput()` would let the extension's streaming decoder reassemble multi-byte characters split across calls. The extension side already decodes with `stream: true`, so no change is needed here.

## Prototype findings

The prototype is uncommitted in worktree `D:\dev\PlusPim\.claude\worktrees\agent-a62403dc5b8227ab1`. That worktree started at 73fc28f, so its contents were first synced to `agent/temp` with `git restore --source=agent/temp` (no branch switch).

What was built:
- C# handshake, encoding and bind-error handling in `Program.cs`.
- `adapterProcess.ts`, `lineDiscipline.ts`, `debuggeeTerminal.ts`, `inlayHints.ts`, and the rewired `extension.ts` / `package.json`.
- Checks: `dotnet build` and `dotnet format --verify-no-changes --include PlusPim/Program.cs` are clean, as are `bun run compile` (tsc) and `bun run lint`. A pre-existing format error in `RuntimeCall.cs` on `agent/temp` is not caused by this work.

Exercised with node scripts against the real binary (spawn, handshake, DAP initialize/launch/continue, stdin through `LineDiscipline`, exit):
- Port 0 works, including three concurrent sessions while 4711 is held by another listener.
- An explicit occupied port originally crashed with an unhandled exception, with CP932 text on stderr. The plan now catches `AddressAlreadyInUse` (exit 2) and wraps stderr in UTF-8 too.
- Piped stdout without explicit encoding produced CP932 bytes (`日本` became U+FFFD). The first fix used the `Console.OutputEncoding` setter; it worked even with a console-less (`detached`) spawn. The plan switched to wrapping only redirected streams, because the setter changes a shared console's code page. Japanese input through `read_string` and back out was verified in both the attached and detached cases.
- `HandshakeScanner` handled the marker split across chunks, noise before the marker, a malformed port, and `--help`-style output.
- `vsce ls` showed that `.vscodeignore` (`out/**` with `!out/extension.js`) silently drops every other compiled module. This was not in the original plan; the plan now changes the rule to `!out/*.js`.
- The `disconnect` response is lost (see Cross-area dependencies). The harness had to wait for process exit instead of the response.
- Startup numbers (table above):
  - The first assumption, that "slow start" is mostly the binary, looks weak. Warm start is about 220ms shipped and about 150ms with the handshake plus ReadyToRun.
  - PublishSingleFile looked best warm but its first run took 2.8s, so it was rejected.
  - The plan now leads with in-VS Code instrumentation.
- The Debug-build item turned out to be already implemented and verified (0 `bin/debug` entries in `vsce ls`). The plan was reduced to closing it.
- Not verified: the Pseudoterminal and inlay hints inside a real VS Code extension host, which was not available. Only the type check covers them.

## Open questions for the user

None remaining. All four earlier questions were answered (see Resolved decisions).
