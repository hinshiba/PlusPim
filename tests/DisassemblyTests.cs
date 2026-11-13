using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program;
using Xunit;

namespace PlusPimTests;

/// <summary>
/// <see cref="IInstruction.Disassembly"/> による命令の表記のテスト
/// </summary>
public class DisassemblyTests {
    [Theory]
    [InlineData("lui $t0, 0x1000", "lui $t0, 0x1000")]
    [InlineData("ori $t0, $t1, 255", "ori $t0, $t1, 0x00FF")]
    [InlineData("addiu $sp, $sp, -4", "addiu $sp, $sp, 0xFFFC")]
    [InlineData("addu $t2, $t1, $zero", "addu $t2, $t1, $zero")]
    [InlineData("slt $8, $9, $10", "slt $t0, $t1, $t2")]
    [InlineData("sll $t0, $t1, 2", "sll $t0, $t1, 2")]
    [InlineData("sra $ra, $gp, 31", "sra $ra, $gp, 31")]
    public void Disassembly_RendersInstruction(string line, string expected) {
        IInstruction? instruction = TestHelpers.ParseInstruction(line);
        Assert.NotNull(instruction);
        Assert.Equal(expected, instruction.Disassembly);
    }

    [Theory]
    [InlineData("jr $ra")]
    [InlineData("lw $t0, 0($sp)")]
    [InlineData("syscall")]
    public void Disassembly_IsNullForUnsupportedInstructions(string line) {
        IInstruction? instruction = TestHelpers.ParseInstruction(line);
        Assert.NotNull(instruction);
        Assert.Null(instruction.Disassembly);
    }

    [Theory]
    [InlineData("nop", new[] { "sll $zero, $zero, 0" })]
    [InlineData("move $t2, $t1", new[] { "addu $t2, $t1, $zero" })]
    [InlineData("li $t1, 5", new[] { "ori $t1, $zero, 0x0005" })]
    [InlineData("li $t1, 0x12345", new[] { "lui $t1, 0x0001", "ori $t1, $t1, 0x2345" })]
    [InlineData("la $a0, msg", new[] { "lui $a0, 0x1000", "ori $a0, $a0, 0x0010" })]
    public void Disassembly_RendersPseudoInstructionExpansions(string line, string[] expected) {
        SymbolTable symbols = new();
        _ = symbols.Add(new("msg", new(0x10000010)));
        Assert.True(InstructionRegistry.Default.TryParseAll(line, 1, symbols, out IInstruction[]? instructions));
        Assert.Equal(expected, instructions.Select(instruction => instruction.Disassembly));
    }
}
