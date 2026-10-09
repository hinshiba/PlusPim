# PlusPim — MIPS Assembly Debugger for VS Code

A VS Code extension that integrates the PlusPim time-travel debugger for MIPS assembly code.

## Features

- **Step execution** — Step through MIPS assembly one instruction at a time.
- **Step back** — Rewind execution to the previous state (time-travel debugging).
- **Register view** — Inspect all 32 MIPS registers, `HI`, `LO`, and `PC` during a debug session.
- **Label resolution** — Jump targets and branch labels are resolved automatically.
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

| Property      | Type       | Default       | Description                                                                                                 |
| ------------- | ---------- | ------------- | ----------------------------------------------------------------------------------------------------------- |
| `program`     | `string[]` | `["${file}"]` | Path to the MIPS assembly file(s). A single string or an array of paths.                                    |
| `stopOnEntry` | `boolean`  | `true`        | Stop at the first instruction. When `false`, the program runs until a breakpoint, an exception or the end. |
| `port`        | `number`   | `4711`        | TCP port used for the DAP connection.                                                                       |
| `args`        | `string[]` | `[]`          | Extra arguments passed to the debug adapter (e.g. `["--verbose"]`).                                         |
| `trace`       | `boolean`  | `false`       | Show DAP protocol messages in the output channel.                                                           |

## How It Works

When a debug session starts, the extension:

1. Spawns the bundled `PlusPim` binary in a VS Code terminal, passing the target file and options.
2. Waits for the debug adapter to start listening on the configured TCP port (default `4711`).
3. Connects VS Code to the adapter via `DebugAdapterServer` over TCP.

On session termination, the terminal is automatically disposed.

## Known Limitations

- Only Windows (x64) and Linux (x64) are supported. macOS is not available.

## Development

Prerequisites:

- [Bun](https://bun.sh/) 1.3 or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

Run the following in `vscode_ext/pluspim`:

```sh
bun install
bun run compile
```

| Command                        | Description                                                                  |
| ------------------------------ | ---------------------------------------------------------------------------- |
| `bun run package:win32-x64`    | Publish the PlusPim binary for `win-x64` and build a platform-specific VSIX. |
| `bun run package:linux-x64`    | Same for `linux-x64`.                                                        |
| `bun run dotnet:publish:win`   | Publish the `win-x64` binary to `bin/win-x64` only (no VSIX).                |
| `bun run dotnet:publish:linux` | Publish the `linux-x64` binary to `bin/linux-x64` only (no VSIX).            |
| `bun run dotnet:debug:win`     | Build a Debug binary to `bin/debug` (never included in a VSIX).              |

## License

MIT
