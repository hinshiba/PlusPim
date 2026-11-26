using PlusPim.Dbg;
using PlusPim.Image;
using PlusPim.Instructions;
using System.Buffers;

namespace PlusPim.Conformance.Tests;

/// <summary>1ケースの結果</summary>
internal readonly union SstOutcome(SstPassed, SstSkipped, SstKnown, SstMismatch);

/// <summary>R3000と一致した</summary>
internal readonly record struct SstPassed;

/// <summary>PlusPimで実行できない. 比べていない</summary>
internal readonly record struct SstSkipped(string Reason);

/// <summary>既知の差. PlusPimの仕様か, 未修正の差分</summary>
internal readonly record struct SstKnown(string Reason);

/// <summary>R3000と一致しない</summary>
internal readonly record struct SstMismatch(string Detail);

/// <summary>
/// SingleStepTestsの1ケースをPlusPimで1命令実行し, 最終状態を比べる
/// </summary>
/// <remarks>
/// PlusPimはテキストセグメントにしか命令を置けないので, 命令の番地を上位4ビットを落とした番地へ移す.
/// 移した番地から決まる値は, 比べる前に同じだけ移す.
/// </remarks>
internal static class SstRunner {
    /// <summary>PSXの例外ベクタ. 最終状態のPCがこれなら例外が起きた</summary>
    private const uint PsxExceptionVector = 0x80000080;

    public static SstOutcome Run(SstCase c) {
        Address pc = new(c.Address & 0x0FFFFFFF);
        if(!MemoryMap.Text.Contains(pc, 4)) {
            return new SstSkipped("番地を移すとテキストセグメントの外");
        }
        uint delta = pc.Addr - c.Address;
        if(R3000Decoder.Decode(c.Opcode, pc) is not Decoded decoded) {
            return new SstSkipped("PlusPimにない命令");
        }

        Debuggee d = Debuggee.Load(ProgramImage.Create(pc, [(pc, [decoded.Instruction])], []), DelaySlotsConfig.All);
        State s = d.State;
        s.Core = new Core();
        ref Registers regs = ref s.Core.Registers;
        for(int i = 0; i < 32; i++) {
            regs[(GprId)i] = c.Initial.Gpr[i];
        }
        regs[RName.Hi] = c.Initial.Hi;
        regs[RName.Lo] = c.Initial.Lo;
        regs.Pc = pc;
        if(c.Initial.Load is (int reg, uint value)) {
            s.Core.Load = new DelayedLoad((GprId)reg, value);
        }
        // 成立した分岐の移り先は命令の番地によらないので移さない
        SstBranch slot = c.Initial.Branch;
        if(slot.Slot) {
            s.Core.Branch = slot.Taken ? new Branch(pc - 4, new Address(slot.Target), Transfer.Jump) : new Branch(pc - 4, pc + 4, Transfer.Fallthrough);
        }
        foreach(SstCycle y in c.Cycles.Where(y => y.Access == SstAccess.Read)) {
            for(int i = 0; i < y.Size; i++) {
                s.Memory.Write(new Address(y.Address) + (uint)i, (byte)(y.Value >> (8 * i)));
            }
        }
        s.Memory.Commit();

        _ = d.Step(new ArrayBufferWriter<byte>());
        Core core = s.Core;
        if(core.Status.Value is RuntimeErrorRaised { Error.Value: RuntimeErrorKind k }) {
            return k switch {
                RuntimeErrorKind.DivisionByZero => new SstKnown("0除算. PlusPimはランタイムエラーにする"),
                RuntimeErrorKind.TextAccess => new SstKnown("テキストのアドレスへのロード, ストア. PlusPimはランタイムエラーにする"),
                RuntimeErrorKind.TransferInDelaySlot => new SstKnown("分岐遅延スロットの分岐. PlusPimはランタイムエラーにする"),
                _ => new SstMismatch($"runtime error {k}"),
            };
        }

        List<string> diff = [];
        SstState f = c.Final;
        // 戻りアドレスは移した番地から決まる
        uint link = c.Address + 8;
        // R3000は完了していないロードと同じレジスタへのロードで, 先のロードを取り消す
        int? cancelled = (c.Opcode >> 29) == 0b100 && c.Initial.Load is (int pending, _) && pending == (int)((c.Opcode >> 16) & 31) ? pending : null;
        bool known = false;
        for(int i = 1; i < 32; i++) {
            uint expected = f.Gpr[i] == link ? link + delta : f.Gpr[i];
            if(i == cancelled && expected != regs[(GprId)i] && regs[(GprId)i] == c.Initial.Load!.Value.Value) {
                known = true;
                continue;
            }
            Compare(diff, $"r{i}", expected, regs[(GprId)i]);
        }
        Compare(diff, "hi", f.Hi, regs[RName.Hi]);
        Compare(diff, "lo", f.Lo, regs[RName.Lo]);

        if(f.Pc == PsxExceptionVector) {
            uint code = (f.Cause >> 2) & 0x1F;
            if(core.Status.Value is ExceptionRaised e) {
                Compare(diff, "ExcCode", code, (uint)e.Code);
                Compare(diff, "EPC", f.Epc + delta, e.Epc.Addr);
                Compare(diff, "BD", f.Cause & Cp0.Bd, regs[RName.Cause] & Cp0.Bd);
            } else {
                diff.Add($"exception: expected ExcCode {code}, actual {core.Status.Value}");
            }
        } else {
            if(core.Status.Value is ExceptionRaised e) {
                diff.Add($"exception: expected none, actual {e.Code}");
            }
            Compare(diff, "pc", f.Pc + (slot is { Slot: true, Taken: true } ? 0 : delta), regs.Pc.Addr);
            CompareBranch(diff, f.Branch, core.Branch, decoded.PcRelative ? delta : 0);
        }

        // $zeroへのロードは書き込まれないので, ないものとみなす
        (int, uint)? expectedLoad = f.Load is (int r, _) && r != 0 ? f.Load : null;
        (int, uint)? actualLoad = core.Load is DelayedLoad l && l.Reg != GprId.Zero ? ((int)l.Reg, l.Value) : null;
        if(expectedLoad != actualLoad) {
            diff.Add($"load: expected {expectedLoad}, actual {actualLoad}");
        }

        int written = 0;
        foreach(SstCycle y in c.Cycles.Where(y => y.Access == SstAccess.Write)) {
            for(int i = 0; i < y.Size; i++) {
                Compare(diff, $"mem[{y.Address + (uint)i:X8}]", (byte)(y.Value >> (8 * i)), s.Memory.Read(new Address(y.Address) + (uint)i));
            }
            written += y.Size;
        }
        Compare(diff, "bytes written", (uint)written, (uint)s.Memory.Journal);

        return diff.Count != 0 ? new SstMismatch(string.Join(", ", diff))
            : known ? new SstKnown("差分: 完了していないロードと同じレジスタへのロードで, PlusPimは先のロードを書き込む. R3000は取り消す")
            : new SstPassed();
    }

    private static void CompareBranch(List<string> diff, SstBranch expected, Branch? actual, uint delta) {
        string? e = !expected.Slot ? null : expected.Taken ? $"taken {expected.Target + delta:X8}" : "not taken";
        string? a = actual is not Branch b ? null : b.Transfer == Transfer.Fallthrough ? "not taken" : $"taken {b.Target.Addr:X8}";
        if(e != a) {
            diff.Add($"branch: expected {e ?? "none"}, actual {a ?? "none"}");
        }
    }

    private static void Compare(List<string> diff, string name, uint expected, uint actual) {
        if(expected != actual) {
            diff.Add($"{name}: expected {expected:X8}, actual {actual:X8}");
        }
    }
}
