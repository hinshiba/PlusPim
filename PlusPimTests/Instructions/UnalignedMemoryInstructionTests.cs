using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// <c>lwl</c>，<c>lwr</c>，<c>swl</c>，<c>swr</c> の固有の振る舞い (手計算の期待値) のテスト．
/// 参照モデルによる全パターンの網羅は Load/StoreInstructionTests にある．
/// 対象ワードは [11 22 33 44]，次のワードは [55 66 77 88] (低位アドレス順)
/// </summary>
public class UnalignedMemoryInstructionTests {
    private const uint PriorRt = 0x8badf00d;
    private const uint StoreRt = 0xa1b2c3d4;

    private static readonly (RegisterID Rt, RegisterID Rs) Regs = RegisterAliases.Registers(Alias2Reg.None);

    private static IInstruction Parse(string op, ushort offset) {
        return InstructionHarness.Parse($"{op} {RegisterAliases.Name(Regs.Rt)}, {InstructionHarness.Imm(offset)}({RegisterAliases.Name(Regs.Rs)})");
    }

    private static RuntimeContext Setup(uint ea, ushort offset, uint rtValue) {
        RuntimeContext context = InstructionHarness.CreateUser();
        MemoryReference.FillWindow(context);
        MemoryReference.WriteWord(context, MemoryReference.TargetWord, 0x44332211);
        MemoryReference.WriteWord(context, MemoryReference.TargetWord + 4, 0x88776655);
        context.Registers[Regs.Rt] = rtValue;
        context.Registers[Regs.Rs] = MemoryReference.BaseFor(ea, offset);
        return context;
    }

    private static uint ReadWord(RuntimeContext context, uint address) {
        return context.ReadMemoryBytes(new Address(address), 4, false);
    }

    [Theory]
    [InlineData("lwl", 0u, 0x11adf00du)]
    [InlineData("lwl", 1u, 0x2211f00du)]
    [InlineData("lwl", 2u, 0x3322110du)]
    [InlineData("lwl", 3u, 0x44332211u)]
    [InlineData("lwr", 0u, 0x44332211u)]
    [InlineData("lwr", 1u, 0x8b443322u)]
    [InlineData("lwr", 2u, 0x8bad4433u)]
    [InlineData("lwr", 3u, 0x8badf044u)]
    public void Load_AllOffsets_MergesBytes(string op, uint off, uint expected) {
        // 負のオフセットを使って符号拡張も同時に確認する
        const ushort offset = 0xfff0;
        RuntimeContext context = Setup(MemoryReference.TargetWord + off, offset, PriorRt);
        IInstruction inst = Parse(op, offset);
        MachineState before = MemoryReference.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);

        Assert.Equal(expected, context.Registers[Regs.Rt]);
        Assert.False(context.IsKernelMode);
        Processor.Undo(context, record);
        MachineState.AssertEqual(before, MemoryReference.Capture(context));
    }

    [Theory]
    [InlineData("swl", 0u, 0x443322a1u, 0x88776655u)]
    [InlineData("swl", 1u, 0x4433a1b2u, 0x88776655u)]
    [InlineData("swl", 2u, 0x44a1b2c3u, 0x88776655u)]
    [InlineData("swl", 3u, 0xa1b2c3d4u, 0x88776655u)]
    [InlineData("swr", 0u, 0xa1b2c3d4u, 0x88776655u)]
    [InlineData("swr", 1u, 0xb2c3d411u, 0x88776655u)]
    [InlineData("swr", 2u, 0xc3d42211u, 0x88776655u)]
    [InlineData("swr", 3u, 0xd4332211u, 0x88776655u)]
    public void Store_AllOffsets_WritesBytes(string op, uint off, uint expectedWord, uint expectedNext) {
        const ushort offset = 0xfff0;
        RuntimeContext context = Setup(MemoryReference.TargetWord + off, offset, StoreRt);
        IInstruction inst = Parse(op, offset);
        MachineState before = MemoryReference.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);

        Assert.Equal(expectedWord, ReadWord(context, MemoryReference.TargetWord));
        Assert.Equal(expectedNext, ReadWord(context, MemoryReference.TargetWord + 4));
        Assert.False(context.IsKernelMode);
        Processor.Undo(context, record);
        MachineState.AssertEqual(before, MemoryReference.Capture(context));
    }

    [Theory]
    [InlineData("lwl", 0x2211f00du)] // EA = TargetWord + 1
    [InlineData("lwr", 0x8b443322u)]
    [InlineData("swl", 0x4433a1b2u)]
    [InlineData("swr", 0xb2c3d411u)]
    public void Execute_OddAddress_DoesNotRaise(string op, uint expectedValue) {
        uint rt = op.StartsWith("sw", StringComparison.Ordinal) ? StoreRt : PriorRt;
        RuntimeContext context = Setup(MemoryReference.TargetWord + 1, 0, rt);

        _ = Processor.Execute(context, Parse(op, 0));

        Assert.False(context.IsKernelMode);
        Assert.Null(context.LastException);
        uint actual = op.StartsWith("sw", StringComparison.Ordinal)
            ? ReadWord(context, MemoryReference.TargetWord)
            : context.Registers[Regs.Rt];
        Assert.Equal(expectedValue, actual);
    }

    /// <summary>
    /// <c>$rs = 0</c>，オフセット -4 の実効アドレスは 0xFFFFFFFC (符号拡張した加算が 32bit で折り返す)．
    /// メモリはスパースなので，4 GB 境界のワードにも直接置ける
    /// </summary>
    [Theory]
    [InlineData("lwl", 0x11adf00du)]
    [InlineData("lwr", 0x44332211u)]
    [InlineData("swl", 0x443322a1u)]
    [InlineData("swr", 0xa1b2c3d4u)]
    public void Execute_EffectiveAddressWrapsBelowZero_AccessesTopOfAddressSpace(string op, uint expected) {
        const uint top = 0xfffffffc;
        bool isStore = op.StartsWith("sw", StringComparison.Ordinal);
        RuntimeContext context = InstructionHarness.CreateUser();
        MemoryReference.WriteWord(context, top, 0x44332211);
        context.Registers[Regs.Rt] = isStore ? StoreRt : PriorRt;
        context.Registers[Regs.Rs] = 0;
        MachineState before = MemoryReference.Capture(context);

        ExecutionRecord record = Processor.Execute(context, Parse(op, 0xfffc));

        Assert.False(context.IsKernelMode);
        Assert.Null(context.LastException);
        Assert.Equal(expected, isStore ? ReadWord(context, top) : context.Registers[Regs.Rt]);
        Processor.Undo(context, record);
        MachineState.AssertEqual(before, MemoryReference.Capture(context));
        Assert.Equal(0x44332211u, ReadWord(context, top));
    }

    /// <summary>
    /// <c>lwr EA</c>，<c>lwl EA+3</c> で非アラインのワードを読める
    /// </summary>
    [Theory]
    [InlineData(1u, 0x55443322u)]
    [InlineData(2u, 0x66554433u)]
    [InlineData(3u, 0x77665544u)]
    public void LwrThenLwl_LoadsUnalignedWord(uint off, uint expected) {
        RuntimeContext context = Setup(MemoryReference.TargetWord + off, 0, PriorRt);
        // 基準レジスタは EA を指し，lwl は offset 3 で EA+3 を指す
        _ = Processor.Execute(context, Parse("lwr", 0));
        _ = Processor.Execute(context, Parse("lwl", 3));

        Assert.Equal(expected, context.Registers[Regs.Rt]);
    }

    /// <summary>
    /// <c>swr EA</c>，<c>swl EA+3</c> で非アラインのワードを書ける
    /// </summary>
    [Fact]
    public void SwrThenSwl_StoresUnalignedWord() {
        RuntimeContext context = Setup(MemoryReference.TargetWord + 1, 0, StoreRt);

        _ = Processor.Execute(context, Parse("swr", 0));
        _ = Processor.Execute(context, Parse("swl", 3));

        // バイト列 [11 d4 c3 b2] [a1 66 77 88]
        Assert.Equal(0xb2c3d411u, ReadWord(context, MemoryReference.TargetWord));
        Assert.Equal(0x887766a1u, ReadWord(context, MemoryReference.TargetWord + 4));
    }
}
