using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// 命令レベルテストで検証する状態 (doc/tests/instructions/execution_model.md)
/// </summary>
internal sealed record MachineState(
    uint[] Registers,
    uint HI,
    uint LO,
    Address PC,
    uint BadVAddr,
    uint Status,
    uint Cause,
    uint EPC,
    ExceptionEvent? LastException,
    bool IsTerminated,
    int CallStackDepth,
    Label CurrentLabel,
    byte[] Memory
) {
    /// <summary>
    /// <see cref="TestHelpers.SeedRegisters"/> がメモリを書き込む範囲
    /// </summary>
    public static readonly Address SeededMemoryBase = new(0x10000000);
    public const int SeededMemorySize = 16;

    public static MachineState Capture(RuntimeContext context) {
        (uint badVAddr, uint status, uint cause, uint epc) = context.GetCP0DisplayValues();
        byte[] memory = new byte[SeededMemorySize];
        for(int i = 0; i < SeededMemorySize; i++) {
            memory[i] = context.ReadMemoryByte(SeededMemoryBase + i);
        }
        return new MachineState(
            context.Registers.ToArray(),
            context.HI,
            context.LO,
            context.PC,
            badVAddr,
            status,
            cause,
            epc,
            context.LastException,
            context.IsTerminated,
            context.CallStack.Count,
            context.CurrentLabel,
            memory
        );
    }

    /// <summary>
    /// 1つの汎用レジスタだけを書き換えた状態を返す
    /// </summary>
    public MachineState WithRegister(RegisterID id, uint value) {
        uint[] registers = (uint[])this.Registers.Clone();
        registers[(int)id] = value;
        return this with { Registers = registers };
    }

    /// <summary>
    /// ユーザーモードで例外が発生した直後の状態を返す
    /// </summary>
    public MachineState WithException(ExcCode code, Address epc, uint badVAddr = 0) {
        return this with {
            BadVAddr = badVAddr,
            Status = 0x2,
            Cause = (uint)code << 2,
            EPC = epc.Addr,
            LastException = new ExceptionEvent(code, IsDouble: false)
        };
    }

    /// <summary>
    /// 全項目が一致することをアサートする
    /// </summary>
    public static void AssertEqual(MachineState expected, MachineState actual) {
        Assert.Equal(expected.Registers, actual.Registers);
        Assert.Equal(expected.HI, actual.HI);
        Assert.Equal(expected.LO, actual.LO);
        Assert.Equal(expected.PC, actual.PC);
        Assert.Equal(expected.BadVAddr, actual.BadVAddr);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Cause, actual.Cause);
        Assert.Equal(expected.EPC, actual.EPC);
        Assert.Equal(expected.LastException, actual.LastException);
        Assert.Equal(expected.IsTerminated, actual.IsTerminated);
        Assert.Equal(expected.CallStackDepth, actual.CallStackDepth);
        Assert.Equal(expected.CurrentLabel, actual.CurrentLabel);
        Assert.Equal(expected.Memory, actual.Memory);
    }
}
