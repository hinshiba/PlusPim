using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Logging;
using Xunit;

namespace PlusPimTests;

/// <summary>
/// アセンブラ (行の解析，ラベルの配置，シンボルの解決) のテスト
/// </summary>
public class AssemblerTests {
    private static readonly Address T = TextSegment.TextSegmentBase;

    /// <summary>
    /// 一時ファイル群を<see cref="ParsedPrograms"/>へ解析した結果と，記録したログ
    /// </summary>
    private sealed class Assembled: IDisposable {
        public FileInfo[] Files { get; }
        public List<(LogLevel Level, string Message)> Logs { get; } = [];

        private ParsedPrograms? _programs;
        public ParsedPrograms Programs => this._programs ?? throw new InvalidOperationException("Not assembled");

        public Assembled(string[] sources) {
            this.Files = [.. sources.Select(TestHelpers.WriteTempAsm)];
        }

        public Assembled Build(bool strict = false) {
            Logger logger = new(LogLevel.Warning);
            logger.AddSink((level, _, message) => this.Logs.Add((level, message)));
            this._programs = new ParsedPrograms(this.Files, logger, strict);
            return this;
        }

        public Address Resolve(string name) {
            Label? label = this.Programs.ResolveFromAll(name);
            Assert.NotNull(label);
            return label.Value.Addr;
        }

        public void Dispose() {
            foreach(FileInfo file in this.Files) {
                file.Delete();
            }
        }
    }

    private static Assembled Assemble(params string[] sources) {
        Assembled assembled = new(sources);
        try {
            return assembled.Build();
        } catch {
            assembled.Dispose();
            throw;
        }
    }

    private static AssemblyException AssembleFails(params string[] sources) {
        return AssembleFails(false, sources);
    }

    private static AssemblyException AssembleFails(bool strict, params string[] sources) {
        using Assembled assembled = new(sources);
        return Assert.Throws<AssemblyException>(() => assembled.Build(strict));
    }

    // ===== 解析に失敗した行とラベルの配置 =====

    [Fact]
    public void MalformedLine_DoesNotShiftLaterLabels() {
        using Assembled a = Assemble("""
            .text
            main:
                add $t0, $t1
                li $t0, 0x10000
                foo $t0
            target:
                addi $t0, $t0, 1
            """);

        // add (解析失敗)，li (2命令)，foo (解析失敗)
        Assert.Equal(T + 8, a.Resolve("target"));
        Assert.Equal(a.Programs.GetAddressForLine(a.Files[0], 7), a.Resolve("target"));
    }

    [Fact]
    public void MalformedLine_LogsWarningWithFileAndLine() {
        using Assembled a = Assemble("""
            .text
            main:
                add $t0, $t1
                .align 2
            """);

        string name = a.Files[0].Name;
        Assert.Contains(a.Logs, log => log.Level == LogLevel.Warning && log.Message.Contains($"{name}:3") && log.Message.Contains("add $t0, $t1"));
        Assert.Contains(a.Logs, log => log.Level == LogLevel.Warning && log.Message.Contains($"{name}:4") && log.Message.Contains(".align 2"));
    }

    [Fact]
    public void Strict_SkippedLinesAndDirectivesAreErrors() {
        AssemblyException ex = AssembleFails(true, """
            .text
            main:
                add $t0, $t1
                .align 2
                addi $t0, $t0, 1
            """);

        Assert.Equal(2, ex.Errors.Count);
        Assert.Contains(":3", ex.Errors[0]);
        Assert.Contains(":4", ex.Errors[1]);
    }

    [Fact]
    public void Strict_ValidProgram_Succeeds() {
        using Assembled a = new(["""
            .text
            main:
                li $t0, 1
            .data
            d:
                .word 1
            """]);

        _ = a.Build(strict: true);

        Assert.Equal(T, a.Resolve("main"));
    }

    [Fact]
    public void Nop_WithOperands_IsRejected() {
        Assert.False(InstructionRegistry.Default.TryParseAll("nop $t0", 1, new SymbolTable(), out _));
        Assert.Equal(0, InstructionRegistry.Default.GetInstructionCount("nop $t0"));
    }

    // ===== la =====

    [Fact]
    public void La_ForwardReference_Resolves() {
        using Assembled a = Assemble("""
            .text
            main:
                la $t0, later
            later:
                nop
            """);

        Assert.Equal(T + 8, a.Resolve("later"));
    }

    [Fact]
    public void La_UndefinedLabel_ThrowsAssemblyException() {
        AssemblyException ex = AssembleFails("""
            .text
            main:
                la $t0, nowhere
                la $t1, nowhere2
            """);

        Assert.Equal(2, ex.Errors.Count);
        Assert.Contains(":3", ex.Errors[0]);
        Assert.Contains("nowhere", ex.Errors[0]);
        Assert.Contains(":4", ex.Errors[1]);
    }

    [Fact]
    public void La_UndefinedLabel_ErrorsFromAllFilesAreCollected() {
        AssemblyException ex = AssembleFails(
            """
            .text
            main:
                la $t0, nowhere
            """,
            """
            .text
                la $t0, nowhere
            """);

        Assert.Equal(2, ex.Errors.Count);
    }

    // ===== 命令と同じ行のラベル =====

    [Fact]
    public void InlineLabels_ResolveToConsecutiveAddresses() {
        using Assembled a = Assemble("""
            .text
            main: addi $t0, $t0, 1
            a: b:addi $t0, $t0, 2
            loop:
                addi $t0, $t0, 3
            c:	li $t0, 0x10000   # comment: here
            d: # only a label
                nop
            """);

        Assert.Equal(T, a.Resolve("main"));
        Assert.Equal(T + 4, a.Resolve("a"));
        Assert.Equal(T + 4, a.Resolve("b"));
        Assert.Equal(T + 8, a.Resolve("loop"));
        Assert.Equal(T + 12, a.Resolve("c"));
        Assert.Equal(T + 20, a.Resolve("d"));
        Assert.Equal(T + 4, a.Programs.GetAddressForLine(a.Files[0], 3));
        Assert.Equal(T + 12, a.Programs.GetAddressForLine(a.Files[0], 6));
    }

    [Fact]
    public void InlineLabel_InData_ColonInStringIsNotALabel() {
        using Assembled a = Assemble("""
            .data
            msg: .asciiz "a: b"
            n:	.word 7
            """);

        Address msg = a.Resolve("msg");
        Assert.Equal(DataSegment.DataSegmentBase, msg);
        Assert.Equal((byte)'a', a.Programs.MemoryImage[msg]);
        Assert.Equal((byte)':', a.Programs.MemoryImage[msg + 1]);
        Assert.Equal(DataSegment.DataSegmentBase + 8, a.Resolve("n"));
        Assert.Equal(7, a.Programs.MemoryImage[a.Resolve("n")]);
        Assert.DoesNotContain(a.Logs, log => log.Level >= LogLevel.Warning);
    }

    [Fact]
    public void InlineLabel_BeforeSegmentDirective_StaysInPreviousSegment() {
        using Assembled a = Assemble("""
            .text
            main:
                nop
            end: .data
            d: .word 1
            .text
            after: nop
            """);

        Assert.Equal(T + 4, a.Resolve("end"));
        Assert.Equal(T + 4, a.Resolve("after"));
        Assert.Equal(DataSegment.DataSegmentBase, a.Resolve("d"));
    }

    [Fact]
    public void InlineLabel_WithDollar_IsALabel() {
        using Assembled a = Assemble("""
            .text
            main: nop
            $ret: jr $ra
            """);

        Assert.Equal(T + 4, a.Resolve("$ret"));
    }

    // ===== ParsedLine =====

    [Fact]
    public void ParsedLine_SizeMismatch_Throws() {
        ParsedLine line = ParsedLine.Deferred(2, (ISymbolResolver _, out string? unresolved) => {
            unresolved = null;
            return [];
        });

        _ = Assert.Throws<InvalidOperationException>(() => line.Materialize(new SymbolTable(), out _));
    }

    [Fact]
    public void ParsedLine_La_UnresolvedKeepsSize() {
        Assert.True(InstructionRegistry.Default.TryParseLine("la $t0, nowhere", 3, out ParsedLine? line));

        IInstruction[] instructions = line.Materialize(new SymbolTable(), out string? unresolved);

        Assert.Equal("nowhere", unresolved);
        Assert.Equal(line.Size, instructions.Length);
        Assert.All(instructions, instruction => Assert.Equal(3, instruction.SourceLine));
    }
}
