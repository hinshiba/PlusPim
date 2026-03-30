using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests;

public class InstructionUndoTests {
    [Theory]
    [InlineData("add $t2, $t0, $t1")]
    [InlineData("addu $t2, $t0, $t1")]
    [InlineData("sub $t2, $t0, $t1")]
    [InlineData("subu $t2, $t0, $t1")]
    [InlineData("and $t2, $t0, $t1")]
    [InlineData("or $t2, $t0, $t1")]
    [InlineData("xor $t2, $t0, $t1")]
    [InlineData("nor $t2, $t0, $t1")]
    [InlineData("slt $t2, $t0, $t1")]
    [InlineData("sltu $t2, $t0, $t1")]
    public void ExecuteUndo_RType_RestoresState(string assemblyLine) {
        RuntimeContext ctx = TestHelpers.CreateRuntimeContext();
        TestHelpers.SeedRegisters(ctx, 42);
        ctx.Registers[RegisterID.T0] = 100;
        ctx.Registers[RegisterID.T1] = 200;
        ContextSnapshot snapshot = TestHelpers.TakeSnapshot(ctx);

        bool parsed = InstructionRegistry.Default.TryParse(assemblyLine, 1, out IInstruction? instruction);
        Assert.True(parsed);
        Assert.NotNull(instruction);

        instruction.Execute(ctx);
        instruction.Undo(ctx);

        TestHelpers.AssertSnapshotEqual(snapshot, ctx);
    }

    [Theory]
    [InlineData("addiu $t0, $t1, 100")]
    [InlineData("andi $t0, $t1, 0xFF")]
    [InlineData("ori $t0, $t1, 0xFF")]
    [InlineData("xori $t0, $t1, 0xFF")]
    [InlineData("slti $t0, $t1, 100")]
    [InlineData("sltiu $t0, $t1, 100")]
    [InlineData("lui $t0, 0xFF")]
    public void ExecuteUndo_IType_RestoresState(string assemblyLine) {
        RuntimeContext ctx = TestHelpers.CreateRuntimeContext();
        TestHelpers.SeedRegisters(ctx, 77);
        ContextSnapshot snapshot = TestHelpers.TakeSnapshot(ctx);

        bool parsed = InstructionRegistry.Default.TryParse(assemblyLine, 1, out IInstruction? instruction);
        Assert.True(parsed);
        Assert.NotNull(instruction);

        instruction.Execute(ctx);
        instruction.Undo(ctx);

        TestHelpers.AssertSnapshotEqual(snapshot, ctx);
    }

    [Theory]
    [InlineData("sll $t2, $t0, 3")]
    [InlineData("srl $t2, $t0, 3")]
    [InlineData("sra $t2, $t0, 3")]
    [InlineData("sllv $t2, $t0, $t1")]
    [InlineData("srlv $t2, $t0, $t1")]
    [InlineData("srav $t2, $t0, $t1")]
    public void ExecuteUndo_Shift_RestoresState(string assemblyLine) {
        RuntimeContext ctx = TestHelpers.CreateRuntimeContext();
        TestHelpers.SeedRegisters(ctx, 99);
        ContextSnapshot snapshot = TestHelpers.TakeSnapshot(ctx);

        bool parsed = InstructionRegistry.Default.TryParse(assemblyLine, 1, out IInstruction? instruction);
        Assert.True(parsed);
        Assert.NotNull(instruction);

        instruction.Execute(ctx);
        instruction.Undo(ctx);

        TestHelpers.AssertSnapshotEqual(snapshot, ctx);
    }

    [Theory]
    [InlineData("mult $t0, $t1")]
    [InlineData("multu $t0, $t1")]
    [InlineData("div $t0, $t1")]
    [InlineData("divu $t0, $t1")]
    public void ExecuteUndo_MulDiv_RestoresHiLo(string assemblyLine) {
        RuntimeContext ctx = TestHelpers.CreateRuntimeContext();
        TestHelpers.SeedRegisters(ctx, 55);
        ctx.Registers[RegisterID.T1] = 7;
        ContextSnapshot snapshot = TestHelpers.TakeSnapshot(ctx);

        bool parsed = InstructionRegistry.Default.TryParse(assemblyLine, 1, out IInstruction? instruction);
        Assert.True(parsed);
        Assert.NotNull(instruction);

        instruction.Execute(ctx);
        instruction.Undo(ctx);

        TestHelpers.AssertSnapshotEqual(snapshot, ctx);
    }

    [Theory]
    [InlineData("mfhi $t0")]
    [InlineData("mflo $t0")]
    [InlineData("mthi $t0")]
    [InlineData("mtlo $t0")]
    public void ExecuteUndo_LoHi_RestoresState(string assemblyLine) {
        RuntimeContext ctx = TestHelpers.CreateRuntimeContext();
        TestHelpers.SeedRegisters(ctx, 33);
        ContextSnapshot snapshot = TestHelpers.TakeSnapshot(ctx);

        bool parsed = InstructionRegistry.Default.TryParse(assemblyLine, 1, out IInstruction? instruction);
        Assert.True(parsed);
        Assert.NotNull(instruction);

        instruction.Execute(ctx);
        instruction.Undo(ctx);

        TestHelpers.AssertSnapshotEqual(snapshot, ctx);
    }

    [Fact]
    public void ExecuteUndo_Sw_RestoresMemory() {
        RuntimeContext ctx = TestHelpers.CreateRuntimeContext();
        ctx.Registers[RegisterID.T0] = 0xDEADBEEF;
        ctx.Registers[RegisterID.Sp] = 0x10000000;

        Address[] memoryAddresses = [
            new Address(0x10000000),
            new Address(0x10000001),
            new Address(0x10000002),
            new Address(0x10000003),
        ];
        ContextSnapshot snapshot = TestHelpers.TakeSnapshot(ctx, memoryAddresses);

        bool parsed = InstructionRegistry.Default.TryParse("sw $t0, 0($sp)", 1, out IInstruction? instruction);
        Assert.True(parsed);
        Assert.NotNull(instruction);

        instruction.Execute(ctx);
        instruction.Undo(ctx);

        TestHelpers.AssertSnapshotEqual(snapshot, ctx, memoryAddresses);
    }

    [Fact]
    public void ExecuteUndo_Lw_RestoresRegister() {
        RuntimeContext ctx = TestHelpers.CreateRuntimeContext();
        ctx.WriteMemoryBytes(new Address(0x10000000), 0x12345678, 4);
        ctx.Registers[RegisterID.Sp] = 0x10000000;
        ctx.Registers[RegisterID.T0] = 0;
        ContextSnapshot snapshot = TestHelpers.TakeSnapshot(ctx);

        bool parsed = InstructionRegistry.Default.TryParse("lw $t0, 0($sp)", 1, out IInstruction? instruction);
        Assert.True(parsed);
        Assert.NotNull(instruction);

        instruction.Execute(ctx);
        instruction.Undo(ctx);

        TestHelpers.AssertSnapshotEqual(snapshot, ctx);
    }

    [Fact]
    public void ExecuteUndo_WriteToZero_StillRestores() {
        RuntimeContext ctx = TestHelpers.CreateRuntimeContext();
        TestHelpers.SeedRegisters(ctx, 11);
        ctx.Registers[RegisterID.T0] = 100;
        ctx.Registers[RegisterID.T1] = 200;
        ContextSnapshot snapshot = TestHelpers.TakeSnapshot(ctx);

        bool parsed = InstructionRegistry.Default.TryParse("add $zero, $t0, $t1", 1, out IInstruction? instruction);
        Assert.True(parsed);
        Assert.NotNull(instruction);

        instruction.Execute(ctx);
        instruction.Undo(ctx);

        TestHelpers.AssertSnapshotEqual(snapshot, ctx);
    }

    [Fact]
    public void ExecuteUndo_Syscall_RestoresException() {
        RuntimeContext ctx = TestHelpers.CreateRuntimeContext();
        ContextSnapshot snapshot = TestHelpers.TakeSnapshot(ctx);

        bool parsed = InstructionRegistry.Default.TryParse("syscall", 1, out IInstruction? instruction);
        Assert.True(parsed);
        Assert.NotNull(instruction);

        instruction.Execute(ctx);
        Assert.NotNull(ctx.LastException);

        instruction.Undo(ctx);

        TestHelpers.AssertSnapshotEqual(snapshot, ctx);
    }

    [Fact]
    public void ExecuteUndo_Break_RestoresException() {
        RuntimeContext ctx = TestHelpers.CreateRuntimeContext();
        ContextSnapshot snapshot = TestHelpers.TakeSnapshot(ctx);

        bool parsed = InstructionRegistry.Default.TryParse("break", 1, out IInstruction? instruction);
        Assert.True(parsed);
        Assert.NotNull(instruction);

        instruction.Execute(ctx);
        Assert.NotNull(ctx.LastException);

        instruction.Undo(ctx);

        TestHelpers.AssertSnapshotEqual(snapshot, ctx);
    }

    [Fact]
    public void ExecuteUndo_AddOverflow_RestoresState() {
        RuntimeContext ctx = TestHelpers.CreateRuntimeContext();
        ctx.Registers[RegisterID.T0] = int.MaxValue;
        ctx.Registers[RegisterID.T1] = 1;
        ContextSnapshot snapshot = TestHelpers.TakeSnapshot(ctx);

        bool parsed = InstructionRegistry.Default.TryParse("add $t2, $t0, $t1", 1, out IInstruction? instruction);
        Assert.True(parsed);
        Assert.NotNull(instruction);

        instruction.Execute(ctx);
        Assert.Equal(ExcCode.Ov, ctx.LastException?.Code);

        instruction.Undo(ctx);

        TestHelpers.AssertSnapshotEqual(snapshot, ctx);
    }
}
