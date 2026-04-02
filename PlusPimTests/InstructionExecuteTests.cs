using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests;

public class InstructionExecuteTests {
    // ===== R-Type Arithmetic =====

    [Fact]
    public void Execute_Add_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 10;
        context.Registers[RegisterID.T1] = 20;

        IInstruction? instruction = TestHelpers.ParseInstruction("add $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(30u, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_Addu_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0xFFFFFFFF;
        context.Registers[RegisterID.T1] = 1;

        IInstruction? instruction = TestHelpers.ParseInstruction("addu $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0u, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_Sub_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 30;
        context.Registers[RegisterID.T1] = 10;

        IInstruction? instruction = TestHelpers.ParseInstruction("sub $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(20u, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_Subu_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0;
        context.Registers[RegisterID.T1] = 1;

        IInstruction? instruction = TestHelpers.ParseInstruction("subu $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0xFFFFFFFF, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_And_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0xFF00;
        context.Registers[RegisterID.T1] = 0x0FF0;

        IInstruction? instruction = TestHelpers.ParseInstruction("and $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0x0F00u, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_Or_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0xFF00;
        context.Registers[RegisterID.T1] = 0x0FF0;

        IInstruction? instruction = TestHelpers.ParseInstruction("or $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0xFFF0u, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_Xor_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0xFF00;
        context.Registers[RegisterID.T1] = 0x0FF0;

        IInstruction? instruction = TestHelpers.ParseInstruction("xor $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0xF0F0u, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_Nor_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0xFF00;
        context.Registers[RegisterID.T1] = 0x00FF;

        IInstruction? instruction = TestHelpers.ParseInstruction("nor $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0xFFFF0000, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_Slt_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = unchecked((uint)-1); // 0xFFFFFFFF, signed = -1
        context.Registers[RegisterID.T1] = 1;

        IInstruction? instruction = TestHelpers.ParseInstruction("slt $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(1u, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_Sltu_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 1;
        context.Registers[RegisterID.T1] = 0xFFFFFFFF;

        IInstruction? instruction = TestHelpers.ParseInstruction("sltu $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(1u, context.Registers[RegisterID.T2]);
    }

    // ===== Shift =====

    [Fact]
    public void Execute_Sll_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 1;

        IInstruction? instruction = TestHelpers.ParseInstruction("sll $t2, $t0, 5");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(32u, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_Srl_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0x80000000;

        IInstruction? instruction = TestHelpers.ParseInstruction("srl $t2, $t0, 1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0x40000000u, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_Sra_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0x80000000;

        IInstruction? instruction = TestHelpers.ParseInstruction("sra $t2, $t0, 1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0xC0000000, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_Sllv_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 1;
        context.Registers[RegisterID.T1] = 5;

        IInstruction? instruction = TestHelpers.ParseInstruction("sllv $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(32u, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_Srlv_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0x80000000;
        context.Registers[RegisterID.T1] = 1;

        IInstruction? instruction = TestHelpers.ParseInstruction("srlv $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0x40000000u, context.Registers[RegisterID.T2]);
    }

    [Fact]
    public void Execute_Srav_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0x80000000;
        context.Registers[RegisterID.T1] = 1;

        IInstruction? instruction = TestHelpers.ParseInstruction("srav $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0xC0000000, context.Registers[RegisterID.T2]);
    }

    // ===== I-Type =====

    [Fact]
    public void Execute_Addiu_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T1] = 100;

        IInstruction? instruction = TestHelpers.ParseInstruction("addiu $t0, $t1, 50");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(150u, context.Registers[RegisterID.T0]);
    }

    [Fact]
    public void Execute_Andi_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T1] = 0xFF0F;

        IInstruction? instruction = TestHelpers.ParseInstruction("andi $t0, $t1, 0x0FF0");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0x0F00u, context.Registers[RegisterID.T0]);
    }

    [Fact]
    public void Execute_Ori_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T1] = 0xFF00;

        IInstruction? instruction = TestHelpers.ParseInstruction("ori $t0, $t1, 0x00FF");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0xFFFFu, context.Registers[RegisterID.T0]);
    }

    [Fact]
    public void Execute_Xori_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T1] = 0xFF00;

        IInstruction? instruction = TestHelpers.ParseInstruction("xori $t0, $t1, 0x0FF0");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0xF0F0u, context.Registers[RegisterID.T0]);
    }

    [Fact]
    public void Execute_Slti_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T1] = unchecked((uint)-5); // signed -5

        IInstruction? instruction = TestHelpers.ParseInstruction("slti $t0, $t1, 1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(1u, context.Registers[RegisterID.T0]);
    }

    [Fact]
    public void Execute_Sltiu_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T1] = 1;

        IInstruction? instruction = TestHelpers.ParseInstruction("sltiu $t0, $t1, 2");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(1u, context.Registers[RegisterID.T0]);
    }

    [Fact]
    public void Execute_Lui_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();

        IInstruction? instruction = TestHelpers.ParseInstruction("lui $t0, 0x1234");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0x12340000u, context.Registers[RegisterID.T0]);
    }

    // ===== MulDiv =====

    [Fact]
    public void Execute_Mult_CorrectHiLo() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0x10000;
        context.Registers[RegisterID.T1] = 0x10000;

        IInstruction? instruction = TestHelpers.ParseInstruction("mult $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(1u, context.HI);
        Assert.Equal(0u, context.LO);
    }

    [Fact]
    public void Execute_Multu_CorrectHiLo() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0x10000;
        context.Registers[RegisterID.T1] = 0x10000;

        IInstruction? instruction = TestHelpers.ParseInstruction("multu $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(1u, context.HI);
        Assert.Equal(0u, context.LO);
    }

    [Fact]
    public void Execute_Div_CorrectHiLo() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 17;
        context.Registers[RegisterID.T1] = 5;

        IInstruction? instruction = TestHelpers.ParseInstruction("div $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(3u, context.LO); // quotient
        Assert.Equal(2u, context.HI); // remainder
    }

    [Fact]
    public void Execute_Divu_CorrectHiLo() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 17;
        context.Registers[RegisterID.T1] = 5;

        IInstruction? instruction = TestHelpers.ParseInstruction("divu $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(3u, context.LO); // quotient
        Assert.Equal(2u, context.HI); // remainder
    }

    // ===== LoHi =====

    [Fact]
    public void Execute_Mfhi_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.HI = 0xDEAD;

        IInstruction? instruction = TestHelpers.ParseInstruction("mfhi $t0");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0xDEADu, context.Registers[RegisterID.T0]);
    }

    [Fact]
    public void Execute_Mflo_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.LO = 0xBEEF;

        IInstruction? instruction = TestHelpers.ParseInstruction("mflo $t0");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0xBEEFu, context.Registers[RegisterID.T0]);
    }

    [Fact]
    public void Execute_Mthi_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0x1234;

        IInstruction? instruction = TestHelpers.ParseInstruction("mthi $t0");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0x1234u, context.HI);
    }

    [Fact]
    public void Execute_Mtlo_CorrectResult() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0x5678;

        IInstruction? instruction = TestHelpers.ParseInstruction("mtlo $t0");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0x5678u, context.LO);
    }

    // ===== Memory =====

    [Fact]
    public void Execute_Sw_WritesMemory() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 0xDEADBEEF;
        context.Registers[RegisterID.Sp] = 0x10000000;

        IInstruction? instruction = TestHelpers.ParseInstruction("sw $t0, 0($sp)");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        uint value = context.ReadMemoryBytes(new Address(0x10000000), 4, false);
        Assert.Equal(0xDEADBEEF, value);
    }

    [Fact]
    public void Execute_Lw_ReadsMemory() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.WriteMemoryBytes(new Address(0x10000000), 0x12345678, 4);
        context.Registers[RegisterID.Sp] = 0x10000000;

        IInstruction? instruction = TestHelpers.ParseInstruction("lw $t0, 0($sp)");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0x12345678u, context.Registers[RegisterID.T0]);
    }

    // ===== $zero protection =====

    [Fact]
    public void Execute_Add_WriteToZero_RegisterUnchanged() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 10;
        context.Registers[RegisterID.T1] = 20;

        IInstruction? instruction = TestHelpers.ParseInstruction("add $zero, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0u, context.Registers[RegisterID.Zero]);
    }

    [Fact]
    public void Execute_Addiu_WriteToZero_RegisterUnchanged() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = 100;

        IInstruction? instruction = TestHelpers.ParseInstruction("addiu $zero, $t0, 100");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        Assert.Equal(0u, context.Registers[RegisterID.Zero]);
    }

    // ===== Exception cases =====

    [Fact]
    public void Execute_Add_Overflow_RaisesException() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = int.MaxValue; // 0x7FFFFFFF
        context.Registers[RegisterID.T1] = 1;

        IInstruction? instruction = TestHelpers.ParseInstruction("add $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        _ = Assert.NotNull(context.LastException);
        Assert.Equal(ExcCode.Ov, context.LastException?.Code);
    }

    [Fact]
    public void Execute_Sub_Overflow_RaisesException() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.T0] = unchecked((uint)int.MinValue); // 0x80000000
        context.Registers[RegisterID.T1] = 1;

        IInstruction? instruction = TestHelpers.ParseInstruction("sub $t2, $t0, $t1");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        _ = Assert.NotNull(context.LastException);
        Assert.Equal(ExcCode.Ov, context.LastException?.Code);
    }

    [Fact]
    public void Execute_Sw_Misaligned_RaisesException() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.Sp] = 0x10000001; // misaligned

        IInstruction? instruction = TestHelpers.ParseInstruction("sw $t0, 0($sp)");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        _ = Assert.NotNull(context.LastException);
        Assert.Equal(ExcCode.AdES, context.LastException?.Code);
    }

    [Fact]
    public void Execute_Lw_Misaligned_RaisesException() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        context.Registers[RegisterID.Sp] = 0x10000001; // misaligned

        IInstruction? instruction = TestHelpers.ParseInstruction("lw $t0, 0($sp)");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        _ = Assert.NotNull(context.LastException);
        Assert.Equal(ExcCode.AdEL, context.LastException?.Code);
    }

    [Fact]
    public void Execute_Syscall_RaisesException() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();

        IInstruction? instruction = TestHelpers.ParseInstruction("syscall");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        _ = Assert.NotNull(context.LastException);
        Assert.Equal(ExcCode.Sys, context.LastException?.Code);
    }

    [Fact]
    public void Execute_Break_RaisesException() {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();

        IInstruction? instruction = TestHelpers.ParseInstruction("break");
        Assert.NotNull(instruction);
        instruction.Execute(context);

        _ = Assert.NotNull(context.LastException);
        Assert.Equal(ExcCode.Bp, context.LastException?.Code);
    }
}
