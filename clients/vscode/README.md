# PlusPim — MIPS Assembly Debugger for VS Code

A VS Code extension that integrates the PlusPim time-travel debugger for MIPS assembly code.

## Features

- **Step execution** — Step through MIPS assembly one instruction at a time.
- **Step back** — Rewind execution to the previous state (time-travel debugging).
- **Register view** — Inspect all 32 MIPS registers, `HI`, `LO`, and `PC` during a debug session.
- **Label resolution** — Jump targets and branch labels are resolved automatically.
- **Pseudo-instruction hints** — During a debug session, lines with a pseudo-instruction (`la`, `li`, `move`, ...) show the machine instructions they expand to as inlay hints, e.g. `=> lui $a0, 0x1000; ori $a0, $a0, 0x0000`. Stepping is per machine instruction, so such a line takes several steps. The hints are hidden while the file has unsaved or post-launch edits, and can be turned off with `pluspim.inlayHints.pseudoInstructions` (VS Code's `editor.inlayHints.enabled` also applies).
- **DAP trace** — Optional DAP communication logging to the *PlusPim DAP Trace* output channel.

## Requirements

- [.NET 10 Runtime](https://dotnet.microsoft.com/download) (bundled in the published extension)
- VS Code `^1.108.0`

## Getting Started

1. Open a MIPS assembly file (`.asm`).
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
            "program": "${file}"
        }
    ]
}
```

4. Press **F5** to start debugging the currently open file.

## Launch Configuration

| Property            | Type       | Default       | Description                                                                                                                                                                 |
| ------------------- | ---------- | ------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `program`           | `string[]` | `["${file}"]` | Path to the MIPS assembly file(s). A single string or an array of paths.                                                                                                    |
| `stopOnEntry`       | `boolean`  | `true`        | Stop at the first instruction. When `false`, the program runs until a breakpoint, an exception or the end.                                                                  |
| `forceTerminalUtf8` | `boolean`  | `true`        | Ask PlusPim to switch the terminal to UTF-8 while it runs and restore it on exit (currently effective on Windows only). Set to `false` to keep the terminal's own encoding. |
| `args`              | `string[]` | `[]`          | Extra arguments passed to the debug adapter (e.g. `["--verbose"]`).                                                                                                         |
| `trace`             | `boolean`  | `false`       | Show DAP protocol messages in the output channel.                                                                                                                           |

## How It Works

When a debug session starts, the extension:

1. Starts listening on a free loopback port chosen by the OS.
2. Spawns the bundled `PlusPim` binary in a VS Code terminal with `--connect 127.0.0.1:<port>`, passing the target file and options.
3. Takes the first connection from PlusPim as the debug session. If PlusPim does not connect within 5 seconds, or exits before connecting, the session fails; see the terminal for details.

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

| Command                            | Description                                                                  |
| ---------------------------------- | ---------------------------------------------------------------------------- |
| `bun run package:win32-x64`        | Publish the PlusPim binary for `win-x64` and build a platform-specific VSIX. |
| `bun run package:linux-x64`        | Same for `linux-x64`.                                                        |
| `bun run package:darwin-x64`       | Same for `osx-x64` (Intel Mac).                                              |
| `bun run package:darwin-arm64`     | Same for `osx-arm64` (Apple Silicon).                                        |
| `bun run dotnet:publish:win`       | Publish the `win-x64` binary to `bin/win-x64` only (no VSIX).                |
| `bun run dotnet:publish:linux`     | Publish the `linux-x64` binary to `bin/linux-x64` only (no VSIX).            |
| `bun run dotnet:publish:mac-x64`   | Publish the `osx-x64` binary to `bin/osx-x64` only (no VSIX).                |
| `bun run dotnet:publish:mac-arm64` | Publish the `osx-arm64` binary to `bin/osx-arm64` only (no VSIX).            |
| `bun run dotnet:debug:win`         | Build a Debug binary to `bin/debug` (never included in a VSIX).              |

Release publishes are compiled with ReadyToRun. Cross-publishing from another OS works, but the binaries are unsigned, so macOS may require `xattr -d com.apple.quarantine` on first run.

## License

MIT
