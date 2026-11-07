using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// <c>j $target</c>，<c>jal $target</c>，<c>jr $rs</c> の命令レベルテスト
/// (doc/tests/instructions/instruction_tests.md，jump_model.md)
/// </summary>
public class JumpInstructionTests {
    /// <summary>
    /// ラベルによるジャンプ先 (values.md「ジャンプ先」)
    /// </summary>
    public enum LabelTarget {
        /// <summary>前方ラベル <c>fwd</c></summary>
        Forward,
        /// <summary>後方ラベル <c>back</c></summary>
        Backward,
        /// <summary>命令自身のアドレスを指すラベル <c>self</c></summary>
        Self,
        /// <summary>未定義ラベル</summary>
        Undefined,
    }

    /// <summary>
    /// <c>jal</c> 実行前のコールスタック (jump_model.md「追加のパラメータ」)
    /// </summary>
    public enum CallStackSetup {
        /// <summary>空</summary>
        Empty,
        /// <summary><c>jal</c> 1回分のフレームが積まれている</summary>
        OneFrame,
    }

    /// <summary>
    /// <c>jr</c> のソースレジスタ
    /// </summary>
    public enum JrRegister {
        /// <summary><c>$ra</c></summary>
        Ra,
        /// <summary><c>$t0</c></summary>
        T0,
    }

    /// <summary>
    /// <c>jr</c> のジャンプ先 (レジスタ値だがジャンプ先に準ずる値)
    /// </summary>
    public enum JrTarget {
        /// <summary>前方ラベルのアドレス</summary>
        ForwardLabelAddress,
        /// <summary>後方ラベルのアドレス</summary>
        BackwardLabelAddress,
        /// <summary>命令自身のアドレス</summary>
        SelfAddress,
        /// <summary>ラベルでないアドレス</summary>
        NonLabelAddress,
    }

    /// <summary>
    /// <c>jr</c> 実行前のコールスタック．
    /// 「<c>jal</c> 1回分のフレーム」をフレームの <c>PC + 4</c> がジャンプ先と一致するかで2通りに分ける
    /// </summary>
    public enum JrCallStack {
        /// <summary>空</summary>
        Empty,
        /// <summary>先頭フレームの <c>PC + 4</c> がジャンプ先と一致する</summary>
        ReturnFrame,
        /// <summary>先頭フレームの <c>PC + 4</c> がジャンプ先と一致しない</summary>
        OtherFrame,
    }

    private static readonly Address InstructionAddress = InstructionHarness.InstructionAddress;

    /// <summary>
    /// 既存フレームを積んだ <c>jal</c> が呼び出していた関数のラベル (命令はこの関数内で実行中とみなす)
    /// </summary>
    private static readonly Label CalleeLabel = new("callee", new Address(0x00400080));

    /// <summary>
    /// <see cref="CallStackSetup.OneFrame"/> のフレームを積んだ <c>jal</c> のアドレス
    /// </summary>
    private static readonly Address OuterJalAddress = new(0x00400010);

    /// <summary>
    /// <see cref="JrCallStack.OtherFrame"/> のフレームを積んだ <c>jal</c> のアドレス．
    /// <c>PC + 4</c> はどの <see cref="JrTarget"/> とも一致しない
    /// </summary>
    private static readonly Address UnrelatedJalAddress = new(0x00400300);

    /// <summary>
    /// <c>jal</c> 実行前の <c>$ra</c>．戻り先アドレスと異なる値にして，フレームのスナップショットが変更前であることを検出する
    /// </summary>
    private const uint InitialRa = 0x0badf00c;

    // ---- パラメータ ----

    public static TheoryData<LabelTarget> JCases() {
        TheoryData<LabelTarget> data = new();
        foreach(LabelTarget target in Enum.GetValues<LabelTarget>()) {
            data.Add(target);
        }
        return data;
    }

    public static TheoryData<LabelTarget, CallStackSetup> JalCases() {
        TheoryData<LabelTarget, CallStackSetup> data = new();
        foreach(LabelTarget target in Enum.GetValues<LabelTarget>()) {
            foreach(CallStackSetup setup in Enum.GetValues<CallStackSetup>()) {
                data.Add(target, setup);
            }
        }
        return data;
    }

    public static TheoryData<JrRegister, JrTarget, JrCallStack> JrCases() {
        TheoryData<JrRegister, JrTarget, JrCallStack> data = new();
        foreach(JrRegister rs in Enum.GetValues<JrRegister>()) {
            foreach(JrTarget target in Enum.GetValues<JrTarget>()) {
                foreach(JrCallStack stack in Enum.GetValues<JrCallStack>()) {
                    data.Add(rs, target, stack);
                }
            }
        }
        return data;
    }

    // ---- 実装に依存しない期待値の計算 ----

    private static string LabelName(LabelTarget target) {
        return target switch {
            LabelTarget.Forward => InstructionHarness.ForwardLabel.Name,
            LabelTarget.Backward => InstructionHarness.BackwardLabel.Name,
            LabelTarget.Self => InstructionHarness.SelfLabel.Name,
            LabelTarget.Undefined => InstructionHarness.UndefinedLabelName,
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }

    /// <summary>
    /// ジャンプ先のラベル．未定義ラベルは無効ラベル (アドレス <c>0</c>)
    /// </summary>
    private static Label ExpectedLabel(LabelTarget target) {
        return target switch {
            LabelTarget.Forward => new Label("fwd", new Address(0x00400200)),
            LabelTarget.Backward => new Label("back", new Address(0x00400040)),
            LabelTarget.Self => new Label("self", new Address(0x00400100)),
            LabelTarget.Undefined => Label.Invalid,
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }

    /// <summary>
    /// ジャンプ後の PC．未定義ラベルは無効アドレス <c>0</c> (branch_model.md と同じ規則)
    /// </summary>
    private static Address ExpectedPC(LabelTarget target) {
        return target switch {
            LabelTarget.Forward => new Address(0x00400200),
            LabelTarget.Backward => new Address(0x00400040),
            LabelTarget.Self => new Address(0x00400100),
            LabelTarget.Undefined => new Address(0),
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }

    private static uint JrTargetValue(JrTarget target) {
        return target switch {
            JrTarget.ForwardLabelAddress => 0x00400200,
            JrTarget.BackwardLabelAddress => 0x00400040,
            JrTarget.SelfAddress => 0x00400100,
            JrTarget.NonLabelAddress => 0x7ffffffc,
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }

    private static RegisterID JrRegisterId(JrRegister rs) {
        return rs switch {
            JrRegister.Ra => RegisterID.Ra,
            JrRegister.T0 => RegisterID.T0,
            _ => throw new ArgumentOutOfRangeException(nameof(rs)),
        };
    }

    /// <summary>
    /// <c>jal</c> の結果を検証する: <c>$ra = PC + 4</c>，PC がジャンプ先，
    /// 実行前の PC・ラベル・レジスタ・HI/LO を持つフレームが1つ積まれ，現在のラベルがジャンプ先のラベルになる
    /// </summary>
    private static void AssertJalResult(MachineState before, MachineState after, LabelTarget target) {
        Assert.Equal(before.CallStackDepth + 1, after.CallStackDepth);
        StackFrame pushed = after.CallStack[0];
        Assert.Equal(before.PC, pushed.CurrentPC);
        Assert.Equal(before.CurrentLabel, pushed.Label);
        Assert.Equal(before.Registers, pushed.Registers.ToArray());
        Assert.Equal(before.HI, pushed.HISnapshot);
        Assert.Equal(before.LO, pushed.LOSnapshot);

        MachineState expected = before
            .WithRegister(RegisterID.Ra, unchecked(before.PC.Addr + 4u))
            .WithPC(ExpectedPC(target))
            .WithCurrentLabel(ExpectedLabel(target))
            .WithFramePushed(pushed);
        MachineState.AssertEqual(expected, after);
    }

    /// <summary>
    /// <c>jr</c> の期待状態: PC がジャンプ先になり，
    /// 先頭フレームの <c>PC + 4</c> がジャンプ先と一致すればそのフレームを取り出して現在のラベルをフレームのラベルに戻す．
    /// <c>$ra</c> を使い，コールスタックが空でフレームを取り出さなかった場合は終了する
    /// </summary>
    private static MachineState ExpectedJr(MachineState before, RegisterID rs, uint target) {
        MachineState expected = before.WithPC(new Address(target));
        if(before.CallStack.Length > 0 && unchecked(before.CallStack[0].CurrentPC.Addr + 4u) == target) {
            return expected.WithFramePopped().WithCurrentLabel(before.CallStack[0].Label);
        }
        if(rs == RegisterID.Ra && before.CallStack.Length == 0) {
            return expected.WithTerminated();
        }
        return expected;
    }

    // ---- 準備 ----

    private static IInstruction ParseJ(LabelTarget target) {
        return InstructionHarness.Parse($"j {LabelName(target)}");
    }

    private static IInstruction ParseJal(LabelTarget target) {
        return InstructionHarness.Parse($"jal {LabelName(target)}");
    }

    private static IInstruction ParseJr(RegisterID rs) {
        return InstructionHarness.Parse($"jr {RegisterAliases.Name(rs)}");
    }

    private static RuntimeContext SetupJal(CallStackSetup setup) {
        RuntimeContext context = InstructionHarness.CreateUser();
        if(setup == CallStackSetup.OneFrame) {
            InstructionHarness.PushCallFrame(context, OuterJalAddress, CalleeLabel);
        }
        context.Registers[RegisterID.Ra] = InitialRa;
        return context;
    }

    private static RuntimeContext SetupJr(RegisterID rs, uint target, JrCallStack stack) {
        RuntimeContext context = InstructionHarness.CreateUser();
        switch(stack) {
            case JrCallStack.Empty:
                break;
            case JrCallStack.ReturnFrame:
                InstructionHarness.PushCallFrame(context, new Address(target - 4u), CalleeLabel);
                break;
            case JrCallStack.OtherFrame:
                InstructionHarness.PushCallFrame(context, UnrelatedJalAddress, CalleeLabel);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stack));
        }
        context.Registers[rs] = target;
        return context;
    }

    // ---- j ----

    [Theory]
    [MemberData(nameof(JCases))]
    public void Execute_J_JumpsToTarget(LabelTarget target) {
        IInstruction inst = ParseJ(target);
        RuntimeContext context = InstructionHarness.CreateUser();
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // PC だけが変化し，コールスタックと現在のラベルは変わらない
        MachineState expected = before.WithPC(ExpectedPC(target));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(JCases))]
    public void Undo_J_RestoresState(LabelTarget target) {
        IInstruction inst = ParseJ(target);
        RuntimeContext context = InstructionHarness.CreateUser();
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    [Fact]
    public void RepeatedExecute_J_UndoesInReverseOrder() {
        IInstruction inst = ParseJ(LabelTarget.Backward);
        RuntimeContext context = InstructionHarness.CreateUser();

        // ループで同じ命令に異なる経路から到達する
        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            c => c.PC = InstructionAddress,
            c => c.PC = new Address(0x00400300),
            c => c.PC = new Address(0x00400040)
        );

        foreach((MachineState beforeExecute, MachineState afterExecute) in results) {
            MachineState.AssertEqual(beforeExecute.WithPC(ExpectedPC(LabelTarget.Backward)), afterExecute);
        }
    }

    // ---- jal ----

    [Theory]
    [MemberData(nameof(JalCases))]
    public void Execute_Jal_LinksJumpsAndPushesFrame(LabelTarget target, CallStackSetup setup) {
        IInstruction inst = ParseJal(target);
        RuntimeContext context = SetupJal(setup);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        AssertJalResult(before, MachineState.Capture(context), target);
    }

    [Theory]
    [MemberData(nameof(JalCases))]
    public void Undo_Jal_RestoresState(LabelTarget target, CallStackSetup setup) {
        IInstruction inst = ParseJal(target);
        RuntimeContext context = SetupJal(setup);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        // フレームは参照で比較されるため，既存フレームの同一性も確認される
        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    [Fact]
    public void RepeatedExecute_Jal_UndoesInReverseOrder() {
        IInstruction inst = ParseJal(LabelTarget.Forward);
        RuntimeContext context = SetupJal(CallStackSetup.OneFrame);

        // 実行ごとに PC (戻り先) と HI/LO を変え，フレームが入れ子に積まれる
        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            c => c.PC = InstructionAddress,
            c => {
                c.PC = new Address(0x00400300);
                c.HI = 0x000cafe0;
            },
            c => {
                c.PC = new Address(0x00400040);
                c.LO = 0x800babe0;
            }
        );

        foreach((MachineState beforeExecute, MachineState afterExecute) in results) {
            AssertJalResult(beforeExecute, afterExecute, LabelTarget.Forward);
        }
    }

    // ---- jr ----

    [Theory]
    [MemberData(nameof(JrCases))]
    public void Execute_Jr_JumpsAndUpdatesCallStack(JrRegister rsParam, JrTarget targetParam, JrCallStack stack) {
        RegisterID rs = JrRegisterId(rsParam);
        uint target = JrTargetValue(targetParam);
        IInstruction inst = ParseJr(rs);
        RuntimeContext context = SetupJr(rs, target, stack);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState.AssertEqual(ExpectedJr(before, rs, target), MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(JrCases))]
    public void Undo_Jr_RestoresState(JrRegister rsParam, JrTarget targetParam, JrCallStack stack) {
        RegisterID rs = JrRegisterId(rsParam);
        uint target = JrTargetValue(targetParam);
        IInstruction inst = ParseJr(rs);
        RuntimeContext context = SetupJr(rs, target, stack);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        // 取り出したフレームの同一性，現在のラベル，IsTerminated も戻る
        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    [Fact]
    public void RepeatedExecute_Jr_UndoesInReverseOrder() {
        IInstruction inst = ParseJr(RegisterID.Ra);
        RuntimeContext context = InstructionHarness.CreateUser();
        Address innerJalAddress = new(0x00400090);
        InstructionHarness.PushCallFrame(context, OuterJalAddress, CalleeLabel);
        InstructionHarness.PushCallFrame(context, innerJalAddress, new Label("inner", new Address(0x00400400)));

        // 取り出さない → 内側から戻る → 外側から戻る → main から戻る (終了)
        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            RepeatedExecution.SetRegisters((RegisterID.Ra, 0x7ffffffc)),
            RepeatedExecution.SetRegisters((RegisterID.Ra, innerJalAddress.Addr + 4u)),
            RepeatedExecution.SetRegisters((RegisterID.Ra, OuterJalAddress.Addr + 4u)),
            RepeatedExecution.SetRegisters((RegisterID.Ra, 0x00400040))
        );

        Assert.Equal(new[] { 2, 1, 0, 0 }, results.Select(r => r.AfterExecute.CallStackDepth));
        Assert.True(results[^1].AfterExecute.IsTerminated);
        foreach((MachineState beforeExecute, MachineState afterExecute) in results) {
            MachineState.AssertEqual(ExpectedJr(beforeExecute, RegisterID.Ra, beforeExecute.Registers[(int)RegisterID.Ra]), afterExecute);
        }
    }

    // ---- パラメータの網羅 ----

    [Fact]
    public void Parameters_CoverAllTargetsAndCallStacks() {
        Assert.Equal(Enum.GetValues<LabelTarget>().Length, JCases().Count<object[]>());
        Assert.Equal(Enum.GetValues<LabelTarget>().Length * Enum.GetValues<CallStackSetup>().Length, JalCases().Count<object[]>());
        Assert.NotEmpty(JrCases());

        // jr は「取り出す」「取り出さない」「取り出さずに終了する」をすべて含む
        bool pops = false, keeps = false, terminates = false;
        foreach(object[] row in JrCases()) {
            (JrRegister rs, JrCallStack stack) = ((JrRegister)row[0], (JrCallStack)row[2]);
            pops |= stack == JrCallStack.ReturnFrame;
            keeps |= stack == JrCallStack.OtherFrame;
            terminates |= rs == JrRegister.Ra && stack == JrCallStack.Empty;
        }
        Assert.True(pops);
        Assert.True(keeps);
        Assert.True(terminates);
    }
}
