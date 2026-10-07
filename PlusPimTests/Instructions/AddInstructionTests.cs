using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// 3レジスタ命令のレジスタエイリアスパターン (doc/tests/instructions/values.md)
/// </summary>
public enum Alias3Reg {
    None,
    RdRs,
    RdRt,
    RsRt,
    All,
}

/// <summary>
/// <c>add $rd $rs $rt</c> の命令レベルテスト (doc/tests/instructions/instruction_tests.md)
/// </summary>
public class AddInstructionTests {
    private static readonly Address InstructionAddress = new(0x00400100);

    /// <summary>
    /// 符号付き32bit値の代表値，境界値，準境界値
    /// </summary>
    private static readonly uint[] SignedValues = [
        // 代表値
        0x000cafe0, 0x800babe0, 0x00000000,
        // 境界値
        0x80000000, 0xffffffff, 0x00000001, 0x7fffffff,
        // 準境界値
        0x7ffffffe, 0x80000001,
    ];

    public static TheoryData<Alias3Reg, uint, uint> AllCases() {
        TheoryData<Alias3Reg, uint, uint> data = new();
        foreach(Alias3Reg alias in Enum.GetValues<Alias3Reg>()) {
            bool rsIsRt = alias is Alias3Reg.RsRt or Alias3Reg.All;
            foreach(uint rsVal in SignedValues) {
                if(rsIsRt) {
                    data.Add(alias, rsVal, rsVal);
                    continue;
                }
                foreach(uint rtVal in SignedValues) {
                    data.Add(alias, rsVal, rtVal);
                }
            }
        }
        return data;
    }

    public static TheoryData<Alias3Reg, uint, uint> NormalCases() {
        return Filter(overflow: false);
    }

    public static TheoryData<Alias3Reg, uint, uint> OverflowCases() {
        return Filter(overflow: true);
    }

    private static TheoryData<Alias3Reg, uint, uint> Filter(bool overflow) {
        TheoryData<Alias3Reg, uint, uint> data = new();
        foreach(object[] row in AllCases()) {
            (Alias3Reg alias, uint rsVal, uint rtVal) = ((Alias3Reg)row[0], (uint)row[1], (uint)row[2]);
            if(Overflows(rsVal, rtVal) == overflow) {
                data.Add(alias, rsVal, rtVal);
            }
        }
        return data;
    }

    /// <summary>
    /// 実装に依存しない期待値の計算
    /// </summary>
    private static long ExactSum(uint rsVal, uint rtVal) {
        return (long)(int)rsVal + (int)rtVal;
    }

    private static bool Overflows(uint rsVal, uint rtVal) {
        long sum = ExactSum(rsVal, rtVal);
        return sum is < int.MinValue or > int.MaxValue;
    }

    private static (RegisterID Rd, RegisterID Rs, RegisterID Rt) Registers(Alias3Reg alias) {
        return alias switch {
            Alias3Reg.None => (RegisterID.T0, RegisterID.T1, RegisterID.T2),
            Alias3Reg.RdRs => (RegisterID.T0, RegisterID.T0, RegisterID.T1),
            Alias3Reg.RdRt => (RegisterID.T0, RegisterID.T1, RegisterID.T0),
            Alias3Reg.RsRt => (RegisterID.T0, RegisterID.T1, RegisterID.T1),
            Alias3Reg.All => (RegisterID.T0, RegisterID.T0, RegisterID.T0),
            _ => throw new ArgumentOutOfRangeException(nameof(alias)),
        };
    }

    private static string RegName(RegisterID id) {
        return $"${id.ToString().ToLowerInvariant()}";
    }

    private static IInstruction Parse(RegisterID rd, RegisterID rs, RegisterID rt) {
        IInstruction? inst = TestHelpers.ParseInstruction($"add {RegName(rd)}, {RegName(rs)}, {RegName(rt)}");
        Assert.NotNull(inst);
        return inst;
    }

    /// <summary>
    /// 全レジスタをランダム化した上で，入力レジスタと命令アドレスを設定したコンテキストを作る
    /// </summary>
    private static RuntimeContext Setup(RegisterID rs, RegisterID rt, uint rsVal, uint rtVal) {
        RuntimeContext context = TestHelpers.CreateRuntimeContext();
        TestHelpers.SeedRegisters(context, 42);
        context.PC = InstructionAddress;
        context.Registers[rs] = rsVal;
        context.Registers[rt] = rtVal;
        return context;
    }

    [Theory]
    [MemberData(nameof(NormalCases))]
    public void Execute_Normal_WritesSumToRd(Alias3Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rd, RegisterID rs, RegisterID rt) = Registers(alias);
        IInstruction inst = Parse(rd, rs, rt);
        RuntimeContext context = Setup(rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        inst.Execute(context);

        // rd だけが変化し，例外も PC の変更も起きない
        MachineState expected = before.WithRegister(rd, unchecked((uint)ExactSum(rsVal, rtVal)));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(NormalCases))]
    public void Undo_Normal_RestoresState(Alias3Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rd, RegisterID rs, RegisterID rt) = Registers(alias);
        IInstruction inst = Parse(rd, rs, rt);
        RuntimeContext context = Setup(rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        inst.Execute(context);
        inst.Undo(context);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(OverflowCases))]
    public void Execute_Overflow_RaisesOv(Alias3Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rd, RegisterID rs, RegisterID rt) = Registers(alias);
        IInstruction inst = Parse(rd, rs, rt);
        RuntimeContext context = Setup(rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        inst.Execute(context);

        // rd は書き換わらず，CP0 だけが例外発生直後の状態になる
        MachineState expected = before.WithException(ExcCode.Ov, InstructionAddress);
        MachineState.AssertEqual(expected, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(OverflowCases))]
    public void Undo_Overflow_RestoresState(Alias3Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rd, RegisterID rs, RegisterID rt) = Registers(alias);
        IInstruction inst = Parse(rd, rs, rt);
        RuntimeContext context = Setup(rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        inst.Execute(context);
        inst.Undo(context);

        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.False(context.IsKernelMode);
    }

    [Fact]
    public void Parameters_CoverBothNormalAndOverflow() {
        Assert.NotEmpty(NormalCases());
        Assert.NotEmpty(OverflowCases());
    }

    [Fact]
    public void RepeatedExecute_NormalOverflowNormal_UndoesInReverseOrder() {
        RegisterID rd = RegisterID.T0, rs = RegisterID.T1, rt = RegisterID.T2;
        IInstruction inst = Parse(rd, rs, rt);
        RuntimeContext context = Setup(rs, rt, 0, 0);

        (uint Rs, uint Rt)[] inputs = [
            (0x000cafe0, 0x800babe0), // 正常系
            (0x7fffffff, 0x00000001), // 異常系 (Ov)
            (0xffffffff, 0x80000001), // 正常系 (例外ハンドラ内)
        ];

        // 入力の設定は他の命令による書き換えとみなし，undo 時に巻き戻す
        Stack<(MachineState BeforeExecute, uint PrevRs, uint PrevRt)> history = new();
        foreach((uint rsVal, uint rtVal) in inputs) {
            uint prevRs = context.Registers[rs];
            uint prevRt = context.Registers[rt];
            context.Registers[rs] = rsVal;
            context.Registers[rt] = rtVal;
            history.Push((MachineState.Capture(context), prevRs, prevRt));
            inst.Execute(context);
        }

        while(history.Count > 0) {
            (MachineState beforeExecute, uint prevRs, uint prevRt) = history.Pop();
            inst.Undo(context);
            MachineState.AssertEqual(beforeExecute, MachineState.Capture(context));
            context.Registers[rs] = prevRs;
            context.Registers[rt] = prevRt;
        }
    }
}
