using System.Runtime.CompilerServices;

namespace PlusPim.Instructions.Formats;

/// <summary>
/// 2つのレジスタからHIとLOを求める乗除算を抽象化する
/// </summary>
public interface IHiLoOp {
    static abstract string Mnemonic { get; }
    static abstract Effect Apply(uint lhs, uint rhs, ref uint hi, ref uint lo);
}

/// <summary>
/// 乗除算の共通部分を提供する
/// </summary>
/// <typeparam name="TOp">乗除算</typeparam>
/// <param name="rs">rs レジスタ番号</param>
/// <param name="rt">rt レジスタ番号</param>
public sealed class HiLoOp<TOp>(GprId rs, GprId rt): IAssemblerable, IExecute
    where TOp : struct, IHiLoOp {

    public static string Mnemonic => TOp.Mnemonic;
    private readonly RegIds4 _regs = RegIds4.Of(RName.Hi, RName.Lo, rs, rt);

    public ReadOnlySpan<RegisterName> Src => this._regs[2..];
    public ReadOnlySpan<RegisterName> Dest => this._regs[..2];

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands is [GprId s, GprId t] ? new HiLoOp<TOp>(s, t) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return TOp.Apply(src[0], src[1], ref dest[0], ref dest[1]);
    }
}

[InlineArray(4)]
internal struct RegIds4 {
    private RegisterName _e0;

    /// <summary>平易な初期化のための便利関数. コレクション式が使えないため.</summary>
    public static RegIds4 Of(RegisterName a, RegisterName b, RegisterName c, RegisterName d) {
        RegIds4 r = default;
        r[0] = a;
        r[1] = b;
        r[2] = c;
        r[3] = d;
        return r;
    }
}
