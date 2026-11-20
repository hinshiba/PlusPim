using static PlusPim.Asm.Tests.Harness;
using static PlusPim.Instructions.GprId;

namespace PlusPim.Asm.Tests;

/// <summary>
/// ラベル定義 (docs/tests/assembler/values.md)
/// </summary>
public class LabelTests {
    [Theory]
    [InlineData("_loop")]
    [InlineData(".L1")]
    [InlineData("a$b")]
    [InlineData("$x")]
    [InlineData("$32")]
    [InlineData("    indented")]
    public void Name_Accepted(string name) {
        AssemblyResult r = Assemble(Head + $"nop\n{name}: nop\nla $t0, {name.Trim()}");
        r.AssertCodes();
        Assert.Equal((T + 4).Addr, r.Run(4)[(int)T0]);
    }

    [Fact]
    public void Name_StartingWithDigit_LineIsDropped() {
        AssemblyResult r = Assemble(Head + "1abc: nop");
        r.AssertCodes(WarningCode.UnparsableLine);
        Assert.Null(r.Prog().Image.Fetch(T));
    }

    /// <summary>レジスタと同じ綴りはレジスタとして読むので, ラベルとして参照できない</summary>
    [Theory]
    [InlineData("$t0")]
    [InlineData("$31")]
    public void Name_SpelledAsRegister_CannotBeReferenced(string name) {
        AssemblyResult r = Assemble(Head + $"{name}: nop\nj {name}");
        r.AssertCodes(WarningCode.OperandMismatch);
    }

    [Fact]
    public void Name_CaseSensitive() {
        AssemblyResult r = Assemble(Head + "Loop: nop\nloop: nop\nla $t0, Loop\nla $t1, loop");
        r.AssertCodes();
        uint[] gpr = r.Run(6);
        Assert.Equal(T.Addr, gpr[(int)T0]);
        Assert.Equal((T + 4).Addr, gpr[(int)T1]);
    }

    [Fact]
    public void SeveralOnOneLine_ShareAddress() {
        AssemblyResult r = Assemble(Head + "nop\na: b: c: nop\nla $t0, a\nla $t1, b\nla $t2, c");
        r.AssertCodes();
        uint[] gpr = r.Run(8);
        Assert.All([gpr[(int)T0], gpr[(int)T1], gpr[(int)T2]], a => Assert.Equal((T + 4).Addr, a));
    }

    [Fact]
    public void LabelOnlyLine_PointsToNextPlacement() {
        AssemblyResult r = Assemble(Head + "nop\nl:\n\n# c\nnop\nla $t0, l");
        r.AssertCodes();
        Assert.Equal((T + 4).Addr, r.Run(4)[(int)T0]);
    }

    [Fact]
    public void LabelsOfDroppedLine_AreDropped() {
        AssemblyResult r = Assemble(Head + "l: foo\nnop\nla $t0, l");
        r.AssertCodes(WarningCode.UnknownMnemonic, WarningCode.UndefinedSymbol);
    }

    [Theory]
    [InlineData("x: nop\nx: nop", 8u)]
    [InlineData("x: x: nop\nnop", 4u)]
    public void Redefinition_LaterWins(string body, uint offset) {
        AssemblyResult r = Assemble(Head + "nop\n" + body + "\nla $t0, x");
        r.AssertCodes(WarningCode.DuplicateLabel);
        Assert.Equal(["x"], r.Diagnostics[0].Args);
        Assert.Equal((T + offset).Addr, r.Run(5)[(int)T0]);
    }
}
