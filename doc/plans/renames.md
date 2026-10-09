# Planned renames (process first)

Source: `code-quality.md`, section "Naming". References are to commit 148383b (`agent/temp`). The renames are processed before the feature work, so the other plans build on the final names. If a feature branch already exists, process them last instead.

Common rules:
- One commit per rename group, on an `agent/...` branch, with `dotnet build` at 0 warnings, `dotnet test` green and `dotnet format --verify-no-changes PlusPim.slnx` clean.
- Use whole-word replacement, and cover `PlusPim/`, `PlusPimTests/` and any benchmark project.

| # | From | To | Reason | Scope and notes |
| --- | --- | --- | --- | --- |
| 1 | `Address.InValid` (`Address.cs:77`) | `Address.Invalid` | Inconsistent capitalization. `Label.Invalid` (`Label.cs:13`) and `InstructionIndex.Invalid` (`InstructionIndex.cs:59`) already use `Invalid`. | Production uses: `CP0RegisterFile.cs:30`, `Label.cs:13`, `BranchInstruction.cs:49`, `JumpInstruction.cs:37`. No test uses. The value stays `new(0)`, so behavior is unchanged. |
| 2 | Namespace and folder `Instruction/instructions` (and `.Factories`, `.Jump`) | `Instruction/Instructions` | C# namespaces are PascalCase. `.editorconfig:59` has `dotnet_style_namespace_match_folder = true`, so folder and namespace must change together. | About 73 replacements in 57 files. On Windows a direct `git mv` fails ("Invalid argument"); rename through a temporary name in two steps so the index records the case change for Linux CI. Run `dotnet format PlusPim.slnx` afterwards, because the case change re-orders `using` lines and CI fails otherwise. |
| 3 | Namespace and folder `Program/records` | `Program/Records` | Same as #2. | Same procedure as #2. |
| 4 | `lineIndex` (mixed 0-based and 1-based), 95 occurrences in 30 files | `lineNumber`, 1-based everywhere | One name currently means two things: 0-based in `ParsedProgram`/`TextSegmentBuilder`/`DataSegmentBuilder`, 1-based after `TextSegmentBuilder.cs:25` adds 1. `IInstruction.SourceLine`, `IDebugger.SetBreakpoints`, DAP stack frames and `doc/assembler.md:21` are already 1-based, so 1-based is canonical. | Two commits. A (semantic, 3 files): the reader loop in `ParsedProgram.cs:54-56` counts from 1 with pre-increment, drop `lineIndex + 1` conversions, rename the tuple field `LineIndex` to `LineNumber`, and drop the `+ 1` at `TextSegmentBuilder.cs:25`. B (mechanical): rename the remaining occurrences. Prerequisite: a characterization test that pins the exact stack-frame `Line` after each step and `SetBreakpoints` `Verified` for blank, label and pseudo-instruction lines (test owner), because `IntegrationTests.Step_GetCurrentLine_MatchesSourceLine` only checks that lines increase. `IInstruction.SourceLine` keeps its name. |
| 5 | `_debugger_` (`Application.cs:12-13,41,54,68,72`) | `_debugger` | Trailing underscores are not the project convention. | `inst_` and `maxLength_` from the todo no longer exist on this branch. |
| 6 | `exc_` (`PlusPimDbg.cs:160-161`) | `lastException` | Same as #5. The name also says what the value is. | Local variable. |
| 7 | `addr_` (`PlusPimDbg.cs:200-201`) | `lineAddr` | Same as #5. | Local variable. |

Done when `rg "\b[a-z]\w*_\b" --glob "*.cs"` returns nothing and no `lineIndex`, `InValid`, `.instructions` or `.records` remains.

Not renamed: `IInstruction.SourceLine` (interface stability), `Label.Invalid` and `InstructionIndex.Invalid` (already correct).
