using System.Runtime.CompilerServices;

namespace PlusPim.Instructions.Formats;

/// <summary>
/// R形式命令の2項演算を抽象化する
/// </summary>
public interface IRBinOp {
    static abstract string Mnemonic { get; }
    static abstract Effect Apply(uint lhs, uint rhs, ref uint result);
}

/// <summary>
/// R形式命令の2項演算の共通部分を提供する
/// </summary>
/// <typeparam name="TOp">R形式命令の2項演算</typeparam>
/// <param name="rd">rd レジスタ番号</param>
/// <param name="rs">rs レジスタ番号</param>
/// <param name="rt">rt レジスタ番号</param>
/// <param name="sourceLine">行番号</param>
public sealed class RBinOp<TOp>(GprId rd, GprId rs, GprId rt, int sourceLine): IAssemblerable, IExecute
    where TOp : struct, IRBinOp {

    public static string Mnemonic => TOp.Mnemonic;
    private readonly RegIds3 _regs = RegIds3.Of(rd, rs, rt);

    public ReadOnlySpan<RegisterName> Src => this._regs[1..];
    public ReadOnlySpan<RegisterName> Dest => this._regs[..1];
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands is [GprId d, GprId s, GprId t] ? new RBinOp<TOp>(d, s, t, sourceLine) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return TOp.Apply(src[0], src[1], ref dest[0]);
    }
}

[InlineArray(3)]
internal struct RegIds3 {
    private RegisterName _e0;

    /// <summary>平易な初期化のための便利関数. コレクション式が使えないため.</summary>
    public static RegIds3 Of(RegisterName a, RegisterName b, RegisterName c) {
        RegIds3 r = default;
        r[0] = a;
        r[1] = b;
        r[2] = c;
        return r;
    }
}
