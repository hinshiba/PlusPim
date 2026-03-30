using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests;

public class TimeTravelTests {
    [Fact]
    public void StepBack_OneStep_Roundtrip() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 42
              addiu $t1, $zero, 10
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            // Record initial state
            (uint[] initRegs, uint initPC, uint initHI, uint initLO) = debugger.GetRegisters();

            // Step forward
            debugger.Step();
            (uint[] stateA_Regs, uint stateA_PC, uint stateA_HI, uint stateA_LO) = debugger.GetRegisters();

            // StepBack → should return to initial
            Assert.True(debugger.StepBack());
            (uint[] backRegs, uint backPC, uint backHI, uint backLO) = debugger.GetRegisters();
            Assert.Equal(initRegs, backRegs);
            Assert.Equal(initPC, backPC);

            // Re-Step → should return to stateA
            debugger.Step();
            (uint[] reRegs, uint rePC, uint reHI, uint reLO) = debugger.GetRegisters();
            Assert.Equal(stateA_Regs, reRegs);
            Assert.Equal(stateA_PC, rePC);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void StepBack_FullRewind() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 1
              addiu $t1, $zero, 2
              addiu $t2, $zero, 3
              addiu $t3, $zero, 4
              addiu $t4, $zero, 5
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            int n = 5;
            List<(uint[] Regs, uint PC, uint HI, uint LO)> states = new();

            // Record state before each step
            states.Add(debugger.GetRegisters());
            for(int i = 0; i < n; i++) {
                debugger.Step();
                states.Add(debugger.GetRegisters());
            }

            // StepBack N times, checking each intermediate state in reverse
            for(int i = n; i >= 1; i--) {
                Assert.True(debugger.StepBack());

                (uint[] regs, uint pc, _, _) = debugger.GetRegisters();
                Assert.Equal(states[i - 1].Regs, regs);
                Assert.Equal(states[i - 1].PC, pc);
            }
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void StepBack_PartialRewindThenReexecute() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 1
              addiu $t1, $zero, 2
              addiu $t2, $zero, 3
              addiu $t3, $zero, 4
              addiu $t4, $zero, 5
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            // Step 5
            List<(uint[] Regs, uint PC, uint HI, uint LO)> states = new();
            states.Add(debugger.GetRegisters());
            for(int i = 0; i < 5; i++) {
                debugger.Step();
                states.Add(debugger.GetRegisters());
            }

            // StepBack 2
            _ = debugger.StepBack();
            _ = debugger.StepBack();


            // State should match step 3
            (uint[] regs3, uint pc3, _, _) = debugger.GetRegisters();
            Assert.Equal(states[3].Regs, regs3);
            Assert.Equal(states[3].PC, pc3);

            // Re-Step 2
            debugger.Step();
            debugger.Step();


            // State should match step 5
            (uint[] regs5, uint pc5, _, _) = debugger.GetRegisters();
            Assert.Equal(states[5].Regs, regs5);
            Assert.Equal(states[5].PC, pc5);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void StepBack_AtInitialState_ReturnsFalse() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 1
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            Assert.False(debugger.StepBack());
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void StepBack_LoopTimeTravel() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 0
              addiu $t1, $zero, 3
            loop:
              addiu $t0, $t0, 1
              bne $t0, $t1, loop
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            // Execute full loop: 2 init + 3 iterations * 2 = 8 steps
            int totalSteps = 8;
            for(int i = 0; i < totalSteps; i++) {
                debugger.Step();
            }

            (uint[] finalRegs, uint finalPC, _, _) = debugger.GetRegisters();

            // Full rewind
            for(int i = 0; i < totalSteps; i++) {
                Assert.True(debugger.StepBack());
            }

            // Re-execute
            for(int i = 0; i < totalSteps; i++) {
                debugger.Step();
            }

            // Should match original final state
            (uint[] reRegs, uint rePC, _, _) = debugger.GetRegisters();
            Assert.Equal(finalRegs, reRegs);
            Assert.Equal(finalPC, rePC);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void StepBack_BranchStability() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 5
              addiu $t1, $zero, 5
              beq $t0, $t1, target
              addiu $t2, $zero, 999
            target:
              addiu $t3, $zero, 42
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            debugger.Step(); // addiu $t0
            debugger.Step(); // addiu $t1
            debugger.Step(); // beq (should be taken)

            (uint[] afterBranch, uint pcAfterBranch, uint hi1, uint lo1) = debugger.GetRegisters();

            // StepBack to before beq
            _ = debugger.StepBack();
            // Re-step (beq again)
            debugger.Step();

            (uint[] reAfterBranch, uint rePcAfterBranch, uint hi2, uint lo2) = debugger.GetRegisters();
            Assert.Equal(afterBranch, reAfterBranch);
            Assert.Equal(pcAfterBranch, rePcAfterBranch);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void StepBack_JalJr_Roundtrip() {
        string asm = """
            .text
            main:
              jal func
              addiu $t0, $zero, 99
              break
            func:
              addiu $t1, $zero, 42
              jr $ra
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            // Step through: jal → addiu $t1 → jr $ra → addiu $t0
            List<(uint[] Regs, uint PC, uint HI, uint LO)> states = new();
            states.Add(debugger.GetRegisters());

            for(int i = 0; i < 4; i++) {
                debugger.Step();
                states.Add(debugger.GetRegisters());
            }

            // StepBack through entire sequence
            for(int i = 4; i >= 1; i--) {
                Assert.True(debugger.StepBack());

                (uint[] regs, uint pc, _, _) = debugger.GetRegisters();
                Assert.Equal(states[i - 1].Regs, regs);
                Assert.Equal(states[i - 1].PC, pc);
            }
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void StepBack_MemoryTimeTravel() {
        string asm = """
            .text
            main:
              lui $sp, 0x1000
              addiu $t0, $zero, 123
              sw $t0, 0($sp)
              lw $t1, 0($sp)
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            debugger.Step(); // lui $sp
            debugger.Step(); // addiu $t0

            (uint[] beforeSw, uint pcBeforeSw, uint hi1, uint lo1) = debugger.GetRegisters();

            debugger.Step(); // sw

            (uint[] afterSw, uint pcAfterSw, uint hi2, uint lo2) = debugger.GetRegisters();

            // StepBack (undo sw)
            _ = debugger.StepBack();

            (uint[] undoSw, uint pcUndoSw, uint hi3, uint lo3) = debugger.GetRegisters();
            Assert.Equal(beforeSw, undoSw);
            Assert.Equal(pcBeforeSw, pcUndoSw);

            // Re-Step (sw again)
            debugger.Step();

            (uint[] reSw, uint pcReSw, uint hi4, uint lo4) = debugger.GetRegisters();
            Assert.Equal(afterSw, reSw);
            Assert.Equal(pcAfterSw, pcReSw);

            // Continue to lw
            debugger.Step();

            (uint[] afterLw, uint pcAfterLw, uint hi5, uint lo5) = debugger.GetRegisters();
            Assert.Equal(123u, afterLw[(int)RegisterID.T1]);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void StepBack_MultipleFullCycles() {
        string asm = """
            .text
            main:
              addiu $t0, $zero, 10
              addiu $t1, $zero, 20
              add $t2, $t0, $t1
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            int n = 3;
            (uint[] initRegs, uint initPC, uint initHI, uint initLO) = debugger.GetRegisters();

            // Cycle 1: Step N → Back N
            for(int i = 0; i < n; i++) {
                debugger.Step();
            }
            for(int i = 0; i < n; i++) {
                Assert.True(debugger.StepBack());
            }

            (uint[] cycle1Regs, uint cycle1PC, _, _) = debugger.GetRegisters();
            Assert.Equal(initRegs, cycle1Regs);
            Assert.Equal(initPC, cycle1PC);

            // Cycle 2: Step N → Back N
            for(int i = 0; i < n; i++) {
                debugger.Step();
            }
            for(int i = 0; i < n; i++) {
                Assert.True(debugger.StepBack());
            }

            (uint[] cycle2Regs, uint cycle2PC, _, _) = debugger.GetRegisters();
            Assert.Equal(initRegs, cycle2Regs);
            Assert.Equal(initPC, cycle2PC);
        } finally {
            tempFile.Delete();
        }
    }
}
