using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;
using Xunit;
using App = PlusPim.Application.Application;

namespace PlusPimTests;

/// <summary>
/// 読み込み時のレジスタの初期値 (<c>$sp</c>，<c>$gp</c>) のテスト
/// </summary>
public class InitialRegistersTests {
    private const uint StackPointer = 0x7FFFEFFC;
    private const uint GlobalPointer = 0x10008000;

    /// <summary>
    /// 典型的なプロローグとエピローグ．<c>$sp</c> を使ってスタックに積み，戻す
    /// </summary>
    private const string Prologue = """
        .text
        main:
          addiu $sp, $sp, -4
          sw $ra, 0($sp)
          lw $t0, 0($sp)
          addiu $sp, $sp, 4
        """;

    [Fact]
    public void Load_SetsSpAndGpAndLeavesOthersZero() {
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(Prologue);
        try {
            uint[] registers = TestHelpers.TakeSnapshot(debugger).Registers;

            Assert.Equal(StackPointer, registers[(int)RegisterID.Sp]);
            Assert.Equal(GlobalPointer, registers[(int)RegisterID.Gp]);
            for(int i = 0; i < registers.Length; i++) {
                if(i is not ((int)RegisterID.Sp) and not ((int)RegisterID.Gp)) {
                    Assert.Equal(0u, registers[i]);
                }
            }
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void Constants_MatchSpimAndMars() {
        Assert.Equal(StackPointer, InitialRegisters.StackPointer);
        Assert.Equal(GlobalPointer, InitialRegisters.GlobalPointer);
    }

    [Fact]
    public void RuntimeContext_DoesNotApplyInitialValues() {
        // 命令レベルのテストのコンテキストはすべて 0 のまま
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        Assert.All(context.Registers.ToArray(), value => Assert.Equal(0u, value));
    }

    [Fact]
    public void StepBack_ToBeginning_KeepsInitialSpAndGp() {
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(Prologue);
        try {
            DebuggerSnapshot initial = TestHelpers.TakeSnapshot(debugger);

            _ = debugger.Step(); // addiu $sp, $sp, -4
            Assert.Equal(StackPointer - 4, TestHelpers.TakeSnapshot(debugger).Registers[(int)RegisterID.Sp]);
            _ = debugger.Step(); // sw $ra, 0($sp)

            // 初期値は履歴に含まれないので，先頭まで戻っても残る
            while(debugger.Back()) { }
            TestHelpers.AssertSnapshotEqual(initial, debugger);
        } finally {
            tempFile.Delete();
        }
    }

    [Fact]
    public void ReverseContinue_KeepsInitialSpAndGp() {
        FileInfo tempFile = TestHelpers.WriteTempAsm(Prologue);
        try {
            App app = new([tempFile], Logger.Null);
            Assert.True(app.Load());
            for(int i = 0; i < 4; i++) {
                _ = app.StepIn();
            }

            Assert.True(app.ReverseContinue());

            uint[] registers = app.GetCallStack()[0].Registers;
            Assert.Equal(StackPointer, registers[(int)RegisterID.Sp]);
            Assert.Equal(GlobalPointer, registers[(int)RegisterID.Gp]);
            Assert.Equal(0u, registers[(int)RegisterID.T0]);
        } finally {
            tempFile.Delete();
        }
    }
}
