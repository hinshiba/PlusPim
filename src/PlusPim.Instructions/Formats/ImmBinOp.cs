using System.Runtime.CompilerServices;

namespace PlusPim.Instructions.Formats;

/// <summary>
/// レジスタと即値の2項演算を抽象化する
/// </summary>
public interface IImmBinOp {
    static abstract string Mnemonic { get; }

    /// <summary>即値を rhs となる32bit値へ拡張する. 命令が受け付けない即値なら <see langword="null"/></summary>
    static abstract uint? Extend(Immediate imm);

    static abstract Effect Apply(uint lhs, uint rhs, ref uint result);
}

/// <summary>
/// レジスタと即値の2項演算の共通部分を提供する
/// </summary>
/// <typeparam name="TOp">レジスタと即値の2項演算</typeparam>
/// <param name="dest">書き込むレジスタ番号</param>
/// <param name="src">読み込むレジスタ番号</param>
/// <param name="rhs">拡張済みの即値</param>
/// <param name="sourceLine">行番号</param>
public sealed class ImmBinOp<TOp>(GprId dest, GprId src, uint rhs, int sourceLine): IAssemblerable, IExecute
    where TOp : struct, IImmBinOp {

    public static string Mnemonic => TOp.Mnemonic;
    private readonly RegIds2 _regs = RegIds2.Of(dest, src);

    public ReadOnlySpan<RegisterName> Src => this._regs[1..];
    public ReadOnlySpan<RegisterName> Dest => this._regs[..1];
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands is [GprId d, GprId s, Immediate i] && TOp.Extend(i) is uint rhs
            ? new ImmBinOp<TOp>(d, s, rhs, sourceLine)
            : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return TOp.Apply(src[0], rhs, ref dest[0]);
    }
}

[InlineArray(2)]
internal struct RegIds2 {
    private RegisterName _e0;

    /// <summary>平易な初期化のための便利関数. コレクション式が使えないため.</summary>
    public static RegIds2 Of(RegisterName a, RegisterName b) {
        RegIds2 r = default;
        r[0] = a;
        r[1] = b;
        return r;
    }
}
