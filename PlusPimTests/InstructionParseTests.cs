using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using Xunit;

namespace PlusPimTests;

public class InstructionParseTests {
    // ===== Normal - Real instructions =====

    [Theory]
    [InlineData("add $t0, $t1, $t2")]
    [InlineData("addiu $t0, $t1, 100")]
    [InlineData("sll $t0, $t1, 5")]
    [InlineData("lw $t0, 0($sp)")]
    [InlineData("sw $t0, 4($sp)")]
    [InlineData("beq $t0, $t1, label")]
    [InlineData("j label")]
    [InlineData("jr $ra")]
    [InlineData("lui $t0, 0xFF")]
    [InlineData("mult $t0, $t1")]
    [InlineData("mfhi $t0")]
    [InlineData("syscall")]
    [InlineData("break")]
    public void TryParse_RealInstruction_Succeeds(string assemblyLine) {
        int lineIndex = 5;

        bool result = InstructionRegistry.Default.TryParse(assemblyLine, lineIndex, out IInstruction? instruction);

        Assert.True(result);
        Assert.NotNull(instruction);
        Assert.Equal(lineIndex, instruction.SourceLine);
    }

    [Fact]
    public void TryParse_WriteToZero_StillParses() {
        bool result = InstructionRegistry.Default.TryParse("add $zero, $t1, $t2", 1, out IInstruction? instruction);

        Assert.True(result);
        Assert.NotNull(instruction);
    }

    [Fact]
    public void GetInstructionCount_RealInstruction_ReturnsOne() {
        int count = InstructionRegistry.Default.GetInstructionCount("add $t0, $t1, $t2");

        Assert.Equal(1, count);
    }

    // ===== Normal - Pseudo instructions =====

    [Fact]
    public void TryParseAll_Li_LargeValue_ExpandsToTwo() {
        SymbolTable symbolTable = new();

        bool result = InstructionRegistry.Default.TryParseAll("li $t0, 70000", 1, symbolTable, out IInstruction[]? instructions);

        Assert.True(result);
        Assert.NotNull(instructions);
        Assert.Equal(2, instructions.Length);
    }

    [Fact]
    public void TryParseAll_Li_SmallValue_ExpandsToOne() {
        SymbolTable symbolTable = new();

        bool result = InstructionRegistry.Default.TryParseAll("li $t0, 42", 1, symbolTable, out IInstruction[]? instructions);

        Assert.True(result);
        Assert.NotNull(instructions);
        _ = Assert.Single(instructions);
    }

    [Fact]
    public void TryParseAll_Move_ExpandsToOne() {
        SymbolTable symbolTable = new();

        bool result = InstructionRegistry.Default.TryParseAll("move $t0, $t1", 1, symbolTable, out IInstruction[]? instructions);

        Assert.True(result);
        Assert.NotNull(instructions);
        _ = Assert.Single(instructions);
    }

    [Fact]
    public void TryParseAll_Nop_ExpandsToOne() {
        SymbolTable symbolTable = new();

        bool result = InstructionRegistry.Default.TryParseAll("nop", 1, symbolTable, out IInstruction[]? instructions);

        Assert.True(result);
        Assert.NotNull(instructions);
        _ = Assert.Single(instructions);
    }

    [Fact]
    public void TryParseAll_La_ValidLabel_Succeeds() {
        SymbolTable symbolTable = new();
        _ = symbolTable.Add(new Label("mydata", new Address(0x10000004)));

        bool result = InstructionRegistry.Default.TryParseAll("la $t0, mydata", 1, symbolTable, out IInstruction[]? instructions);

        Assert.True(result);
        Assert.NotNull(instructions);
    }

    // ===== Error cases =====

    [Fact]
    public void TryParse_UnknownInstruction_ReturnsFalse() {
        bool result = InstructionRegistry.Default.TryParse("foobar $t0, $t1, $t2", 1, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_MissingOperands_ReturnsFalse() {
        bool result = InstructionRegistry.Default.TryParse("add $t0, $t1", 1, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_EmptyLine_ReturnsFalse() {
        bool result = InstructionRegistry.Default.TryParse("", 1, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParseAll_La_UndefinedLabel_ReturnsFalse() {
        SymbolTable symbolTable = new();


        bool result = InstructionRegistry.Default.TryParseAll("la $t0, undefinedLabel", 1, symbolTable, out _);

        Assert.False(result);
    }

    [Fact]
    public void GetInstructionCount_UnknownInstruction_ReturnsZero() {
        int count = InstructionRegistry.Default.GetInstructionCount("xyz");

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetInstructionCount_EmptyLine_ReturnsZero() {
        int count = InstructionRegistry.Default.GetInstructionCount("");

        Assert.Equal(0, count);
    }
}
