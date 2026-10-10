using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// シフト命令 <c>sll</c>，<c>srl</c>，<c>sra</c> (<c>op $rd, $rt, shamt</c>) と
/// <c>sllv</c>，<c>srlv</c>，<c>srav</c> (<c>op $rd, $rt, $rs</c>) の命令レベルテスト (doc/tests/instructions/instruction_tests.md)
/// </summary>
public class RTypeShiftInstructionTests {
    /// <summary>
    /// シフトの種類．即値版と可変版で共通に用いる
    /// </summary>
    public enum ShiftKind {
        /// <summary><c>sll</c> / <c>sllv</c></summary>
        LeftLogical,
        /// <summary><c>srl</c> / <c>srlv</c></summary>
        RightLogical,
        /// <summary><c>sra</c> / <c>srav</c></summary>
        RightArithmetic,
    }

    // ---- パラメータ ----

    /// <summary>
    /// 即値シフト: (種類, エイリアス, rt の値, shamt)
    /// </summary>
    public static TheoryData<ShiftKind, Alias2Reg, uint, uint> ImmediateCases() {
        TheoryData<ShiftKind, Alias2Reg, uint, uint> data = new();
        foreach(ShiftKind kind in Enum.GetValues<ShiftKind>()) {
            foreach(object[] row in TestValues.DestSourceCases(TestValues.ShiftSources, TestValues.ShiftAmountsImmediate)) {
                data.Add(kind, (Alias2Reg)row[0], (uint)row[1], (uint)row[2]);
            }
        }
        return data;
    }

    /// <summary>
    /// 可変シフト: (種類, エイリアス, rt の値, rs の値)．
    /// オペランド位置は r1=rd, r2=rt, r3=rs であり，rt==rs のパターンでは rs の値も rt の値になる
    /// </summary>
    public static TheoryData<ShiftKind, Alias3Reg, uint, uint> VariableCases() {
        TheoryData<ShiftKind, Alias3Reg, uint, uint> data = new();
        foreach(ShiftKind kind in Enum.GetValues<ShiftKind>()) {
            foreach(object[] row in TestValues.ThreeRegCases(TestValues.ShiftSources, TestValues.ShiftAmountsRegister)) {
                data.Add(kind, (Alias3Reg)row[0], (uint)row[1], (uint)row[2]);
            }
        }
        return data;
    }

    public static TheoryData<ShiftKind> Kinds() {
        TheoryData<ShiftKind> data = new();
        foreach(ShiftKind kind in Enum.GetValues<ShiftKind>()) {
            data.Add(kind);
        }
        return data;
    }

    // ---- 実装に依存しない期待値の計算 ----

    /// <summary>
    /// ビット単位の定義に基づくシフト結果 (<paramref name="amount"/> は 0〜31)
    /// </summary>
    private static uint ExpectedShift(ShiftKind kind, uint value, int amount) {
        Assert.InRange(amount, 0, 31);
        bool signBit = ((value >> 31) & 1) == 1;
        uint result = 0;
        for(int i = 0; i < 32; i++) {
            bool bit = kind switch {
                // 結果の bit i は元の bit (i - amount)．下位から 0 が入る
                ShiftKind.LeftLogical => i >= amount && Bit(value, i - amount),
                // 結果の bit i は元の bit (i + amount)．上位から 0 が入る
                ShiftKind.RightLogical => i + amount < 32 && Bit(value, i + amount),
                // 結果の bit i は元の bit (i + amount)．上位から符号ビットが入る
                ShiftKind.RightArithmetic => i + amount < 32 ? Bit(value, i + amount) : signBit,
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
            if(bit) {
                result |= 1u << i;
            }
        }
        return result;
    }

    private static bool Bit(uint value, int index) {
        return ((value >> index) & 1) == 1;
    }

    /// <summary>
    /// 可変シフトのシフト量は rs の下位5bit
    /// </summary>
    private static int VariableAmount(uint rsVal) {
        return (int)(rsVal % 32);
    }

    // ---- 準備 ----

    private static string ImmediateMnemonic(ShiftKind kind) {
        return kind switch {
            ShiftKind.LeftLogical => "sll",
            ShiftKind.RightLogical => "srl",
            ShiftKind.RightArithmetic => "sra",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static string VariableMnemonic(ShiftKind kind) {
        return ImmediateMnemonic(kind) + "v";
    }

    private static IInstruction ParseImmediate(ShiftKind kind, RegisterID rd, RegisterID rt, uint shamt) {
        return InstructionHarness.Parse($"{ImmediateMnemonic(kind)} {RegisterAliases.Name(rd)}, {RegisterAliases.Name(rt)}, {shamt}");
    }

    private static IInstruction ParseVariable(ShiftKind kind, RegisterID rd, RegisterID rt, RegisterID rs) {
        return InstructionHarness.Parse($"{VariableMnemonic(kind)} {RegisterAliases.Name(rd)}, {RegisterAliases.Name(rt)}, {RegisterAliases.Name(rs)}");
    }

    // ---- 即値シフト ----

    [Theory]
    [MemberData(nameof(ImmediateCases))]
    public void Execute_Immediate_WritesShiftedRtToRd(ShiftKind kind, Alias2Reg alias, uint rtVal, uint shamt) {
        (RegisterID rd, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = ParseImmediate(kind, rd, rt, shamt);
        RuntimeContext context = InstructionHarness.CreateUser();
        context.Registers[rt] = rtVal;
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // rd だけが変化し，例外も PC の変更も起きない
        MachineState expected = before.WithRegister(rd, ExpectedShift(kind, rtVal, (int)shamt));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(ImmediateCases))]
    public void Undo_Immediate_RestoresState(ShiftKind kind, Alias2Reg alias, uint rtVal, uint shamt) {
        (RegisterID rd, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = ParseImmediate(kind, rd, rt, shamt);
        RuntimeContext context = InstructionHarness.CreateUser();
        context.Registers[rt] = rtVal;
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    // ---- 可変シフト ----

    [Theory]
    [MemberData(nameof(VariableCases))]
    public void Execute_Variable_WritesShiftedRtToRd(ShiftKind kind, Alias3Reg alias, uint rtVal, uint rsVal) {
        (RegisterID rd, RegisterID rt, RegisterID rs) = RegisterAliases.Registers(alias);
        IInstruction inst = ParseVariable(kind, rd, rt, rs);
        RuntimeContext context = InstructionHarness.CreateUser();
        context.Registers[rt] = rtVal;
        context.Registers[rs] = rsVal;
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // rd だけが変化し，例外も PC の変更も起きない
        MachineState expected = before.WithRegister(rd, ExpectedShift(kind, rtVal, VariableAmount(rsVal)));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(VariableCases))]
    public void Undo_Variable_RestoresState(ShiftKind kind, Alias3Reg alias, uint rtVal, uint rsVal) {
        (RegisterID rd, RegisterID rt, RegisterID rs) = RegisterAliases.Registers(alias);
        IInstruction inst = ParseVariable(kind, rd, rt, rs);
        RuntimeContext context = InstructionHarness.CreateUser();
        context.Registers[rt] = rtVal;
        context.Registers[rs] = rsVal;
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    // ---- パラメータの網羅性 ----

    [Fact]
    public void Parameters_CoverImmediateAndVariableShifts() {
        Assert.NotEmpty(ImmediateCases());
        Assert.NotEmpty(VariableCases());
        // 可変シフトは下位5bitだけを使うことを，31 を超えるシフト量で確認する
        Assert.Contains(TestValues.ShiftAmountsRegister, amount => amount > 31);
        Assert.Contains(VariableCases(), row => !((Alias3Reg)row[1]).SourcesAliased() && (uint)row[3] > 31);
        // 算術シフトの符号拡張を，符号ビットが1の値で確認する
        Assert.Contains(TestValues.ShiftSources, value => (value & 0x80000000) != 0);
    }

    // ---- 繰り返し実行 ----

    [Theory]
    [MemberData(nameof(Kinds))]
    public void RepeatedExecute_Immediate_UndoesInReverseOrder(ShiftKind kind) {
        (RegisterID rd, RegisterID rt) = RegisterAliases.Registers(Alias2Reg.None);
        const uint Shamt = 15;
        IInstruction inst = ParseImmediate(kind, rd, rt, Shamt);
        RuntimeContext context = InstructionHarness.CreateUser();

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst, context,
            RepeatedExecution.SetRegisters((rt, 0xaaaaaaaa)),
            RepeatedExecution.SetRegisters((rt, 0x800babe0)),
            RepeatedExecution.SetRegisters((rt, 0x7fffffff))
        );

        foreach((MachineState beforeExecute, MachineState afterExecute) in results) {
            uint rtVal = beforeExecute.Registers[(int)rt];
            MachineState expected = beforeExecute.WithRegister(rd, ExpectedShift(kind, rtVal, (int)Shamt));
            MachineState.AssertEqual(expected, afterExecute);
        }
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void RepeatedExecute_Variable_UndoesInReverseOrder(ShiftKind kind) {
        (RegisterID rd, RegisterID rt, RegisterID rs) = RegisterAliases.Registers(Alias3Reg.None);
        IInstruction inst = ParseVariable(kind, rd, rt, rs);
        RuntimeContext context = InstructionHarness.CreateUser();

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst, context,
            RepeatedExecution.SetRegisters((rt, 0xaaaaaaaa), (rs, 1)),
            RepeatedExecution.SetRegisters((rt, 0x800babe0), (rs, 0xff)),
            RepeatedExecution.SetRegisters((rt, 0x7fffffff), (rs, 32))
        );

        foreach((MachineState beforeExecute, MachineState afterExecute) in results) {
            uint rtVal = beforeExecute.Registers[(int)rt];
            uint rsVal = beforeExecute.Registers[(int)rs];
            MachineState expected = beforeExecute.WithRegister(rd, ExpectedShift(kind, rtVal, VariableAmount(rsVal)));
            MachineState.AssertEqual(expected, afterExecute);
        }
    }
}
