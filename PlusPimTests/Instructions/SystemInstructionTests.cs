using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// <c>syscall</c>，<c>break</c> の命令レベルテスト
/// (doc/tests/instructions/instruction_tests.md，system_call_model.md)
/// </summary>
/// <remarks>
/// 異常系 (<c>Sys</c>/<c>Bp</c>) のみ．カーネルモードでの実行は二重例外になる
/// (execution_model.md「カーネルモードでの例外(二重例外)」)
/// </remarks>
public class SystemInstructionTests {
    private static readonly string[] Mnemonics = ["syscall", "break"];

    /// <summary>
    /// 実装に依存しない期待値: 命令に対応する例外コード
    /// </summary>
    private static ExcCode ExpectedCode(string mnemonic) {
        return mnemonic switch {
            "syscall" => ExcCode.Sys,
            "break" => ExcCode.Bp,
            _ => throw new ArgumentOutOfRangeException(nameof(mnemonic)),
        };
    }

    /// <summary>
    /// 命令 × ユーザーモードの開始状態 (初期値 / 各 eret 後)
    /// </summary>
    public static TheoryData<string, ExcCode?, uint?> AllCases() {
        TheoryData<string, ExcCode?, uint?> data = new();
        foreach(string mnemonic in Mnemonics) {
            foreach((ExcCode? postEretCode, uint? badVAddr) in PriorExceptionStates.UserStates()) {
                data.Add(mnemonic, postEretCode, badVAddr);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Execute_RaisesException(string mnemonic, ExcCode? postEretCode, uint? badVAddr) {
        IInstruction inst = InstructionHarness.Parse(mnemonic);
        RuntimeContext context = PriorExceptionStates.CreateUser(postEretCode, badVAddr);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // CP0 と LastException だけが例外発生直後の状態になる (BadVAddr はアドレス例外でないので保持)
        MachineState expected = before.WithException(ExpectedCode(mnemonic), InstructionHarness.InstructionAddress);
        MachineState.AssertEqual(expected, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Undo_RaisesException_RestoresState(string mnemonic, ExcCode? postEretCode, uint? badVAddr) {
        IInstruction inst = InstructionHarness.Parse(mnemonic);
        RuntimeContext context = PriorExceptionStates.CreateUser(postEretCode, badVAddr);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        // CP0 (残っていた Cause/EPC/BadVAddr を含む) と LastException も厳密に戻る
        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.False(context.IsKernelMode);
    }

    /// <summary>
    /// 命令 × カーネルモードの開始状態 (各例外の発生直後)
    /// </summary>
    public static TheoryData<string, ExcCode, uint?> KernelCases() {
        TheoryData<string, ExcCode, uint?> data = new();
        foreach(string mnemonic in Mnemonics) {
            foreach((ExcCode code, uint? badVAddr) in PriorExceptionStates.KernelStates()) {
                data.Add(mnemonic, code, badVAddr);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(KernelCases))]
    public void Execute_Kernel_RaisesDoubleException(string mnemonic, ExcCode priorCode, uint? badVAddr) {
        IInstruction inst = InstructionHarness.Parse(mnemonic);
        RuntimeContext context = PriorExceptionStates.CreateKernel(priorCode, badVAddr);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // CP0 は変化せず，二重例外として記録されて終了する
        MachineState expected = before
            .WithLastException(new ExceptionEvent(ExpectedCode(mnemonic), IsDouble: true))
            .WithTerminated();
        MachineState.AssertEqual(expected, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(KernelCases))]
    public void Undo_Kernel_DoubleException_RestoresState(string mnemonic, ExcCode priorCode, uint? badVAddr) {
        IInstruction inst = InstructionHarness.Parse(mnemonic);
        RuntimeContext context = PriorExceptionStates.CreateKernel(priorCode, badVAddr);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        // LastException と IsTerminated も実行前に戻る
        MachineState.AssertEqual(before, MachineState.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Fact]
    public void Parameters_CoverInitialAndPostEretStates() {
        Assert.NotEmpty(AllCases());
        Assert.Contains(PriorExceptionStates.UserStates(), s => s.PostEretCode is null);
        Assert.Contains(PriorExceptionStates.UserStates(), s => s.PostEretCode is not null && s.BadVAddr is null);
        Assert.Contains(PriorExceptionStates.UserStates(), s => s.PostEretCode is not null && s.BadVAddr is not null);
    }

    [Theory]
    [InlineData("syscall")]
    [InlineData("break")]
    public void RepeatedExecute_ExceptionHandlerReturnException_UndoesInReverseOrder(string mnemonic) {
        IInstruction inst = InstructionHarness.Parse(mnemonic);
        RuntimeContext context = InstructionHarness.CreateUser();
        ExcCode code = ExpectedCode(mnemonic);

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(
            inst,
            context,
            _ => { }, // 初期状態
            PriorExceptionStates.ReturnFromHandler, // 例外ハンドラからの復帰後
            PriorExceptionStates.ReturnFromHandler
        );

        foreach((MachineState beforeExecute, MachineState afterExecute) in results) {
            MachineState.AssertEqual(beforeExecute.WithException(code, InstructionHarness.InstructionAddress), afterExecute);
        }
    }
}

/// <summary>
/// 例外を経た CP0 の開始状態 (cp0_model.md「追加のパラメータ」) の共通定義．
/// <see cref="SystemInstructionTests"/> と <see cref="CP0InstructionTests"/> で用いる
/// </summary>
internal static class PriorExceptionStates {
    /// <summary>
    /// 開始状態の元になった例外の EPC (命令アドレスとは異なる通常のアドレス)
    /// </summary>
    public static readonly Address Epc = new(0x00400010);

    /// <summary>
    /// 開始状態の元になった例外の BadVAddr
    /// </summary>
    public const uint BadVAddr = 0x10000001;

    /// <summary>
    /// 例外発生直後のカーネルモードの状態: 各例外コード × BadVAddr あり/なし．
    /// アドレス例外は必ず BadVAddr を設定するので「あり」のみ．
    /// それ以外は BadVAddr を変えないので，以前のアドレス例外の値が残る「あり」も含める
    /// </summary>
    public static IEnumerable<(ExcCode Code, uint? BadVAddr)> KernelStates() {
        foreach(ExcCode code in Enum.GetValues<ExcCode>()) {
            if(code is not (ExcCode.AdEL or ExcCode.AdES)) {
                yield return (code, null);
            }
            yield return (code, BadVAddr);
        }
    }

    /// <summary>
    /// ユーザーモードの状態: 初期値 (<c>PostEretCode</c> が <see langword="null"/>) と，各カーネルモードの状態からの eret 後
    /// </summary>
    public static IEnumerable<(ExcCode? PostEretCode, uint? BadVAddr)> UserStates() {
        yield return (null, null);
        foreach((ExcCode code, uint? badVAddr) in KernelStates()) {
            yield return (code, badVAddr);
        }
    }

    /// <summary>
    /// <see cref="UserStates"/> の1つのコンテキストを作る．PC は <see cref="InstructionHarness.InstructionAddress"/>
    /// </summary>
    public static RuntimeContext CreateUser(ExcCode? postEretCode, uint? badVAddr) {
        return postEretCode is ExcCode code
            ? InstructionHarness.CreatePostEret(code, Epc, badVAddr)
            : InstructionHarness.CreateUser();
    }

    /// <summary>
    /// <see cref="KernelStates"/> の1つのコンテキストを作る．PC は <see cref="InstructionHarness.KernelInstructionAddress"/>
    /// </summary>
    public static RuntimeContext CreateKernel(ExcCode code, uint? badVAddr) {
        return InstructionHarness.CreateKernel(code, Epc, badVAddr);
    }

    /// <summary>
    /// 外部の操作としての例外ハンドラからの復帰 (デバッガのステップ開始時の Ack と，EXL のクリア)．
    /// Cause/EPC/BadVAddr は残り，PC は変えない
    /// </summary>
    public static void ReturnFromHandler(RuntimeContext context) {
        context.AckException();
        context.RestoreCP0(context.GetCP0Snapshot() with { Exl = false });
    }
}
