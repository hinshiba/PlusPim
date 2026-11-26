using PlusPim.Image;
using PlusPim.Instructions;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;

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
    public static StepResult Step(State s, ProgramImage image, DelaySlotsConfig slots, InputTape input, IBufferWriter<byte> output) {
        return s.Core.Status switch {
            Running => StepExecute(s, image, slots, input, output),
            ExceptionRaised t => AckException(s, t),
            _ => StepResult.Halted,
        };
    }

    /// <summary>
    /// 前ステップで生じた例外の通知を回収し，例外ベクタへジャンプする
    /// </summary>
    private static StepResult AckException(State s, ExceptionRaised t) {
        s.Core.Push(new Frame(MemoryMap.ExceptionVector, t.Epc, s.Core.Registers, t.Code));
        s.Core.Registers.Pc = MemoryMap.ExceptionVector;
        s.Core.Status = new Running();
        return StepResult.Stepped;
    }

    private static StepResult StepExecute(State s, ProgramImage image, DelaySlotsConfig slots, InputTape input, IBufferWriter<byte> output) {
        ref Registers regs = ref s.Core.Registers;
        Address pc = regs.Pc;
        bool kernel = Cp0.IsKernel(regs[RName.Status]);
        bool fetchable = kernel || !MemoryMap.KText.Contains(pc, 4);
        if((fetchable ? image.Fetch(pc) : null) is not IExecute inst) {
            Land(s);
            // セグメント内の整列した番地はMIPSでは実行できるが, PlusPimは命令しか実行できない
            if(fetchable && pc % 4 == 0 && (MemoryMap.IsText(pc) || MemoryMap.Data.Contains(pc, 4))) {
                return Fail(s, RuntimeErrorKind.NonInstructionExecution, pc);
            }
            Raise(s, ExcCode.AdEL, pc);
            return StepResult.Stepped;
        }

        // 読み込み対象のレジスタの値を取り出す
        ReadOnlySpan<RegisterName> srcNames = inst.Src;
        Span<uint> src = stackalloc uint[srcNames.Length];
        for(int i = 0; i < src.Length; i++) {
            src[i] = regs[srcNames[i]];
        }

        // 書き込み先を準備する
        ReadOnlySpan<RegisterName> destNames = inst.Dest;
        Span<uint> dest = stackalloc uint[destNames.Length];

        // 命令を評価
        Effect e = inst.Eval(src, dest);

        if(e is RuntimeRequest rq && !RuntimeCalls.TryRun(s, rq, dest, input, output)) {
            return StepResult.NeedInput;
        }
        Land(s);
        bool enableBranchDelay = (slots & DelaySlotsConfig.Branch) != 0;
        bool enableLoadDelay = (slots & DelaySlotsConfig.Load) != 0;
        uint linkPcOffset = enableBranchDelay ? 8u : 4u;
        // このステップ終了後の次のPCの値
        Address nextPc = pc + 4;
        // このステップで発生した分岐
        Branch? occurredBranch = null;

        switch(e) {
            case Next:
                break;

            case Raise r:
                Raise(s, r.Code, null);
                return StepResult.Stepped;
            case AddressError ae:
                Raise(s, ae.Code, ae.BadVAddr);
                return StepResult.Stepped;
            case RaiseRuntimeError f:
                return Fail(s, f.Kind, pc);
            // テキストの命令はメモリに置かないので, 読み書きすると実行される命令と食い違う
            case Load l when MemoryMap.IsText(l.Addr):
            case Store st when MemoryMap.IsText(st.Addr):
                return Fail(s, RuntimeErrorKind.TextAccess, pc);
            case Jump or Call or NotTaken or ExceptionReturn when s.Core.Branch is not null:
                return Fail(s, RuntimeErrorKind.TransferInDelaySlot, pc);

            case Jump j:
                occurredBranch = new Branch(pc, j.Target, Transfer.Jump);
                break;
            case Call c:
                dest[0] = (pc + linkPcOffset).Addr;
                occurredBranch = new Branch(pc, c.Target, Transfer.Call);
                break;
            case NotTaken:
                if(!dest.IsEmpty) {
                    dest[0] = (pc + linkPcOffset).Addr;
                }
                occurredBranch = new Branch(pc, pc + linkPcOffset, Transfer.Fallthrough);
                break;
            case ExceptionReturn er:
                s.Core.PopException();
                nextPc = er.Target;
                break;
            case Load l:
                // lwl/lwrのバイパスのため, 遅延スロットのロードの値を評価する
                uint value = Compose(s.Memory, l, regs[l.Rt]);
                if(enableLoadDelay) {
                    s.Core.Load = new DelayedLoad(l.Rt, value);
                } else {
                    // 書き込み先を確保してないのでここで書き込む
                    // 本来ならWBで行うべきだがロード遅延スロットがないなら影響はない
                    regs[l.Rt] = value;
                }
                break;
            case Store st:
                Write(s.Memory, st);
                break;
            case RuntimeRequest:
                if(s.Core.Status is RuntimeErrorRaised) {
                    return StepResult.Stepped;
                }
                break;
        }

        // 書き込み先のレジスタに値を書き込む
        for(int i = 0; i < dest.Length; i++) {
            regs[destNames[i]] = dest[i];
        }

        // 分岐遅延スロットの有無に応じて次のPCを設定する
        // arrival: 今回のステップで反映する分岐
        // それ以外は遅延として状態に保持する
        (Branch? arrival, s.Core.Branch) = enableBranchDelay ? (s.Core.Branch, occurredBranch) : (occurredBranch, null);
        if(arrival is Branch a) {
            nextPc = a.Target;
            if(s.Core.Status is Running) {
                Arrive(s, a, linkPcOffset);
            }
        }
        regs.Pc = nextPc;
        return StepResult.Stepped;
    }

    /// <summary>
    /// ロードを完了させる
    /// </summary>
    /// <remarks>
    /// 遅延スロットの命令がオペランドを読んだ後, 書き込む前に呼ぶ.
    /// 遅延スロットの命令が例外やエラーで完了しなくても, 直前の命令は完了している.
    /// </remarks>
    private static void Land(State s) {
        if(s.Core.Load is DelayedLoad l) {
            s.Core.Registers[l.Reg] = l.Value;
            s.Core.Load = null;
        }
    }

    /// <summary>
    /// 遅延スロットを終えて <paramref name="b"/> の移る先へ着いたときのコールスタックと終了の処理
    /// </summary>
    /// <param name="link">呼び出した命令から戻り先までのバイト数</param>
    /// <remarks>PCを移す前に呼ぶので, 積むフレームのレジスタは呼び出し元のものである</remarks>
    private static void Arrive(State s, Branch b, uint link) {
        switch(b.Transfer) {
            case Transfer.Call:
                s.Core.Push(new Frame(b.Target, b.Site, s.Core.Registers, null));
                break;
            case Transfer.Jump when b.Target == Loader.Sentinel:
                s.Core.Status = new Exited(ExitCause.Returned);
                break;
            case Transfer.Jump:
                s.Core.PopCallReturningTo(b.Target, link);
                break;
        }
    }

    private static StepResult Fail(State s, RuntimeErrorKind kind, Address pc) {
        s.Core.Status = new RuntimeErrorRaised(kind, pc);
        return StepResult.Stepped;
    }

    /// <summary>
    /// 例外を発生させる
    /// </summary>
    /// <remarks>遅延スロットなら分岐を取り消し, 例外処理から戻ったときに分岐からやり直せるようEPCを分岐の命令にする</remarks>
    private static void Raise(State s, ExcCode code, Address? badVAddr) {
        ref Registers regs = ref s.Core.Registers;
        Address pc = regs.Pc;
        if(Cp0.IsKernel(regs[RName.Status])) {
            s.Core.Status = new RuntimeErrorRaised(new DoubleException(code), pc);
            return;
        }
        Branch? slot = s.Core.Branch;
        s.Core.Branch = null;
        Address epc = slot?.Site ?? pc;
        regs[RName.Epc] = epc.Addr;
        regs[RName.Cause] = ((uint)code << 2) | (slot is null ? 0 : Cp0.Bd);
        regs[RName.Status] |= Cp0.Exl;
        if(badVAddr is Address bad) {
            regs[RName.BadVAddr] = bad.Addr;
        }
        s.Core.Status = new ExceptionRaised(code, epc);
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
                throw new UnreachableException(nameof(l));
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
            _ => throw new UnreachableException(nameof(st)),
        };
        for(int i = 0; i < bytes.Length; i++) {
            mem.Write(at + (uint)i, bytes[i]);
        }
    }
}
