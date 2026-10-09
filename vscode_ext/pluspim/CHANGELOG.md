# Change Log

## [Unreleased]

### Added

- `stopOnEntry` launch option (default `true`). When `false`, the program runs
  until a breakpoint, an exception or the end
- Inlay hints that show the machine instructions each pseudo-instruction
  expands to, during a debug session (setting
  `pluspim.inlayHints.pseudoInstructions`, default `true`)
- *PlusPim* output channel with the elapsed time of each startup phase
  (`[+<ms>ms] <phase>`), and elapsed times in the `--verbose` log

### Changed

- Display name is now "PlusPim for VS Code"
- The debug adapter listens on a free port chosen by the OS, so a session
  starts even when port 4711 is in use and several sessions can run at once.
  The `port` launch option no longer has a default and only fixes the port
  when set
- The bundled PlusPim binary is published with ReadyToRun, which shortens
  session startup (about 226ms to 158ms on Windows, warm)
- The extension starts PlusPim itself and shows its input and output in a
  terminal that stays open after the session ends (press any key to close it)

### Fixed

- The program's output no longer disappears with its terminal when the
  session ends
- A debug client that sends nothing within 50ms of connecting is no longer
  dropped as a readiness probe
- Non-ASCII program output and input (e.g. Japanese) are no longer garbled on
  Windows
- A fixed port that is already in use now reports
  `Port N is already in use. Use --port 0 to pick a free port.` (exit code 2)
  instead of crashing

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
