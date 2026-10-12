# Change Log

## [Unreleased]

### Added

- `stopOnEntry` launch option (default `true`). When `false`, the program runs
  until a breakpoint, an exception or the end
- Inlay hints that show the machine instructions each pseudo-instruction
  expands to, during a debug session (setting
  `pluspim.inlayHints.pseudoInstructions`, default `true`)
- `.utf-8z` data directive: places a string as UTF-8 bytes followed by a NUL byte

### Changed

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
