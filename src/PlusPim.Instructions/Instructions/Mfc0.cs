using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

/// <summary>
/// CP0レジスタの値を汎用レジスタへコピーする
/// </summary>
/// <param name="rt">書き込むレジスタ番号</param>
/// <param name="rd">読み込むCP0レジスタ</param>
/// <remarks>特権検査は <see cref="Privileged{TInst}"/> が行う</remarks>
internal sealed class Mfc0(GprId rt, RName rd): IAssemblerable, IExecute {
    public static string Mnemonic => "mfc0";
    private readonly RegIds2 _regs = RegIds2.Of(rt, rd);

    public ReadOnlySpan<RegisterName> Src => this._regs[1..];
    public ReadOnlySpan<RegisterName> Dest => this._regs[..1];

    public string Text => Syntax.Of(Mnemonic, this._regs);

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands is [GprId t, GprId n] && Cp0.FromNumber(n) is RName d ? new Mfc0(t, d) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        dest[0] = src[0];
        return new Effect(new Next());
    }
}
