# Plan: assembler-frontend

Area: parsing, preprocessing, symbol resolution (`PlusPim/Debuggers/PlusPimDbg/Program/`, `Instruction/Parser/`, `Instruction/Pseudo/`).
Baseline: branch `agent/temp` @ `148383b`. All file:line references point to that commit. Some line numbers in `doc/todo.md` are stale (for example, the memory operand regex is at `OperandParser.cs:35`, not `:32`).

## Resolved decisions (user answers, applied)

- Strict mode: implement it, provided it stays simple. Design below: a `--strict` CLI flag (reachable from launch.json `args`, so no DAP or schema work is required); lines that fail to parse then become errors collected into an `AssemblyException`. Default stays skip and warn.
- Register syntax is exact match: names are case-sensitive, no leading zeros (`$08` rejected), and no extra aliases such as `$s8` unless `RegisterID` defines them. This is stricter than today (case-insensitive); existing tests and sample `.asm` files must be checked and fixed if they use uppercase names.
- `la` with an undefined label is an error (`AssemblyException`, launch fails), not a warning.
- `.global` is accepted as an alias for `.globl`.
- `mfc0`/`mtc0` operand form was not answered; the plan's default (numbers only, `$0`-`$31`) stands.

## Summary

The six todo items share one parsing path: `ParsedProgram` scan, then pass 1 (`ParsedProgram.BuildTextSegmentSymbols`), then pass 2 (`TextSegmentBuilder.AddLine` → `InstructionRegistry.TryParseAll`). The plan restructures that path around three ideas.

1. **Parse each line once, in pass 1.** Pass 1 produces a `ParsedLine` whose size is final. Pass 2 only resolves symbols. It can never drop or resize a line. This removes the cause of the label drift bug (#7) instead of trying to keep two parsers in sync.
2. **One operand layer.** `RegisterParser` (registers) and `Immediate.TryParseInteger` (integer literals) become the only places that interpret register tokens and numbers. `OperandParser` keeps its regex-per-shape functions, but every one of them calls these two.
3. **A two-phase multi-file build.** Phase 1 runs over all files (scan, pass 1, data, local symbols, `.globl` names). Then the global table is built. Then phase 2 (pass 2) runs for every file. This is what makes `.globl` work for `la` as well as for runtime-resolved `j`/`jal`/branches.

A prototype of all six items built cleanly. All 25,839 existing tests passed, after one adjustment described in Prototype findings. A throwaway probe of 30 cases (since deleted) confirmed each new behavior.

## Task breakdown

### 1. Lines that fail to parse shift label addresses (#7)

**Problem.** If a line fails to parse, every label after it gets the wrong address.

**Root cause.**
- Pass 1 counts a line as soon as the mnemonic is known. `InstructionRegistry.GetInstructionCount` returns `1` when `_parsers.ContainsKey(op)` is true and never checks the operands (`InstructionRegistry.cs:189`). It is called from `ParsedProgram.cs:172`.
- Pass 2 drops any line that fails to parse (`TextSegmentBuilder.cs:25-30`).
- `la` is always counted as 2 (`LaInstructionParser.cs:26-28`), but pass 2 drops it when the label does not resolve (`LaInstructionParser.cs:44-46`).
- Example: in `main: add $t0, $t1 / ... / target:`, pass 1 counts the malformed `add` but pass 2 drops it, so `target` points one instruction too far.

**Chosen design: single parse with a fixed size.**
- `InstructionRegistry.TryParseLine(line, lineNumber, out ParsedLine)` fully parses real instructions and pseudo-instructions in pass 1.
- `ParsedLine` is either:
  - `Fixed(IInstruction[])`: real instructions, `li`, `move`, `nop`.
  - `Deferred(size, SymbolExpansion)`: `la`.
- Pass 1 stores `(ParsedLine, lineIndex)` and adds `Size` to the instruction counter.
- Pass 2 calls `Materialize`. Materialize throws `InvalidOperationException` if the result length differs from `Size`, which turns any regression into a loud internal error instead of silent drift.
- `la` with an undefined symbol still emits 2 placeholder instructions (address 0) so the layout stays fixed, because forward references make symbol existence unknowable in pass 1. Pass 2 records the unresolved symbol as an error with `file:line`, and after all files are materialized `ParsedPrograms` throws `AssemblyException` with every collected message, which fails the launch (decided by the user). Branches/jumps to missing labels keep their current runtime behavior (`BranchInstruction.cs:49`, `JumpInstruction.cs:37`) and are out of scope.

**Rejected alternative.** Keep two parses but make `GetExpansionSize` validate operands. That still duplicates the logic, which is exactly what caused this bug, and it keeps the `li` hack of calling `TryExpand` with an empty `SymbolTable` (`LiInstructionParser.cs:33`).

**Policy for lines that fail to parse: skip and log a warning (default, no option yet).**
- The warning includes `file:line` and the source text, for example `fib.asm:12 Line skipped (cannot parse): ...`. The current message has neither (`TextSegmentBuilder.cs:29`).
- The logger already forwards warnings to the VS Code Debug Console during launch: the sink is installed at `DebugAdapter.cs:43-52`, and `_isInit` is set before `Load` at `DebugAdapter.cs:102-105`.
- Unsupported directives in a text segment (`.align`, `.set`, ...) also get a `file:line` warning. They are currently ignored silently (`TextSegmentBuilder.cs:20-22`).
- The todo's stated reason, skipping macros PlusPim cannot process, is served by skipping.
- **Strict mode (decided: implement, because it is simple).** CLI flag `--strict` (`Program.cs`) passed down as a `bool strict` constructor argument of `Application` / `PlusPimDbg` / `ParsedPrograms`. Users enable it with `"args": ["--strict"]` in launch.json, which already works, so no DAP or package.json change is needed (a dedicated `strict` launch property can be added later). In strict mode each skipped line and each unsupported directive adds a message to the same error list as `la` and duplicate globals, and `ParsedPrograms` throws `AssemblyException` after all files are processed. Default stays skip and warn.

**Interfaces.**
```csharp
// Instruction/Parser/ParsedLine.cs (new)
internal sealed class ParsedLine {
    public delegate IInstruction[] SymbolExpansion(ISymbolResolver symbols, out string? unresolved);
    public int Size { get; }
    public static ParsedLine Fixed(params IInstruction[] instructions);
    public static ParsedLine Deferred(int size, SymbolExpansion expansion);
    public IInstruction[] Materialize(ISymbolResolver symbols, out string? unresolved); // length == Size, or throws
}

// IPseudoInstructionParser: replaces GetExpansionSize + TryExpand
bool TryParse(string operands, int lineNumber, [MaybeNullWhen(false)] out ParsedLine line);

// InstructionRegistry
public bool TryParseLine(string assemblyLine, int lineNumber, [MaybeNullWhen(false)] out ParsedLine line);
// GetInstructionCount / TryParseAll: keep only as thin wrappers over TryParseLine
// (the existing tests use them), or delete them and migrate the tests.

// TextSegmentBuilder: replaces AddLine(string, int, SymbolTable)
public void Add(ParsedLine line, int lineNumber, ISymbolResolver symbols, string fileName);
```

**Implementation steps.**
1. Add `ParsedLine`. Change `IPseudoInstructionParser` and rewrite the four pseudo parsers.
   - `nop` now rejects operands. It used to accept anything because `GetExpansionSize` returned 1 unconditionally.
2. Add `InstructionRegistry.TryParseLine`. Reduce `GetInstructionCount`/`TryParseAll` to wrappers. `TryParseAll` keeps returning false for an unresolved `la`, so `InstructionParseTests.cs:132-139` stays valid.
3. In `ParsedProgram`, replace `BuildTextSegmentSymbols` (`ParsedProgram.cs:161-175`) with `ParseTextLines`. It registers labels, warns on directives, and parses or skips lines.
4. Make `TextSegmentBuilder` materialize only.
5. Derive `TextSegmentSize`/`KernelTextSegmentSize` (`ParsedProgram.cs:204,209`) from the pass-1 count. This is required by task 4.

**Risks.**
- Instruction objects are now created in pass 1 and are stateful (undo stacks). Each one is still created exactly once, so this is safe.
- Lines that previously "half worked" (a known mnemonic with bad operands) still disappear, but now visibly. The label drift is gone.

**Verification.** Add a file with a malformed line before a label. `ResolveFromAll(label)` must equal `GetAddressForLine(file, labelLine + 1)`. The probe confirmed this.

### 2. Out-of-range register numbers such as `$40` are accepted (#2)

**Problem.** `$40` is accepted at parse time and crashes at run time.

**Root cause.**
- `Enum.TryParse<RegisterID>(s, true, ...)` accepts any numeric string.
- There are 18 call sites. The todo says 17; the zero-compare branch parser was probably added after it was written:
  - `OperandParser.cs:47, 66-67, 99-101, 134-135, 166, 194-195, 224-225, 250, 276-277`
  - `LiInstructionParser.cs:46`
  - `LaInstructionParser.cs:39`
- The resulting `RegisterID` 40 indexes `uint[32]` at `RegisterFile.cs:17` and throws `IndexOutOfRangeException`.
- `mfc0`/`mtc0` rely on this numeric pass-through on purpose. They parse the CP0 number as a GPR and cast it (`CP0RegisterInstruction.cs:55-57`), so `mfc0 $k0, $sp` is currently accepted as CP0 register 29.

**Chosen design.** A single `RegisterParser`.
- Accepts `0`-`31` written without leading zeros (`$01` is rejected).
- Accepts exactly the names defined in `RegisterID`, case-sensitively (decided by the user: exact match). Implement with a dictionary built from `Enum.GetNames<RegisterID>()` rather than `Enum.TryParse`, so numeric strings and other casings are never accepted by accident. No extra aliases (`s8` is not added unless `RegisterID` already has it).
- `TryParseNumber` is used only for the CP0 operand.
- `OperandParser.TryParseCp0Operands` accepts only `$<number>` for the CP0 register.
- All 18 sites call `RegisterParser.TryParse`. `li`/`la` drop their private regexes and use the new `OperandParser.TryParseRegTokenOperands`.

**Interfaces.**
```csharp
internal static class RegisterParser {          // Instruction/Parser/RegisterParser.cs (new)
    public static bool TryParse(string nameWithoutDollar, out RegisterID register);
    public static bool TryParseNumber(string nameWithoutDollar, out int number); // 0-31 only
}
internal static bool TryParseCp0Operands(string operands, out RegisterID rt, out int cp0Reg);              // OperandParser
internal static bool TryParseRegTokenOperands(string operands, out RegisterID rt, [MaybeNullWhen(false)] out string token); // OperandParser
```

**Rejected alternative.** A tokenizer that rewrites every operand regex into typed operands. It would give one place for whitespace and operand forms, but it rewrites 11 functions and every instruction factory for no behavior these tasks need. The regexes already isolate the register token, so replacing the token interpreter is enough.

**Risks.**
- CP0 operands narrow from "anything the enum accepts" to numbers only. `kseg.asm` and the existing tests use `$13`/`$14`/`$n` only, and they pass.
- Case-sensitivity is a behavior change: `$T0` and `$ZERO` used to parse. Grep the tests and sample `.asm` files for uppercase register names before landing, and fix them.
- Leading-zero numbers (`$08`) are rejected (decided by the user).

**Verification.**
- Parse tests for `$31` (ok), `$32`/`$40`/`$01`/`$08` (fail), `$T0` (fail), `mfc0 $k0, $sp` (fail), `mfc0 $k0, $14` (ok).
- The existing test `bgtz $8, lbl` (`InstructionParseTests.cs:143`) still passes.

### 3. `li` does not accept hexadecimal (#7)

**Problem.** `li` rejects `0x` literals, and also every value at or above `0x80000000`.

**Root cause.**
- `int.TryParse(match.Groups["imm"].Value, null, out int imm)` at `LiInstructionParser.cs:51` does not accept `0x`.
- It also rejects 2147483648 and above.
- 16-bit immediates have their own parser, `Immediate.TryParse` (`Immediate.cs:36-80`), with three separate branches.

**Chosen design.** One literal grammar, two range checks.
- `Immediate.TryParseInteger(s, out long)` accepts an optional `+`/`-`, then decimal (up to 10 digits) or `0x`/`0X` hex (1-8 digits).
- `Immediate.TryParse` (16-bit) range-checks to `[-32768, 65535]`. This keeps the current accepted set and adds `-0x...`.
- `Immediate.TryParse32` range-checks to `[-2^31, 2^32-1]` and returns the bit pattern as `uint`.
- `li` keeps its expansion rule:
  - 1 instruction (`ori`) when the upper 16 bits are 0.
  - Otherwise 2 instructions (`lui`+`ori`).
  - Size is a pure function of the value, so pass 1 stays exact.

**Interfaces.**
```csharp
internal static bool TryParseInteger([NotNullWhen(true)] string? s, out long value);   // Immediate
public static bool TryParse32([NotNullWhen(true)] string? s, out uint value);          // Immediate
```

**Rejected alternative.** A separate `Immediate32` class. Nothing needs a type, because `li` immediately splits the value into two `Immediate`s.

**Risks.**
- The 16-bit parser is now built on the shared core. The roughly 25k exhaustive instruction tests passed unchanged, which is the main regression net.
- The `IFormatProvider` argument is now ignored (InvariantCulture).

**Verification.**
- `li $t0, 0x80000000 | 0xFFFFFFFF | 4294967295 | -1 | -2147483648` loads the expected value.
- `4294967296`, `-2147483649` and `0x100000000` are rejected.
- `0x10` expands to 1 instruction.

### 4. Cross-file label resolution with `.globl`

**Problem.** A label in one file cannot be used from another file, and `.globl` does nothing.

**Root cause.**
- `.globl` lines start with `.`, so they are ignored (`ParsedProgram.cs:171`, `TextSegmentBuilder.cs:20-22`).
- Every lookup is per-file:
  - The runtime resolver uses the symbol table of the file that contains the PC (`ParsedPrograms.cs:155-165`).
  - `la` resolves in pass 2 against the file's own table (`ParsedProgram.cs:125,133`).
- Pass 2 runs inside each file's constructor, before later files are parsed (`ParsedPrograms.cs:40`). So a global table could not exist in time for `la`.
- `main` is found by `ResolveFromAll`, which returns the first match (`PlusPimDbg.cs:40`, `ParsedPrograms.cs:171-180`).

**Chosen design.**

*Directive parsing:*
- `.globl name[, name ...]` and its alias `.global` (case-insensitive, comma- or space-separated) is recognized in the scan phase in any segment, including before the first segment directive.
- It is recorded in `ParsedProgram.GlobalDeclarations` and not forwarded to any segment.

*Two-phase build in `ParsedPrograms`:*
1. For each file, `new ParsedProgram(...)` runs the scan, pass 1, the data segment and the local symbols.
2. Base offsets come from the pass-1 counts (task 1 makes them exact).
3. `ParsedPrograms` builds `GlobalSymbols`:
   - A declared name that is not defined in the same file gets a warning.
   - The same global defined in two files is an **error**.
4. For each file, `program.Assemble(GlobalSymbols)` runs pass 2 with `ScopedSymbolResolver(local, global)`.

*Resolution order:*
- Local first, then global. This applies both in pass 2 (`la`) and in the runtime resolver (`CreateResolver`).
- `ResolveFromAll("main")` prefers the global `main`.
- Labels that are not global stay invisible to other files. This is a behavior change only in theory: today nothing resolves across files at all.

*How the error surfaces:*
- Every error is logged at `Error` level.
- Then `ParsedPrograms` throws `AssemblyException(errors)`.
- Turning that into a failed DAP launch belongs to the debugger-dap area (see Cross-area dependencies).

**Rejected alternatives for the error path.**
- Log-only, keeping the first definition. It contradicts the todo ("重複をエラーにする", "make duplicates an error").
- A diagnostics list returned through `IDebugger`. It needs interface changes in `Application`/DAP and gives the user nothing more than an exception carrying the messages.

**Rejected alternative for resolution.** Resolve every label (branches/jumps too) at assembly time. That changes the runtime instruction classes (runtime area) and is not needed. Making the runtime resolver scoped is enough.

**Interfaces.**
```csharp
internal interface ISymbolResolver { Label? Resolve(string name); }                    // Program/ISymbolResolver.cs (new)
internal sealed class ScopedSymbolResolver(SymbolTable local, SymbolTable global): ISymbolResolver;
internal sealed class AssemblyException(IReadOnlyList<string> errors): Exception { IReadOnlyList<string> Errors { get; } }
// SymbolTable : ISymbolResolver
// ParsedProgram
public IReadOnlyList<(string Name, int LineIndex)> GlobalDeclarations { get; }
public int TextInstructionCount { get; }          // from pass 1
public int KernelTextInstructionCount { get; }    // from pass 1
public void Assemble(SymbolTable globals);         // pass 2; TextSegment/KernelTextSegment throw before this
// ParsedPrograms
public SymbolTable GlobalSymbols { get; }
```

**Implementation steps.**
1. Add `ISymbolResolver`, `ScopedSymbolResolver` and `AssemblyException`. Make `SymbolTable` implement `ISymbolResolver`.
2. Recognize `.globl` in the `ParsedProgram` scan.
3. Split the `ParsedProgram` constructor at the end of pass 1. Move the pass-2 loops (`ParsedProgram.cs:120-138`) into `Assemble`.
4. In `ParsedPrograms`, compute offsets and cumulative lengths from `TextInstructionCount`/`KernelTextInstructionCount` (they currently use `program.TextSegment.Instructions.Length` at `ParsedPrograms.cs:48,50`). Build the global table, throw on errors, then call `Assemble` on every program.
5. Use the global table in `CreateResolver` and `ResolveFromAll`.

**Risks.**
- `ParsedProgram` cannot be used on its own for text any more (see Prototype findings).
- `.global` (GNU spelling) and `.extern` are not covered. Both can be added later in the same branch.
- Two files that both define a non-global `main` keep today's first-match behavior.

**Verification.**
- Two files, with `jal func` and `la $a1, shared` in A and the globals defined in B: after stepping, `$v0`, the PC and `$a1` have the expected values.
- A local definition shadows a global one.
- A non-global label in another file resolves to null.
- A duplicate global throws `AssemblyException`.

### 5. Label on the same line as an instruction (`loop: addi ...`)

**Problem.** A label written on the same line as an instruction is not recognized.

**Root cause.**
- `IsLabel` requires the whole trimmed line to end with `:` and contain no space (`ParsedProgram.cs:182-184`).
- So `loop: addi $t0, $t0, 1` reaches the instruction parser with the label still attached, and fails because `loop:` is not a mnemonic.
- The same is true in `.data`: `msg: .asciiz "x"` is passed to `DataSegmentBuilder.AddLine` and rejected as "Unexpected data segment content" (`DataSegmentBuilder.cs:62-64`).

**Chosen design.**
- In the scan phase, after comment removal, repeatedly strip leading `^(?<label>[A-Za-z_.][\w.$]*):\s*`. Several labels per line are allowed.
- Each label becomes a `SourceLine(Text, LineIndex, IsLabel: true)` in the current segment. The rest of the line becomes a normal `SourceLine` with the same line index.
- This also fixes the `.data` case at no extra cost.
- Downstream code switches from the string test `IsLabel(trimmed)` to the `IsLabel` flag, so labels are detected exactly once.
- The label pattern requires the colon directly after the name. Its first character cannot be `"`, so a colon inside a string (`.asciiz "a: b"`) is never taken as a label. `.text`/`.data` with a label in front still switch the segment, and the label stays in the previous segment.

**Interfaces.**
```csharp
private readonly record struct SourceLine(string Text, int LineIndex, bool IsLabel); // ParsedProgram
```

**Rejected alternative.** Detect inline labels inside `TextSegmentBuilder`/`InstructionRegistry`. Labels would then be split in two places, and pass 1 would need the same logic again.

**Risks.**
- Strings that used to count as labels but do not match the identifier pattern (for example `foo-bar:` or GNU numeric `1:`) now become "cannot parse" warnings.
- Breakpoints on `loop: addi` keep working: `GetAddressForLine` matches `SourceLine`, which is unchanged.

**Verification.**
- `main: addi`, `a: b: addi` and a standalone `loop:` resolve to consecutive addresses.
- `msg: .asciiz "a: b"` places `:` at `msg+1`.

### 6. Memory operand with the offset omitted (`sw $t0, ($sp)`) (#7)

**Problem.** `sw $t0, ($sp)` fails to parse.

**Root cause.** `MemoryOperandPattern` requires `(?<offset>\S+)` before `\(` (`OperandParser.cs:35`).

**Chosen design.**
- Change the pattern to `^\$(?<rt>\w+),\s*(?<offset>[^\s(]*)\(\s*\$(?<rs>\w+)\s*\)$`. An empty offset means 0 (via a private `TryParseOffset`), and spaces are allowed inside the parentheses.
- All eight `MemoryInstruction` mnemonics and the four `lwl/lwr/swl/swr` (`UnalignedMemoryInstruction.cs:79`) share `TryParseMemoryOperands`, so all of them get the new form.
- Label offsets (`lw $t0, label`) are out of scope. That is a separate pseudo-instruction.

**Risks.** Minimal. Any string the old pattern accepted is still accepted.

**Verification.** `sw $t0, ($sp)`, `lw $t0, ( $sp )` and `lw $t0, -0x4($sp)` parse, and executing `sw`/`lw` with an omitted offset uses the address in `$sp`.

## Ordering and dependencies

1. **Task 2 (RegisterParser)** and **task 3 (integer literals)** come first. They are independent leaf changes and both feed the new pseudo-parser signatures.
2. **Task 6 (memory operand)** is independent. It can go anywhere after task 2 because it touches the same function.
3. **Task 1 (single parse, fixed size)** comes next. It changes `IPseudoInstructionParser`, so `li`/`la` should already be on the shared helpers from tasks 2 and 3 to avoid rewriting them twice.
4. **Task 5 (inline labels)** comes after task 1. Both rewrite the `ParsedProgram` scan and pass 1, so do them in sequence to avoid conflicts. Task 5 introduces `SourceLine`, and pass 1 from task 1 consumes it.
5. **Task 4 (`.globl`)** comes last. It depends on task 1, because exact pass-1 sizes let file offsets be computed before pass 2. It also adds a `.globl` branch to the scan rewritten in task 5.

Each step builds and passes the existing tests on its own. The prototype did all six together, and the suite passed.

## Files expected to change

Modified:
- `PlusPim/Debuggers/PlusPimDbg/Instruction/Parser/OperandParser.cs`
- `PlusPim/Debuggers/PlusPimDbg/Instruction/Parser/Immediate.cs`
- `PlusPim/Debuggers/PlusPimDbg/Instruction/Parser/IPseudoInstructionParser.cs`
- `PlusPim/Debuggers/PlusPimDbg/Instruction/Parser/InstructionRegistry.cs` (only `GetInstructionCount`/`TryParse*`, not the registration table)
- `PlusPim/Debuggers/PlusPimDbg/Instruction/Pseudo/LiInstructionParser.cs`, `LaInstructionParser.cs`, `MoveInstructionParser.cs`, `NopInstructionParser.cs`
- `PlusPim/Debuggers/PlusPimDbg/Instruction/instructions/CP0RegisterInstruction.cs` (only `CreateParser`, lines 53-60)
- `PlusPim/Debuggers/PlusPimDbg/Program/ParsedProgram.cs`
- `PlusPim/Debuggers/PlusPimDbg/Program/ParsedPrograms.cs` (constructor, `CreateResolver`, `ResolveFromAll`)
- `PlusPim/Debuggers/PlusPimDbg/Program/TextSegmentBuilder.cs`
- `PlusPim/Debuggers/PlusPimDbg/Program/SymbolTable.cs` (adds the interface only)

New:
- `PlusPim/Debuggers/PlusPimDbg/Instruction/Parser/RegisterParser.cs`
- `PlusPim/Debuggers/PlusPimDbg/Instruction/Parser/ParsedLine.cs`
- `PlusPim/Debuggers/PlusPimDbg/Program/ISymbolResolver.cs` (`ISymbolResolver`, `ScopedSymbolResolver`, `AssemblyException`; these can be split into separate files)

Not changed: `RuntimeContext.cs` (the resolver delegate signature `Func<string, Address, bool, Label?>` is kept), the instruction execution classes, and `DataSegmentBuilder.cs`.

Tests that will need updating (the work belongs to the test tasks): `InstructionParseTests.cs` if the `GetInstructionCount`/`TryParseAll` wrappers are dropped, and `DataSegmentTests.cs:204` (`InstructionCount`).

## Cross-area dependencies

**code-quality.** It plans deletions and renames in the same files:
- `ParsedProgram.GetInstruction` (`ParsedProgram.cs:192-194`), `ParsedProgram.InstructionCount` (`:199`)
- `TextSegmentBuilder.CurrentInstructionIndex`/`CurrentAddr` (`TextSegmentBuilder.cs:33-39`)
- `Immediate.Parse` (`Immediate.cs:25-27`)
- `Address.FromInstructionIndex(…, bool)`, `Label.Invalid`/`Address.InValid`
- unifying 0-based and 1-based `lineIndex`

Suggested order:
1. Code-quality **pure deletions first**. They are small and mechanical and shrink the files this plan rewrites.
2. Then this plan.
3. Then the **`lineIndex` naming unification**, after this plan or folded into it. The new APIs here already use `lineNumber` for 1-based values (`TryParseLine`, `IPseudoInstructionParser.TryParse`). `TextSegmentBuilder.Add` and `SourceLine` still take 0-based indices; whoever lands second should convert them.

Two specific overlaps:
- `ParsedProgram.InstructionCount` is used by `DataSegmentTests.cs:204`. The prototype redirected it to the pass-1 count. If code-quality deletes it, that test must switch to `TextInstructionCount`.
- `Label.Invalid` is used by `JalInstruction.cs:13`, which this plan does not touch.

**debugger-dap.** `ParsedPrograms` will throw `AssemblyException` for duplicate globals. It propagates through `PlusPimDbg` (`PlusPimDbg.cs:36`) and `Application.Load` (`Application.cs:40-41`) to `HandleLaunchRequest` (`DebugAdapter.cs:105`). The DAP side should catch it and fail the launch with `Errors` in the message (for example by throwing a `ProtocolException`), instead of sending `stopped`/`initialized` for a debugger that does not exist. Until then, the error is logged to the Debug Console, but the launch fails in an unspecified way. Strict mode needs no launch.json change in this area: it is the `--strict` CLI flag, set through `args`.

**runtime.** No API change. Pass 2 and the runtime resolver keep `RuntimeContext`'s resolver delegate. With task 2, `RegisterFile.cs:17` can no longer be reached with an out-of-range id from source code, so a defensive check there is optional.

**vscode-extension.** The inlay hints for pseudo-instruction expansions (#11) can read expansions after pass 2 (`TextSegment.Instructions` grouped by `SourceLine`). This plan does not block that work, but `IInstruction` has no textual form yet. (The bundled default kernel handler was dropped, so nothing extra is loaded.)

## Prototype findings

**What was tried.** All six tasks were implemented in the worktree using the interfaces above (13 files modified, 3 new).
- `dotnet build`: succeeded. The only new warnings are XML-doc param mismatches in `TextSegmentBuilder`, cosmetic.
- `dotnet test`: 25,839/25,839 passed after one fix (below).
- A temporary 30-case probe passed and was then deleted. It covered label drift, register bounds, CP0 operands, memory operands, 16/32-bit literal ranges, executing `li`, inline labels in text and data, cross-file `jal`/`la`, local-over-global shadowing, invisible non-global labels, and duplicate-global errors.

**Finding 1: the two-phase split leaks into `ParsedProgram`'s standalone use.**
- `DataSegmentTests.Align_DataLabelIsResolvedByLa` builds `new ParsedProgram(...)` directly and reads `InstructionCount`, which went through `TextSegment`. That now throws, because `Assemble` has not run.
- The plan changed accordingly:
  - `ParsedProgram` exposes the pass-1 counts (`TextInstructionCount`, `KernelTextInstructionCount`).
  - Everything that needs only sizes (segment sizes, cumulative lengths, `InstructionCount`) uses them.
  - Only real access to the instructions requires `Assemble`.
- The originally sketched alternative was to keep `ParsedProgram` doing both passes and add a global-table parameter to it. It does not work, because the global table needs every file's pass 1 first.

**Finding 2: the `.globl` task depends hard on task 1, not just softly.**
- File base offsets are currently computed from pass-2 output (`TextSegmentSize` → `TextSegment.Instructions.Length`, `ParsedProgram.cs:204`; `ParsedPrograms.cs:43,48`).
- Running pass 2 after all pass 1s is only correct if the pass-1 count is exact.
- The plan's ordering was therefore tightened to "task 1 before task 4", and `Materialize` gained the size-mismatch check as an assertion.

**Finding 3: CP0 parsing relied on the register bug.**
- `mfc0`/`mtc0` take the CP0 number through the GPR parser plus a cast (`CP0RegisterInstruction.cs:55-57`).
- Fixing task 2 naively would either still accept `mfc0 $k0, $sp` or break `$12`-style operands.
- A dedicated `TryParseCp0Operands` (numbers 0-31 only) was added to the plan. Existing CP0 tests and `kseg.asm` use numbers only and pass.

**Finding 4: `TryParseAll` semantics for an unresolved `la`.**
- The existing test expects `false` (`InstructionParseTests.cs:132-139`), while the new pass 2 emits a placeholder instead.
- The compatibility wrapper keeps `false` by checking `unresolved`. The assembler path warns and emits.

**Finding 5: smaller observations.**
- Inline labels in `.data` were broken in the same way and are fixed by the same change.
- `nop` with operands is now rejected.
- `.kdata` is listed as supported in `doc/instructions.md` but is not handled by the `ParsedProgram` scan. It is outside these todo items; it is noted here so that someone picks it up.

**Environment note.** The worktree was created from `main` (`73fc28f`). It was fast-forwarded to `agent/temp` (`148383b`), only inside the worktree branch, so that the prototype and these line numbers match the code the todo describes.

## Open questions for the user

Only one remains, with a default: `mfc0`/`mtc0` operand form (numbers only, as planned, versus also names such as `$status`). Everything else was answered (see Resolved decisions).

## Reconciliation with other plans (added after review)

- debugger-dap.md must catch `AssemblyException` in launch handling (see its reconciliation section).
- vscode-extension.md found that `.ascii` / `.asciiz` truncate each UTF-16 unit to one byte (`DataSegmentBuilder.cs:244,248`), which garbles non-ASCII literals. This is not covered by the tasks above. Treat it as an additional small task: encode string literals as UTF-8. It matches the UTF-8 output decision in runtime.md.
- debugger-dap.md's inlay hints need the `li` hex fix from this plan before `li` expansions show up.
- code-quality.md orders its `TextSegmentBuilder.CurrentAddr` / `CurrentInstructionIndex` removal after the #7 fix here; confirm this plan does not need them.
