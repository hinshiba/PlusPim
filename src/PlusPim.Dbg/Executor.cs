using PlusPim.Image;
using PlusPim.Instructions;
using System.Buffers;
using System.Buffers.Binary;

namespace PlusPim.Dbg;

/// <summary>
/// 状態を1ステップ進める
/// </summary>
internal static class Executor {
    /// <summary>
    /// 1命令実行する
    /// </summary>
    /// <param name="s">進める状態</param>
    /// <param name="image">命令を取り出すプログラム</param>
    /// <param name="slots">有効な遅延スロットの種類</param>
    /// <param name="input">デバッギへの入力</param>
    /// <param name="output">デバッギの出力の書き込み先</param>
    public static StepResult Step(State s, ProgramImage image, DelaySlotsType slots, InputTape input, IBufferWriter<byte> output) {
        switch(s.Core.Status) {
            case Running:
                break;
            case ExceptionRaised t:
                s.Core.Push(new Frame(MemoryMap.ExceptionVector, t.Epc, s.Core.Registers, t.Code));
                s.Core.Registers.Pc = MemoryMap.ExceptionVector;
                s.Core.Status = new Running();
                return StepResult.Stepped;
            default:
                return StepResult.Halted;
        }

        ref Registers regs = ref s.Core.Registers;
        Address pc = regs.Pc;
        bool kernel = Cp0.IsKernel(regs[RName.Status]);
        bool fetchable = kernel || !MemoryMap.KText.Contains(pc, 4);
        if((fetchable ? image.Fetch(pc) : null) is not IExecute inst) {
            Branch? fetchSlot = s.Core.Retire();
            // セグメント内の整列した番地はMIPSでは実行できるが, PlusPimは命令しか実行できない
            if(fetchable && pc % 4 == 0 && (MemoryMap.IsText(pc) || MemoryMap.Data.Contains(pc, 4))) {
                return Fail(s, RuntimeErrorKind.NonInstructionExecution, pc);
            }
            Raise(s, ExcCode.AdEL, pc, fetchSlot);
            return StepResult.Stepped;
        }

        ReadOnlySpan<RegisterName> srcNames = inst.Src;
        ReadOnlySpan<RegisterName> destNames = inst.Dest;
        Span<uint> src = stackalloc uint[srcNames.Length];
        Span<uint> dest = stackalloc uint[destNames.Length];
        for(int i = 0; i < src.Length; i++) {
            src[i] = regs[srcNames[i]];
        }
        // 現在値で埋めておくと, lwl/lwrの合成と書かれなかったdestを同じ反映で扱える
        for(int i = 0; i < dest.Length; i++) {
            dest[i] = regs[destNames[i]];
        }

        Address next = pc + 4;
        switch(inst.Eval(src, dest)) {
            case Next:
                break;
            case Raise r:
                Raise(s, r.Code, null);
                return StepResult.Stepped;
            case AddressError e:
                Raise(s, e.Code, e.BadVAddr);
                return StepResult.Stepped;
            case RaiseRuntimeError f:
                return Fail(s, f.Kind, pc);
            // テキストの命令はメモリに置かないので, 読み書きすると実行される命令と食い違う
            case Load l when MemoryMap.IsText(l.Addr):
            case Store st when MemoryMap.IsText(st.Addr):
                return Fail(s, RuntimeErrorKind.TextAccess, pc);
            case Jump j:
                next = j.Target;
                if(j.Target == Loader.Sentinel) {
                    s.Status = new Exited(ExitCause.Returned);
                } else {
                    s.PopCallReturningTo(j.Target);
                }
                break;
            case Call c:
                s.Push(new Frame(c.Target, pc, regs, null));
                next = c.Target;
                break;
            case ExceptionReturn er:
                s.PopException();
                next = er.Target;
                break;
            case Load l:
                dest[0] = Compose(s.Memory, l, dest[0]);
                break;
            case Store st:
                Write(s.Memory, st);
                break;
            case RuntimeRequest rq:
                if(!RuntimeCalls.TryRun(s, rq, dest, input, output)) {
                    return StepResult.NeedInput;
                }
                if(s.Status is RuntimeErrorRaised) {
                    return StepResult.Stepped;
                }
                break;
        }

        for(int i = 0; i < dest.Length; i++) {
            regs[destNames[i]] = dest[i];
        }
        regs.Pc = next;
        return StepResult.Stepped;
    }

    private static StepResult Fail(State s, RuntimeErrorKind kind, Address pc) {
        s.Status = new RuntimeErrorRaised(kind, pc);
        return StepResult.Stepped;
    }

    private static void Raise(State s, ExcCode code, Address? badVAddr) {
        ref Registers regs = ref s.Registers;
        Address pc = regs.Pc;
        if(Cp0.IsKernel(regs[RName.Status])) {
            s.Status = new RuntimeErrorRaised(new DoubleException(code), pc);
            return;
        }
        regs[RName.Epc] = pc.Addr;
        regs[RName.Cause] = (uint)code << 2;
        regs[RName.Status] |= Cp0.Exl;
        if(badVAddr is Address bad) {
            regs[RName.BadVAddr] = bad.Addr;
        }
        s.Status = new ExceptionRaised(code, pc);
    }

    /// <summary>
    /// ロードした値. <see cref="LoadKind.Left"/> と <see cref="LoadKind.Right"/> は <paramref name="rt"/> と合成する
    /// </summary>
    private static uint Compose(Memory mem, Load l, uint rt) {
        Span<byte> b = stackalloc byte[4];
        Address aligned = new(l.Addr.Addr & ~3u);
        int k = (int)(l.Addr.Addr & 3) * 8;
        switch(l.Kind) {
            case LoadKind.B:
                return unchecked((uint)(sbyte)mem.Read(l.Addr));
            case LoadKind.BU:
                return mem.Read(l.Addr);
            case LoadKind.H:
                mem.Read(l.Addr, b[..2]);
                return unchecked((uint)BinaryPrimitives.ReadInt16LittleEndian(b));
            case LoadKind.HU:
                mem.Read(l.Addr, b[..2]);
                return BinaryPrimitives.ReadUInt16LittleEndian(b);
            case LoadKind.W:
                mem.Read(l.Addr, b);
                return BinaryPrimitives.ReadUInt32LittleEndian(b);
            case LoadKind.Left:
                // aligned から Addr までの k / 8 + 1 バイトを上位へ
                mem.Read(aligned, b);
                return (BinaryPrimitives.ReadUInt32LittleEndian(b) << (24 - k)) | (rt & (uint)((1UL << (24 - k)) - 1));
            case LoadKind.Right:
                // Addr から Addr | 3 までを下位へ
                mem.Read(aligned, b);
                return (BinaryPrimitives.ReadUInt32LittleEndian(b) >> k) | (rt & ~(uint.MaxValue >> k));
            default:
                throw new ArgumentOutOfRangeException(nameof(l));
        }
    }

    private static void Write(Memory mem, Store st) {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, st.Value);
        int k = (int)(st.Addr.Addr & 3);
        // 上位の k + 1 バイトを Addr & ~3 から Addr まで
        Address at = st.Kind == StoreKind.Left ? new(st.Addr.Addr & ~3u) : st.Addr;
        ReadOnlySpan<byte> bytes = st.Kind switch {
            StoreKind.B => b[..1],
            StoreKind.H => b[..2],
            StoreKind.W => b,
            StoreKind.Left => b[(3 - k)..],
            // 下位の 4 - k バイトを Addr から Addr | 3 まで
            StoreKind.Right => b[..(4 - k)],
            _ => throw new ArgumentOutOfRangeException(nameof(st)),
        };
        for(int i = 0; i < bytes.Length; i++) {
            mem.Write(at + (uint)i, bytes[i]);
        }
    }
}
