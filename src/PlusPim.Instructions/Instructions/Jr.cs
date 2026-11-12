namespace PlusPim.Instructions.Instructions;

/// <summary>
/// レジスタの値へジャンプする
/// </summary>
/// <param name="rs">ジャンプ先を持つレジスタ番号</param>
public sealed class Jr(GprId rs): IAssemblerable, IExecute {
    public static string Mnemonic => "jr";
    private readonly RegisterName _rs = rs;

    public ReadOnlySpan<RegisterName> Src => new(in this._rs);
    public ReadOnlySpan<RegisterName> Dest => [];

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands is [GprId s] ? new Jr(s) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return new Effect(new Jump(new Address(src[0])));
    }
}
