# PlusPim — MIPS Assembly Debugger for VS Code

A VS Code extension that integrates the PlusPim time-travel debugger for MIPS assembly code.

## Features

- **Step execution** — Step over, step into and step out, one source line at a time.
- **Time-travel debugging** — Step back and reverse continue to rewind execution.
- **Breakpoints** — Line breakpoints, continue and pause.
- **Exception breakpoints** — Choose which MIPS exceptions stop execution. Runtime errors always stop.
- **Variables view** — Inspect the general-purpose registers (_General_), `pc`, `hi` and `lo` (_Special_), and `BadVAddr`, `Status`, `Cause` and `EPC` (_CP0_).
- **Memory view** — Open the memory at a register value, or at `.data`, `$sp` and `$gp` from the _Memory_ scope.
- **Call stack** — Calls push a frame, and the exception handler is shown as a frame until `eret`.
- **Delay slots** — Branch, jump and load delay slots are emulated. Pass `--no-delayed-branches` or `--no-delayed-loads` in `args` to disable them.
- **Run Without Debugging** — `Ctrl+F5` runs the program ignoring breakpoints and exception filters.
- **Pseudo-instruction hints** — During a debug session, lines with a pseudo-instruction (`la`, `li`, `move`, ...) show the machine instructions they expand to as inlay hints, e.g. `=> lui $a0, 0x1000; ori $a0, $a0, 0x0000`. A step runs the whole line, including all of its expanded instructions. The hints are hidden while the file has unsaved or post-launch edits, and can be turned off with `pluspim.inlayHints.pseudoInstructions` (VS Code's `editor.inlayHints.enabled` also applies).
- **DAP trace** — Optional DAP communication logging to the _PlusPim DAP Trace_ output channel.

## Requirements

- VS Code `^1.108.0`
- One of the following platforms. No .NET runtime is needed; each platform-specific VSIX bundles a self-contained PlusPim binary.

| VS Code target | Notes                                                          |
| -------------- | -------------------------------------------------------------- |
| `win32-x64`    |                                                                |
| `win32-arm64`  |                                                                |
| `linux-x64`    | glibc 2.28 or later                                            |
| `linux-arm64`  |                                                                |
| `alpine-x64`   | Requires `libstdc++` and `libgcc` (`apk add libstdc++ libgcc`) |
| `alpine-arm64` | Same as `alpine-x64`                                           |
| `darwin-x64`   | Unsigned and not notarized                                     |
| `darwin-arm64` | Same as `darwin-x64`                                           |

## Installation

Install **PlusPim for VS Code** from the Visual Studio Marketplace. The Marketplace picks the VSIX for your platform.

## Getting Started

1. Open a MIPS assembly file (`.asm` or `.s`).
2. Open the **Run and Debug** view (`Ctrl+Shift+D`).
3. If no `launch.json` exists, VS Code will prompt you to create one — select **PlusPim Debugger**.
   The generated configuration looks like this:

```json
{
	"version": "0.2.0",
	"configurations": [
		{
			"type": "pluspim",
			"request": "launch",
			"name": "Minimal Launch",
			"program": ["${file}"]
		}
	]
}
```

4. Press **F5** to start debugging the currently open file, or **Ctrl+F5** to run it without debugging.

### Using `syscall`

PlusPim does not handle `syscall` by itself. A `syscall` raises an exception that jumps to the exception handler in the kernel text (`.ktext`), and `runtime_call!` in the handler performs the I/O. Without a handler, the program stops with the runtime error `NonInstructionExecution`.

Copy [`samples/delay-slots/kseg.asm`](https://github.com/hinshiba/PlusPim/blob/main/samples/delay-slots/kseg.asm), a minimal handler (or [`samples/no-delay-slots/kseg.asm`](https://github.com/hinshiba/PlusPim/blob/main/samples/no-delay-slots/kseg.asm) when delay slots are disabled), into your workspace and add it to `program`:

```json
{
	"type": "pluspim",
	"request": "launch",
	"name": "Launch with kernel handler",
	"program": ["${file}", "${workspaceFolder}/kseg.asm"]
}
```

## Launch Configuration

| Property            | Type       | Default              | Description                                                                                                                                                                 |
| ------------------- | ---------- | -------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `program`           | `string[]` | `["${file}"]`        | Path to the MIPS assembly file(s). A single string or an array of paths, placed in that order. A relative path is resolved against `cwd`.                                   |
| `cwd`               | `string`   | `${workspaceFolder}` | Absolute directory that relative paths in `program` are resolved against. Without it, every path in `program` must be absolute.                                             |
| `stopOnEntry`       | `boolean`  | `true`               | Stop at the first instruction. When `false`, the program runs until a breakpoint, an exception or the end.                                                                  |
| `forceTerminalUtf8` | `boolean`  | `true`               | Ask PlusPim to switch the terminal to UTF-8 while it runs and restore it on exit (currently effective on Windows only). Set to `false` to keep the terminal's own encoding. |
| `args`              | `string[]` | `[]`                 | Extra arguments passed to PlusPim (e.g. `["--verbose"]`, `["--strict"]`, `["--no-delayed-branches"]`).                                                                      |
| `trace`             | `boolean`  | `false`              | Show DAP protocol messages in the output channel.                                                                                                                           |

## How It Works

When a debug session starts, the extension:

1. Starts listening on a free loopback port chosen by the OS.
2. Spawns the bundled `pluspim` binary in a VS Code terminal with `debug connect 127.0.0.1:<port>` and the options.
3. Takes the first connection from PlusPim as the debug session, and PlusPim loads `program` on the `launch` request. If PlusPim does not connect within 5 seconds, or exits before connecting, the session fails; see the terminal for details.

The program's standard input and output are the terminal itself, so you type input there and the output is written there as raw bytes. Non-ASCII output is shown correctly only if the terminal decodes it as UTF-8. By default the extension asks PlusPim to switch the terminal to UTF-8 while it runs (`forceTerminalUtf8`); if you turn that off, the terminal itself must decode UTF-8.

## Known Limitations

- The terminal is closed by VS Code when PlusPim exits, so the program's output disappears when the session ends.
- On Windows, UTF-8 output is garbled when `forceTerminalUtf8` is `false` and the console code page is not UTF-8 (for example Shift_JIS). The code page is restored when PlusPim exits, but not if PlusPim is killed forcibly.
- Pseudo-instruction hints are only shown during a debug session, because the expansion needs the assembled program.

## Development

Prerequisites:

- [Bun](https://bun.sh/) 1.3 or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

Run the following in `clients/vscode`:

```sh
bun install
bun run compile
```

`bun run test` runs the unit tests, `bun run typecheck` type-checks the sources, tests and scripts, and `bun run ci` runs the lint and format checks that CI runs.

| Command                            | Description                                                                  |
| ---------------------------------- | ---------------------------------------------------------------------------- |
| `bun run package:win32-x64`        | Publish the PlusPim binary for `win-x64` and build a platform-specific VSIX. |
| `bun run package:linux-x64`        | Same for `linux-x64`.                                                        |
| `bun run package:darwin-x64`       | Same for `osx-x64` (Intel Mac).                                              |
| `bun run package:darwin-arm64`     | Same for `osx-arm64` (Apple Silicon).                                        |
| `bun run dotnet:publish:win`       | Publish the `win-x64` binary to `bin/release` only (no VSIX).                |
| `bun run dotnet:publish:linux`     | Publish the `linux-x64` binary to `bin/release` only (no VSIX).              |
| `bun run dotnet:publish:mac-x64`   | Publish the `osx-x64` binary to `bin/release` only (no VSIX).                |
| `bun run dotnet:publish:mac-arm64` | Publish the `osx-arm64` binary to `bin/release` only (no VSIX).              |
| `bun run dotnet:debug:win`         | Build a Debug binary to `bin/debug` (never included in a VSIX).              |

The other targets (`win32-arm64`, `linux-arm64`, `alpine-x64`, `alpine-arm64`) have no script; run `bun scripts/vsix.ts <target>` directly. `bun scripts/vsix.ts --publish-only <target>` only publishes the binary, and `bun scripts/vsix.ts --package-only <target>` only builds the VSIX from the binary already in `bin/release`.

A VSIX bundles a single binary in `bin/release`, whatever the target. When `bin/debug` holds a binary, the extension launches it instead while being debugged in an Extension Development Host.

The publish settings live in `src/PlusPim/PlusPim.csproj`. `win32-x64` and `linux-x64` are compiled with Native AOT, which can only be published on the same OS, so publish them on Windows and Linux respectively. `linux-x64` is published in an AlmaLinux 8 container by `scripts/build/publish.sh` to keep the glibc 2.28 requirement, so it needs Docker. The other targets are single-file, trimmed and ReadyToRun, and can be cross-published from another OS. The binaries are unsigned, so macOS may require `xattr -d com.apple.quarantine` on first run.

## License

MIT
