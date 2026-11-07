using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// 分岐命令 <c>beq</c>，<c>bne</c>，<c>bgez</c>，<c>bgtz</c>，<c>blez</c>，<c>bltz</c> の命令レベルテスト
/// (doc/tests/instructions/instruction_tests.md，branch_model.md)
/// </summary>
public class BranchInstructionTests {
    /// <summary>
    /// テスト対象の分岐命令
    /// </summary>
    public enum BranchOp {
        Beq,
        Bne,
        Bgez,
        Bgtz,
        Blez,
        Bltz,
    }

    /// <summary>
    /// 分岐先 (values.md「ジャンプ先」)
    /// </summary>
    public enum BranchTarget {
        /// <summary>前方ラベル</summary>
        Forward,
        /// <summary>後方ラベル</summary>
        Backward,
        /// <summary>命令自身のアドレスを指すラベル</summary>
        Self,
        /// <summary>未定義ラベル</summary>
        Undefined,
    }

    private static readonly BranchOp[] EqualityOps = [BranchOp.Beq, BranchOp.Bne];

    private static readonly BranchOp[] ZeroOps = [BranchOp.Bgez, BranchOp.Bgtz, BranchOp.Blez, BranchOp.Bltz];

    /// <summary>
    /// 符号ビットだけが異なる組 (beq/bne の縮小集合に追加する)
    /// </summary>
    private static readonly (uint Rs, uint Rt)[] SignBitPairs = [
        (0x000cafe0, 0x800cafe0),
        (0x800cafe0, 0x000cafe0),
    ];

    /// <summary>
    /// 未定義ラベルへの分岐成立時の PC (branch_model.md)
    /// </summary>
    private const uint InvalidAddress = 0x00000000;

    // ---- パラメータ ----

    /// <summary>
    /// beq/bne の全ケース (命令，エイリアス，rs の値，rt の値，分岐先)
    /// </summary>
    public static TheoryData<BranchOp, Alias2Reg, uint, uint, BranchTarget> EqualityCases() {
        TheoryData<BranchOp, Alias2Reg, uint, uint, BranchTarget> data = new();
        List<(Alias2Reg Alias, uint Rs, uint Rt)> values = [];
        foreach(object[] row in TestValues.TwoSourceCases(TestValues.BranchEquality)) {
            values.Add(((Alias2Reg)row[0], (uint)row[1], (uint)row[2]));
        }
        foreach((uint rs, uint rt) in SignBitPairs) {
            values.Add((Alias2Reg.None, rs, rt));
        }

        foreach(BranchOp op in EqualityOps) {
            foreach((Alias2Reg alias, uint rs, uint rt) in values) {
                foreach(BranchTarget target in Enum.GetValues<BranchTarget>()) {
                    data.Add(op, alias, rs, rt, target);
                }
            }
        }
        return data;
    }

    public static TheoryData<BranchOp, Alias2Reg, uint, uint, BranchTarget> EqualityTakenCases() {
        return FilterEquality(taken: true);
    }

    public static TheoryData<BranchOp, Alias2Reg, uint, uint, BranchTarget> EqualityNotTakenCases() {
        return FilterEquality(taken: false);
    }

    private static TheoryData<BranchOp, Alias2Reg, uint, uint, BranchTarget> FilterEquality(bool taken) {
        TheoryData<BranchOp, Alias2Reg, uint, uint, BranchTarget> data = new();
        foreach(object[] row in EqualityCases()) {
            (BranchOp op, Alias2Reg alias, uint rs, uint rt, BranchTarget target) =
                ((BranchOp)row[0], (Alias2Reg)row[1], (uint)row[2], (uint)row[3], (BranchTarget)row[4]);
            if(IsTaken(op, rs, rt) == taken) {
                data.Add(op, alias, rs, rt, target);
            }
        }
        return data;
    }

    /// <summary>
    /// bgez/bgtz/blez/bltz の全ケース (命令，rs の値，分岐先)
    /// </summary>
    public static TheoryData<BranchOp, uint, BranchTarget> ZeroCases() {
        TheoryData<BranchOp, uint, BranchTarget> data = new();
        foreach(BranchOp op in ZeroOps) {
            foreach(uint rs in TestValues.BranchZero) {
                foreach(BranchTarget target in Enum.GetValues<BranchTarget>()) {
                    data.Add(op, rs, target);
                }
            }
        }
        return data;
    }

    public static TheoryData<BranchOp, uint, BranchTarget> ZeroTakenCases() {
        return FilterZero(taken: true);
    }

    public static TheoryData<BranchOp, uint, BranchTarget> ZeroNotTakenCases() {
        return FilterZero(taken: false);
    }

    private static TheoryData<BranchOp, uint, BranchTarget> FilterZero(bool taken) {
        TheoryData<BranchOp, uint, BranchTarget> data = new();
        foreach(object[] row in ZeroCases()) {
            (BranchOp op, uint rs, BranchTarget target) = ((BranchOp)row[0], (uint)row[1], (BranchTarget)row[2]);
            if(IsTaken(op, rs, 0) == taken) {
                data.Add(op, rs, target);
            }
        }
        return data;
    }

    public static TheoryData<BranchOp> AllOps() {
        TheoryData<BranchOp> data = new();
        foreach(BranchOp op in Enum.GetValues<BranchOp>()) {
            data.Add(op);
        }
        return data;
    }

    // ---- 実装に依存しない期待値の計算 ----

    /// <summary>
    /// 分岐が成立するか．bgez/bgtz/blez/bltz では <paramref name="rtVal"/> を用いない
    /// </summary>
    private static bool IsTaken(BranchOp op, uint rsVal, uint rtVal) {
        int signedRs = unchecked((int)rsVal);
        return op switch {
            BranchOp.Beq => rsVal == rtVal,
            BranchOp.Bne => rsVal != rtVal,
            BranchOp.Bgez => signedRs >= 0,
            BranchOp.Bgtz => signedRs > 0,
            BranchOp.Blez => signedRs <= 0,
            BranchOp.Bltz => signedRs < 0,
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };
    }

    private static string TargetLabelName(BranchTarget target) {
        return target switch {
            BranchTarget.Forward => InstructionHarness.ForwardLabel.Name,
            BranchTarget.Backward => InstructionHarness.BackwardLabel.Name,
            BranchTarget.Self => InstructionHarness.SelfLabel.Name,
            BranchTarget.Undefined => InstructionHarness.UndefinedLabelName,
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }

    private static uint TargetAddress(BranchTarget target) {
        return target switch {
            BranchTarget.Forward => 0x00400200,
            BranchTarget.Backward => 0x00400040,
            BranchTarget.Self => 0x00400100,
            BranchTarget.Undefined => InvalidAddress,
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }

    /// <summary>
    /// 実行後の PC．成立なら分岐先，不成立なら命令アドレス + 4
    /// </summary>
    private static Address ExpectedPC(bool taken, BranchTarget target) {
        return new Address(taken ? TargetAddress(target) : InstructionHarness.InstructionAddress.Addr + 4);
    }

    // ---- 準備 ----

    private static string Mnemonic(BranchOp op) {
        return op.ToString().ToLowerInvariant();
    }

    private static IInstruction ParseEquality(BranchOp op, RegisterID rs, RegisterID rt, BranchTarget target) {
        return InstructionHarness.Parse($"{Mnemonic(op)} {RegisterAliases.Name(rs)}, {RegisterAliases.Name(rt)}, {TargetLabelName(target)}");
    }

    private static IInstruction ParseZero(BranchOp op, BranchTarget target) {
        return InstructionHarness.Parse($"{Mnemonic(op)} {RegisterAliases.Name(RegisterAliases.Single)}, {TargetLabelName(target)}");
    }

    private static RuntimeContext SetupEquality(RegisterID rs, RegisterID rt, uint rsVal, uint rtVal) {
        RuntimeContext context = InstructionHarness.CreateUser();
        context.Registers[rs] = rsVal;
        context.Registers[rt] = rtVal;
        return context;
    }

    private static RuntimeContext SetupZero(uint rsVal) {
        RuntimeContext context = InstructionHarness.CreateUser();
        context.Registers[RegisterAliases.Single] = rsVal;
        return context;
    }

    // ---- beq/bne ----

    [Theory]
    [MemberData(nameof(EqualityTakenCases))]
    public void Execute_EqualityTaken_SetsPCToTarget(BranchOp op, Alias2Reg alias, uint rsVal, uint rtVal, BranchTarget target) {
        this.AssertEqualityExecute(op, alias, rsVal, rtVal, target, taken: true);
    }

    [Theory]
    [MemberData(nameof(EqualityNotTakenCases))]
    public void Execute_EqualityNotTaken_AdvancesPC(BranchOp op, Alias2Reg alias, uint rsVal, uint rtVal, BranchTarget target) {
        this.AssertEqualityExecute(op, alias, rsVal, rtVal, target, taken: false);
    }

    private void AssertEqualityExecute(BranchOp op, Alias2Reg alias, uint rsVal, uint rtVal, BranchTarget target, bool taken) {
        (RegisterID rs, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = ParseEquality(op, rs, rt, target);
        RuntimeContext context = SetupEquality(rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        // 未定義ラベルへの分岐成立でも C# の例外は発生しない (branch_model.md)
        Exception? ex = Record.Exception(() => Processor.Execute(context, inst));
        Assert.Null(ex);

        // PC だけが変化する
        MachineState expected = before.WithPC(ExpectedPC(taken, target));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(EqualityCases))]
    public void Undo_Equality_RestoresState(BranchOp op, Alias2Reg alias, uint rsVal, uint rtVal, BranchTarget target) {
        (RegisterID rs, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = ParseEquality(op, rs, rt, target);
        RuntimeContext context = SetupEquality(rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    // ---- bgez/bgtz/blez/bltz ----

    [Theory]
    [MemberData(nameof(ZeroTakenCases))]
    public void Execute_ZeroTaken_SetsPCToTarget(BranchOp op, uint rsVal, BranchTarget target) {
        this.AssertZeroExecute(op, rsVal, target, taken: true);
    }

    [Theory]
    [MemberData(nameof(ZeroNotTakenCases))]
    public void Execute_ZeroNotTaken_AdvancesPC(BranchOp op, uint rsVal, BranchTarget target) {
        this.AssertZeroExecute(op, rsVal, target, taken: false);
    }

    private void AssertZeroExecute(BranchOp op, uint rsVal, BranchTarget target, bool taken) {
        IInstruction inst = ParseZero(op, target);
        RuntimeContext context = SetupZero(rsVal);
        MachineState before = MachineState.Capture(context);

        // 未定義ラベルへの分岐成立でも C# の例外は発生しない (branch_model.md)
        Exception? ex = Record.Exception(() => Processor.Execute(context, inst));
        Assert.Null(ex);

        // PC だけが変化する
        MachineState expected = before.WithPC(ExpectedPC(taken, target));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(ZeroCases))]
    public void Undo_Zero_RestoresState(BranchOp op, uint rsVal, BranchTarget target) {
        IInstruction inst = ParseZero(op, target);
        RuntimeContext context = SetupZero(rsVal);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    // ---- パラメータの網羅 ----

    [Fact]
    public void Parameters_CoverTakenAndNotTakenForEveryOpAndTarget() {
        foreach(BranchOp op in EqualityOps) {
            foreach(BranchTarget target in Enum.GetValues<BranchTarget>()) {
                Assert.Contains(EqualityTakenCases(), row => (BranchOp)row[0] == op && (BranchTarget)row[4] == target);
                Assert.Contains(EqualityNotTakenCases(), row => (BranchOp)row[0] == op && (BranchTarget)row[4] == target);
            }
            foreach(Alias2Reg alias in Enum.GetValues<Alias2Reg>()) {
                Assert.Contains(EqualityCases(), row => (BranchOp)row[0] == op && (Alias2Reg)row[1] == alias);
            }
            foreach((uint rs, uint rt) in SignBitPairs) {
                Assert.Contains(EqualityCases(), row => (BranchOp)row[0] == op && (uint)row[2] == rs && (uint)row[3] == rt);
            }
        }
        foreach(BranchOp op in ZeroOps) {
            foreach(BranchTarget target in Enum.GetValues<BranchTarget>()) {
                Assert.Contains(ZeroTakenCases(), row => (BranchOp)row[0] == op && (BranchTarget)row[2] == target);
                Assert.Contains(ZeroNotTakenCases(), row => (BranchOp)row[0] == op && (BranchTarget)row[2] == target);
            }
        }
    }

    // ---- 繰り返し実行 ----

    /// <summary>
    /// 成立 → 不成立 → 成立の順に入力を変えて同じインスタンスを3回実行し，逆順に undo する
    /// </summary>
    [Theory]
    [MemberData(nameof(AllOps))]
    public void RepeatedExecute_TakenNotTakenTaken_UndoesInReverseOrder(BranchOp op) {
        bool isEquality = op is BranchOp.Beq or BranchOp.Bne;
        RegisterID rs = RegisterID.T0, rt = RegisterID.T1;
        const BranchTarget target = BranchTarget.Forward;
        IInstruction inst = isEquality ? ParseEquality(op, rs, rt, target) : ParseZero(op, target);
        RuntimeContext context = InstructionHarness.CreateUser();

        ((uint Rs, uint Rt) taken, (uint Rs, uint Rt) notTaken) = op switch {
            BranchOp.Beq => ((0x000cafe0u, 0x000cafe0u), (0x000cafe0u, 0x800cafe0u)),
            BranchOp.Bne => ((0x000cafe0u, 0x800cafe0u), (0x800babe0u, 0x800babe0u)),
            BranchOp.Bgez => ((0x00000000u, 0u), (0x80000000u, 0u)),
            BranchOp.Bgtz => ((0x00000001u, 0u), (0x00000000u, 0u)),
            BranchOp.Blez => ((0x00000000u, 0u), (0x00000001u, 0u)),
            BranchOp.Bltz => ((0xffffffffu, 0u), (0x00000000u, 0u)),
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };
        (uint Rs, uint Rt)[] inputs = [taken, notTaken, taken];
        bool[] expectedTaken = [true, false, true];

        // ループで同じ命令に戻ってきたとみなし，入力レジスタと PC を設定する (rt は beq/bne でのみ使う)
        Action<RuntimeContext>[] steps = [.. inputs.Select(input => (Action<RuntimeContext>)(ctx => {
            ctx.Registers[rs] = input.Rs;
            if(isEquality) {
                ctx.Registers[rt] = input.Rt;
            }
            ctx.PC = InstructionHarness.InstructionAddress;
        }))];

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(inst, context, steps);

        for(int i = 0; i < inputs.Length; i++) {
            Assert.Equal(expectedTaken[i], IsTaken(op, inputs[i].Rs, inputs[i].Rt));
            MachineState expected = results[i].BeforeExecute.WithPC(ExpectedPC(expectedTaken[i], target));
            MachineState.AssertEqual(expected, results[i].AfterExecute);
        }
    }
}
