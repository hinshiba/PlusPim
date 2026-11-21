using PlusPim.Instructions;
using PlusPim.Instructions.Formats;
using PlusPim.Instructions.Instructions;
using System.Collections.Frozen;
using System.Globalization;
using static PlusPim.Instructions.GprId;

namespace PlusPim.Asm;

/// <summary>
/// 疑似命令の一覧表
/// </summary>
/// <remarks>
/// 展開後の命令数はラベル参照のアドレスに依存してはならない. Layout で相対オフセットを確定させるため.
/// 疑似命令の追加は, オペランドの形を受け持つ形式関数に展開式を渡す表の1行で済ませる.
/// 展開で一時レジスタが要る場合は <c>$at</c> を使う.
/// </remarks>
internal static class Pseudos {
    /// <summary>
    /// 疑似命令のオペランドを命令の列へ展開する
    /// </summary>
    /// <param name="args">疑似命令のオペランド</param>
    /// <param name="r">誤りの報告先</param>
    /// <returns>オペランドの形が合わなければ <see langword="null"/>, 値の範囲の誤りを報告したなら空</returns>
    public delegate Template[]? Expand(ReadOnlySpan<Arg> args, Reporter r);

    /// <summary>ニーモニックから展開関数を引く</summary>
    public static FrozenDictionary<string, Expand> ByMnemonic { get; } = new Dictionary<string, Expand> {
        ["nop"] = Nullary(() => [IType<Sll>(Zero, Zero, 0)]),
        ["move"] = Regs2((rt, rs) => [RType<Addu>(rt, rs, Zero)]),
        ["clear"] = Regs1(rt => [RType<Addu>(rt, Zero, Zero)]),
        ["neg"] = Regs2((rd, rs) => [RType<Sub>(rd, Zero, rs)]),
        ["negu"] = Regs2((rd, rs) => [RType<Subu>(rd, Zero, rs)]),
        ["not"] = Regs2((rd, rs) => [RType<Nor>(rd, rs, Zero)]),

        ["b"] = Label(l => [Branch<Beq>(Zero, Zero, l)]),
        ["beqz"] = RegLabel((rs, l) => [Branch<Beq>(rs, Zero, l)]),
        ["bnez"] = RegLabel((rs, l) => [Branch<Bne>(rs, Zero, l)]),
        ["blt"] = CmpBranch<Slt, Bne>(swap: false),
        ["bgt"] = CmpBranch<Slt, Bne>(swap: true),
        ["ble"] = CmpBranch<Slt, Beq>(swap: true),
        ["bge"] = CmpBranch<Slt, Beq>(swap: false),
        ["bltu"] = CmpBranch<Sltu, Bne>(swap: false),
        ["bgtu"] = CmpBranch<Sltu, Bne>(swap: true),
        ["bleu"] = CmpBranch<Sltu, Beq>(swap: true),
        ["bgeu"] = CmpBranch<Sltu, Beq>(swap: false),

        ["sgt"] = Regs3((rd, rs, rt) => [RType<Slt>(rd, rt, rs)]),
        ["sgtu"] = Regs3((rd, rs, rt) => [RType<Sltu>(rd, rt, rs)]),
        ["sge"] = Regs3((rd, rs, rt) => [RType<Slt>(rd, rs, rt), IType<Xori>(rd, rd, 1)]),
        ["sgeu"] = Regs3((rd, rs, rt) => [RType<Sltu>(rd, rs, rt), IType<Xori>(rd, rd, 1)]),
        ["sle"] = Regs3((rd, rs, rt) => [RType<Slt>(rd, rt, rs), IType<Xori>(rd, rd, 1)]),
        ["sleu"] = Regs3((rd, rs, rt) => [RType<Sltu>(rd, rt, rs), IType<Xori>(rd, rd, 1)]),
        ["seq"] = Regs3((rd, rs, rt) => [RType<Xor>(rd, rs, rt), IType<Sltiu>(rd, rd, 1)]),
        ["sne"] = Regs3((rd, rs, rt) => [RType<Xor>(rd, rs, rt), RType<Sltu>(rd, Zero, rd)]),

        ["mul"] = Regs3((rd, rs, rt) => [HiLo<Mult>(rs, rt), Move<Mflo>(rd)]),
        ["rem"] = Regs3((rd, rs, rt) => [HiLo<Div>(rs, rt), Move<Mfhi>(rd)]),

        ["li"] = Li,
        ["la"] = La,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // 形式: オペランドの形を検査して展開式へ渡す

    private static Expand Nullary(Func<Template[]> body) {
        return (args, _) => args.IsEmpty ? body() : null;
    }

    private static Expand Regs1(Func<GprId, Template[]> body) {
        return (args, _) => args is [GprId a] ? body(a) : null;
    }

    private static Expand Regs2(Func<GprId, GprId, Template[]> body) {
        return (args, _) => args is [GprId a, GprId b] ? body(a, b) : null;
    }

    private static Expand Regs3(Func<GprId, GprId, GprId, Template[]> body) {
        return (args, _) => args is [GprId a, GprId b, GprId c] ? body(a, b, c) : null;
    }

    private static Expand Label(Func<LabelRef, Template[]> body) {
        return (args, _) => args is [LabelRef l] ? body(l) : null;
    }

    private static Expand RegLabel(Func<GprId, LabelRef, Template[]> body) {
        return (args, _) => args is [GprId a, LabelRef l] ? body(a, l) : null;
    }

    /// <summary>
    /// <c>TSet $at, a, b</c> で比べ, <c>TBr $at, $zero, label</c> で分岐する
    /// </summary>
    /// <param name="swap">比べる2つのレジスタを入れ替える</param>
    private static Expand CmpBranch<TSet, TBr>(bool swap) where TSet : struct, IRBinOp where TBr : struct, IBranchOp {
        return (args, _) => args is [GprId a, GprId b, LabelRef l]
            ? [RType<TSet>(At, swap ? b : a, swap ? a : b), Branch<TBr>(At, Zero, l)]
            : null;
    }

    // 実命令の Template を作る

    private static Template RType<T>(GprId rd, GprId rs, GprId rt) where T : struct, IRBinOp {
        return new(RBinOp<T>.FromOperands, [rd, rs, rt]);
    }

    private static Template IType<T>(GprId rt, GprId rs, ushort imm) where T : struct, IImmBinOp {
        return new(ImmBinOp<T>.FromOperands, [rt, rs, new Immediate(imm)]);
    }

    private static Template Branch<T>(GprId rs, GprId rt, LabelRef target) where T : struct, IBranchOp {
        return new(BranchOp<T>.FromOperands, [rs, rt, target]);
    }

    private static Template HiLo<T>(GprId rs, GprId rt) where T : struct, IHiLoOp {
        return new(HiLoOp<T>.FromOperands, [rs, rt]);
    }

    private static Template Move<T>(GprId r) where T : struct, IMoveOp {
        return new(MoveOp<T>.FromOperands, [r]);
    }

    // 形式に収まらない疑似命令

    /// <summary>
    /// 上位16bitが0なら <c>ori $rt, $zero, lo</c>, そうでなければ <c>lui $rt, hi</c> と <c>ori $rt, $rt, lo</c>
    /// </summary>
    private static Template[]? Li(ReadOnlySpan<Arg> args, Reporter r) {
        if(args is not [GprId rt, long v]) {
            return null;
        }
        if(v is < int.MinValue or > uint.MaxValue) {
            r.Report(ErrorCode.ValueOutOfRange, v.ToString(CultureInfo.InvariantCulture));
            return [];
        }
        uint bits = unchecked((uint)v);
        ushort lo = (ushort)bits;
        return bits >> 16 == 0
            ? [IType<Ori>(rt, Zero, lo)]
            : [new(Lui.FromOperands, [rt, new Immediate((ushort)(bits >> 16))]), IType<Ori>(rt, rt, lo)];
    }

    /// <summary>
    /// <c>lui $rt, hi(sym)</c> と <c>ori $rt, $rt, lo(sym)</c>. <c>ori</c> はゼロ拡張なので桁上がりの補正は要らない
    /// </summary>
    private static Template[]? La(ReadOnlySpan<Arg> args, Reporter r) {
        return args is [GprId rt, LabelRef s]
            ? [new(Lui.FromOperands, [rt, new LabelHi(s.Name)]), new(ImmBinOp<Ori>.FromOperands, [rt, rt, new LabelLo(s.Name)])]
            : null;
    }
}
