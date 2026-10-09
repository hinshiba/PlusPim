using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
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
        int lineNumber = 5;

        bool result = InstructionRegistry.Default.TryParse(assemblyLine, lineNumber, out IInstruction? instruction);

        Assert.True(result);
        Assert.NotNull(instruction);
        Assert.Equal(lineNumber, instruction.SourceLine);
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

    [Theory]
    [InlineData("bgez $t0, lbl")]
    [InlineData("bgtz $8, lbl")]
    [InlineData("blez $t0,lbl")]
    [InlineData("bltz $zero, lbl")]
    public void TryParse_BranchZero_Succeeds(string assemblyLine) {
        Assert.True(InstructionRegistry.Default.TryParse(assemblyLine, 1, out IInstruction? instruction));
        Assert.NotNull(instruction);
        Assert.Equal(1, InstructionRegistry.Default.GetInstructionCount(assemblyLine));
    }

    [Theory]
    [InlineData("bgez $t0")]
    [InlineData("bgez $t0, $t1, lbl")]
    [InlineData("bgez t0, lbl")]
    [InlineData("bltz $nosuch, lbl")]
    [InlineData("bgtz lbl")]
    [InlineData("blez")]
    public void TryParse_BranchZero_Malformed_ReturnsFalse(string assemblyLine) {
        Assert.False(InstructionRegistry.Default.TryParse(assemblyLine, 1, out _));
    }

    // ===== Register operands =====

    [Theory]
    [InlineData("add $t0, $t1, $31")]
    [InlineData("add $t0, $t1, $0")]
    [InlineData("add $t0, $t1, $9")]
    [InlineData("add $zero, $at, $ra")]
    [InlineData("add $fp, $sp, $gp")]
    public void TryParse_ValidRegister_Succeeds(string assemblyLine) {
        Assert.True(InstructionRegistry.Default.TryParse(assemblyLine, 1, out _));
    }

    [Theory]
    [InlineData("add $t0, $t1, $32")]
    [InlineData("add $t0, $t1, $40")]
    [InlineData("add $t0, $t1, $01")]
    [InlineData("add $t0, $t1, $08")]
    [InlineData("add $t0, $t1, $00")]
    [InlineData("add $t0, $t1, $-1")]
    [InlineData("add $T0, $t1, $t2")]
    [InlineData("add $t0, $ZERO, $t2")]
    [InlineData("add $t0, $Sp, $t2")]
    [InlineData("add $t0, $s8, $t2")]
    [InlineData("jr $40")]
    [InlineData("lw $t0, 0($32)")]
    [InlineData("addi $40, $t0, 1")]
    [InlineData("beq $t0, $99, label")]
    public void TryParse_InvalidRegister_ReturnsFalse(string assemblyLine) {
        Assert.False(InstructionRegistry.Default.TryParse(assemblyLine, 1, out _));
    }

    [Theory]
    [InlineData("li $40, 1")]
    [InlineData("li $T0, 1")]
    [InlineData("move $t0, $32")]
    [InlineData("la $08, mydata")]
    public void TryParseAll_PseudoInvalidRegister_ReturnsFalse(string assemblyLine) {
        SymbolTable symbolTable = new();
        _ = symbolTable.Add(new Label("mydata", new Address(0x10000004)));

        Assert.False(InstructionRegistry.Default.TryParseAll(assemblyLine, 1, symbolTable, out _));
    }

    [Theory]
    [InlineData("mfc0 $k0, $14")]
    [InlineData("mtc0 $k0, $12")]
    [InlineData("mfc0 $26, $0")]
    [InlineData("mtc0 $t0, $31")]
    public void TryParse_Cp0NumberOperand_Succeeds(string assemblyLine) {
        Assert.True(InstructionRegistry.Default.TryParse(assemblyLine, 1, out _));
    }

    [Theory]
    [InlineData("mfc0 $k0, $sp")]
    [InlineData("mtc0 $k0, $zero")]
    [InlineData("mfc0 $k0, $32")]
    [InlineData("mfc0 $k0, $014")]
    [InlineData("mfc0 $K0, $14")]
    public void TryParse_Cp0InvalidOperand_ReturnsFalse(string assemblyLine) {
        Assert.False(InstructionRegistry.Default.TryParse(assemblyLine, 1, out _));
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
