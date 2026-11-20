namespace PlusPim.Instructions.Formats;

/// <summary>
/// 汎用でないレジスタとの値の転送を抽象化する
/// </summary>
public interface IMoveOp {
    static abstract string Mnemonic { get; }

    /// <summary>オペランドから書き込むレジスタを決める</summary>
    static abstract RegisterName Dest(GprId r);

    /// <summary>オペランドから読み込むレジスタを決める</summary>
    static abstract RegisterName Src(GprId r);
}

/// <summary>
/// 転送の共通部分を提供する
/// </summary>
/// <typeparam name="TOp">転送</typeparam>
/// <param name="r">オペランドのレジスタ番号</param>
public sealed class MoveOp<TOp>(GprId r): IAssemblerable, IExecute
    where TOp : struct, IMoveOp {

    public static string Mnemonic => TOp.Mnemonic;
    private readonly RegIds2 _regs = RegIds2.Of(TOp.Dest(r), TOp.Src(r));

    public ReadOnlySpan<RegisterName> Src => this._regs[1..];
    public ReadOnlySpan<RegisterName> Dest => this._regs[..1];

    public string Text => Syntax.Of(Mnemonic, this._regs[0].Value is GprId ? this.Dest : this.Src);

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands is [GprId r] ? new MoveOp<TOp>(r) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        dest[0] = src[0];
        return new Effect(new Next());
    }
}
