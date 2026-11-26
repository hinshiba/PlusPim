# Change Log

## [Unreleased]

## [0.5.0] - YYYY-MM-DD

### Added

- `cwd` launch option (default `${workspaceFolder}`) that relative paths in `program` are resolved against
- Run Without Debugging (`Ctrl+F5`)
- VSIX for `win32-arm64`, `linux-arm64`, `alpine-x64` and `alpine-arm64`
- Publishing to the Visual Studio Marketplace from `vscode-v<version>` tags
- `--package-only` option of `scripts/vsix.ts`, which builds a VSIX from the binary already in `bin/release`
- Emulation of branch, jump and load delay slots.
  Disable them with `"args": ["--no-delayed-branches"]` or `"args": ["--no-delayed-loads"]`
- Instructions `jalr`, `bgezal`, `bltzal` and the trap instructions, and many pseudo-instructions
  (`b`, `beqz`, `blt`, `bal`, `mul`, `rem`, `not`, `abs`, `ulw`, `usw`, ...)
- `$gp` in the _Memory_ scope
- The exception handler is shown as a call stack frame until `eret`

### Changed

- Bundle PlusPim 0.7.0 instead of 0.5.0. See its changelog for the other changes
- Steps are now per source line instead of per machine instruction, so a pseudo-instruction takes one step
- Scopes are renamed to _General_, _Special_, _CP0_ and _Memory_, and variables to `$zero`, `pc`, `hi`, `lo`, `BadVAddr`, ...
- Runtime errors always stop, regardless of the exception filters
- Assembly warnings are written to the Debug Console after launch, with codes such as `PPW0001`
- Launch PlusPim with `debug connect` instead of the removed `--connect` option
- Follow the rename of the bundled binary from `PlusPim` to `pluspim`
- Pass the programs to PlusPim through the `program` of the `launch` request instead of command-line arguments.
  Without `cwd`, every path in `program` must be absolute
- Activate only when a PlusPim debug session starts, instead of on any debug session
- The bundled binary is compiled with Native AOT on `win32-x64` and `linux-x64`, and trimmed on the other targets,
  which makes it about a quarter of its former size
- The `linux-x64` VSIX requires glibc 2.28 or later
- The bundled binary is placed in `bin/release` whatever the target.
  The extension no longer checks the platform, since the Marketplace installs the VSIX for it

## [0.3.0] - 2026-10-13

### Added

- `stopOnEntry` launch option (default `true`). When `false`, the program runs
  until a breakpoint, an exception or the end
- Inlay hints that show the machine instructions each pseudo-instruction
  expands to, during a debug session (setting
  `pluspim.inlayHints.pseudoInstructions`, default `true`)
- `.utf-8z` data directive: places a string as UTF-8 bytes followed by a NUL byte
- `forceTerminalUtf8` launch option (default `true`). The extension passes
  `--force-terminal-utf8` to PlusPim, which switches the terminal to UTF-8
  while it runs and restores it on exit (currently effective on Windows only)
- Packaging scripts for macOS (`package:darwin-x64` and `package:darwin-arm64`).
  The extension picks the bundled binary by `process.arch` on macOS. The
  binaries are not signed or notarized

### Changed

- The bundled PlusPim binary is now compiled with ReadyToRun
- `.ascii` and `.asciiz` now reject non-ASCII characters with an assembly error
  (always, regardless of `--strict`). Use `.utf-8z` for UTF-8 strings
- Display name is now "PlusPim for VS Code"
- The extension now listens on a loopback port chosen by the OS, and PlusPim
  connects to it with `--connect`. The first connection is taken as the debug
  session. This replaces the fixed port and the probe connection
- Terminals are managed per debug session, so ending one session no longer
  disposes the terminal of another
- The program's standard input and output are passed through the terminal as
  raw bytes, and no longer go through the Debug Console

### Removed

- `port` launch option, and the `--port` and `--stdio` command line options
  (use `--connect <host:port>` or `--listen <host:port>`)

### Fixed

- `print_char` and `print_string` write raw bytes instead of re-encoding them,
  so UTF-8 text printed byte by byte is no longer garbled when the terminal
  decodes it as UTF-8
- UTF-8 program output is no longer garbled on Windows terminals using a legacy
  code page (for example Shift_JIS)

## [0.2.0] - 2026-08-31

### Added

- Byte and halfword memory instructions: `lb`, `lbu`, `lh`, `lhu`, `sb`, `sh`
- `.half` data directive for 16-bit values
- `--stdio` transport for the debug adapter (DAP over stdin/stdout as an alternative to TCP)
- Example MIPS programs (sum, Fibonacci, GCD, strlen, array, bubble sort)

### Fixed

- Arithmetic overflow in I-type instructions (e.g. `addi`) no longer terminates
  the emulator; it now raises the `Ov` exception
- R-type arithmetic instructions (`add`, `sub`) no longer raise a spurious `Ov`
  exception when no overflow occurred
- `.align` is now honored correctly (labels are resolved after the following
  data is aligned and placed, instead of before)

## [0.1.0] - 2026-04-06

### Added

- Step execution, step over, and step back (time-travel debugging) for MIPS assembly
- Breakpoint support
- Continue / Reverse Continue
- Register view (GPR, HI/LO, PC, CP0)
- Exception emulation (CP0 Status/Cause/EPC, exception breakpoint filters)
- Multi-file program support
- DAP trace logging
