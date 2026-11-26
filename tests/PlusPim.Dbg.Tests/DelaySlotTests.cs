using PlusPim.Image;
using PlusPim.Instructions;
using System.Buffers;
using static PlusPim.Dbg.Tests.Harness;
using static PlusPim.Instructions.GprId;

namespace PlusPim.Dbg.Tests;

/// <summary>
/// 分岐とロードの遅延スロットを, 1ステップごとの状態で観測する
/// </summary>
/// <remarks>docs/tests/debugger/delay_slot_model.md</remarks>
public class DelaySlotTests {
    /// <summary><c>main</c> のアドレス. テキストの先頭</summary>
    private static readonly Address Main = MemoryMap.Text.Base;

    private const string Head = ".text\n.globl main\nmain:\n";

    /// <summary><c>w</c> は7, <c>$t1</c> は <c>w</c> を指してから <c>body</c> を実行する. <c>body</c> は <c>Main + 8</c> から始まる</summary>
    private static string WithWord(string body) {
        return ".data\nw: .word 7\n" + Head + "la $t1, w\n" + body;
    }

    private static Debuggee Load(DelaySlotsConfig slots, params string[] sources) {
        return Debuggee.Load(Assemble(sources), slots);
    }

    [Theory]
    [InlineData(DelaySlotsConfig.All, 1u)]
    [InlineData(DelaySlotsConfig.Load, 0u)]
    public void TakenJump_RunsSlotThenTarget(DelaySlotsConfig slots, uint slotRan) {
        Debuggee d = Load(slots, Head + "j l\naddiu $t0, $zero, 1\naddiu $t1, $zero, 1\nl: nop");
        Address l = Main + 12;
        Core c = d.Step();
        if(slotRan == 1) {
            Assert.Equal(new Branch(Main, l, Transfer.Jump), c.Branch);
            Assert.Equal(Main + 4, c.Pc);
            c = d.Step();
        }
        Assert.Null(c.Branch);
        Assert.Equal(l, c.Pc);
        Assert.Equal(slotRan, c.Registers[T0]);
        Assert.Equal(0u, c.Registers[T1]);
    }

    [Fact]
    public void NotTakenBranch_RunsSlotThenFallsThrough() {
        Debuggee d = Load(DelaySlotsConfig.All, Head + "bne $zero, $zero, l\naddiu $t0, $zero, 1\naddiu $t1, $zero, 1\nl: nop");
        Assert.Equal(new Branch(Main, Main + 8, Transfer.Fallthrough), d.Step().Branch);
        Core c = d.Step();
        Assert.Null(c.Branch);
        Assert.Equal(Main + 8, c.Pc);
        Assert.Equal(1u, c.Registers[T0]);
        Assert.Equal(1u, d.Step().Registers[T1]);
    }

    /// <summary>戻りアドレスは分岐の遅延スロットが有効なら8バイト後, 無効なら4バイト後</summary>
    [Theory]
    [InlineData(DelaySlotsConfig.All, 8u)]
    [InlineData(DelaySlotsConfig.Load, 4u)]
    public void Link_DependsOnBranchSlot(DelaySlotsConfig slots, uint link) {
        const string F = "\nnop\nf: nop";
        Assert.Equal((Main + link).Addr, Load(slots, Head + "jal f" + F).Step().Registers[Ra]);
        // 不成立でも書く
        Assert.Equal((Main + link).Addr, Load(slots, Head + "bltzal $zero, f" + F).Step().Registers[Ra]);
        // la は2命令に展開される
        Assert.Equal((Main + 8 + link).Addr, Load(slots, Head + "la $t9, f\njalr $t0, $t9" + F).Step(3).Registers[T0]);
    }

    /// <summary>遅延スロットは呼び出し元の深さで実行され, 移った後にフレームが積まれ, 戻った後に取り出される</summary>
    [Fact]
    public void Call_PushesAndPopsAfterSlot() {
        Debuggee d = Load(DelaySlotsConfig.All, Head + "jal f\naddiu $t0, $zero, 1\nnop\nf:\njr $ra\naddiu $t1, $zero, 1");
        Address f = Main + 12;
        Assert.Equal(0, d.Step().Depth);
        Core c = d.Step();
        Assert.Equal((1, f, 1u), (c.Depth, c.Pc, c.Registers[T0]));
        Assert.Equal((f, Main), (c.CallStack.Peek().Entry, c.CallStack.Peek().CallSite));
        Assert.Equal(1, d.Step().Depth);
        c = d.Step();
        Assert.Equal((0, Main + 8, 1u), (c.Depth, c.Pc, c.Registers[T1]));
    }

    [Fact]
    public void ReturnFromMain_ExitsAfterSlot() {
        Debuggee d = Load(DelaySlotsConfig.All, Head + "jr $ra\naddiu $t0, $zero, 1");
        Assert.True(d.Step().Status is Running);
        Core c = d.Step();
        Assert.Equal(new Exited(ExitCause.Returned), c.Status);
        Assert.Equal(1u, c.Registers[T0]);
    }

    /// <summary>EPCは分岐を指し, eretで分岐からやり直す. ハンドラは2回目に整列するよう<c>$t1</c>を直す</summary>
    [Fact]
    public void ExceptionInSlot_PointsEpcAtBranchAndSetsBd() {
        Debuggee d = Load(DelaySlotsConfig.All,
            WithWord("addiu $t1, $t1, 1\nj l\nlw $t0, 0($t1)\nnop\nl: nop\nnop"),
            ".ktext\naddiu $t1, $t1, -1\neret");
        Address j = Main + 12;
        Core c = d.Step(5);
        Assert.Equal(new ExceptionRaised(ExcCode.AdEL, j), c.Status);
        Assert.Equal((j.Addr, ((uint)ExcCode.AdEL << 2) | Cp0.Bd), (c.Registers[RName.Epc], c.Registers[RName.Cause]));
        Assert.Null(c.Branch);
        // ベクタ, addiu, eret, j, lw
        c = d.Step(5);
        Assert.Equal(Main + 24, c.Pc);
        Assert.Equal(new DelayedLoad(T0, 7), c.Load);
    }

    [Fact]
    public void ExceptionOutsideSlot_ClearsBd() {
        Core c = Load(DelaySlotsConfig.All, Head + "lw $t0, 1($zero)").Step();
        Assert.Equal(new ExceptionRaised(ExcCode.AdEL, Main), c.Status);
        Assert.Equal((uint)ExcCode.AdEL << 2, c.Registers[RName.Cause]);
    }

    [Theory]
    [InlineData("j l")]
    [InlineData("beq $zero, $zero, l")]
    [InlineData("bne $zero, $zero, l")]
    [InlineData("jal l")]
    public void TransferInSlot_IsRuntimeError(string slot) {
        Core c = Load(DelaySlotsConfig.All, Head + $"j l\n{slot}\nl: nop").Step(2);
        Assert.Equal(new RuntimeErrorRaised(RuntimeErrorKind.TransferInDelaySlot, Main + 4), c.Status);
        // 遅延スロットの命令は完了していないので, 分岐は着いていない
        Assert.Equal(new Branch(Main, Main + 8, Transfer.Jump), c.Branch);
    }

    [Fact]
    public void EretInSlot_IsRuntimeError() {
        Debuggee d = Load(DelaySlotsConfig.All, Head + "syscall", ".ktext\nb l\neret\nl: eret");
        // syscall, ベクタ, b, eret
        Core c = d.Step(4);
        Assert.Equal(new RuntimeErrorRaised(RuntimeErrorKind.TransferInDelaySlot, MemoryMap.ExceptionVector + 4), c.Status);
    }

    /// <summary>ロードの次の命令は旧値を, その次は新しい値を読む</summary>
    [Theory]
    [InlineData(DelaySlotsConfig.All, 0u)]
    [InlineData(DelaySlotsConfig.Branch, 7u)]
    public void Load_NextInstructionReadsOldValue(DelaySlotsConfig slots, uint inSlot) {
        Debuggee d = Load(slots, WithWord("lw $t0, 0($t1)\naddu $t2, $t0, $zero\naddu $t3, $t0, $zero"));
        Assert.Equal(inSlot == 0 ? new DelayedLoad(T0, 7) : null, d.Step(3).Load);
        Core c = d.Step();
        Assert.Null(c.Load);
        Assert.Equal((inSlot, 7u), (c.Registers[T2], c.Registers[T0]));
        Assert.Equal(7u, d.Step().Registers[T3]);
    }

    [Fact]
    public void LoadSlotWrite_Wins() {
        Debuggee d = Load(DelaySlotsConfig.All, WithWord("lw $t0, 0($t1)\naddiu $t0, $zero, 5\nnop"));
        Assert.Equal(5u, d.Step(4).Registers[T0]);
        Assert.Equal(5u, d.Step().Registers[T0]);
    }

    [Fact]
    public void ExceptionInLoadSlot_CompletesLoad() {
        Core c = Load(DelaySlotsConfig.All, WithWord("lw $t0, 0($t1)\nlw $t2, 1($t1)")).Step(4);
        Assert.True(c.Status is ExceptionRaised);
        Assert.Null(c.Load);
        Assert.Equal(7u, c.Registers[T0]);
    }

    /// <summary>分岐の遅延スロットのロードは, 移った先の最初の命令が遅延スロットになる</summary>
    [Fact]
    public void LoadInBranchSlot_DelaysTargetInstruction() {
        Debuggee d = Load(DelaySlotsConfig.All, WithWord("j l\nlw $t0, 0($t1)\nnop\nl: addu $t2, $t0, $zero\naddu $t3, $t0, $zero"));
        Core c = d.Step(6);
        Assert.Equal((0u, 7u), (c.Registers[T2], c.Registers[T3]));
    }

    /// <summary>入力待ちでは状態を変えず, ロードも完了しない</summary>
    [Fact]
    public void NeedInputInLoadSlot_KeepsLoadPending() {
        Debuggee d = Load(DelaySlotsConfig.All, WithWord("addiu $v0, $zero, 12\nsyscall"), ".ktext\nlw $t0, 0($t1)\nruntime_call!");
        // la, addiu, syscall, ベクタ, lw
        Core c = d.Step(6);
        Assert.Equal(StepResult.NeedInput, d.Step(new ArrayBufferWriter<byte>()));
        Assert.Equal(c.Load, d.View.Core.Load);
        Assert.Equal(c.Registers, d.View.Core.Registers);
        d.Feed("a"u8);
        c = d.Step();
        Assert.Equal((7u, (uint)'a'), (c.Registers[T0], c.Registers[V0]));
    }
}
