# Change Log

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
