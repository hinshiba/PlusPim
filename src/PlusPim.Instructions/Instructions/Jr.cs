namespace PlusPim.Instructions.Instructions;

/// <summary>
/// レジスタの値へジャンプする
/// </summary>
/// <param name="rs">ジャンプ先を持つレジスタ番号</param>
/// <param name="sourceLine">行番号</param>
public sealed class Jr(GprId rs, int sourceLine): IAssemblerable, IExecute {
    public static string Mnemonic => "jr";
    private readonly RegisterName _rs = rs;

    public ReadOnlySpan<RegisterName> Src => new(in this._rs);
    public ReadOnlySpan<RegisterName> Dest => [];
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands is [GprId s] ? new Jr(s, sourceLine) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return new Effect(new Jump(new Address(src[0])));
    }
}
