using PlusPim.Image;
using PlusPim.Instructions;
using static PlusPim.Asm.Tests.Harness;

namespace PlusPim.Asm.Tests;

/// <summary>
/// セグメント指令とセクションの配置 (docs/tests/assembler/values.md)
/// </summary>
public class SegmentTests {
    private static object[] Errors(AssemblyResult r) {
        return [.. r.Codes().OfType<ErrorCode>().Cast<object>()];
    }

    [Theory]
    [InlineData(".text", 0x00400000u)]
    [InlineData(".ktext", 0x80000180u)]
    [InlineData(".text 0x00400100", 0x00400100u)]
    [InlineData(".ktext 0x80000000", 0x80000000u)]
    public void Text_PlacedAt(string directive, uint addr) {
        AssemblyResult r = Assemble($".globl main\n{directive}\nmain: nop");
        r.AssertCodes();
        Assert.Equal(new SourceLocation(PathOf(0), 3), r.Prog().DebugInfo.SourceMap.LocationOf(new Address(addr)));
    }

    [Theory]
    [InlineData(".data", 0x10000000u)]
    [InlineData(".data 0x10000100", 0x10000100u)]
    public void Data_PlacedAt(string directive, uint addr) {
        AssemblyResult r = Assemble(Head + $"{directive}\n.byte 1");
        r.AssertCodes();
        Assert.Equal("01", r.DataAt(new Address(addr)));
    }

    [Fact]
    public void Resume_ContinuesPreviousSectionOfSameKind() {
        AssemblyResult r = Assemble(Head + "nop\n.data\n.byte 1\n.text\nnop\n.data\n.byte 2");
        r.AssertCodes();
        Assert.Equal(new SourceLocation(PathOf(0), 8), r.Prog().DebugInfo.SourceMap.LocationOf(T + 4));
        Assert.Equal("01", r.DataAt(D));
        Assert.Equal("02", r.DataAt(D + 1));
    }

    [Theory]
    [InlineData(".data -1")]
    public void Address_OutOfRange_Fails(string directive) {
        Assemble(Head + directive).AssertFails(ErrorCode.ValueOutOfRange);
    }

    [Theory]
    [InlineData(".text 0")]
    [InlineData(".data 0")]
    [InlineData(".data 0xffffffff")]
    [InlineData(".ktext 0xfffffffc")]
    public void Address_OutsideSegment_Fails(string directive) {
        AssemblyResult r = Assemble(Head + directive);
        r.AssertFails(ErrorCode.SectionOutOfRange);
        Assert.Equal(4, r.Diagnostics[0].Location?.Line);
    }

    /// <summary>セクションがセグメントからはみ出すと失敗する</summary>
    /// <param name="directive">セクションを始める指令</param>
    /// <param name="body">セクションの中身</param>
    /// <param name="fits">セグメントに収まるか</param>
    [Theory]
    [InlineData(".text 0x00400000", "nop", true)]
    [InlineData(".text 0x003ffffc", "nop", false)]
    [InlineData(".text 0x0ffffffc", "nop", true)]
    [InlineData(".text 0x0ffffffc", "nop\nnop", false)]
    [InlineData(".data 0x10000000", ".byte 1", true)]
    [InlineData(".data 0x0fffffff", ".byte 1", false)]
    [InlineData(".data 0x7fffffff", ".byte 1", true)]
    [InlineData(".data 0x7fffffff", ".byte 1, 2", false)]
    [InlineData(".ktext 0x80000000", "nop", true)]
    [InlineData(".ktext 0x7ffffffc", "nop", false)]
    [InlineData(".ktext 0x8ffffffc", "nop", true)]
    [InlineData(".ktext 0x8ffffffc", "nop\nnop", false)]
    public void Section_SegmentBounds(string directive, string body, bool fits) {
        AssemblyResult r = Assemble($"{directive}\n{body}");
        Assert.Equal(fits ? [] : [ErrorCode.SectionOutOfRange], Errors(r));
        Assert.Equal(fits, r.Program is not null);
    }

    [Theory]
    [InlineData(".text", 0x00400100u, true)]
    [InlineData(".text", 0x00400101u, false)]
    [InlineData(".text", 0x00400102u, false)]
    [InlineData(".text", 0x00400103u, false)]
    [InlineData(".ktext", 0x80000101u, false)]
    [InlineData(".data", 0x10000101u, true)]
    public void Address_TextMustBeWordAligned(string directive, uint addr, bool aligned) {
        AssemblyResult r = Assemble(Head + $"nop\n{directive} 0x{addr:x8}");
        Assert.Equal(aligned ? [] : [ErrorCode.MisalignedText], Errors(r));
        if(!aligned) {
            Assert.Equal($"0x{addr:X}", r.Diagnostics[0].Args.Single());
        }
    }

    /// <summary>前のセクションと重なる配置は失敗する</summary>
    /// <param name="first">先に置くセクション. テキストは8バイト, データは2バイト</param>
    /// <param name="directive">後のセクションを始める指令</param>
    /// <param name="body">後のセクションの中身</param>
    /// <param name="disjoint">2つのセクションが重ならないか</param>
    [Theory]
    [InlineData("nop\nnop", ".text 0x00400008", "nop", true)]
    [InlineData("nop\nnop", ".text 0x00400004", "nop", false)]
    [InlineData(".data\n.byte 1, 2", ".data 0x10000002", ".byte 3", true)]
    [InlineData(".data\n.byte 1, 2", ".data 0x10000001", ".byte 3", false)]
    public void Section_OverlapWithPrevious(string first, string directive, string body, bool disjoint) {
        AssemblyResult r = Assemble(Head + $"{first}\n{directive}\n{body}");
        Assert.Equal(disjoint ? [] : [ErrorCode.SectionOverlap], Errors(r));
        Assert.Equal(disjoint, r.Program is not null);
    }

    /// <summary>重なりの診断は後のセクションに付き, 先のセクションの場所を引数に持つ</summary>
    [Fact]
    public void Section_OverlapAcrossFiles_NamesBothLocations() {
        AssemblyResult r = Assemble(Head + "nop", ".text 0x00400000\nnop");
        r.AssertFails(ErrorCode.SectionOverlap);
        Diagnostic d = Assert.Single(r.Diagnostics);
        Assert.Equal(new SourceLocation(PathOf(1), 1), d.Location);
        Assert.Equal(["0x400000", PathOf(0).Value, "1"], d.Args);
    }
}
