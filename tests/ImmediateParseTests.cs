using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests;

/// <summary>
/// 整数リテラル (16bit即値と <c>li</c> の32bit値) の解析
/// </summary>
public class ImmediateParseTests {
    // ===== 16bit =====

    [Theory]
    [InlineData("0", 0x0000)]
    [InlineData("65535", 0xFFFF)]
    [InlineData("-1", 0xFFFF)]
    [InlineData("-32768", 0x8000)]
    [InlineData("+5", 0x0005)]
    [InlineData("0xff", 0x00FF)]
    [InlineData("0XFFFF", 0xFFFF)]
    [InlineData("-0x8000", 0x8000)]
    [InlineData("-0x1", 0xFFFF)]
    [InlineData("0x0000FFFF", 0xFFFF)]
    public void TryParse16_Valid_Succeeds(string literal, uint expected) {
        Assert.True(Immediate.TryParse(literal, null, out Immediate? imm));
        Assert.Equal(expected, imm.ToUInt());
    }

    [Theory]
    [InlineData("")]
    [InlineData("65536")]
    [InlineData("-32769")]
    [InlineData("0x10000")]
    [InlineData("-0x8001")]
    [InlineData("0x")]
    [InlineData("-")]
    [InlineData("0xG")]
    [InlineData("1a")]
    [InlineData("--1")]
    [InlineData("0x000000001")]
    [InlineData("label")]
    public void TryParse16_Invalid_ReturnsFalse(string literal) {
        Assert.False(Immediate.TryParse(literal, null, out _));
    }

    // ===== 32bit =====

    [Theory]
    [InlineData("0x80000000", 0x80000000u)]
    [InlineData("0xFFFFFFFF", 0xFFFFFFFFu)]
    [InlineData("4294967295", 0xFFFFFFFFu)]
    [InlineData("-1", 0xFFFFFFFFu)]
    [InlineData("-2147483648", 0x80000000u)]
    [InlineData("2147483648", 0x80000000u)]
    [InlineData("0x10", 0x10u)]
    public void TryParse32_Valid_Succeeds(string literal, uint expected) {
        Assert.True(Immediate.TryParse32(literal, out uint value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("4294967296")]
    [InlineData("-2147483649")]
    [InlineData("0x100000000")]
    [InlineData("12345678901")]
    [InlineData("")]
    public void TryParse32_Invalid_ReturnsFalse(string literal) {
        Assert.False(Immediate.TryParse32(literal, out _));
    }

    // ===== li =====

    [Theory]
    [InlineData("0x80000000", 0x80000000u)]
    [InlineData("0xFFFFFFFF", 0xFFFFFFFFu)]
    [InlineData("4294967295", 0xFFFFFFFFu)]
    [InlineData("-1", 0xFFFFFFFFu)]
    [InlineData("-2147483648", 0x80000000u)]
    [InlineData("0x10", 0x10u)]
    [InlineData("70000", 70000u)]
    [InlineData("0x12345678", 0x12345678u)]
    public void Li_LoadsExpectedValue(string literal, uint expected) {
        Assert.True(InstructionRegistry.Default.TryParseAll($"li $t0, {literal}", 1, new SymbolTable(), out IInstruction[]? instructions));
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0xDEADBEEF;

        foreach(IInstruction instruction in instructions) {
            _ = instruction.Execute(context);
        }

        Assert.Equal(expected, context.Registers[RegisterID.T0]);
    }

    [Theory]
    [InlineData("4294967296")]
    [InlineData("-2147483649")]
    [InlineData("0x100000000")]
    [InlineData("label")]
    public void Li_OutOfRange_ReturnsFalse(string literal) {
        Assert.False(InstructionRegistry.Default.TryParseAll($"li $t0, {literal}", 1, new SymbolTable(), out _));
    }

    [Theory]
    [InlineData("0x10", 1)]
    [InlineData("0xFFFF", 1)]
    [InlineData("0x10000", 2)]
    [InlineData("-1", 2)]
    public void Li_ExpansionSize(string literal, int expected) {
        Assert.Equal(expected, InstructionRegistry.Default.GetInstructionCount($"li $t0, {literal}"));
    }
}
