using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;
using Xunit;

namespace PlusPimTests;

public class DataSegmentTests {

    private static readonly Address Base = DataSegment.DataSegmentBase;

    /// <summary>
    /// アセンブリ文字列をParsedProgramへ解析する
    /// </summary>
    private static ParsedProgram Parse(string asm) {
        FileInfo file = TestHelpers.WriteTempAsm(asm);
        try {
            return new ParsedProgram(file, TextSegment.TextSegmentBase, Base, TextSegment.KernelTextSegmentBase, Logger.Null);
        } finally {
            file.Delete();
        }
    }

    private static Address Resolve(ParsedProgram program, string name) {
        Label? label = program.SymbolTable.Resolve(name);
        Assert.NotNull(label);
        return label.Value.Addr;
    }

    [Fact]
    public void Align_AfterLabel_AlignsLabelAddress() {
        // 報告された再現コード
        ParsedProgram program = Parse("""
            .data
            dialog:
                .align  2
                .asciiz "The factorial 10 is "

            msg:
                .align  2
                .asciiz "Multiplying this by 5 and adding 2116516 gives "
            endl:
                .align  2
                .asciiz "\n"
            """);

        Assert.Equal(Base, Resolve(program, "dialog"));
        // "The factorial 10 is " は20文字+NULで21バイト．次のワード境界は+0x18
        Assert.Equal(Base + 0x18, Resolve(program, "msg"));

        foreach(string name in new[] { "dialog", "msg", "endl" }) {
            Assert.Equal(0, Resolve(program, name) % 4);
        }
    }

    [Fact]
    public void Align_LabelPointsToActualData() {
        ParsedProgram program = Parse("""
            .data
            head:
                .asciiz "abc"
            tail:
                .align  2
                .asciiz "XYZ"
            """);

        Address tail = Resolve(program, "tail");
        Assert.Equal(Base + 4, tail);
        Assert.Equal((byte)'X', program.DataSegment.MemoryImage[tail]);
        Assert.Equal((byte)'Y', program.DataSegment.MemoryImage[tail + 1]);
        Assert.Equal((byte)'Z', program.DataSegment.MemoryImage[tail + 2]);
        Assert.Equal((byte)0, program.DataSegment.MemoryImage[tail + 3]);
    }

    [Fact]
    public void Word_ImplicitAlignment_AlignsLabelAddress() {
        ParsedProgram program = Parse("""
            .data
            x:
                .byte   1
            y:
                .word   5
            """);

        Assert.Equal(Base, Resolve(program, "x"));

        Address y = Resolve(program, "y");
        Assert.Equal(Base + 4, y);
        Assert.Equal((byte)5, program.DataSegment.MemoryImage[y]);
        Assert.Equal((byte)0, program.DataSegment.MemoryImage[y + 1]);
    }

    [Fact]
    public void Space_LabelPointsToStartOfReservedArea() {
        ParsedProgram program = Parse("""
            .data
            buf:
                .space  8
            after:
                .byte   7
            """);

        Assert.Equal(Base, Resolve(program, "buf"));

        Address after = Resolve(program, "after");
        Assert.Equal(Base + 8, after);
        Assert.Equal((byte)7, program.DataSegment.MemoryImage[after]);
    }

    [Fact]
    public void ConsecutiveLabels_ShareSameAddress() {
        ParsedProgram program = Parse("""
            .data
            pad:
                .byte   1
            first:
            second:
                .align  2
                .asciiz "hi"
            """);

        Assert.Equal(Base + 4, Resolve(program, "first"));
        Assert.Equal(Resolve(program, "first"), Resolve(program, "second"));
    }

    [Fact]
    public void WithoutAlign_LabelsStayContiguous() {
        ParsedProgram program = Parse("""
            .data
            a:
                .asciiz "ab"
            b:
                .asciiz "c"
            """);

        Assert.Equal(Base, Resolve(program, "a"));
        // "ab"+NULで3バイト．整列指定がなければ詰めて配置される
        Assert.Equal(Base + 3, Resolve(program, "b"));
    }

    [Fact]
    public void TrailingLabel_PointsToEndOfDataSegment() {
        ParsedProgram program = Parse("""
            .data
            a:
                .asciiz "ab"
            end:
            """);

        Assert.Equal(Base + 3, Resolve(program, "end"));
        Assert.Equal(3u, program.DataSegment.Size);
    }

    [Fact]
    public void Align_LaThenLoad_ReadsAlignedData() {
        // シンボル解決からlaの符号化，メモリイメージ，lwまでを通しで確認する
        // 修正前はmsgが0x10000001を指すためlwがAdELを起こす
        string asm = """
            .data
            pad:
                .byte   1
            msg:
                .align  2
                .asciiz "AB"
            .text
            main:
                la      $a0,    msg
                lw      $t0,    0($a0)
            """;
        (PlusPim.Debuggers.PlusPimDbg.PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            // laはlui/oriの2命令に展開される
            _ = debugger.Step();
            _ = debugger.Step();
            _ = debugger.Step();

            (uint[] regs, uint _, uint _, uint _) = debugger.GetRegisters();
            Assert.Equal((Base + 4).Addr, regs[(int)RegisterID.A0]);
            // リトルエンディアンで 'A', 'B', NUL, 未書き込み(0)
            Assert.Equal(0x00004241u, regs[(int)RegisterID.T0]);
            Assert.Null(debugger.GetLastException());
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void Align_DataLabelIsResolvedByLa() {
        // パス2の命令パースより前にデータラベルが確定していることの確認
        ParsedProgram program = Parse("""
            .data
            pad:
                .byte   1
            msg:
                .align  2
                .asciiz "hi"
            .text
                la      $a0,    msg
            """);

        Assert.Equal(Base + 4, Resolve(program, "msg"));
        // la は lui/ori の2命令に展開される
        Assert.Equal(2, program.InstructionCount);
    }

    [Fact]
    public void Half_WritesLittleEndian() {
        ParsedProgram program = Parse("""
            .data
            x:
                .half   0x1234
            """);

        Address x = Resolve(program, "x");
        Assert.Equal(Base, x);
        Assert.Equal((byte)0x34, program.DataSegment.MemoryImage[x]);
        Assert.Equal((byte)0x12, program.DataSegment.MemoryImage[x + 1]);
        Assert.Equal(2u, program.DataSegment.Size);
    }

    [Fact]
    public void Half_ImplicitAlignment_AlignsLabelAddress() {
        ParsedProgram program = Parse("""
            .data
            pad:
                .byte   1
            x:
                .half   -1
            """);

        Assert.Equal(Base, Resolve(program, "pad"));

        // .halfは2バイト境界へ整列されるので0x10000001ではなく0x10000002
        Address x = Resolve(program, "x");
        Assert.Equal(Base + 2, x);
        Assert.Equal((byte)0xFF, program.DataSegment.MemoryImage[x]);
        Assert.Equal((byte)0xFF, program.DataSegment.MemoryImage[x + 1]);
    }

    [Fact]
    public void Half_MultipleValues_PlacedContiguously() {
        ParsedProgram program = Parse("""
            .data
            tbl:
                .half   1, 2, 0x0304
            """);

        Address tbl = Resolve(program, "tbl");
        Assert.Equal(Base, tbl);
        Assert.Equal((byte)0x01, program.DataSegment.MemoryImage[tbl]);
        Assert.Equal((byte)0x00, program.DataSegment.MemoryImage[tbl + 1]);
        Assert.Equal((byte)0x02, program.DataSegment.MemoryImage[tbl + 2]);
        Assert.Equal((byte)0x00, program.DataSegment.MemoryImage[tbl + 3]);
        Assert.Equal((byte)0x04, program.DataSegment.MemoryImage[tbl + 4]);
        Assert.Equal((byte)0x03, program.DataSegment.MemoryImage[tbl + 5]);
        Assert.Equal(6u, program.DataSegment.Size);
    }

    [Fact]
    public void Half_LhReadsSignExtendedValue() {
        // .half の配置から lh/lhu の符号拡張までを通しで確認する
        string asm = """
            .data
            tbl:
                .half   0x1234, -1
            .text
            main:
                la      $a0,    tbl
                lh      $t0,    0($a0)
                lh      $t1,    2($a0)
                lhu     $t2,    2($a0)
            """;
        (PlusPim.Debuggers.PlusPimDbg.PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            // laはlui/oriの2命令に展開される
            for(int i = 0; i < 5; i++) {
                _ = debugger.Step();
            }

            (uint[] regs, uint _, uint _, uint _) = debugger.GetRegisters();
            Assert.Equal(0x00001234u, regs[(int)RegisterID.T0]);
            Assert.Equal(0xFFFFFFFFu, regs[(int)RegisterID.T1]);
            Assert.Equal(0x0000FFFFu, regs[(int)RegisterID.T2]);
            Assert.Null(debugger.GetLastException());
        } finally {
            tempFile.Delete();
        }
    }
}
