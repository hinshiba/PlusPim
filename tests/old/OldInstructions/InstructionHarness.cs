using PlusPim.Application;
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
    public static RuntimeContext CreateUser(int seed = DefaultSeed, IDebuggeeIO? io = null) {
        RuntimeContext context = TestHelpers.CreateRuntimeContext(CreateSymbolTable(), io);
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
    public static RuntimeContext CreateKernel(ExcCode code, Address epc, uint? badVAddr = null, int seed = DefaultSeed, IDebuggeeIO? io = null) {
        RuntimeContext context = CreateUser(seed, io);
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
    public static RuntimeContext CreatePostEret(ExcCode code, Address epc, uint? badVAddr = null, int seed = DefaultSeed, IDebuggeeIO? io = null) {
        RuntimeContext context = CreateKernel(code, epc, badVAddr, seed, io);
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
