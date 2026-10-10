using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// 命令レベルテストの共通の準備 (パース，コンテキスト生成，ラベル表)
/// </summary>
internal static class InstructionHarness {
    /// <summary>
    /// ユーザーモードで実行する命令のアドレス (<see cref="CreateUser"/>，<see cref="CreatePostEret"/> の PC)
    /// </summary>
    public static readonly Address InstructionAddress = new(0x00400100);

    /// <summary>
    /// カーネルモードで実行する命令のアドレス (<see cref="CreateKernel"/> の PC)
    /// </summary>
    public static readonly Address KernelInstructionAddress = new(0x80000200);

    /// <summary>
    /// レジスタ・HI・LO・メモリを初期化する既定のシード
    /// </summary>
    public const int DefaultSeed = 42;

    // ---- ラベル表 (values.md「ジャンプ先」) ----

    /// <summary>前方ラベル <c>fwd</c></summary>
    public static readonly Label ForwardLabel = new("fwd", new Address(0x00400200));

    /// <summary>後方ラベル <c>back</c></summary>
    public static readonly Label BackwardLabel = new("back", new Address(0x00400040));

    /// <summary>命令自身 (<see cref="InstructionAddress"/>) を指すラベル <c>self</c></summary>
    public static readonly Label SelfLabel = new("self", InstructionAddress);

    /// <summary>ラベル表に存在しないラベル名</summary>
    public const string UndefinedLabelName = "undef";

    /// <summary>
    /// <see cref="ForwardLabel"/>，<see cref="BackwardLabel"/>，<see cref="SelfLabel"/> を持つラベル表を作る
    /// </summary>
    public static SymbolTable CreateSymbolTable() {
        SymbolTable table = new();
        table.Add(ForwardLabel);
        table.Add(BackwardLabel);
        table.Add(SelfLabel);
        return table;
    }

    // ---- パース ----

    /// <summary>
    /// 命令を1つパースする．失敗したらテストを失敗させる
    /// </summary>
    public static IInstruction Parse(string assemblyLine) {
        IInstruction? inst = TestHelpers.ParseInstruction(assemblyLine);
        Assert.NotNull(inst);
        return inst;
    }

    /// <summary>
    /// 16bit 即値のアセンブリ表記 (例: <c>0xbabe</c>)
    /// </summary>
    public static string Imm(ushort value) {
        return $"0x{value:x4}";
    }

    // ---- コンテキスト ----

    /// <summary>
    /// ユーザーモード (CP0 初期値) のコンテキスト．
    /// 全レジスタ・HI・LO・既定メモリ窓をシードで乱数化し，PC は <see cref="InstructionAddress"/>
    /// </summary>
    public static RuntimeContext CreateUser(int seed = DefaultSeed) {
        RuntimeContext context = TestHelpers.CreateRuntimeContext(CreateSymbolTable());
        TestHelpers.SeedRegisters(context, seed);
        context.PC = InstructionAddress;
        return context;
    }

    /// <summary>
    /// 例外発生直後 (例外ハンドラ実行中) のカーネルモードのコンテキスト．PC は <see cref="KernelInstructionAddress"/>
    /// </summary>
    /// <remarks>
    /// PC=<paramref name="epc"/> で例外を発生させ，<c>LastException</c> を消してからハンドラへ移る
    /// </remarks>
    /// <param name="badVAddr">アドレス例外の実効アドレス．<see langword="null"/> なら設定しない</param>
    public static RuntimeContext CreateKernel(ExcCode code, Address epc, uint? badVAddr = null, int seed = DefaultSeed) {
        RuntimeContext context = CreateUser(seed);
        context.PC = epc;
        context.RaiseException(code, badVAddr is uint v ? new Address(v) : null);
        context.AckException();
        context.PC = KernelInstructionAddress;
        return context;
    }

    /// <summary>
    /// <c>eret</c> 後のユーザーモードのコンテキスト (EXL=0 だが Cause/EPC/BadVAddr に値が残る)．
    /// PC は <see cref="InstructionAddress"/>
    /// </summary>
    public static RuntimeContext CreatePostEret(ExcCode code, Address epc, uint? badVAddr = null, int seed = DefaultSeed) {
        RuntimeContext context = CreateKernel(code, epc, badVAddr, seed);
        context.WriteCP0Register(12, 0);
        context.PC = InstructionAddress;
        return context;
    }

    /// <summary>
    /// <paramref name="jalAddress"/> の <c>jal</c> で <paramref name="callee"/> を呼んだ時と同じフレームを積む．PC は変えない
    /// </summary>
    /// <returns>積んだフレーム</returns>
    public static StackFrame PushCallFrame(RuntimeContext context, Address jalAddress, Label callee) {
        Address pc = context.PC;
        context.PC = jalAddress;
        context.PushCallStack(callee);
        context.PC = pc;
        return context.CallStack.First();
    }
}

/// <summary>
/// ランタイムエラー (runtime_error_model.md) のアサーション．
/// 発生機構の知識はこのクラスだけに閉じ込める
/// </summary>
internal static class RuntimeErrorAssert {
    /// <summary>
    /// <paramref name="execute"/> がランタイムエラーを起こすことをアサートする．
    /// C# の例外が発生せず，実行の記録が例外ではなくランタイムエラーを示すことを確認する
    /// </summary>
    /// <returns>発生したランタイムエラー</returns>
    public static RuntimeError Raised(Func<ExecutionRecord> execute) {
        ExecutionRecord record;
        try {
            record = execute();
        } catch(Exception ex) {
            Assert.Fail($"Expected a runtime error, but got {ex.GetType().FullName}: {ex.Message}");
            throw;
        }
        Assert.True(record.Failed, "Expected a runtime error, but the execution did not fail.");
        Assert.False(record.Raised, "A runtime error must not raise an exception at the same time.");
        return record.Result.Error!.Value;
    }

    /// <summary>
    /// <paramref name="execute"/> が <paramref name="kind"/> のランタイムエラーを起こし，
    /// ランタイムエラー以外の状態を変更しないこと，および undo で実行前に戻ることをアサートする
    /// </summary>
    /// <param name="memoryBase">検証するメモリ窓の先頭．<see langword="null"/> なら既定のメモリ窓</param>
    /// <returns>発生したランタイムエラー</returns>
    public static RuntimeError RaisedWithoutSideEffects(
        RuntimeContext context, Func<ExecutionRecord> execute, RuntimeErrorKind kind,
        Address? memoryBase = null, int memorySize = MachineState.SeededMemorySize
    ) {
        Address windowBase = memoryBase ?? MachineState.SeededMemoryBase;
        MachineState before = MachineState.Capture(context, windowBase, memorySize);

        ExecutionRecord? record = null;
        RuntimeError error = Raised(() => record = execute());
        Assert.NotNull(record);

        Assert.Equal(kind, error.Kind);
        Assert.Equal(error, context.RuntimeError);
        MachineState.AssertEqual(before.WithRuntimeError(kind), MachineState.Capture(context, windowBase, memorySize));

        Processor.Undo(context, record);
        MachineState.AssertEqual(before, MachineState.Capture(context, windowBase, memorySize));
        return error;
    }
}

/// <summary>
/// 同じ命令インスタンスの繰り返し実行と逆順の undo (execution_model.md「繰り返し実行」)
/// </summary>
internal static class RepeatedExecution {
    /// <summary>
    /// 各ステップで入力を変えてから <c>Execute</c> し，その後逆順に <c>Undo</c> する．
    /// 各 <c>Undo</c> の後の状態が対応する <c>Execute</c> 直前の状態と一致することをアサートし，
    /// その後ステップによる入力変更を巻き戻す (他の命令による書き換えの undo に相当)
    /// </summary>
    /// <remarks>
    /// ステップはコールスタックと現在のラベルを変えてはならない．
    /// メモリは既定のメモリ窓だけを検証する
    /// </remarks>
    /// <returns>各ステップの (Execute 直前, Execute 直後) の状態．順方向の結果の検証に使う</returns>
    public static IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> Run(
        IInstruction inst, RuntimeContext context, params Action<RuntimeContext>[] steps
    ) {
        return RunWithMemory(inst, context, MachineState.SeededMemoryBase, MachineState.SeededMemorySize, steps);
    }

    /// <summary>
    /// <see cref="Run"/> のメモリ窓を指定する版
    /// </summary>
    public static IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> RunWithMemory(
        IInstruction inst, RuntimeContext context, Address memoryBase, int memorySize, params Action<RuntimeContext>[] steps
    ) {
        Assert.NotEmpty(steps);
        Stack<(MachineState BeforeInput, MachineState BeforeExecute, ExecutionRecord Record)> history = new();
        List<(MachineState BeforeExecute, MachineState AfterExecute)> results = [];

        foreach(Action<RuntimeContext> step in steps) {
            MachineState beforeInput = MachineState.Capture(context, memoryBase, memorySize);
            step(context);
            MachineState beforeExecute = MachineState.Capture(context, memoryBase, memorySize);
            ExecutionRecord record = Processor.Execute(context, inst);
            history.Push((beforeInput, beforeExecute, record));
            results.Add((beforeExecute, MachineState.Capture(context, memoryBase, memorySize)));
        }

        while(history.Count > 0) {
            (MachineState beforeInput, MachineState beforeExecute, ExecutionRecord record) = history.Pop();
            Processor.Undo(context, record);
            MachineState.AssertEqual(beforeExecute, MachineState.Capture(context, memoryBase, memorySize));
            beforeInput.ApplyTo(context);
            MachineState.AssertEqual(beforeInput, MachineState.Capture(context, memoryBase, memorySize));
        }
        return results;
    }

    /// <summary>
    /// レジスタを設定するステップ
    /// </summary>
    public static Action<RuntimeContext> SetRegisters(params (RegisterID Id, uint Value)[] values) {
        return context => {
            foreach((RegisterID id, uint value) in values) {
                context.Registers[id] = value;
            }
        };
    }
}
