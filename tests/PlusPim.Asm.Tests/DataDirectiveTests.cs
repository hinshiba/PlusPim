using PlusPim.Instructions;
using static PlusPim.Asm.Tests.Harness;

namespace PlusPim.Asm.Tests;

/// <summary>
/// データ配置指令 (docs/tests/assembler/values.md)
/// </summary>
public class DataDirectiveTests {
    private static AssemblyResult Data(string body) {
        return Assemble(Head + ".data\n" + body);
    }

    [Theory]
    [InlineData(".byte", "0", "00")]
    [InlineData(".byte", "1", "01")]
    [InlineData(".byte", "-1", "FF")]
    [InlineData(".byte", "-128", "80")]
    [InlineData(".byte", "255", "FF")]
    [InlineData(".half", "-1", "FFFF")]
    [InlineData(".half", "-32768", "0080")]
    [InlineData(".half", "65535", "FFFF")]
    [InlineData(".half", "0x1234", "3412")]
    [InlineData(".word", "-1", "FFFFFFFF")]
    [InlineData(".word", "-2147483648", "00000080")]
    [InlineData(".word", "4294967295", "FFFFFFFF")]
    [InlineData(".word", "0x12345678", "78563412")]
    [InlineData(".byte", "1, 2, 3", "010203")]
    [InlineData(".half", "1, 2, 3", "010002000300")]
    [InlineData(".word", "1, 2", "0100000002000000")]
    public void Ints_LittleEndianInOrder(string directive, string values, string bytes) {
        AssemblyResult r = Data($"{directive} {values}");
        r.AssertCodes();
        Assert.Equal(bytes, r.DataAt(D));
    }

    [Theory]
    [InlineData(".byte", "-129")]
    [InlineData(".byte", "256")]
    [InlineData(".half", "-32769")]
    [InlineData(".half", "65536")]
    [InlineData(".word", "-2147483649")]
    public void Ints_OutOfRange_Fails(string directive, string value) {
        AssemblyResult r = Data($"{directive} 1, {value}");
        r.AssertFails(ErrorCode.ValueOutOfRange);
        Assert.Equal(value, r.Diagnostics[0].Args.Single());
    }

    [Theory]
    [InlineData("x", "x")]
    [InlineData("\"s\"", "s")]
    [InlineData("$t0", "T0")]
    public void Ints_NonInteger_IsDropped(string value, string arg) {
        AssemblyResult r = Data($".word 1, {value}, 2\nx:");
        r.AssertCodes(WarningCode.DiscardedOperand);
        Assert.Equal(arg, r.Diagnostics[0].Args.Single());
        Assert.Equal("0100000002000000", r.DataAt(D));
    }

    [Theory]
    [InlineData(".byte")]
    [InlineData(".half")]
    [InlineData(".word")]
    public void Ints_NoValue_LineIsDropped(string directive) {
        Data(directive).AssertCodes(WarningCode.OperandMismatch);
    }

    [Theory]
    [InlineData(".word 1")]
    [InlineData(".byte 1")]
    [InlineData(".asciiz \"a\"")]
    [InlineData(".space 4")]
    [InlineData(".align 2")]
    public void DataDirectiveInText_IsDropped(string line) {
        AssemblyResult r = Assemble(Head + "nop\n" + line + "\nl: nop\nla $t0, l");
        r.AssertCodes(WarningCode.MisplacedDirective);
        Assert.Equal(5, r.Diagnostics[0].Location?.Line);
        Assert.Equal((T + 4).Addr, r.Run(4)[(int)GprId.T0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(42)]
    public void Space_ReservesZeros(int n) {
        AssemblyResult r = Data($".byte 1\n.space {n}\n.byte 2");
        r.AssertCodes();
        Assert.Equal(n == 0 ? "0102" : "01", r.DataAt(D));
        Assert.Equal(n == 0 ? "" : "02", r.DataAt(D + 1u + (uint)n));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("2147483648")]
    public void Space_OutOfRange_IsDropped(string n) {
        AssemblyResult r = Data($".byte 1\n.space {n}\n.byte 2");
        r.AssertCodes(WarningCode.DiscardedOperand);
        Assert.Equal("0102", r.DataAt(D));
    }

    [Fact]
    public void Space_FillsSegmentExactly() {
        AssemblyResult r = Assemble(Head + ".data 0x7ffffff0\n.space 16");
        r.AssertCodes();
        Assert.Empty(r.Prog().Image.Data);
    }

    [Fact]
    public void Space_BeyondSegment_Fails() {
        Assemble(Head + ".data 0x7ffffff0\n.space 17").AssertFails(ErrorCode.SectionOutOfRange);
    }

    /// <summary>32bitを超える大きさは0に戻らず範囲外として報告される</summary>
    [Theory]
    [InlineData(".byte 1\n.byte 1")]
    [InlineData(".align 2")]
    public void Size_Beyond32Bit_Fails(string tail) {
        Data(".space 2147483647\n.space 2147483647\n" + tail).AssertFails(ErrorCode.SectionOutOfRange);
    }

    /// <summary><c>.align</c> の後のラベルは詰め物の後を指す</summary>
    /// <param name="before">整列前に置くバイト数</param>
    /// <param name="offset">整列後のラベルの位置</param>
    [Theory]
    [InlineData(4, 4)]
    [InlineData(5, 8)]
    [InlineData(3, 4)]
    [InlineData(0, 0)]
    public void Align_LabelPointsAfterPadding(int before, uint offset) {
        string bytes = before == 0 ? "" : ".byte " + string.Join(", ", Enumerable.Repeat(1, before));
        Assert.Equal((D + offset).Addr, AddressOf(Head + $".data\n{bytes}\n.align 2\nx: .byte 2", 0, "x"));
    }

    [Fact]
    public void Align_Zero_DoesNothing() {
        Assert.Equal((D + 1).Addr, AddressOf(Head + ".data\n.byte 1\n.align 0\nx: .byte 2", 0, "x"));
    }

    [Fact]
    public void Align_Max_AlignsSectionStart() {
        Assert.Equal(0x40000000u, AddressOf(Head + ".data\n.align 30\nx: .byte 2", 0, "x"));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("31")]
    public void Align_OutOfRange_IsDropped(string n) {
        AssemblyResult r = Data($".byte 1\n.align {n}\n.byte 2");
        r.AssertCodes(WarningCode.DiscardedOperand);
        Assert.Equal("0102", r.DataAt(D));
    }

    [Fact]
    public void Align_InFixedSection_UsesAbsoluteAddress() {
        Assert.Equal(0x10000104u, AddressOf(Head + ".data 0x10000101\n.byte 1\n.align 2\nx: .byte 2", 0, "x"));
    }

    [Theory]
    [InlineData("", 0x10000001u)]
    [InlineData(".align 2\n", 0x10000004u)]
    public void DataOfNextFile_ContinuesUnlessAligned(string align, uint addr) {
        Assert.Equal(addr, AddressOf(Head + ".data\n.byte 1", 0, "y", $".globl y\n.data\n{align}y: .byte 2"));
    }

    [Theory]
    [InlineData(".ascii", "\"abc\"", "616263")]
    [InlineData(".asciiz", "\"abc\"", "61626300")]
    [InlineData(".utf-8z", "\"abc\"", "61626300")]
    [InlineData(".ascii", "\"\"", "")]
    [InlineData(".asciiz", "\"\"", "00")]
    [InlineData(".utf-8z", "\"\"", "00")]
    [InlineData(".ascii", "\"\u007f\"", "7F")]
    [InlineData(".utf-8z", "\"\u0080\"", "C28000")]
    [InlineData(".utf-8z", "\"\u07ff\"", "DFBF00")]
    [InlineData(".utf-8z", "\"\u0800\"", "E0A08000")]
    [InlineData(".utf-8z", "\"\uffff\"", "EFBFBF00")]
    [InlineData(".utf-8z", "\"\U00010000\"", "F090808000")]
    [InlineData(".ascii", "\"\\n\\t\\0\\\\\\\"\"", "0A09005C22")]
    [InlineData(".utf-8z", "\"\\n\\t\\0\\\\\\\"\"", "0A09005C2200")]
    [InlineData(".ascii", "\"\\a\"", "5C61")]
    [InlineData(".ascii", "\"a#b,c\"", "6123622C63")]
    [InlineData(".ascii", "  \"x\"  # c", "78")]
    public void String_Bytes(string directive, string literal, string bytes) {
        AssemblyResult r = Data($"{directive} {literal}");
        r.AssertCodes();
        Assert.Equal(bytes, r.DataAt(D));
    }

    /// <summary>孤立サロゲートは置換文字として出力される</summary>
    /// <remarks>孤立サロゲートは UTF-8 へ損失なく直列化できないので, 理論のデータにしない</remarks>
    [Fact]
    public void String_LoneSurrogate_BecomesReplacementCharacter() {
        AssemblyResult r = Data(".utf-8z \"\ud800\"");
        r.AssertCodes();
        Assert.Equal("EFBFBD00", r.DataAt(D));
    }

    [Theory]
    [InlineData(".ascii")]
    [InlineData(".asciiz")]
    public void String_NonAsciiInAscii_Fails(string directive) {
        AssemblyResult r = Data($"{directive} \"a\u0080\"\n{directive} \"\U00010000\"");
        Assert.Null(r.Program);
        r.AssertCodes(ErrorCode.NonAsciiString, ErrorCode.NonAsciiString);
        Assert.Equal([5, 6], r.Diagnostics.Select(d => d.Location?.Line));
    }

    [Fact]
    public void String_NonAsciiInComment_IsIgnored() {
        Data(".ascii \"a\" # \u3042").AssertCodes();
    }

    [Theory]
    [InlineData("\"abc")]
    [InlineData("\"a\\\"")]
    [InlineData("\"a\\")]
    public void String_Unclosed_LineIsDropped(string literal) {
        AssemblyResult r = Data($".asciiz {literal}");
        r.AssertCodes(WarningCode.UnparsableLine);
        Assert.Equal("", r.DataAt(D));
    }

    [Theory]
    [InlineData(".asciiz")]
    [InlineData(".asciiz 1")]
    [InlineData(".asciiz \"a\", \"b\"")]
    public void String_WrongOperands_LineIsDropped(string line) {
        Data(line).AssertCodes(WarningCode.OperandMismatch);
    }

    [Fact]
    public void String_MultiByte_LabelCountsBytes() {
        Assert.Equal((D + 4).Addr, AddressOf(Head + ".data\n.utf-8z \"\u3042\"\nx: .byte 1", 0, "x"));
    }
}
