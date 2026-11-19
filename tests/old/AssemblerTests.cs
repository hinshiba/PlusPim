using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
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

    // ===== 文字列指令のASCII判定 =====

    // ===== la =====

    // ===== 命令と同じ行のラベル =====

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

    // ===== .globl =====

    private const string CallerSource = """
        .text
        main:
            la $a1, shared
            jal func
            addi $t0, $zero, 1
        """;

    private const string CalleeSource = """
        .globl func, shared
        .text
        func:
            li $v0, 42
            jr $ra
        .data
        shared: .word 5
        """;

    // ===== ParsedLine =====

}
