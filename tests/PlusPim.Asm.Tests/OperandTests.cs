using PlusPim.Instructions;
using static PlusPim.Asm.Tests.Harness;
using static PlusPim.Instructions.GprId;

namespace PlusPim.Asm.Tests;

/// <summary>
/// 命令のオペランドと整数リテラル (docs/tests/assembler/values.md)
/// </summary>
public class OperandTests {
    private static void AssertDropped(string line, WarningCode code) {
        AssemblyResult r = Assemble(Head + line);
        r.AssertCodes(code);
        Assert.Null(r.Prog().Image.Fetch(T));
    }

    [Theory]
    [InlineData("42", "2A000000")]
    [InlineData("+42", "2A000000")]
    [InlineData("-42", "D6FFFFFF")]
    [InlineData("0x2a", "2A000000")]
    [InlineData("0X2A", "2A000000")]
    [InlineData("-0x2a", "D6FFFFFF")]
    [InlineData("0xffffffff", "FFFFFFFF")]
    [InlineData("4_2", "2A000000")]
    [InlineData("1_0", "0A000000")]
    public void IntegerLiteral_Accepted(string literal, string bytes) {
        AssemblyResult r = Assemble(Head + ".data\n.word " + literal);
        r.AssertCodes();
        Assert.Equal(bytes, r.DataAt(D));
    }

    [Theory]
    [InlineData("4294967296")]
    [InlineData("-4294967296")]
    [InlineData("0x100000000")]
    [InlineData("42_")]
    [InlineData("0x_2a")]
    [InlineData("0x")]
    [InlineData("-")]
    [InlineData("0x1g")]
    public void IntegerLiteral_Malformed_LineIsDropped(string literal) {
        AssemblyResult r = Assemble(Head + ".data\n.word " + literal);
        r.AssertCodes(WarningCode.UnparsableLine);
        Assert.Equal("", r.DataAt(D));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("0x0afe", 0x0afe)]
    [InlineData("-1", 0xffff)]
    [InlineData("-32768", 0x8000)]
    [InlineData("65535", 0xffff)]
    public void Immediate16_Accepted(string literal, int bits) {
        AssemblyResult r = Assemble(Head + "addiu $t0, $t1, " + literal);
        r.AssertCodes();
        AssertSame(I("addiu", T0, T1, Imm(bits)), r.Fetch(0));
    }

    [Theory]
    [InlineData("addi $t0, $t1, 65535", "addi $t0, $t1, -1")]
    [InlineData("andi $t0, $t1, -1", "andi $t0, $t1, 0xffff")]
    public void Immediate16_SignIsChosenByInstruction(string line, string same) {
        Assert.Equal(Assemble(Head + same).Fetch(0).Text, Assemble(Head + line).Fetch(0).Text);
    }

    [Theory]
    [InlineData("-32769")]
    [InlineData("65536")]
    public void Immediate16_OutOfRange_Fails(string literal) {
        AssemblyResult r = Assemble(Head + "addi $t0, $t1, " + literal);
        r.AssertFails(ErrorCode.ValueOutOfRange);
        Assert.Equal(literal, r.Diagnostics[0].Args.Single());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(31)]
    public void ShiftAmount_Accepted(int shamt) {
        AssemblyResult r = Assemble(Head + $"sll $t0, $t1, {shamt}");
        r.AssertCodes();
        AssertSame(I("sll", T0, T1, Imm(shamt)), r.Fetch(0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(32)]
    public void ShiftAmount_OutOfRange_LineIsDropped(int shamt) {
        AssertDropped($"sll $t0, $t1, {shamt}", WarningCode.OperandMismatch);
    }

    [Theory]
    [InlineData("4($sp)", Sp, 4)]
    [InlineData("-4($sp)", Sp, -4)]
    [InlineData("0x10($t0)", T0, 0x10)]
    [InlineData("($t0)", T0, 0)]
    [InlineData("4 ( $t0 )", T0, 4)]
    [InlineData("-32768($t0)", T0, -32768)]
    [InlineData("65535($t0)", T0, 0xffff)]
    public void Memory_Accepted(string operand, GprId baseReg, int offset) {
        AssemblyResult r = Assemble(Head + "lw $t1, " + operand);
        r.AssertCodes();
        AssertSame(I("lw", T1, new Memory(baseReg, Imm(offset))), r.Fetch(0));
    }

    [Theory]
    [InlineData("-32769($t0)")]
    [InlineData("65536($t0)")]
    public void Memory_OffsetOutOfRange_Fails(string operand) {
        Assemble(Head + "lw $t1, " + operand).AssertFails(ErrorCode.ValueOutOfRange);
    }

    [Theory]
    [InlineData("4($t0")]
    [InlineData("4(label)")]
    [InlineData("label($t0)")]
    [InlineData("4($t0)($t1)")]
    public void Memory_Malformed_LineIsDropped(string operand) {
        AssertDropped("lw $t1, " + operand, WarningCode.UnparsableLine);
    }

    [Theory]
    [InlineData("$t0", T0)]
    [InlineData("$8", T0)]
    [InlineData("$zero", Zero)]
    [InlineData("$0", Zero)]
    [InlineData("$ra", Ra)]
    [InlineData("$31", Ra)]
    [InlineData("$at", At)]
    [InlineData("$fp", Fp)]
    public void Register_Accepted(string reg, GprId id) {
        AssemblyResult r = Assemble(Head + $"add {reg}, {reg}, {reg}");
        r.AssertCodes();
        AssertSame(I("add", id, id, id), r.Fetch(0));
    }

    /// <summary>レジスタでない <c>$</c> で始まる語はラベルとして読むので, レジスタの位置では受け付けない</summary>
    [Theory]
    [InlineData("$32")]
    [InlineData("$08")]
    [InlineData("$T0")]
    [InlineData("$t10")]
    [InlineData("$s8")]
    public void Register_Unknown_LineIsDropped(string reg) {
        AssertDropped($"add $t0, $t1, {reg}", WarningCode.OperandMismatch);
    }

    [Fact]
    public void LabelRef_Forward() {
        AssemblyResult r = Assemble(Head + "j fwd\nfwd: nop");
        r.AssertCodes();
        AssertSame(I("j", T + 4), r.Fetch(0));
    }

    [Fact]
    public void LabelRef_Backward() {
        AssemblyResult r = Assemble(Head + "nop\nback: nop\nbeq $t0, $t1, back");
        r.AssertCodes();
        AssertSame(I("beq", T0, T1, T + 4), r.Fetch(2));
    }

    [Fact]
    public void LabelRef_Self() {
        AssemblyResult r = Assemble(Head + "nop\nself: j self");
        r.AssertCodes();
        AssertSame(I("j", T + 4), r.Fetch(1));
    }

    [Fact]
    public void LabelRef_Undefined_ResolvesToZero() {
        AssemblyResult r = Assemble(Head + "j nowhere\nj nowhere");
        r.AssertCodes(WarningCode.UndefinedSymbol, WarningCode.UndefinedSymbol);
        Assert.Equal(["nowhere"], r.Diagnostics[0].Args);
        Assert.Equal([4, 5], r.Diagnostics.Select(d => d.Location?.Line));
        AssertSame(I("j", new Address(0)), r.Fetch(0));
    }

    [Fact]
    public void LabelRef_Undefined_ReportedOncePerLine() {
        Assemble(Head + "la $t0, nowhere").AssertCodes(WarningCode.UndefinedSymbol);
    }

    [Fact]
    public void LabelRef_GlobalInOtherFile() {
        AssemblyResult r = Assemble(Head + "j ext", ".text\n.globl ext\next: nop");
        r.AssertCodes();
        AssertSame(I("j", T + 4), r.Fetch(0));
    }

    [Fact]
    public void LabelRef_PrivateInOtherFile_IsUndefined() {
        AssemblyResult r = Assemble(Head + "j ext", ".text\next: nop");
        r.AssertCodes(WarningCode.UndefinedSymbol);
        AssertSame(I("j", new Address(0)), r.Fetch(0));
    }

    [Fact]
    public void LabelRef_LocalShadowsGlobalOfOtherFile() {
        AssemblyResult r = Assemble(Head + "j ext\next: nop", ".text\n.globl ext\next: nop");
        r.AssertCodes();
        AssertSame(I("j", T + 4), r.Fetch(0));
    }
}
