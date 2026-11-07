using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// <c>mult</c>，<c>multu</c>，<c>div</c>，<c>divu</c> (<c>op $rs, $rt</c>) の命令レベルテスト
/// (doc/tests/instructions/instruction_tests.md「乗除算」)
/// </summary>
/// <remarks>
/// 結果は HI/LO にだけ書き込まれる．<c>div</c>/<c>divu</c> は LO に商，HI に剰余を書き込む．
/// ゼロ除算と <c>div</c> の <c>0x80000000 / -1</c> はランタイムエラーとなる
/// </remarks>
public class MulDivInstructionTests {
    private const uint IntMin = 0x80000000;
    private const uint MinusOne = 0xffffffff;

    // ---- パラメータ ----

    public static TheoryData<string, Alias2Reg, uint, uint> NormalCases() {
        TheoryData<string, Alias2Reg, uint, uint> data = new();
        foreach(string mnemonic in (string[])["mult", "multu", "div", "divu"]) {
            foreach(object[] row in TestValues.TwoSourceCases(SourceValues(mnemonic), (rs, rt) => !RaisesRuntimeError(mnemonic, rs, rt))) {
                data.Add(mnemonic, (Alias2Reg)row[0], (uint)row[1], (uint)row[2]);
            }
        }
        return data;
    }

    public static TheoryData<string, Alias2Reg, uint, uint> RuntimeErrorCases() {
        TheoryData<string, Alias2Reg, uint, uint> data = new();
        foreach(string mnemonic in (string[])["div", "divu"]) {
            foreach(object[] row in TestValues.TwoSourceCases(SourceValues(mnemonic), (rs, rt) => RaisesRuntimeError(mnemonic, rs, rt))) {
                data.Add(mnemonic, (Alias2Reg)row[0], (uint)row[1], (uint)row[2]);
            }
        }
        return data;
    }

    /// <summary>
    /// 符号付きの命令は符号付き32bit値，符号なしの命令は符号なし32bit値 (前提 A5)
    /// </summary>
    private static IReadOnlyList<uint> SourceValues(string mnemonic) {
        return mnemonic switch {
            "mult" or "div" => TestValues.SignedArithmetic,
            "multu" or "divu" => TestValues.UnsignedArithmetic,
            _ => throw new ArgumentOutOfRangeException(nameof(mnemonic)),
        };
    }

    /// <summary>
    /// ランタイムエラーとなる入力か (ゼロ除算，<c>div</c> の <c>0x80000000 / -1</c>)
    /// </summary>
    private static bool RaisesRuntimeError(string mnemonic, uint rsVal, uint rtVal) {
        return mnemonic switch {
            "mult" or "multu" => false,
            "div" => rtVal == 0 || (rsVal == IntMin && rtVal == MinusOne),
            "divu" => rtVal == 0,
            _ => throw new ArgumentOutOfRangeException(nameof(mnemonic)),
        };
    }

    // ---- 実装に依存しない期待値の計算 ----

    /// <summary>
    /// 命令の定義通りの (HI, LO)
    /// </summary>
    private static (uint Hi, uint Lo) Expected(string mnemonic, uint rsVal, uint rtVal) {
        return mnemonic switch {
            "mult" => SplitProduct(unchecked((ulong)((long)(int)rsVal * (int)rtVal))),
            "multu" => SplitProduct((ulong)rsVal * rtVal),
            "div" => SignedDivide(rsVal, rtVal),
            "divu" => (rsVal % rtVal, rsVal / rtVal),
            _ => throw new ArgumentOutOfRangeException(nameof(mnemonic)),
        };
    }

    /// <summary>
    /// 64bit の積を (上位32bit, 下位32bit) に分ける
    /// </summary>
    private static (uint Hi, uint Lo) SplitProduct(ulong product) {
        return ((uint)(product >> 32), unchecked((uint)product));
    }

    /// <summary>
    /// 符号付き除算の (剰余, 商)．商は0方向へ切り捨て，剰余は被除数と同符号
    /// </summary>
    /// <remarks>long で計算するため，<c>0x80000000 / -1</c> 以外は結果が32bitに収まる</remarks>
    private static (uint Hi, uint Lo) SignedDivide(uint rsVal, uint rtVal) {
        long dividend = (int)rsVal;
        long divisor = (int)rtVal;
        long quotient = dividend / divisor;
        long remainder = dividend - (quotient * divisor);
        return (unchecked((uint)remainder), unchecked((uint)quotient));
    }

    // ---- 準備 ----

    private static IInstruction Parse(string mnemonic, RegisterID rs, RegisterID rt) {
        return InstructionHarness.Parse($"{mnemonic} {RegisterAliases.Name(rs)}, {RegisterAliases.Name(rt)}");
    }

    private static RuntimeContext Setup(RegisterID rs, RegisterID rt, uint rsVal, uint rtVal) {
        RuntimeContext context = InstructionHarness.CreateUser();
        context.Registers[rs] = rsVal;
        context.Registers[rt] = rtVal;
        return context;
    }

    // ---- 正常系 ----

    [Theory]
    [MemberData(nameof(NormalCases))]
    public void Execute_Normal_WritesHiLo(string mnemonic, Alias2Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rs, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(mnemonic, rs, rt);
        RuntimeContext context = Setup(rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // HI と LO だけが変化する
        (uint hi, uint lo) = Expected(mnemonic, rsVal, rtVal);
        MachineState expected = before.WithHiLo(hi, lo);
        MachineState.AssertEqual(expected, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(NormalCases))]
    public void Undo_Normal_RestoresState(string mnemonic, Alias2Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rs, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(mnemonic, rs, rt);
        RuntimeContext context = Setup(rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    /// <summary>
    /// 期待値の計算 (<see cref="Expected"/>) そのものの検算
    /// </summary>
    [Theory]
    [InlineData("mult", 0xfffffff9u, 0x00000002u, 0xffffffffu, 0xfffffff2u)] // -7 * 2 = -14
    [InlineData("mult", 0x80000000u, 0x80000000u, 0x40000000u, 0x00000000u)] // INT_MIN * INT_MIN = 2^62
    [InlineData("multu", 0xffffffffu, 0xffffffffu, 0xfffffffeu, 0x00000001u)] // (2^32-1)^2
    [InlineData("div", 0xfffffff9u, 0x00000002u, 0xffffffffu, 0xfffffffdu)] // -7 / 2 = -3 余り -1
    [InlineData("div", 0x00000007u, 0xfffffffeu, 0x00000001u, 0xfffffffdu)] // 7 / -2 = -3 余り 1
    [InlineData("div", 0xffffffffu, 0xffffffffu, 0x00000000u, 0x00000001u)] // -1 / -1 = 1 余り 0
    [InlineData("divu", 0x80000000u, 0xffffffffu, 0x80000000u, 0x00000000u)] // 2^31 / (2^32-1) = 0 余り 2^31
    [InlineData("divu", 0xfffffff9u, 0x00000002u, 0x00000001u, 0x7ffffffcu)]
    public void Execute_KnownAnswer_WritesHiLo(string mnemonic, uint rsVal, uint rtVal, uint expectedHi, uint expectedLo) {
        Assert.Equal((expectedHi, expectedLo), Expected(mnemonic, rsVal, rtVal));

        (RegisterID rs, RegisterID rt) = RegisterAliases.Registers(Alias2Reg.None);
        IInstruction inst = Parse(mnemonic, rs, rt);
        RuntimeContext context = Setup(rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState.AssertEqual(before.WithHiLo(expectedHi, expectedLo), MachineState.Capture(context));
    }

    // ---- ランタイムエラー ----

    /// <summary>
    /// ランタイムエラーの種類 (ゼロ除算が優先する)
    /// </summary>
    private static RuntimeErrorKind ExpectedErrorKind(uint rtVal) {
        return rtVal == 0 ? RuntimeErrorKind.DivisionByZero : RuntimeErrorKind.DivisionOverflow;
    }

    [Theory]
    [MemberData(nameof(RuntimeErrorCases))]
    public void Execute_DivisionError_RaisesRuntimeError(string mnemonic, Alias2Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rs, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(mnemonic, rs, rt);
        RuntimeContext context = Setup(rs, rt, rsVal, rtVal);

        RuntimeError error = RuntimeErrorAssert.Raised(() => Processor.Execute(context, inst));

        Assert.Equal(ExpectedErrorKind(rtVal), error.Kind);
    }

    [Theory]
    [MemberData(nameof(RuntimeErrorCases))]
    public void Execute_DivisionError_ChangesOnlyRuntimeError(string mnemonic, Alias2Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rs, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(mnemonic, rs, rt);
        RuntimeContext context = Setup(rs, rt, rsVal, rtVal);

        // HI/LO は以前の値のまま
        _ = RuntimeErrorAssert.RaisedWithoutSideEffects(context, () => Processor.Execute(context, inst), ExpectedErrorKind(rtVal));
    }

    [Theory]
    [MemberData(nameof(RuntimeErrorCases))]
    public void Undo_DivisionError_RestoresState(string mnemonic, Alias2Reg alias, uint rsVal, uint rtVal) {
        (RegisterID rs, RegisterID rt) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(mnemonic, rs, rt);
        RuntimeContext context = Setup(rs, rt, rsVal, rtVal);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.Null(context.RuntimeError);
    }

    // ---- パラメータの網羅 ----

    [Fact]
    public void Parameters_CoverAllInstructionsAndRuntimeErrors() {
        TheoryData<string, Alias2Reg, uint, uint> normal = NormalCases();
        TheoryData<string, Alias2Reg, uint, uint> errors = RuntimeErrorCases();
        foreach(string mnemonic in (string[])["mult", "multu", "div", "divu"]) {
            foreach(Alias2Reg alias in Enum.GetValues<Alias2Reg>()) {
                Assert.Contains(normal, row => (string)row[0] == mnemonic && (Alias2Reg)row[1] == alias);
            }
        }
        foreach(string mnemonic in (string[])["div", "divu"]) {
            foreach(Alias2Reg alias in Enum.GetValues<Alias2Reg>()) {
                // ゼロ除算
                Assert.Contains(errors, row => (string)row[0] == mnemonic && (Alias2Reg)row[1] == alias && (uint)row[3] == 0);
            }
        }
        // div の 0x80000000 / -1 はランタイムエラー
        Assert.Contains(errors, row => (string)row[0] == "div" && (uint)row[2] == IntMin && (uint)row[3] == MinusOne);
        // divu の 0x80000000 / 0xffffffff と div の -1 / -1 は正常系
        Assert.Contains(normal, row => (string)row[0] == "divu" && (uint)row[2] == IntMin && (uint)row[3] == MinusOne);
        Assert.Contains(normal, row => (string)row[0] == "div" && (uint)row[2] == MinusOne && (uint)row[3] == MinusOne);
    }

    // ---- 繰り返し実行 ----

    [Theory]
    [InlineData("mult")]
    [InlineData("multu")]
    [InlineData("div")]
    [InlineData("divu")]
    public void RepeatedExecute_ThreeTimes_UndoesInReverseOrder(string mnemonic) {
        (RegisterID rs, RegisterID rt) = RegisterAliases.Registers(Alias2Reg.None);
        IInstruction inst = Parse(mnemonic, rs, rt);
        RuntimeContext context = InstructionHarness.CreateUser();

        (uint Rs, uint Rt)[] inputs = [
            (0x000cafe0, 0x800babe0),
            (0x80000001, 0x7fffffff),
            (0xfffffffe, 0x00000001),
        ];

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            [.. inputs.Select(input => RepeatedExecution.SetRegisters((rs, input.Rs), (rt, input.Rt)))]
        );

        for(int i = 0; i < inputs.Length; i++) {
            (uint hi, uint lo) = Expected(mnemonic, inputs[i].Rs, inputs[i].Rt);
            MachineState.AssertEqual(results[i].BeforeExecute.WithHiLo(hi, lo), results[i].AfterExecute);
        }
    }

    [Theory]
    [InlineData("div")]
    [InlineData("divu")]
    public void RepeatedExecute_NormalErrorNormal_UndoesInReverseOrder(string mnemonic) {
        (RegisterID rs, RegisterID rt) = RegisterAliases.Registers(Alias2Reg.None);
        IInstruction inst = Parse(mnemonic, rs, rt);
        RuntimeContext context = InstructionHarness.CreateUser();

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            // 正常系
            RepeatedExecution.SetRegisters((rs, 0x0000cafe), (rt, 0x00000007)),
            // ランタイムエラー (ゼロ除算)
            RepeatedExecution.SetRegisters((rs, 0x0000babe), (rt, 0x00000000)),
            // 正常系: デバッガではランタイムエラーの後は実行できないので，ランタイムエラーを消してから実行する
            ctx => {
                ctx.RestoreExceptionState(ctx.CaptureExceptionState() with { RuntimeError = null });
                ctx.Registers[rs] = 0xfffffff9;
                ctx.Registers[rt] = 0x00000002;
            }
        );

        Assert.Equal(3, results.Count);
        (uint hi0, uint lo0) = Expected(mnemonic, 0x0000cafe, 0x00000007);
        MachineState.AssertEqual(results[0].BeforeExecute.WithHiLo(hi0, lo0), results[0].AfterExecute);
        MachineState.AssertEqual(results[1].BeforeExecute.WithRuntimeError(RuntimeErrorKind.DivisionByZero), results[1].AfterExecute);
        (uint hi2, uint lo2) = Expected(mnemonic, 0xfffffff9, 0x00000002);
        MachineState.AssertEqual(results[2].BeforeExecute.WithHiLo(hi2, lo2), results[2].AfterExecute);
    }
}
