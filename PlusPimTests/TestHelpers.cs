using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;
using Xunit;

namespace PlusPimTests;

/// <summary>
/// デバッガから観測できる状態 (ライブフレームのレジスタ・PC・HI/LO・CP0，コールスタックと直前の例外，ランタイムエラー)
/// </summary>
internal sealed record DebuggerSnapshot(
    uint[] Registers,
    uint PC,
    uint HI,
    uint LO,
    uint? BadVAddr,
    uint? Status,
    uint? Cause,
    uint? EPC,
    string FrameName,
    int CallStackDepth,
    ExcCode? ExceptionCode,
    bool? ExceptionIsDouble,
    RuntimeErrorKind? RuntimeError
);

internal static class TestHelpers {
    /// <summary>
    /// 一時アセンブリファイルを作成する．呼び出し側がfinallyで削除すること
    /// </summary>
    public static FileInfo WriteTempAsm(string content) {
        string path = Path.ChangeExtension(Path.GetTempFileName(), ".s");
        File.WriteAllText(path, content);
        return new FileInfo(path);
    }

    /// <summary>
    /// 単体テスト用のRuntimeContextを構築する
    /// </summary>
    public static RuntimeContext CreateRuntimeContext(SymbolTable? symbolTable = null) {
        SymbolTable table = symbolTable ?? new SymbolTable();
        return new RuntimeContext(
            Logger.Null.ToAction("Test"),
            (name, _, _) => table.Resolve(name),
            TextSegment.TextSegmentBase,
            new Label("test", new Address(0x400000))
        );
    }

    /// <summary>
    /// 固定シードで全レジスタ・HI・LO・メモリをランダム設定する
    /// </summary>
    public static void SeedRegisters(RuntimeContext context, int seed) {
        Random rng = new(seed);
        // $zero以外の31レジスタをランダム設定
        for(int i = 1; i < 32; i++) {
            context.Registers[(RegisterID)i] = (uint)rng.Next();
        }
        context.HI = (uint)rng.Next();
        context.LO = (uint)rng.Next();

        // メモリ数箇所に書き込み
        Address baseAddr = new(0x10000000);
        for(int i = 0; i < 16; i++) {
            context.WriteMemoryByte(baseAddr + i, (byte)rng.Next(256));
        }
    }

    /// <summary>
    /// デバッガの現在の状態をスナップショットとして取得する
    /// </summary>
    public static DebuggerSnapshot TakeSnapshot(PlusPimDbg debugger) {
        StackFrameInfo[] frames = debugger.GetCallStack();
        StackFrameInfo live = frames[0];
        ExceptionInfo? exception = debugger.GetLastException();
        return new DebuggerSnapshot(
            live.Registers,
            live.PC,
            live.HI,
            live.LO,
            live.CP0BadVAddr,
            live.CP0Status,
            live.CP0Cause,
            live.CP0EPC,
            live.Name,
            frames.Length,
            exception?.Reason,
            exception?.IsDouble,
            debugger.GetRuntimeError()?.Kind
        );
    }

    /// <summary>
    /// スナップショットとデバッガの現在の状態が一致するかをアサートする
    /// </summary>
    public static void AssertSnapshotEqual(DebuggerSnapshot expected, PlusPimDbg actual) {
        AssertSnapshotEqual(expected, TakeSnapshot(actual));
    }

    /// <summary>
    /// 2つのスナップショットが一致するかをアサートする
    /// </summary>
    public static void AssertSnapshotEqual(DebuggerSnapshot expected, DebuggerSnapshot current) {
        Assert.Equal(expected.Registers, current.Registers);
        Assert.Equal(expected.PC, current.PC);
        Assert.Equal(expected.HI, current.HI);
        Assert.Equal(expected.LO, current.LO);
        Assert.Equal(expected.BadVAddr, current.BadVAddr);
        Assert.Equal(expected.Status, current.Status);
        Assert.Equal(expected.Cause, current.Cause);
        Assert.Equal(expected.EPC, current.EPC);
        Assert.Equal(expected.FrameName, current.FrameName);
        Assert.Equal(expected.CallStackDepth, current.CallStackDepth);
        Assert.Equal(expected.ExceptionCode, current.ExceptionCode);
        Assert.Equal(expected.ExceptionIsDouble, current.ExceptionIsDouble);
        Assert.Equal(expected.RuntimeError, current.RuntimeError);
    }

    /// <summary>
    /// アセンブリ文字列からPlusPimDbgインスタンスを生成する
    /// </summary>
    public static (PlusPimDbg Debugger, FileInfo TempFile) CreateDebugger(string asmContent) {
        FileInfo tempFile = WriteTempAsm(asmContent);
        PlusPimDbg debugger = new([tempFile], Logger.Null);
        return (debugger, tempFile);
    }

    /// <summary>
    /// 命令を1つパースして返す．パース失敗時はnullを返す
    /// </summary>
    public static IInstruction? ParseInstruction(string assemblyLine, int lineIndex = 1) {
        return InstructionRegistry.Default.TryParse(assemblyLine, lineIndex, out IInstruction? instruction)
            ? instruction
            : null;
    }

    /// <summary>
    /// PlusPimDbgのGetRegisters結果からレジスタ値を比較用に取得する
    /// </summary>
    public static (uint[] Registers, uint PC, uint HI, uint LO) GetState(PlusPimDbg debugger) {
        return debugger.GetRegisters();
    }
}
