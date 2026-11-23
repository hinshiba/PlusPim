namespace PlusPim.Instructions.Formats;

/// <summary>
/// 2つの値を比べて成立すればトラップ例外を発生させる条件を抽象化する
/// </summary>
public interface ITrapOp {
    /// <summary>2つのレジスタを比べる形式のニーモニック</summary>
    static abstract string Mnemonic { get; }

    /// <summary>レジスタと即値を比べる形式のニーモニック</summary>
    static abstract string ImmMnemonic { get; }

    static abstract bool Taken(uint rs, uint rt);
}

/// <summary>
/// 2つのレジスタを比べるトラップの共通部分を提供する
/// </summary>
/// <typeparam name="TOp">条件</typeparam>
/// <param name="rs">rsレジスタ番号</param>
/// <param name="rt">rtレジスタ番号</param>
public sealed class TrapOp<TOp>(GprId rs, GprId rt): IAssemblerable, IExecute
    where TOp : struct, ITrapOp {

    public static string Mnemonic => TOp.Mnemonic;
    private readonly RegIds2 _regs = RegIds2.Of(rs, rt);

    public ReadOnlySpan<RegisterName> Src => this._regs;
    public ReadOnlySpan<RegisterName> Dest => [];

    public string Text => Syntax.Of(Mnemonic, this._regs);

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands is [GprId s, GprId t] ? new TrapOp<TOp>(s, t) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return TOp.Taken(src[0], src[1]) ? new Effect(new Raise(ExcCode.Tr)) : new Effect(new Next());
    }
}

/// <summary>
/// レジスタと即値を比べるトラップの共通部分を提供する
/// </summary>
/// <typeparam name="TOp">条件</typeparam>
/// <param name="rs">rsレジスタ番号</param>
/// <param name="imm">符号拡張済みの即値</param>
/// <remarks>MIPSの仕様通り, 符号なしで比べる条件でも即値は符号拡張する</remarks>
public sealed class TrapImmOp<TOp>(GprId rs, uint imm): IAssemblerable, IExecute
    where TOp : struct, ITrapOp {

    public static string Mnemonic => TOp.ImmMnemonic;
    private readonly RegisterName _rs = rs;

    public ReadOnlySpan<RegisterName> Src => new(in this._rs);
    public ReadOnlySpan<RegisterName> Dest => [];

    public string Text => Syntax.Of(Mnemonic, this._rs.AsmName(), ImmKind.Signed.Format(imm));

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands is [GprId s, Immediate i] ? new TrapImmOp<TOp>(s, i.SignExtended) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return TOp.Taken(src[0], imm) ? new Effect(new Raise(ExcCode.Tr)) : new Effect(new Next());
    }
}
