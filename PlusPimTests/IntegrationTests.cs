using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests;

public class IntegrationTests {
    [Fact]
    public void Step_LinearProgram_CorrectResult() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 5
              addiu $t1, $zero, 3
              add $t2, $t0, $t1
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            _ = debugger.Step();
            _ = debugger.Step();
            _ = debugger.Step();

            (uint[] regs, uint pc, uint hi, uint lo) = debugger.GetRegisters();
            Assert.Equal(8u, regs[(int)RegisterID.T2]);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void Step_Loop_CorrectCounter() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 0
              addiu $t1, $zero, 5
            loop:
              addiu $t0, $t0, 1
              bne $t0, $t1, loop
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            // 2 initial + 5 iterations * 2 instructions = 12 steps
            for(int i = 0; i < 12; i++) {
                _ = debugger.Step();
            }

            (uint[] regs, _, _, _) = debugger.GetRegisters();
            Assert.Equal(5u, regs[(int)RegisterID.T0]);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void Step_MemoryRoundtrip_ValuePreserved() {
        string asm = """
            .text
            main:
              lui $sp, 0x1000
              addiu $t0, $zero, 42
              sw $t0, 0($sp)
              lw $t1, 0($sp)
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            _ = debugger.Step(); // lui $sp
            _ = debugger.Step(); // addiu $t0
            _ = debugger.Step(); // sw
            _ = debugger.Step(); // lw

            (uint[] regs, uint pc, uint hi, uint lo) = debugger.GetRegisters();
            Assert.Equal(42u, regs[(int)RegisterID.T1]);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void Step_Factorial_CorrectResult() {
        string asm = """
            .text
            main:
              addiu $a0, $zero, 5
              addiu $v0, $zero, 1
            loop:
              slti $t0, $a0, 1
              bne $t0, $zero, done
              mult $a0, $v0
              mflo $v0
              addiu $a0, $a0, -1
              j loop
            done:
              break
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            // 5! = 120
            // 2 init + 5 iterations * (slti + bne + mult + mflo + addiu + j = 6) + final (slti + bne) = 2 + 30 + 2 = 34
            for(int i = 0; i < 34; i++) {
                _ = debugger.Step();
            }

            (uint[] regs, _, _, _) = debugger.GetRegisters();
            Assert.Equal(120u, regs[(int)RegisterID.V0]);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void Step_GetRegisters_PCConsistency() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 1
              addiu $t1, $zero, 2
              addiu $t2, $zero, 3
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            // PC starts at main = 0x00400000
            (uint[] regs0, uint pc0, uint hi0, uint lo0) = debugger.GetRegisters();
            Assert.Equal(0x00400000u, pc0);

            _ = debugger.Step();
            (uint[] regs1, uint pc1, uint hi1, uint lo1) = debugger.GetRegisters();
            Assert.Equal(0x00400004u, pc1);

            _ = debugger.Step();
            (uint[] regs2, uint pc2, uint hi2, uint lo2) = debugger.GetRegisters();
            Assert.Equal(0x00400008u, pc2);

            _ = debugger.Step();
            (uint[] regs3, uint pc3, uint hi3, uint lo3) = debugger.GetRegisters();
            Assert.Equal(0x0040000Cu, pc3);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void Step_GetCurrentLine_MatchesSourceLine() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 1
              addiu $t1, $zero, 2
              addiu $t2, $zero, 3
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            // main: is on line 2 (0-indexed line 1), first instruction on line 3 (1-indexed)

            int line1 = debugger.GetCallStack()[0].Line;
            _ = debugger.Step();
            int line2 = debugger.GetCallStack()[0].Line;
            _ = debugger.Step();
            int line3 = debugger.GetCallStack()[0].Line;

            // Lines should be sequential (exact values depend on source formatting)
            Assert.True(line1 > 0);
            Assert.True(line2 > line1);
            Assert.True(line3 > line2);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void Step_IsTerminated_FalseUntilEnd() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 1
              addiu $t1, $zero, 2
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            Assert.Equal(PlusPim.Application.StopReason.Step, debugger.Step());
            Assert.Equal(PlusPim.Application.StopReason.Step, debugger.Step());
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void Step_AfterTermination_IsNop() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 42
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            _ = debugger.Step(); // addiu

            // Run off the end: RI exception → kernel handler → kernel OOB → double exception → terminated
            // Keep stepping until terminated (bounded to prevent infinite loop)
            StopReason reason = StopReason.Step;
            for(int i = 0; i < 100 && reason != StopReason.Terminated; i++) {
                reason = debugger.Step();
            }

            // Record state after termination
            (uint[] regsBefore, uint pcBefore, uint hiBefore, uint loBefore) = debugger.GetRegisters();

            // Step again should be nop
            _ = debugger.Step();

            (uint[] regsAfter, uint pcAfter, uint hiAfter, uint loAfter) = debugger.GetRegisters();
            Assert.Equal(regsBefore, regsAfter);
            Assert.Equal(pcBefore, pcAfter);
            Assert.Equal(hiBefore, hiAfter);
            Assert.Equal(loBefore, loAfter);
        } finally {
            tempFile.Delete();
        }
    }
}
