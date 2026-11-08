namespace PlusPim.Instructions.Formats;

/// <summary>
/// 2つのレジスタを比べる条件分岐を抽象化する
/// </summary>
public interface IBranchOp {
    static abstract string Mnemonic { get; }
    static abstract bool Taken(uint rs, uint rt);
}

/// <summary>
/// 1つのレジスタをゼロと符号付きで比べる条件分岐を抽象化する
/// </summary>
public interface IBranchZOp {
    static abstract string Mnemonic { get; }
    static abstract bool Taken(int rs);
}

/// <summary>
/// 2つのレジスタを比べる条件分岐の共通部分を提供する
/// </summary>
/// <typeparam name="TOp">条件</typeparam>
/// <param name="rs">rs レジスタ番号</param>
/// <param name="rt">rt レジスタ番号</param>
/// <param name="target">成立時の分岐先</param>
/// <param name="sourceLine">行番号</param>
public sealed class BranchOp<TOp>(GprId rs, GprId rt, Address target, int sourceLine): IAssemblerable, IExecute
    where TOp : struct, IBranchOp {

    public static string Mnemonic => TOp.Mnemonic;
    private readonly RegIds2 _regs = RegIds2.Of(rs, rt);

    public ReadOnlySpan<RegisterName> Src => this._regs;
    public ReadOnlySpan<RegisterName> Dest => [];
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands is [GprId s, GprId t, Address a] ? new BranchOp<TOp>(s, t, a, sourceLine) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return TOp.Taken(src[0], src[1]) ? new Effect(new Jump(target)) : new Effect(new Next());
    }
}

/// <summary>
/// ゼロと比べる条件分岐の共通部分を提供する
/// </summary>
/// <typeparam name="TOp">条件</typeparam>
/// <param name="rs">rs レジスタ番号</param>
/// <param name="target">成立時の分岐先</param>
/// <param name="sourceLine">行番号</param>
public sealed class BranchZOp<TOp>(GprId rs, Address target, int sourceLine): IAssemblerable, IExecute
    where TOp : struct, IBranchZOp {

    public static string Mnemonic => TOp.Mnemonic;
    private readonly RegisterName _rs = rs;

    public ReadOnlySpan<RegisterName> Src => new(in this._rs);
    public ReadOnlySpan<RegisterName> Dest => [];
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands is [GprId s, Address a] ? new BranchZOp<TOp>(s, a, sourceLine) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return TOp.Taken(unchecked((int)src[0])) ? new Effect(new Jump(target)) : new Effect(new Next());
    }
}
