namespace PlusPim.Instructions.Instructions;

/// <summary>
/// CP0レジスタの値を汎用レジスタへコピーする. カーネルモードでのみ実行できる
/// </summary>
/// <param name="rt">書き込むレジスタ番号</param>
/// <param name="rd">読み込むCP0レジスタ</param>
/// <param name="sourceLine">行番号</param>
public sealed class Mfc0(GprId rt, RName rd, int sourceLine): IAssemblerable, IExecute {
    public static string Mnemonic => "mfc0";
    private readonly RegIds3 _regs = RegIds3.Of(rt, RName.Status, rd);

    public ReadOnlySpan<RegisterName> Src => this._regs[1..];
    public ReadOnlySpan<RegisterName> Dest => this._regs[..1];
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands is [GprId t, GprId n] && Cp0.FromNumber(n) is RName d ? new Mfc0(t, d, sourceLine) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        if(!Cp0.IsKernel(src[0])) {
            return new Effect(new Raise(ExcCode.CpU));
        }
        dest[0] = src[1];
        return new Effect(new Next());
    }
}
