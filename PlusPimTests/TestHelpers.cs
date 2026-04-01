using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;
using Xunit;

namespace PlusPimTests;

internal record ContextSnapshot(uint[] Registers, uint HI, uint LO, Address PC, Dictionary<Address, byte>? Memory);

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
    /// RuntimeContextの現在の状態をスナップショットとして取得する
    /// </summary>
    public static ContextSnapshot TakeSnapshot(RuntimeContext context, Address[]? memoryAddresses = null) {
        Dictionary<Address, byte>? memory = null;
        if(memoryAddresses is not null) {
            memory = new Dictionary<Address, byte>();
            foreach(Address addr in memoryAddresses) {
                memory[addr] = context.ReadMemoryByte(addr);
            }
        }
        return new ContextSnapshot(
            context.Registers.ToArray(),
            context.HI,
            context.LO,
            context.PC,
            memory
        );
    }

    /// <summary>
    /// スナップショットと現在のRuntimeContextの状態が一致するかをアサートする
    /// </summary>
    public static void AssertSnapshotEqual(ContextSnapshot expected, RuntimeContext actual, Address[]? memoryAddresses = null) {
        Assert.Equal(expected.Registers, actual.Registers.ToArray());
        Assert.Equal(expected.HI, actual.HI);
        Assert.Equal(expected.LO, actual.LO);
        Assert.Equal(expected.PC, actual.PC);

        if(expected.Memory is not null && memoryAddresses is not null) {
            foreach(Address addr in memoryAddresses) {
                Assert.Equal(expected.Memory[addr], actual.ReadMemoryByte(addr));
            }
        }
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
