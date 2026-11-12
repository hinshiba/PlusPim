using PlusPim.Instructions.Formats;
using PlusPim.Instructions.Instructions;
using PlusPim.TestValues;

namespace PlusPim.Instructions.Tests;

/// <summary>
/// 副作用が主目的の命令の命令単位テスト
/// </summary>
public class ExecuteTests {
    /// <summary>Destが書き換わらないことを検出するための初期値</summary>
    private const uint Sentinel = 0xdeadbeef;

    private static readonly Address Target = new(0x00400040);
    private const uint Pc = 0x00400010;
    private const uint Kernel = 0xff13;
    private const uint User = Kernel & ~Cp0.Exl;

    /// <summary><paramref name="inst"/> を <paramref name="src"/> で評価し, 副作用とDest[0]への書き込みを返す. Destが空なら <see langword="null"/></summary>
    private static (object? Effect, uint? Dest) Run(IExecute? inst, params uint[] src) {
        Assert.NotNull(inst);
        Assert.Equal(inst.Src.Length, src.Length);
        Assert.InRange(inst.Dest.Length, 0, 1);
        uint[] dest = [.. Enumerable.Repeat(Sentinel, inst.Dest.Length)];
        return (inst.Eval(src, dest).Value, dest is [uint d] ? d : null);
    }

    private static void AssertRegs(IExecute? inst, RegisterName[] src, RegisterName[] dest) {
        Assert.NotNull(inst);
        Assert.Equal(src, inst.Src.ToArray());
        Assert.Equal(dest, inst.Dest.ToArray());
    }

    public static TheoryData<uint, uint> Pairs() {
        TheoryData<uint, uint> data = new();
        foreach(uint a in Signed32.All) {
            foreach(uint b in Signed32.All) {
                data.Add(a, b);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Branch_JumpsOnlyWhenTaken(uint rs, uint rt) {
        Operand[] two = [GprId.T0, GprId.T1, Target];
        Operand[] one = [GprId.T0, Target];
        (IExecute? Inst, bool Taken, uint[] Src)[] cases = [
            (BranchOp<Beq>.FromOperands(two), rs == rt, [rs, rt]),
            (BranchOp<Bne>.FromOperands(two), rs != rt, [rs, rt]),
            (BranchZOp<Bgez>.FromOperands(one), (int)rs >= 0, [rs]),
            (BranchZOp<Bgtz>.FromOperands(one), (int)rs > 0, [rs]),
            (BranchZOp<Blez>.FromOperands(one), (int)rs <= 0, [rs]),
            (BranchZOp<Bltz>.FromOperands(one), (int)rs < 0, [rs]),
        ];
        foreach((IExecute? inst, bool taken, uint[] src) in cases) {
            Assert.Equal(taken ? new Jump(Target) : new Next(), Run(inst, src).Effect);
        }
    }

    [Fact]
    public void Branch_DeclaresRegisters() {
        AssertRegs(BranchOp<Beq>.FromOperands([GprId.T0, GprId.T1, Target]), [GprId.T0, GprId.T1], []);
        AssertRegs(BranchZOp<Bltz>.FromOperands([GprId.T0, Target]), [GprId.T0], []);
        Assert.Null(BranchOp<Beq>.FromOperands([GprId.T0, GprId.T1, new Immediate(4)]));
    }

    public static TheoryData<uint, ushort> Addresses() {
        TheoryData<uint, ushort> data = new();
        // baseが0のとき負のオフセットで実効アドレスが0未満になる
        foreach(uint baseValue in new uint[] { 0x10010000, 0 }) {
            foreach(uint low in Alignment.LowBits) {
                foreach(ushort offset in new ushort[] { 0, 0xfffc, 4 }) {
                    data.Add(baseValue | low, offset);
                }
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Addresses))]
    public void Load_RaisesOnlyWhenMisaligned(uint baseValue, ushort offset) {
        Operand[] ops = [GprId.T0, new Memory(GprId.T1, new Immediate(offset))];
        Address addr = new(unchecked(baseValue + (uint)(short)offset));
        (IExecute? Inst, LoadKind Kind, uint Mask)[] cases = [
            (LoadOp<Lb>.FromOperands(ops), LoadKind.B, 0),
            (LoadOp<Lbu>.FromOperands(ops), LoadKind.BU, 0),
            (LoadOp<Lh>.FromOperands(ops), LoadKind.H, 1),
            (LoadOp<Lhu>.FromOperands(ops), LoadKind.HU, 1),
            (LoadOp<Lw>.FromOperands(ops), LoadKind.W, 3),
            (LoadOp<Lwl>.FromOperands(ops), LoadKind.Left, 0),
            (LoadOp<Lwr>.FromOperands(ops), LoadKind.Right, 0),
        ];
        foreach((IExecute? inst, LoadKind kind, uint mask) in cases) {
            AssertRegs(inst, [GprId.T1], [GprId.T0]);
            (object? effect, uint? dest) = Run(inst, baseValue);
            Assert.Equal((addr.Addr & mask) != 0 ? new AddressError(addr, Store: false) : new Load(addr, kind), effect);
            Assert.Equal(Sentinel, dest);
        }
    }

    [Theory]
    [MemberData(nameof(Addresses))]
    public void Store_RaisesOnlyWhenMisaligned(uint baseValue, ushort offset) {
        const uint value = 0x12345678;
        Operand[] ops = [GprId.T0, new Memory(GprId.T1, new Immediate(offset))];
        Address addr = new(unchecked(baseValue + (uint)(short)offset));
        (IExecute? Inst, StoreKind Kind, uint Mask)[] cases = [
            (StoreOp<Sb>.FromOperands(ops), StoreKind.B, 0),
            (StoreOp<Sh>.FromOperands(ops), StoreKind.H, 1),
            (StoreOp<Sw>.FromOperands(ops), StoreKind.W, 3),
            (StoreOp<Swl>.FromOperands(ops), StoreKind.Left, 0),
            (StoreOp<Swr>.FromOperands(ops), StoreKind.Right, 0),
        ];
        foreach((IExecute? inst, StoreKind kind, uint mask) in cases) {
            AssertRegs(inst, [GprId.T0, GprId.T1], []);
            Assert.Equal((addr.Addr & mask) != 0 ? new AddressError(addr, Store: true) : new Store(addr, kind, value), Run(inst, value, baseValue).Effect);
        }
    }

    public static TheoryData<ushort> Immediates() => [.. Unsigned16.All];

    [Theory]
    [MemberData(nameof(Immediates))]
    public void Lui_WritesImmediateToUpperHalf(ushort imm) {
        IExecute? lui = Lui.FromOperands([GprId.T0, new Immediate(imm)]);
        AssertRegs(lui, [], [GprId.T0]);
        Assert.Equal((new Next(), (uint?)((uint)imm << 16)), Run(lui));
        Assert.Null(Lui.FromOperands([GprId.T0, GprId.T1]));
    }

    [Fact]
    public void Trap_RaisesWithoutTouchingRegisters() {
        (IExecute? Inst, ExcCode Code)[] cases = [
            (Syscall.FromOperands([]), ExcCode.Sys),
            (Break.FromOperands([]), ExcCode.Bp),
        ];
        foreach((IExecute? inst, ExcCode code) in cases) {
            AssertRegs(inst, [], []);
            Assert.Equal((new Raise(code), (uint?)null), Run(inst));
        }
        Assert.Null(Syscall.FromOperands([GprId.V0]));
        Assert.Null(Break.FromOperands([new Immediate(0)]));
    }

    [Fact]
    public void Jump_TargetsAddressOrRegister() {
        Assert.Equal(new Jump(Target), Run(J.FromOperands([Target])).Effect);
        Assert.Equal(new Jump(Target), Run(Jr.FromOperands([GprId.Ra]), Target.Addr).Effect);
        AssertRegs(Jr.FromOperands([GprId.Ra]), [GprId.Ra], []);
        Assert.Null(J.FromOperands([GprId.Ra]));
    }

    [Fact]
    public void Call_WritesReturnAddress() {
        IExecute? jal = Jal.FromOperands([Target]);
        AssertRegs(jal, [RName.Pc], [GprId.Ra]);
        Assert.Equal((new Call(Target), (uint?)Pc + 4), Run(jal, Pc));

        AssertRegs(Jalr.FromOperands([GprId.T9]), [RName.Pc, GprId.T9], [GprId.Ra]);
        IExecute? jalr = Jalr.FromOperands([GprId.T0, GprId.T0]);
        AssertRegs(jalr, [RName.Pc, GprId.T0], [GprId.T0]);
        Assert.Equal((new Call(Target), (uint?)Pc + 4), Run(jalr, Pc, Target.Addr));
    }

    [Fact]
    public void Move_CopiesBetweenGprAndHiLo() {
        AssertRegs(MoveOp<Mfhi>.FromOperands([GprId.T0]), [RName.Hi], [GprId.T0]);
        AssertRegs(MoveOp<Mflo>.FromOperands([GprId.T0]), [RName.Lo], [GprId.T0]);
        AssertRegs(MoveOp<Mthi>.FromOperands([GprId.T0]), [GprId.T0], [RName.Hi]);
        AssertRegs(MoveOp<Mtlo>.FromOperands([GprId.T0]), [GprId.T0], [RName.Lo]);
        Assert.Equal((new Next(), (uint?)42u), Run(MoveOp<Mfhi>.FromOperands([GprId.T0]), 42));
        AssertRegs(HiLoOp<Div>.FromOperands([GprId.T0, GprId.T1]), [GprId.T0, GprId.T1], [RName.Hi, RName.Lo]);
    }

    [Fact]
    public void Cp0Move_ResolvesSupportedNumbersOnly() {
        AssertRegs(Mfc0.FromOperands([GprId.K0, (GprId)13]), [RName.Status, RName.Cause], [GprId.K0]);
        AssertRegs(Mtc0.FromOperands([GprId.K0, (GprId)14]), [RName.Status, GprId.K0], [RName.Epc]);
        AssertRegs(Mfc0.FromOperands([GprId.K0, (GprId)8]), [RName.Status, RName.BadVAddr], [GprId.K0]);
        AssertRegs(Mfc0.FromOperands([GprId.K0, (GprId)12]), [RName.Status, RName.Status], [GprId.K0]);
        Assert.Null(Mfc0.FromOperands([GprId.K0, (GprId)9]));
        Assert.Null(Mtc0.FromOperands([GprId.K0, (GprId)0]));
    }

    [Fact]
    public void Cp0Move_RaisesCpUOutsideKernel() {
        foreach(IExecute? inst in new[] { Mfc0.FromOperands([GprId.K0, (GprId)13]), Mtc0.FromOperands([GprId.K0, (GprId)14]) }) {
            Assert.Equal((new Next(), (uint?)42u), Run(inst, Kernel, 42));
            Assert.Equal((new Raise(ExcCode.CpU), (uint?)Sentinel), Run(inst, User, 42));
        }
    }

    [Fact]
    public void Eret_ClearsOnlyExlAndReturnsToEpc() {
        IExecute? eret = Eret.FromOperands([]);
        AssertRegs(eret, [RName.Status, RName.Epc], [RName.Status]);
        Assert.Equal((new ExceptionReturn(Target), (uint?)User), Run(eret, Kernel, Target.Addr));
        Assert.Equal((new Raise(ExcCode.CpU), (uint?)Sentinel), Run(eret, User, Target.Addr));
    }

    [Fact]
    public void RuntimeCall_ReadsArgumentsAndWritesV0() {
        AssertRegs(RuntimeCall.FromOperands([]), [RName.Status, GprId.V0, GprId.A0, GprId.A1], [GprId.V0]);
        Assert.Null(RuntimeCall.FromOperands([GprId.V0]));
    }

    [Fact]
    public void RuntimeCall_RequestsKnownKindWithRawArguments() {
        IExecute? inst = RuntimeCall.FromOperands([]);
        foreach(RuntimeRequestKind kind in Enum.GetValues<RuntimeRequestKind>()) {
            Assert.Equal((new RuntimeRequest(kind, 0x80000000, 0xffffffff), (uint?)(uint)kind), Run(inst, Kernel, (uint)kind, 0x80000000, 0xffffffff));
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    [InlineData(13u)]
    [InlineData(0x101u)] // byte へ切り詰めると print_int になる
    [InlineData(0xffffffffu)]
    public void RuntimeCall_FailsOnUnknownKind(uint v0) {
        Assert.Equal((new Fail(RuntimeErrorKind.UnknownRuntimeCall), (uint?)Sentinel), Run(RuntimeCall.FromOperands([]), Kernel, v0, 0, 0));
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(13u)]
    public void RuntimeCall_RaisesCpUOutsideKernelBeforeCheckingKind(uint v0) {
        Assert.Equal((new Raise(ExcCode.CpU), (uint?)Sentinel), Run(RuntimeCall.FromOperands([]), User, v0, 0, 0));
    }
}
