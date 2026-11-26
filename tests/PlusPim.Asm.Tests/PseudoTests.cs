using PlusPim.Dbg;
using PlusPim.Image;
using PlusPim.Instructions;
using System.Buffers.Binary;
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
    [InlineData("divi $t0, $t1, 2")]
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
    [InlineData(5u, 5u)]
    [InlineData(0xfffffffbu, 5u)]
    [InlineData(0u, 0u)]
    [InlineData(0x7fffffffu, 0x7fffffffu)]
    [InlineData(0x80000001u, 0x7fffffffu)]
    [InlineData(0x80000000u, 0x80000000u)]
    public void Abs_ComputesWithoutOverflowException(uint src, uint expected) {
        foreach((string line, GprId rd) in new[] { ("abs $t0, $t1", T0), ("abs $t1, $t1", T1) }) {
            AssemblyResult r = Assemble(Head + line);
            r.AssertCodes();
            Assert.Equal(3, Span(r).Count);
            Assert.Equal(expected, r.Run(3, (T1, src), (T0, 0xdeadbeef))[(int)rd]);
        }
    }

    [Fact]
    public void Divi_WritesQuotient() {
        AssemblyResult r = Assemble(Head + "li $t1, -7\nli $t2, -2\ndivi $t0, $t1, $t2");
        r.AssertCodes();
        Assert.Equal(2, r.Prog().DebugInfo.SourceMap.PseudosIn(PathOf(0)).Last().Count);
        Assert.Equal(3u, r.Exec().Core.Registers[T0]);
    }

    /// <summary>分岐の遅延スロットが有効なら <c>li</c> は遅延スロットで実行され, 戻りアドレスはその次になる</summary>
    [Theory]
    [InlineData(DelaySlotsConfig.All, 8u, 1u)]
    [InlineData(DelaySlotsConfig.Load, 4u, 0u)]
    public void Bal_CallsLabelAndLinksRa(DelaySlotsConfig slots, uint link, uint t0) {
        AssemblyResult r = Assemble(Head + "bal f\nli $t0, 1\nf: nop");
        r.AssertCodes();
        AssertSame(I("bgezal", Zero, T + 8), r.Fetch(0));
        StateView s = r.Exec(slots);
        Assert.Equal((T + link).Addr, s.Core.Registers[Ra]);
        Assert.Equal(t0, s.Core.Registers[T0]);
        Assert.Equal(1, s.Core.Depth);
    }

    /// <summary>上位バイトの符号ビットが交互に立つ8バイト. <c>x</c>が先頭, <c>y</c>が4バイト目を指す</summary>
    private static readonly byte[] Bytes = [0x81, 0x92, 0x13, 0xa4, 0x35, 0x46, 0xd7, 0x68];
    private static readonly string Data = "\n.data\nx: .byte 0x81, 0x92, 0x13, 0xa4\ny: .byte 0x35, 0x46, 0xd7, 0x68";

    public static TheoryData<int> Alignments() => [0, 1, 2, 3];

    [Theory]
    [MemberData(nameof(Alignments))]
    public void UnalignedLoad_ReadsLittleEndian(int k) {
        ReadOnlySpan<byte> b = Bytes.AsSpan(k);
        uint w = BinaryPrimitives.ReadUInt32LittleEndian(b);
        uint h = unchecked((uint)BinaryPrimitives.ReadInt16LittleEndian(b));
        uint hu = BinaryPrimitives.ReadUInt16LittleEndian(b);
        (string Line, GprId Rt, uint Expected)[] cases = [
            ($"ulw $t0, {k}($t1)", T0, w), ($"ulw $t1, {k}($t1)", T1, w), ($"ulw $t0, {k - 4}($t2)", T0, w),
            ($"ulh $t0, {k}($t1)", T0, h), ($"ulh $t1, {k}($t1)", T1, h), ($"ulh $t0, {k - 4}($t2)", T0, h),
            ($"ulhu $t0, {k}($t1)", T0, hu), ($"ulhu $t1, {k}($t1)", T1, hu), ($"ulhu $t0, {k - 4}($t2)", T0, hu),
        ];
        // 展開はロード遅延スロットの有無によらない. 末尾のnopは展開の最後のロードの遅延スロットである
        foreach((string line, GprId rt, uint expected) in cases) {
            AssemblyResult r = Assemble(Head + "li $t0, 0xdeadbeef\nla $t1, x\nla $t2, y\n" + line + "\nnop" + Data);
            r.AssertCodes();
            foreach(DelaySlotsConfig slots in new[] { DelaySlotsConfig.All, DelaySlotsConfig.None }) {
                Assert.Equal(expected, r.Exec(slots).Core.Registers[rt]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Alignments))]
    public void UnalignedStore_WritesLittleEndian(int k) {
        foreach((string m, int n) in new[] { ("usw", 4), ("ush", 2) }) {
            AssemblyResult r = Assemble(Head + $"li $t0, 0x8899aabb\nla $t1, x\n{m} $t0, {k}($t1)" + Data);
            r.AssertCodes();
            byte[] expected = [.. Bytes];
            BitConverter.GetBytes(0x8899aabbu).AsSpan(0, n).CopyTo(expected.AsSpan(k));
            byte[] actual = new byte[8];
            r.Exec().ReadMemory(D, actual);
            Assert.Equal(expected, actual);
        }
    }

    /// <summary>展開で足すオフセット(<c>ulw</c>, <c>usw</c>は3, 半語は1)まで符号付き16bitに収まる</summary>
    [Theory]
    [InlineData("ulw $t0, 32764($t1)", true)]
    [InlineData("ulw $t0, 32765($t1)", false)]
    [InlineData("usw $t0, 32767($t1)", false)]
    [InlineData("ulh $t0, 32766($t1)", true)]
    [InlineData("ulhu $t0, 32767($t1)", false)]
    [InlineData("ush $t0, 32767($t1)", false)]
    [InlineData("ulw $t0, -32768($t1)", true)]
    [InlineData("ulw $t0, -32769($t1)", false)]
    [InlineData("ulw $t0, 0xffff($t1)", false)]
    public void Unaligned_OffsetRange(string line, bool ok) {
        AssemblyResult r = Assemble(Head + line);
        if(ok) {
            r.AssertCodes();
        } else {
            r.AssertFails(ErrorCode.ValueOutOfRange);
        }
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
