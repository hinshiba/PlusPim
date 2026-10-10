using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Buffers.Binary;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// 命令レベルテストで検証する状態 (doc/tests/instructions/execution_model.md)
/// </summary>
/// <param name="CallStack">コールスタック．先頭 (index 0) がスタックトップ．要素は参照で比較する</param>
/// <param name="MemoryBase">記録したメモリ窓の先頭アドレス</param>
/// <param name="Memory">メモリ窓 <c>[MemoryBase, MemoryBase + Memory.Length)</c> のバイト列</param>
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
    RuntimeErrorKind? RuntimeError,
    StackFrame[] CallStack,
    Label CurrentLabel,
    Address MemoryBase,
    byte[] Memory
) {
    /// <summary>
    /// <see cref="TestHelpers.SeedRegisters"/> がメモリを書き込む範囲 (既定のメモリ窓)
    /// </summary>
    public static readonly Address SeededMemoryBase = new(0x10000000);
    public const int SeededMemorySize = 16;

    /// <summary>
    /// コールスタックの深さ
    /// </summary>
    public int CallStackDepth => this.CallStack.Length;

    /// <summary>
    /// 既定のメモリ窓 (<see cref="SeededMemoryBase"/> から <see cref="SeededMemorySize"/> バイト) で状態を記録する
    /// </summary>
    public static MachineState Capture(RuntimeContext context) {
        return Capture(context, SeededMemoryBase, SeededMemorySize);
    }

    /// <summary>
    /// 指定したメモリ窓で状態を記録する
    /// </summary>
    public static MachineState Capture(RuntimeContext context, Address memoryBase, int memorySize) {
        (uint badVAddr, uint status, uint cause, uint epc) = context.GetCP0DisplayValues();
        byte[] memory = new byte[memorySize];
        for(int i = 0; i < memorySize; i++) {
            memory[i] = context.ReadMemoryByte(memoryBase + i);
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
            context.RuntimeError?.Kind,
            context.CallStack.ToArray(),
            context.CurrentLabel,
            memoryBase,
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
    /// HI と LO を書き換えた状態を返す
    /// </summary>
    public MachineState WithHiLo(uint hi, uint lo) {
        return this with { HI = hi, LO = lo };
    }

    /// <summary>
    /// PC を書き換えた状態を返す
    /// </summary>
    public MachineState WithPC(Address pc) {
        return this with { PC = pc };
    }

    /// <summary>
    /// メモリ窓内の <paramref name="address"/> から <paramref name="bytes"/> を書き込んだ状態を返す
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">書き込み範囲がメモリ窓に収まらないとき</exception>
    public MachineState WithMemory(Address address, params byte[] bytes) {
        int offset = this.OffsetInWindow(address, bytes.Length);
        byte[] memory = (byte[])this.Memory.Clone();
        bytes.CopyTo(memory, offset);
        return this with { Memory = memory };
    }

    /// <summary>
    /// <paramref name="value"/> の下位 <paramref name="size"/> バイトをリトルエンディアンで書き込んだ状態を返す
    /// </summary>
    public MachineState WithMemoryValue(Address address, uint value, int size) {
        if(size is < 1 or > 4) {
            throw new ArgumentOutOfRangeException(nameof(size));
        }
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return this.WithMemory(address, bytes[..size]);
    }

    /// <summary>
    /// メモリ窓内の1バイトを読む
    /// </summary>
    public byte MemoryByte(Address address) {
        return this.Memory[this.OffsetInWindow(address, 1)];
    }

    /// <summary>
    /// CP0 の表示値を書き換えた状態を返す．<see langword="null"/> の項目は変更しない
    /// </summary>
    public MachineState WithCP0(uint? badVAddr = null, uint? status = null, uint? cause = null, uint? epc = null) {
        return this with {
            BadVAddr = badVAddr ?? this.BadVAddr,
            Status = status ?? this.Status,
            Cause = cause ?? this.Cause,
            EPC = epc ?? this.EPC
        };
    }

    /// <summary>
    /// ユーザーモードで例外が発生した直後の状態を返す
    /// </summary>
    /// <param name="badVAddr">アドレス例外の実効アドレス．<see langword="null"/> なら既存の BadVAddr を保持する</param>
    public MachineState WithException(ExcCode code, Address epc, uint? badVAddr = null) {
        return this with {
            BadVAddr = badVAddr ?? this.BadVAddr,
            Status = 0x2,
            Cause = (uint)code << 2,
            EPC = epc.Addr,
            LastException = new ExceptionEvent(code, IsDouble: false)
        };
    }

    /// <summary>
    /// <see cref="LastException"/> を書き換えた状態を返す
    /// </summary>
    public MachineState WithLastException(ExceptionEvent? lastException) {
        return this with { LastException = lastException };
    }

    /// <summary>
    /// <see cref="IsTerminated"/> を書き換えた状態を返す
    /// </summary>
    public MachineState WithTerminated(bool isTerminated = true) {
        return this with { IsTerminated = isTerminated };
    }

    /// <summary>
    /// <see cref="RuntimeError"/> を書き換えた状態を返す
    /// </summary>
    public MachineState WithRuntimeError(RuntimeErrorKind? kind) {
        return this with { RuntimeError = kind };
    }

    /// <summary>
    /// 現在のラベルを書き換えた状態を返す
    /// </summary>
    public MachineState WithCurrentLabel(Label label) {
        return this with { CurrentLabel = label };
    }

    /// <summary>
    /// コールスタックを置き換えた状態を返す (index 0 がスタックトップ)
    /// </summary>
    public MachineState WithCallStack(params StackFrame[] callStack) {
        return this with { CallStack = callStack };
    }

    /// <summary>
    /// コールスタックに <paramref name="frame"/> を積んだ状態を返す
    /// </summary>
    public MachineState WithFramePushed(StackFrame frame) {
        return this with { CallStack = [frame, .. this.CallStack] };
    }

    /// <summary>
    /// コールスタックのトップを取り除いた状態を返す
    /// </summary>
    public MachineState WithFramePopped() {
        Assert.NotEmpty(this.CallStack);
        return this with { CallStack = this.CallStack[1..] };
    }

    /// <summary>
    /// この状態をコンテキストに書き戻す (RepeatedExecution で外部からの入力変更を巻き戻すため)
    /// </summary>
    /// <remarks>
    /// コールスタックと現在のラベルは書き戻す手段がないため，変化していないことをアサートする．
    /// メモリはこの状態のメモリ窓の範囲だけを書き戻す
    /// </remarks>
    public void ApplyTo(RuntimeContext context) {
        MachineState current = Capture(context, this.MemoryBase, this.Memory.Length);
        AssertSameCallStack(this.CallStack, current.CallStack);
        Assert.Equal(this.CurrentLabel, current.CurrentLabel);

        for(int i = 1; i < this.Registers.Length; i++) {
            context.Registers[(RegisterID)i] = this.Registers[i];
        }
        context.HI = this.HI;
        context.LO = this.LO;
        context.PC = this.PC;

        CP0RegisterFile cp0 = new() {
            BadVAddr = this.BadVAddr == 0 ? null : new Address(this.BadVAddr),
            Exl = (this.Status & 0x2) != 0,
            Exc = (ExcCode)((this.Cause >> 2) & 0x1F),
            Epc = new Address(this.EPC)
        };
        // 状態は種類しか持たないので，説明文は空にする
        RuntimeError? runtimeError = this.RuntimeError is RuntimeErrorKind kind ? new RuntimeError(kind, "") : null;
        context.RestoreExceptionState(new ExceptionState(cp0, this.LastException, this.IsTerminated, runtimeError));

        for(int i = 0; i < this.Memory.Length; i++) {
            context.WriteMemoryByte(this.MemoryBase + i, this.Memory[i]);
        }
    }

    private int OffsetInWindow(Address address, int length) {
        long offset = (long)address.Addr - this.MemoryBase.Addr;
        if(offset < 0 || offset + length > this.Memory.Length) {
            throw new ArgumentOutOfRangeException(nameof(address), $"{address} (+{length}) is outside the memory window {this.MemoryBase} (+{this.Memory.Length}).");
        }
        return (int)offset;
    }

    private static void AssertSameCallStack(StackFrame[] expected, StackFrame[] actual) {
        Assert.Equal(expected.Length, actual.Length);
        for(int i = 0; i < expected.Length; i++) {
            Assert.Same(expected[i], actual[i]);
        }
    }

    /// <summary>
    /// 全項目が一致することをアサートする．コールスタックは要素ごとに参照で比較する
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
        Assert.Equal(expected.RuntimeError, actual.RuntimeError);
        AssertSameCallStack(expected.CallStack, actual.CallStack);
        Assert.Equal(expected.CurrentLabel, actual.CurrentLabel);
        Assert.Equal(expected.MemoryBase, actual.MemoryBase);
        Assert.Equal(expected.Memory, actual.Memory);
    }
}
