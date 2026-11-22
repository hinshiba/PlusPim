using PlusPim.Image;
using static PlusPim.Asm.Tests.Harness;
using static PlusPim.Instructions.GprId;

namespace PlusPim.Asm.Tests;

/// <summary>
/// 疑似命令 (docs/tests/assembler/values.md, docs/tests/assembler/pseudo_model.md)
/// </summary>
public class PseudoTests {
    /// <summary>先頭のファイルの唯一の疑似命令の行</summary>
    private static LineSpan Span(AssemblyResult r) {
        return Assert.Single(r.Prog().DebugInfo.SourceMap.PseudosIn(PathOf(0)));
    }

    [Theory]
    [InlineData("0x000cafe0", 0x000cafe0u, 2)]
    [InlineData("-0x7ff45420", 0x800babe0u, 2)]
    [InlineData("0", 0u, 1)]
    [InlineData("-2147483648", 0x80000000u, 2)]
    [InlineData("4294967295", 0xffffffffu, 2)]
    [InlineData("-1", 0xffffffffu, 2)]
    [InlineData("65535", 0x0000ffffu, 1)]
    [InlineData("65536", 0x00010000u, 2)]
    [InlineData("0x00010000", 0x00010000u, 2)]
    [InlineData("0xffff0000", 0xffff0000u, 2)]
    public void Li_LoadsBitsOfValue(string literal, uint value, int count) {
        AssemblyResult r = Assemble(Head + "li $t0, " + literal);
        r.AssertCodes();
        Assert.Equal(new LineSpan(new SourceLocation(PathOf(0), 4), T, count, "li"), Span(r));
        Assert.Equal(value, r.Run(count, (T0, 0xdeadbeef))[(int)T0]);
    }

    [Theory]
    [InlineData("-2147483649")]
    public void Li_OutOfRange_Fails(string literal) {
        Assemble(Head + "li $t0, " + literal).AssertFails(ErrorCode.ValueOutOfRange);
    }

    [Theory]
    [InlineData("li $t0, main")]
    [InlineData("li $t0")]
    [InlineData("li 1, $t0")]
    [InlineData("la $t0, 1")]
    [InlineData("la $t0")]
    [InlineData("la $t0, main, main")]
    [InlineData("move $t0")]
    [InlineData("move $t0, 1")]
    [InlineData("nop $t0")]
    public void WrongOperands_LineIsDropped(string line) {
        AssemblyResult r = Assemble(Head + line);
        r.AssertCodes(WarningCode.OperandMismatch);
        Assert.Null(r.Prog().Image.Fetch(T));
    }

    [Theory]
    [InlineData(".data\n.byte 1\nx: .byte 2", 0x10000001u)]
    [InlineData(".data 0x10010000\nx: .byte 2", 0x10010000u)]
    public void La_AlwaysTwoInstructions(string data, uint addr) {
        AssemblyResult r = Assemble(Head + "la $t0, x\n" + data);
        r.AssertCodes();
        Assert.Equal(2, Span(r).Count);
        Assert.Equal(addr, r.Run(2)[(int)T0]);
    }

    [Fact]
    public void La_BackwardLabel() {
        Assert.Equal((T + 4).Addr, AddressOf(Head + "nop\nback: nop", 2, "back"));
    }

    [Fact]
    public void La_GlobalInOtherFile() {
        Assert.Equal((D + 1).Addr, AddressOf(Head, 0, "x", ".globl x\n.data\n.byte 1\nx: .byte 2"));
    }

    [Fact]
    public void La_Undefined_LoadsZero() {
        AssemblyResult r = Assemble(Head + "la $t0, nowhere");
        r.AssertCodes(WarningCode.UndefinedSymbol);
        Assert.Equal(0u, r.Run(2, (T0, 0xdeadbeef))[(int)T0]);
    }

    [Fact]
    public void Move_CopiesWithoutOverflowException() {
        AssemblyResult r = Assemble(Head + "move $t0, $t1");
        r.AssertCodes();
        Assert.Equal(1, Span(r).Count);
        AssertSame(I("addu", T0, T1, Zero), r.Fetch(0));
        uint[] gpr = r.Run(1, (T1, 0x80000000));
        Assert.Equal(0x80000000u, gpr[(int)T0]);
        Assert.Equal(0x80000000u, gpr[(int)T1]);
    }

    [Fact]
    public void Nop_IsOneSll() {
        AssemblyResult r = Assemble(Head + "nop");
        r.AssertCodes();
        Assert.Equal(1, Span(r).Count);
        AssertSame(I("sll", Zero, Zero, Imm(0)), r.Fetch(0));
    }

    [Theory]
    [InlineData("neg", 5u, 0xfffffffbu)]
    [InlineData("negu", 5u, 0xfffffffbu)]
    [InlineData("not", 5u, 0xfffffffau)]
    public void Unary_Computes(string mnemonic, uint src, uint expected) {
        AssemblyResult r = Assemble(Head + mnemonic + " $t0, $t1");
        r.AssertCodes();
        Assert.Equal(expected, r.Run(1, (T1, src))[(int)T0]);
    }

    [Theory]
    [InlineData("sgt", 3u, 2u, 1u)]
    [InlineData("sgt", 2u, 2u, 0u)]
    [InlineData("sge", 2u, 2u, 1u)]
    [InlineData("sge", 1u, 2u, 0u)]
    [InlineData("sle", 2u, 2u, 1u)]
    [InlineData("sle", 3u, 2u, 0u)]
    [InlineData("seq", 7u, 7u, 1u)]
    [InlineData("seq", 7u, 8u, 0u)]
    [InlineData("sne", 7u, 8u, 1u)]
    [InlineData("sne", 7u, 7u, 0u)]
    [InlineData("sgt", 0xffffffffu, 1u, 0u)]
    [InlineData("sgtu", 0xffffffffu, 1u, 1u)]
    public void Binary_Computes(string mnemonic, uint a, uint b, uint expected) {
        AssemblyResult r = Assemble(Head + mnemonic + " $t0, $t1, $t2");
        r.AssertCodes();
        Assert.Equal(expected, r.Run(Span(r).Count, (T1, a), (T2, b), (T0, 0xdeadbeef))[(int)T0]);
    }

    [Theory]
    [InlineData("blt", "slt", false, "bne")]
    [InlineData("bgt", "slt", true, "bne")]
    [InlineData("ble", "slt", true, "beq")]
    [InlineData("bge", "slt", false, "beq")]
    [InlineData("bltu", "sltu", false, "bne")]
    [InlineData("bgtu", "sltu", true, "bne")]
    [InlineData("bleu", "sltu", true, "beq")]
    [InlineData("bgeu", "sltu", false, "beq")]
    public void CompareBranch_SetsAtThenBranches(string mnemonic, string set, bool swap, string branch) {
        AssemblyResult r = Assemble(Head + mnemonic + " $t1, $t2, skip\nskip: addu $t0, $t0, $t0");
        r.AssertCodes();
        Assert.Equal(2, Span(r).Count);
        AssertSame(I(set, At, swap ? T2 : T1, swap ? T1 : T2), r.Fetch(0));
        AssertSame(I(branch, At, Zero, T + 8), r.Fetch(1));
    }

    [Theory]
    [InlineData("b skip", "beq", 0)]
    [InlineData("beqz $t1, skip", "beq", 1)]
    [InlineData("bnez $t1, skip", "bne", 1)]
    public void ZeroBranch_ComparesWithZero(string line, string branch, int rs) {
        AssemblyResult r = Assemble(Head + line + "\nskip: nop");
        r.AssertCodes();
        AssertSame(I(branch, rs == 0 ? Zero : T1, Zero, T + 4), r.Fetch(0));
    }

    [Fact]
    public void Mul_And_Rem_ReadHiLoAfterOperation() {
        AssemblyResult r = Assemble(Head + "mul $t0, $t1, $t2\nrem $t3, $t1, $t2");
        r.AssertCodes();
        AssertSame(I("mult", T1, T2), r.Fetch(0));
        AssertSame(I("mflo", T0), r.Fetch(1));
        AssertSame(I("div", T1, T2), r.Fetch(2));
        AssertSame(I("mfhi", T3), r.Fetch(3));
    }

    [Theory]
    [InlineData("blt $t0, $t1")]
    [InlineData("blt $t0, $t1, 1")]
    [InlineData("b")]
    [InlineData("beqz skip")]
    [InlineData("sgt $t0, $t1")]
    [InlineData("clear 1")]
    public void NewPseudos_WrongOperands_LineIsDropped(string line) {
        Assemble(Head + line + "\nskip: nop").AssertCodes(WarningCode.OperandMismatch);
    }

    [Fact]
    public void RealInstruction_IsNotPseudo() {
        AssemblyResult r = Assemble(Head + "addu $t0, $t1, $zero");
        Assert.Empty(r.Prog().DebugInfo.SourceMap.PseudosIn(PathOf(0)));
    }

    [Fact]
    public void LabelAfterExpansion_CountsEveryInstruction() {
        Assert.Equal((T + 12).Addr, AddressOf(Head + "li $t1, 0x10000\nnop\nafter: nop", 4, "after"));
    }
}
