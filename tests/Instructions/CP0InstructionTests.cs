using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// <c>mfc0 $rt, n</c>，<c>mtc0 $rt, n</c>，<c>eret</c> の命令レベルテスト
/// (doc/tests/instructions/instruction_tests.md，cp0_model.md)
/// </summary>
/// <remarks>
/// 開始状態は <see cref="PriorExceptionStates"/> を用いる．
/// CP0 操作命令はカーネルモードで例外を起こさないため，二重例外は扱わない
/// (二重例外は <see cref="SystemInstructionTests"/>，<see cref="ProcessorTests"/> で扱う)
/// </remarks>
public class CP0InstructionTests {
    /// <summary>
    /// 1レジスタ命令なのでエイリアスなしのみ
    /// </summary>
    private const RegisterID Rt = RegisterAliases.Single;

    private const uint StatusExlBit = 0x2;
    private const uint CauseExcCodeBits = 0x7c;

    /// <summary>対応する CP0 レジスタ番号 (BadVAddr, Status, Cause, EPC)</summary>
    private static readonly int[] SupportedNumbers = [8, 12, 13, 14];

    /// <summary>未対応の CP0 レジスタ番号</summary>
    private static readonly int[] UnsupportedNumbers = [0, 31];

    private static readonly int[] AllNumbers = [.. SupportedNumbers, .. UnsupportedNumbers];

    /// <summary>
    /// カーネルモードの mtc0 で書き込む値．
    /// 符号なし32bit値とビットパターンに，Status の bit1 / Cause の bit2〜6 だけが立つ値とその反転を加える
    /// </summary>
    private static readonly IReadOnlyList<uint> KernelWriteValues = [
        .. TestValues.Unsigned32.Union(TestValues.BitPatterns).Union([0x00000002u, 0xfffffffdu, 0x0000007cu, 0xffffff83u]),
    ];

    /// <summary>
    /// ユーザーモードの mtc0 で書き込む値 (効果が現れないことの確認用)
    /// </summary>
    private static readonly IReadOnlyList<uint> UserWriteValues = TestValues.BitPatterns;

    // ---- 実装に依存しない期待値 ----

    /// <summary>
    /// 例外 (<paramref name="code"/>，EPC=<paramref name="epc"/>，BadVAddr=<paramref name="badVAddr"/>) 直後のカーネルモードでの読み出し値
    /// </summary>
    private static uint ExpectedRead(int number, ExcCode code, Address epc, uint? badVAddr) {
        return number switch {
            8 => badVAddr ?? 0,
            12 => StatusExlBit, // カーネルモードなので EXL=1
            13 => (uint)code << 2,
            14 => epc.Addr,
            _ => throw new ArgumentOutOfRangeException(nameof(number)),
        };
    }

    /// <summary>
    /// カーネルモードで CP0 レジスタ <paramref name="number"/> に <paramref name="value"/> を書いた後の状態
    /// </summary>
    private static MachineState ExpectedWrite(MachineState before, int number, uint value) {
        return number switch {
            8 => before.WithCP0(badVAddr: value),
            12 => before.WithCP0(status: value & StatusExlBit),
            13 => before.WithCP0(cause: value & CauseExcCodeBits),
            14 => before.WithCP0(epc: value),
            _ => throw new ArgumentOutOfRangeException(nameof(number)),
        };
    }

    private static IInstruction ParseMfc0(int number) {
        return InstructionHarness.Parse($"mfc0 {RegisterAliases.Name(Rt)}, ${number}");
    }

    private static IInstruction ParseMtc0(int number) {
        return InstructionHarness.Parse($"mtc0 {RegisterAliases.Name(Rt)}, ${number}");
    }

    private static IInstruction ParseEret() {
        return InstructionHarness.Parse("eret");
    }

    // ---- パラメータ ----

    public static TheoryData<int, ExcCode, uint?> KernelReadCases() {
        TheoryData<int, ExcCode, uint?> data = new();
        foreach(int number in SupportedNumbers) {
            foreach((ExcCode code, uint? badVAddr) in PriorExceptionStates.KernelStates()) {
                data.Add(number, code, badVAddr);
            }
        }
        return data;
    }

    public static TheoryData<int, uint, ExcCode, uint?> KernelWriteCases() {
        TheoryData<int, uint, ExcCode, uint?> data = new();
        foreach(int number in SupportedNumbers) {
            foreach(uint value in KernelWriteValues) {
                foreach((ExcCode code, uint? badVAddr) in PriorExceptionStates.KernelStates()) {
                    data.Add(number, value, code, badVAddr);
                }
            }
        }
        return data;
    }

    /// <summary>
    /// ユーザーモードの mfc0: 未対応の番号も含む (CpU が優先する)
    /// </summary>
    public static TheoryData<int, ExcCode?, uint?> UserReadCases() {
        TheoryData<int, ExcCode?, uint?> data = new();
        foreach(int number in AllNumbers) {
            foreach((ExcCode? postEretCode, uint? badVAddr) in PriorExceptionStates.UserStates()) {
                data.Add(number, postEretCode, badVAddr);
            }
        }
        return data;
    }

    /// <summary>
    /// ユーザーモードの mtc0: 未対応の番号も含む (CpU が優先する)
    /// </summary>
    public static TheoryData<int, uint, ExcCode?, uint?> UserWriteCases() {
        TheoryData<int, uint, ExcCode?, uint?> data = new();
        foreach(int number in AllNumbers) {
            foreach(uint value in UserWriteValues) {
                foreach((ExcCode? postEretCode, uint? badVAddr) in PriorExceptionStates.UserStates()) {
                    data.Add(number, value, postEretCode, badVAddr);
                }
            }
        }
        return data;
    }

    public static TheoryData<string, int, ExcCode, uint?> KernelUnsupportedCases() {
        TheoryData<string, int, ExcCode, uint?> data = new();
        foreach(string mnemonic in new[] { "mfc0", "mtc0" }) {
            foreach(int number in UnsupportedNumbers) {
                foreach((ExcCode code, uint? badVAddr) in PriorExceptionStates.KernelStates()) {
                    data.Add(mnemonic, number, code, badVAddr);
                }
            }
        }
        return data;
    }

    /// <summary>
    /// カーネルモードの eret: EPC が通常のアドレス / eret 自身のアドレス
    /// </summary>
    public static TheoryData<uint, ExcCode, uint?> KernelEretCases() {
        TheoryData<uint, ExcCode, uint?> data = new();
        foreach(Address epc in new[] { PriorExceptionStates.Epc, InstructionHarness.KernelInstructionAddress }) {
            foreach((ExcCode code, uint? badVAddr) in PriorExceptionStates.KernelStates()) {
                data.Add(epc.Addr, code, badVAddr);
            }
        }
        return data;
    }

    public static TheoryData<ExcCode?, uint?> UserEretCases() {
        TheoryData<ExcCode?, uint?> data = new();
        foreach((ExcCode? postEretCode, uint? badVAddr) in PriorExceptionStates.UserStates()) {
            data.Add(postEretCode, badVAddr);
        }
        return data;
    }

    [Fact]
    public void Parameters_CoverAllModesAndRegisters() {
        Assert.NotEmpty(KernelReadCases());
        Assert.NotEmpty(KernelWriteCases());
        Assert.NotEmpty(UserReadCases());
        Assert.NotEmpty(UserWriteCases());
        Assert.NotEmpty(KernelUnsupportedCases());
        Assert.NotEmpty(KernelEretCases());
        Assert.NotEmpty(UserEretCases());
        // Status に EXL=0 を書く値と EXL=1 を書く値の両方を含む
        Assert.Contains(KernelWriteValues, v => (v & StatusExlBit) == 0);
        Assert.Contains(KernelWriteValues, v => (v & StatusExlBit) != 0);
    }

    // ---- mfc0 ----

    [Theory]
    [MemberData(nameof(KernelReadCases))]
    public void Execute_Mfc0_Kernel_ReadsRegister(int number, ExcCode code, uint? badVAddr) {
        IInstruction inst = ParseMfc0(number);
        RuntimeContext context = PriorExceptionStates.CreateKernel(code, badVAddr);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // rt だけが変化する
        MachineState expected = before.WithRegister(Rt, ExpectedRead(number, code, PriorExceptionStates.Epc, badVAddr));
        MachineState.AssertEqual(expected, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(KernelReadCases))]
    public void Undo_Mfc0_Kernel_RestoresState(int number, ExcCode code, uint? badVAddr) {
        IInstruction inst = ParseMfc0(number);
        RuntimeContext context = PriorExceptionStates.CreateKernel(code, badVAddr);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    [Theory]
    [MemberData(nameof(UserReadCases))]
    public void Execute_Mfc0_User_RaisesCpU(int number, ExcCode? postEretCode, uint? badVAddr) {
        IInstruction inst = ParseMfc0(number);
        RuntimeContext context = PriorExceptionStates.CreateUser(postEretCode, badVAddr);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // rt は書き換わらず，CP0 だけが例外発生直後の状態になる
        MachineState expected = before.WithException(ExcCode.CpU, InstructionHarness.InstructionAddress);
        MachineState.AssertEqual(expected, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(UserReadCases))]
    public void Undo_Mfc0_User_RestoresState(int number, ExcCode? postEretCode, uint? badVAddr) {
        IInstruction inst = ParseMfc0(number);
        RuntimeContext context = PriorExceptionStates.CreateUser(postEretCode, badVAddr);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.False(context.IsKernelMode);
    }

    // ---- mtc0 ----

    [Theory]
    [MemberData(nameof(KernelWriteCases))]
    public void Execute_Mtc0_Kernel_WritesRegister(int number, uint value, ExcCode code, uint? badVAddr) {
        IInstruction inst = ParseMtc0(number);
        RuntimeContext context = PriorExceptionStates.CreateKernel(code, badVAddr);
        context.Registers[Rt] = value;
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // 指定した CP0 レジスタだけが変化する
        MachineState.AssertEqual(ExpectedWrite(before, number, value), MachineState.Capture(context));
        // Status に EXL=0 を書いたときだけユーザーモードに戻る
        bool expectKernel = number != 12 || (value & StatusExlBit) != 0;
        Assert.Equal(expectKernel, context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(KernelWriteCases))]
    public void Undo_Mtc0_Kernel_RestoresState(int number, uint value, ExcCode code, uint? badVAddr) {
        IInstruction inst = ParseMtc0(number);
        RuntimeContext context = PriorExceptionStates.CreateKernel(code, badVAddr);
        context.Registers[Rt] = value;
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(UserWriteCases))]
    public void Execute_Mtc0_User_RaisesCpU(int number, uint value, ExcCode? postEretCode, uint? badVAddr) {
        IInstruction inst = ParseMtc0(number);
        RuntimeContext context = PriorExceptionStates.CreateUser(postEretCode, badVAddr);
        context.Registers[Rt] = value;
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // 書き込みは起きず，CP0 だけが例外発生直後の状態になる
        MachineState expected = before.WithException(ExcCode.CpU, InstructionHarness.InstructionAddress);
        MachineState.AssertEqual(expected, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(UserWriteCases))]
    public void Undo_Mtc0_User_RestoresState(int number, uint value, ExcCode? postEretCode, uint? badVAddr) {
        IInstruction inst = ParseMtc0(number);
        RuntimeContext context = PriorExceptionStates.CreateUser(postEretCode, badVAddr);
        context.Registers[Rt] = value;
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.False(context.IsKernelMode);
    }

    // ---- 未対応の CP0 レジスタ番号 ----

    [Theory]
    [MemberData(nameof(KernelUnsupportedCases))]
    public void Execute_UnsupportedNumber_Kernel_RaisesRuntimeError(string mnemonic, int number, ExcCode code, uint? badVAddr) {
        IInstruction inst = mnemonic == "mfc0" ? ParseMfc0(number) : ParseMtc0(number);
        RuntimeContext context = PriorExceptionStates.CreateKernel(code, badVAddr);
        context.Registers[Rt] = 0xffffffff;

        RuntimeError error = RuntimeErrorAssert.Raised(() => Processor.Execute(context, inst));

        Assert.Equal(RuntimeErrorKind.UnsupportedCP0Register, error.Kind);
    }

    [Theory]
    [MemberData(nameof(KernelUnsupportedCases))]
    public void Execute_UnsupportedNumber_Kernel_ChangesOnlyRuntimeError(string mnemonic, int number, ExcCode code, uint? badVAddr) {
        IInstruction inst = mnemonic == "mfc0" ? ParseMfc0(number) : ParseMtc0(number);
        RuntimeContext context = PriorExceptionStates.CreateKernel(code, badVAddr);
        context.Registers[Rt] = 0xffffffff;

        // rt も CP0 も変化せず，カーネルモードのまま (二重例外にならない)
        _ = RuntimeErrorAssert.RaisedWithoutSideEffects(context, () => Processor.Execute(context, inst), RuntimeErrorKind.UnsupportedCP0Register);
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(KernelUnsupportedCases))]
    public void Undo_UnsupportedNumber_Kernel_RestoresState(string mnemonic, int number, ExcCode code, uint? badVAddr) {
        IInstruction inst = mnemonic == "mfc0" ? ParseMfc0(number) : ParseMtc0(number);
        RuntimeContext context = PriorExceptionStates.CreateKernel(code, badVAddr);
        context.Registers[Rt] = 0xffffffff;
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.Null(context.RuntimeError);
        Assert.True(context.IsKernelMode);
    }

    // ---- eret ----

    [Theory]
    [MemberData(nameof(KernelEretCases))]
    public void Execute_Eret_Kernel_JumpsToEpcAndReturnsToUser(uint epc, ExcCode code, uint? badVAddr) {
        IInstruction inst = ParseEret();
        RuntimeContext context = InstructionHarness.CreateKernel(code, new Address(epc), badVAddr);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // PC=EPC (命令自身が設定し，+4 されない)，EXL=0．LastException，Cause，EPC，BadVAddr は変わらない
        MachineState expected = before.WithPC(new Address(epc)).WithCP0(status: 0);
        MachineState.AssertEqual(expected, MachineState.Capture(context));
        Assert.False(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(KernelEretCases))]
    public void Undo_Eret_Kernel_RestoresState(uint epc, ExcCode code, uint? badVAddr) {
        IInstruction inst = ParseEret();
        RuntimeContext context = InstructionHarness.CreateKernel(code, new Address(epc), badVAddr);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(UserEretCases))]
    public void Execute_Eret_User_RaisesCpU(ExcCode? postEretCode, uint? badVAddr) {
        IInstruction inst = ParseEret();
        RuntimeContext context = PriorExceptionStates.CreateUser(postEretCode, badVAddr);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // EPC へ飛ばない．例外発生前の EPC は eret 自身のアドレスで上書きされる
        MachineState expected = before.WithException(ExcCode.CpU, InstructionHarness.InstructionAddress);
        MachineState.AssertEqual(expected, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(UserEretCases))]
    public void Undo_Eret_User_RestoresState(ExcCode? postEretCode, uint? badVAddr) {
        IInstruction inst = ParseEret();
        RuntimeContext context = PriorExceptionStates.CreateUser(postEretCode, badVAddr);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        // 上書きされた EPC も元に戻る
        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.False(context.IsKernelMode);
    }

    // ---- 繰り返し実行 ----

    [Fact]
    public void RepeatedExecute_Mfc0_ReadCpURead_UndoesInReverseOrder() {
        IInstruction inst = ParseMfc0(14);
        RuntimeContext context = PriorExceptionStates.CreateKernel(ExcCode.Ov, null);

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            // 正常系: EPC を読む
            RepeatedExecution.SetRegisters((Rt, 0xffffffff)),
            // 異常系: Status に EXL=0 を書いてユーザーモードにしたので CpU
            ctx => ctx.WriteCP0Register(12, 0),
            // 正常系: CpU のハンドラ内 (ステップ開始時に LastException は消える) で，CpU が上書きした EPC を読む
            ctx => ctx.AckException()
        );

        Assert.Equal(3, results.Count);
        MachineState.AssertEqual(results[0].BeforeExecute.WithRegister(Rt, PriorExceptionStates.Epc.Addr), results[0].AfterExecute);
        MachineState.AssertEqual(results[1].BeforeExecute.WithException(ExcCode.CpU, InstructionHarness.KernelInstructionAddress), results[1].AfterExecute);
        MachineState.AssertEqual(results[2].BeforeExecute.WithRegister(Rt, InstructionHarness.KernelInstructionAddress.Addr), results[2].AfterExecute);
    }

    [Fact]
    public void RepeatedExecute_Mtc0_WriteCpUWrite_UndoesInReverseOrder() {
        IInstruction inst = ParseMtc0(12);
        RuntimeContext context = PriorExceptionStates.CreateKernel(ExcCode.Ov, null);

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            // 正常系: Status に EXL=0 を書いてユーザーモードへ
            RepeatedExecution.SetRegisters((Rt, 0x00000000)),
            // 異常系: ユーザーモードなので CpU
            RepeatedExecution.SetRegisters((Rt, 0x00000002)),
            // 正常系: CpU のハンドラ内 (ステップ開始時に LastException は消える) で再び EXL=0 を書く
            ctx => {
                ctx.AckException();
                ctx.Registers[Rt] = 0xfffffffd;
            }
        );

        Assert.Equal(3, results.Count);
        MachineState.AssertEqual(results[0].BeforeExecute.WithCP0(status: 0), results[0].AfterExecute);
        MachineState.AssertEqual(results[1].BeforeExecute.WithException(ExcCode.CpU, InstructionHarness.KernelInstructionAddress), results[1].AfterExecute);
        MachineState.AssertEqual(results[2].BeforeExecute.WithCP0(status: 0), results[2].AfterExecute);
    }

    [Fact]
    public void RepeatedExecute_Eret_EretCpUEret_UndoesInReverseOrder() {
        IInstruction inst = ParseEret();
        RuntimeContext context = PriorExceptionStates.CreateKernel(ExcCode.Sys, null);
        Address epc = PriorExceptionStates.Epc;

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            // 正常系: EPC へ復帰してユーザーモードへ
            _ => { },
            // 異常系: ユーザーモード (PC=EPC) なので CpU
            _ => { },
            // 正常系: CpU のハンドラ内 (ステップ開始時に LastException は消える) で再び eret
            ctx => {
                ctx.AckException();
                ctx.PC = InstructionHarness.KernelInstructionAddress;
            }
        );

        Assert.Equal(3, results.Count);
        MachineState.AssertEqual(results[0].BeforeExecute.WithPC(epc).WithCP0(status: 0), results[0].AfterExecute);
        MachineState.AssertEqual(results[1].BeforeExecute.WithException(ExcCode.CpU, epc), results[1].AfterExecute);
        MachineState.AssertEqual(results[2].BeforeExecute.WithPC(epc).WithCP0(status: 0), results[2].AfterExecute);
    }
}
