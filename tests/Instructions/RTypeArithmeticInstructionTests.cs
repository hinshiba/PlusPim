using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// R形式の算術演算 <c>add</c>，<c>sub</c>，<c>addu</c>，<c>subu</c> (<c>op $rd, $rs, $rt</c>) の命令レベルテスト
/// (doc/tests/instructions/instruction_tests.md「R形式命令 / 算術演算」)
/// </summary>
public class RTypeArithmeticInstructionTests {
    /// <summary>符号付きオーバーフローで <c>Ov</c> を発生させる命令</summary>
    private static readonly string[] TrappingMnemonics = ["add", "sub"];

    /// <summary>例外を発生させない命令</summary>
    private static readonly string[] WrappingMnemonics = ["addu", "subu"];

    private static readonly string[] Mnemonics = [.. TrappingMnemonics, .. WrappingMnemonics];

    private const long Modulus = 1L << 32;

    // ---- パラメータ ----

    /// <summary>
    /// 命令ごとのソースの値 (add/sub は符号付き32bit値，addu/subu は符号なし32bit値)
    /// </summary>
    private static IReadOnlyList<uint> SourceValues(string mnemonic) {
        return mnemonic switch {
            "add" or "sub" => TestValues.SignedArithmetic,
            "addu" or "subu" => TestValues.UnsignedArithmetic,
            _ => throw new ArgumentOutOfRangeException(nameof(mnemonic)),
        };
    }

    public static TheoryData<string, Alias3Reg, uint, uint> NormalCases() {
        return Cases(overflow: false);
    }

    public static TheoryData<string, Alias3Reg, uint, uint> OverflowCases() {
        return Cases(overflow: true);
    }

    private static TheoryData<string, Alias3Reg, uint, uint> Cases(bool overflow) {
        TheoryData<string, Alias3Reg, uint, uint> data = new();
        foreach(string mnemonic in Mnemonics) {
            IReadOnlyList<uint> values = SourceValues(mnemonic);
            foreach(object[] row in TestValues.ThreeRegCases(values, (rs, rt) => RaisesOv(mnemonic, rs, rt) == overflow)) {
                data.Add(mnemonic, (Alias3Reg)row[0], (uint)row[1], (uint)row[2]);
            }
        }
        return data;
    }

    // ---- 実装に依存しない期待値の計算 ----

    /// <summary>
    /// add/sub の数学的に正確な (64bit で計算した) 結果
    /// </summary>
    private static long ExactSigned(string mnemonic, uint rsVal, uint rtVal) {
        long rs = ToSigned(rsVal);
        long rt = ToSigned(rtVal);
        return mnemonic switch {
            "add" => rs + rt,
            "sub" => rs - rt,
            _ => throw new ArgumentOutOfRangeException(nameof(mnemonic)),
        };
    }

    private static long ToSigned(uint value) {
        return value >= 0x80000000u ? value - Modulus : value;
    }

    /// <summary>
    /// 命令が <c>Ov</c> を発生させるか
    /// </summary>
    private static bool RaisesOv(string mnemonic, uint rsVal, uint rtVal) {
        if(!TrappingMnemonics.Contains(mnemonic)) {
            return false;
        }
        long exact = ExactSigned(mnemonic, rsVal, rtVal);
        return exact is < -0x80000000L or > 0x7fffffffL;
    }

    /// <summary>
    /// 例外を発生させない場合に rd へ書き込まれる値
    /// </summary>
    private static uint Reference(string mnemonic, uint rsVal, uint rtVal) {
        return mnemonic switch {
            // 正常系では正確な結果が32bitに収まるので，2の補数表現に戻す
            "add" or "sub" => (uint)(((ExactSigned(mnemonic, rsVal, rtVal) % Modulus) + Modulus) % Modulus),
            "addu" => (uint)(((long)rsVal + rtVal) % Modulus),
            "subu" => (uint)(((long)rsVal + Modulus - rtVal) % Modulus),
            _ => throw new ArgumentOutOfRangeException(nameof(mnemonic)),
        };
    }

    /// <summary>
    /// <paramref name="before"/> の状態で実行した直後の期待状態
    /// </summary>
    private static MachineState Expected(string mnemonic, MachineState before, RegisterID rd, uint rsVal, uint rtVal) {
        return RaisesOv(mnemonic, rsVal, rtVal)
            // rd は書き換わらず，CP0 だけが例外発生直後の状態になる
            ? before.WithException(ExcCode.Ov, InstructionHarness.InstructionAddress)
            // rd だけが変化し，例外も PC の変更も起きない
            : before.WithRegister(rd, Reference(mnemonic, rsVal, rtVal));
    }

    // ---- 準備 ----

    private static IInstruction Parse(string mnemonic, RegisterID rd, RegisterID rs, RegisterID rt) {
        return InstructionHarness.Parse($"{mnemonic} {RegisterAliases.Name(rd)}, {RegisterAliases.Name(rs)}, {RegisterAliases.Name(rt)}");
    }

    private static void SetSources(RuntimeContext context, RegisterID rs, RegisterID rt, uint rsVal, uint rtVal) {
        context.Registers[rs] = rsVal;
        context.Registers[rt] = rtVal;
    }

    // ---- 正常系 ----

    [Theory]
    [MemberData(nameof(NormalCases))]
    public void Execute_Normal_WritesResultToRd(string mnemonic, Alias3Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rd, RegisterID rs, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(mnemonic, rd, rs, rt);
        RuntimeContext context = InstructionHarness.CreateUser();
        SetSources(context, rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState expected = before.WithRegister(rd, Reference(mnemonic, rsVal, rtVal));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(NormalCases))]
    public void Undo_Normal_RestoresState(string mnemonic, Alias3Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rd, RegisterID rs, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(mnemonic, rd, rs, rt);
        RuntimeContext context = InstructionHarness.CreateUser();
        SetSources(context, rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    // ---- 異常系 (Ov) ----

    [Theory]
    [MemberData(nameof(OverflowCases))]
    public void Execute_Overflow_RaisesOv(string mnemonic, Alias3Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rd, RegisterID rs, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(mnemonic, rd, rs, rt);
        RuntimeContext context = InstructionHarness.CreateUser();
        SetSources(context, rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState expected = before.WithException(ExcCode.Ov, InstructionHarness.InstructionAddress);
        MachineState.AssertEqual(expected, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(OverflowCases))]
    public void Undo_Overflow_RestoresState(string mnemonic, Alias3Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rd, RegisterID rs, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(mnemonic, rd, rs, rt);
        RuntimeContext context = InstructionHarness.CreateUser();
        SetSources(context, rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.False(context.IsKernelMode);
    }

    /// <summary>
    /// eret 後 (前の例外の Cause/EPC/BadVAddr が残ったユーザーモード) の Ov は，
    /// Cause/EPC/EXL だけを書き換え，BadVAddr は保持する
    /// </summary>
    [Theory]
    [InlineData("add", 0x7fffffffu, 0x00000001u)]
    [InlineData("sub", 0x80000000u, 0x00000001u)]
    public void Execute_OverflowAfterEret_OverwritesCauseAndEpcOnly(string mnemonic, uint rsVal, uint rtVal) {
        Assert.True(RaisesOv(mnemonic, rsVal, rtVal));
        (RegisterID rd, RegisterID rs, RegisterID rt) = RegisterAliases.Registers(Alias3Reg.None);
        IInstruction inst = Parse(mnemonic, rd, rs, rt);
        RuntimeContext context = InstructionHarness.CreatePostEret(ExcCode.AdEL, new Address(0x00400010), 0x10000001);
        SetSources(context, rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);
        Assert.Equal(0x10000001u, before.BadVAddr);

        _ = Processor.Execute(context, inst);

        MachineState expected = before.WithException(ExcCode.Ov, InstructionHarness.InstructionAddress);
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [InlineData("add", 0x7fffffffu, 0x00000001u)]
    [InlineData("sub", 0x80000000u, 0x00000001u)]
    public void Undo_OverflowAfterEret_RestoresState(string mnemonic, uint rsVal, uint rtVal) {
        Assert.True(RaisesOv(mnemonic, rsVal, rtVal));
        (RegisterID rd, RegisterID rs, RegisterID rt) = RegisterAliases.Registers(Alias3Reg.None);
        IInstruction inst = Parse(mnemonic, rd, rs, rt);
        RuntimeContext context = InstructionHarness.CreatePostEret(ExcCode.AdEL, new Address(0x00400010), 0x10000001);
        SetSources(context, rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    // ---- 異常系 (カーネルモードでの Ov = 二重例外) ----

    /// <summary>
    /// オーバーフローする命令と入力 × カーネルモードの開始状態 (各例外の発生直後)
    /// </summary>
    public static TheoryData<string, uint, uint, ExcCode, uint?> KernelOverflowCases() {
        TheoryData<string, uint, uint, ExcCode, uint?> data = new();
        (string Mnemonic, uint Rs, uint Rt)[] inputs = [("add", 0x7fffffffu, 0x00000001u), ("sub", 0x80000000u, 0x00000001u)];
        foreach((string mnemonic, uint rsVal, uint rtVal) in inputs) {
            foreach((ExcCode code, uint? badVAddr) in PriorExceptionStates.KernelStates()) {
                data.Add(mnemonic, rsVal, rtVal, code, badVAddr);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(KernelOverflowCases))]
    public void Execute_Overflow_Kernel_RaisesDoubleException(string mnemonic, uint rsVal, uint rtVal, ExcCode priorCode, uint? badVAddr) {
        Assert.True(RaisesOv(mnemonic, rsVal, rtVal));
        (RegisterID rd, RegisterID rs, RegisterID rt) = RegisterAliases.Registers(Alias3Reg.None);
        IInstruction inst = Parse(mnemonic, rd, rs, rt);
        RuntimeContext context = PriorExceptionStates.CreateKernel(priorCode, badVAddr);
        SetSources(context, rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // rd も CP0 も変化せず，二重例外として記録されて終了する
        MachineState expected = before.WithLastException(new ExceptionEvent(ExcCode.Ov, IsDouble: true)).WithTerminated();
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(KernelOverflowCases))]
    public void Undo_Overflow_Kernel_RestoresState(string mnemonic, uint rsVal, uint rtVal, ExcCode priorCode, uint? badVAddr) {
        (RegisterID rd, RegisterID rs, RegisterID rt) = RegisterAliases.Registers(Alias3Reg.None);
        IInstruction inst = Parse(mnemonic, rd, rs, rt);
        RuntimeContext context = PriorExceptionStates.CreateKernel(priorCode, badVAddr);
        SetSources(context, rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    // ---- パラメータ自体の検証 ----

    [Fact]
    public void Parameters_CoverEveryMnemonicAliasAndOverflow() {
        List<(string Mnemonic, Alias3Reg Alias, uint Rs, uint Rt)> normal = [.. Rows(NormalCases())];
        List<(string Mnemonic, Alias3Reg Alias, uint Rs, uint Rt)> overflow = [.. Rows(OverflowCases())];

        foreach(string mnemonic in Mnemonics) {
            foreach(Alias3Reg alias in Enum.GetValues<Alias3Reg>()) {
                Assert.Contains(normal, c => c.Mnemonic == mnemonic && c.Alias == alias);
            }
        }
        foreach(string mnemonic in TrappingMnemonics) {
            Assert.Contains(overflow, c => c.Mnemonic == mnemonic);
        }
        foreach(string mnemonic in WrappingMnemonics) {
            Assert.DoesNotContain(overflow, c => c.Mnemonic == mnemonic);
        }

        // addu/subu は符号付きならオーバーフローする入力でも例外を起こさないことを確かめている
        Assert.Contains(normal, c => c.Mnemonic == "addu" && (ToSigned(c.Rs) + ToSigned(c.Rt)) > 0x7fffffffL);
        Assert.Contains(normal, c => c.Mnemonic == "subu" && (ToSigned(c.Rs) - ToSigned(c.Rt)) < -0x80000000L);
    }

    private static IEnumerable<(string Mnemonic, Alias3Reg Alias, uint Rs, uint Rt)> Rows(TheoryData<string, Alias3Reg, uint, uint> data) {
        foreach(object[] row in data) {
            yield return ((string)row[0], (Alias3Reg)row[1], (uint)row[2], (uint)row[3]);
        }
    }

    // ---- 繰り返し実行 ----

    [Theory]
    // 正常系 -> 異常系 (Ov) -> 正常系 (例外ハンドラ内)
    [InlineData("add", 0x000cafe0u, 0x800babe0u, 0x7fffffffu, 0x00000001u, 0xffffffffu, 0x80000001u)]
    [InlineData("sub", 0x000cafe0u, 0x00000001u, 0x80000000u, 0x00000001u, 0xffffffffu, 0x80000001u)]
    public void RepeatedExecute_NormalOverflowNormal_UndoesInReverseOrder(
        string mnemonic, uint rs0, uint rt0, uint rs1, uint rt1, uint rs2, uint rt2
    ) {
        (uint Rs, uint Rt)[] inputs = [(rs0, rt0), (rs1, rt1), (rs2, rt2)];
        bool[] expectedOverflow = [false, true, false];
        Assert.Equal(expectedOverflow, inputs.Select(i => RaisesOv(mnemonic, i.Rs, i.Rt)).ToArray());

        AssertRepeated(mnemonic, inputs);
    }

    [Theory]
    [InlineData("addu", 0x000cafe0u, 0x800babe0u, 0x7fffffffu, 0x00000001u, 0xffffffffu, 0xfffffffeu)]
    [InlineData("subu", 0x000cafe0u, 0x800babe0u, 0x80000000u, 0x00000001u, 0x00000000u, 0xffffffffu)]
    public void RepeatedExecute_UndoesInReverseOrder(
        string mnemonic, uint rs0, uint rt0, uint rs1, uint rt1, uint rs2, uint rt2
    ) {
        AssertRepeated(mnemonic, [(rs0, rt0), (rs1, rt1), (rs2, rt2)]);
    }

    private static void AssertRepeated(string mnemonic, (uint Rs, uint Rt)[] inputs) {
        (RegisterID rd, RegisterID rs, RegisterID rt) = RegisterAliases.Registers(Alias3Reg.None);
        IInstruction inst = Parse(mnemonic, rd, rs, rt);
        RuntimeContext context = InstructionHarness.CreateUser();

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            [.. inputs.Select(i => RepeatedExecution.SetRegisters((rs, i.Rs), (rt, i.Rt)))]
        );

        for(int i = 0; i < inputs.Length; i++) {
            (MachineState beforeExecute, MachineState afterExecute) = results[i];
            MachineState expected = Expected(mnemonic, beforeExecute, rd, inputs[i].Rs, inputs[i].Rt);
            MachineState.AssertEqual(expected, afterExecute);
        }
    }
}
