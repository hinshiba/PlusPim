using PlusPim.Image;
using PlusPim.Instructions;
using static PlusPim.Asm.Tests.Harness;
using static PlusPim.Instructions.GprId;

namespace PlusPim.Asm.Tests;

/// <summary>
/// ソースファイル, 行, オペランドの区切り, 指令名, <c>.globl</c>, エントリ (docs/tests/assembler/values.md)
/// </summary>
public class SourceTests {
    private static SourceLocation? LocationOf(AssemblyResult r, Address addr) {
        return r.Prog().DebugInfo.SourceMap.LocationOf(addr);
    }

    [Fact]
    public void OneFile_AssemblesWithoutDiagnostics() {
        AssemblyResult r = Assemble(Head + "nop");
        r.AssertCodes();
        Assert.Equal(T, r.Prog().Image.Entry);
    }

    [Fact]
    public void TwoFiles_TextOfSecondContinuesAfterFirst() {
        AssemblyResult r = Assemble(Head + "nop", ".text\nnop");
        r.AssertCodes();
        Assert.Equal(new SourceLocation(PathOf(1), 2), LocationOf(r, T + 4));
    }

    [Fact]
    public void EmptyFile_ReportsOnlyMissingEntry() {
        AssemblyResult r = Assemble("");
        r.AssertCodes(WarningCode.MissingEntry);
        Assert.Equal(T, r.Prog().Image.Entry);
    }

    [Fact]
    public void SameFileTwice_SecondIsIgnored() {
        SourcePath p = PathOf(0);
        AssemblyResult r = Assemble([p, p], new Dictionary<SourcePath, string> { [p] = Head + "nop" });
        r.AssertCodes(WarningCode.DuplicateSource);
        Assert.Null(r.Diagnostics[0].Location);
        Assert.Null(r.Prog().Image.Fetch(T + 4));
    }

    [Fact]
    public void UnreadableFile_Fails() {
        AssemblyResult r = Assemble([PathOf(0)], new Dictionary<SourcePath, string>());
        r.AssertFails(ErrorCode.SourceNotFound);
    }

    [Fact]
    public void TrailingComment_IsIgnored() {
        AssemblyResult r = Assemble(Head + "addi $t0, $t1, 1 # addi $t0, $t1, 2");
        r.AssertCodes();
        AssertSame(I("addi", T0, T1, Imm(1)), r.Fetch(0));
    }

    [Fact]
    public void CommentOnlyAndEmptyLines_KeepLineNumbers() {
        AssemblyResult r = Assemble(Head + "# comment\n\n   \nnop");
        r.AssertCodes();
        Assert.Equal(new SourceLocation(PathOf(0), 7), LocationOf(r, T));
    }

    [Fact]
    public void HashInString_IsNotComment() {
        AssemblyResult r = Assemble(Head + ".data\n.asciiz \"a#b\"");
        r.AssertCodes();
        Assert.Equal("61236200", r.DataAt(D));
    }

    [Fact]
    public void Crlf_SameAsLf() {
        const string lf = Head + "addi $t0, $t1, 1\nl: j l\n.data\n.asciiz \"ab\"\nfoo";
        AssemblyResult a = Assemble(lf);
        AssemblyResult b = Assemble(lf.Replace("\n", "\r\n"));
        Assert.Equal(a.Diagnostics, b.Diagnostics, (x, y) => x.Code.Value!.Equals(y.Code.Value) && x.Location == y.Location && x.Args.SequenceEqual(y.Args));
        Assert.Equal(a.DataAt(D), b.DataAt(D));
        for(int i = 0; i < 2; i++) {
            Assert.Equal(a.Fetch(i).Text, b.Fetch(i).Text);
            Assert.Equal(LocationOf(a, T + (uint)(i * 4)), LocationOf(b, T + (uint)(i * 4)));
        }
    }

    [Fact]
    public void LineBeforeSegment_IsText() {
        AssemblyResult r = Assemble(".globl main\nmain: nop");
        r.AssertCodes(WarningCode.MissingSegment);
        Assert.Equal(new SourceLocation(PathOf(0), 2), r.Diagnostics[0].Location);
        AssertSame(I("sll", Zero, Zero, Imm(0)), r.Fetch(0));
    }

    [Theory]
    [InlineData("add $t0, $t1, $t2")]
    [InlineData("add $t0,$t1,$t2")]
    [InlineData("add\t$t0 ,\t$t1 , $t2")]
    public void Separator_Accepted(string line) {
        AssemblyResult r = Assemble(Head + line);
        r.AssertCodes();
        AssertSame(I("add", T0, T1, T2), r.Fetch(0));
    }

    [Theory]
    [InlineData("add $t0 $t1 $t2")]
    [InlineData("add $t0, $t1, $t2,")]
    [InlineData("add , $t0, $t1, $t2")]
    [InlineData("add $t0,, $t1, $t2")]
    public void Separator_Rejected(string line) {
        AssemblyResult r = Assemble(Head + line);
        r.AssertCodes(WarningCode.UnparsableLine);
        Assert.Equal(new SourceLocation(PathOf(0), 4), r.Diagnostics[0].Location);
        Assert.Null(r.Prog().Image.Fetch(T));
    }

    [Theory]
    [InlineData(".foo")]
    [InlineData(".TEXT")]
    [InlineData(".Data")]
    public void UnknownDirective_LineIsDropped(string directive) {
        AssemblyResult r = Assemble(Head + directive + "\nnop");
        r.AssertCodes(WarningCode.UnknownDirective);
        Assert.Equal(directive, r.Diagnostics[0].Args.Single());
        AssertSame(I("sll", Zero, Zero, Imm(0)), r.Fetch(0));
    }

    [Theory]
    [InlineData(".globl")]
    [InlineData(".global")]
    public void Globl_ExportsEveryName(string directive) {
        uint a = AddressOf(Head + "nop", 1, "a", $".data\n{directive} a, b\n.byte 1\na: .byte 2\nb: .byte 3");
        uint b = AddressOf(Head + "nop", 1, "b", $".data\n{directive} a, b\n.byte 1\na: .byte 2\nb: .byte 3");
        Assert.Equal((D + 1).Addr, a);
        Assert.Equal((D + 2).Addr, b);
    }

    [Theory]
    [InlineData("1", "1")]
    [InlineData("\"s\"", "s")]
    public void Globl_NonName_IsDropped(string value, string arg) {
        AssemblyResult r = Assemble($".globl {value}, main\n" + Head + "nop");
        r.AssertCodes(WarningCode.DiscardedOperand);
        Assert.Equal(arg, r.Diagnostics[0].Args.Single());
        Assert.Equal(T, r.Prog().Image.Entry);
    }

    [Fact]
    public void Globl_UndefinedInFile_IsDropped() {
        AssemblyResult r = Assemble(Head + "nop\n.globl x", ".data\nx: .byte 1");
        r.AssertCodes(WarningCode.UndefinedGlobal);
        Assert.Equal(new SourceLocation(PathOf(0), 5), r.Diagnostics[0].Location);
    }

    /// <summary>入力を飛ばした後の失敗でも, 診断の場所は入力のパスを指す</summary>
    [Fact]
    public void Diagnostic_AfterSkippedSource_PointsToInputPath() {
        SourcePath ok = PathOf(0), missing = PathOf(1), bad = PathOf(2);
        AssemblyResult r = Assemble([ok, missing, ok, bad], new Dictionary<SourcePath, string> { [ok] = Head + "nop", [bad] = ".data\n.byte 256" });
        Assert.Null(r.Program);
        Assert.Equal(new SourceLocation(bad, 2), r.Diagnostics.Single(d => d.Code.Value is ErrorCode.ValueOutOfRange).Location);
    }

    [Fact]
    public void Globl_SameNameFromTwoFiles_LaterWins() {
        const string first = ".globl x\n.data\nx: .byte 1";
        const string second = ".globl x\n.data\nx: .byte 2";
        AssemblyResult r = Assemble(Head + "la $t0, x", first, second);
        r.AssertCodes(WarningCode.DuplicateGlobal);
        Assert.Equal(new SourceLocation(PathOf(2), 1), r.Diagnostics[0].Location);
        Assert.Equal((D + 1).Addr, r.Run(2)[(int)T0]);
    }

    [Fact]
    public void Entry_GlobalMain() {
        AssemblyResult r = Assemble(".text\n.globl main\nnop\nmain: nop");
        r.AssertCodes();
        Assert.Equal(T + 4, r.Prog().Image.Entry);
    }

    [Theory]
    [InlineData(".text\nnop\nmain: nop")]
    [InlineData(".text\nnop\nnop")]
    public void Entry_WithoutGlobalMain_StartsAtTextHead(string source) {
        AssemblyResult r = Assemble(source);
        r.AssertCodes(WarningCode.MissingEntry);
        Assert.Null(r.Diagnostics[0].Location);
        Assert.Equal(T, r.Prog().Image.Entry);
    }

    [Fact]
    public void Entry_WithoutGlobalMain_StartsAtLowestTextSection() {
        AssemblyResult r = Assemble(".text 0x00400100\nnop\n.ktext\nnop");
        r.AssertCodes(WarningCode.MissingEntry);
        Assert.Equal(new Address(0x00400100), r.Prog().Image.Entry);
    }
}
