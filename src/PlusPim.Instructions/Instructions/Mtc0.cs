using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

/// <summary>
/// 汎用レジスタの値をCP0レジスタへコピーする
/// </summary>
/// <param name="rt">読み込むレジスタ番号</param>
/// <param name="rd">書き込むCP0レジスタ</param>
/// <remarks>特権検査は <see cref="Privileged{TInst}"/> が行う</remarks>
internal sealed class Mtc0(GprId rt, RName rd): IAssemblerable, IExecute {
    public static string Mnemonic => "mtc0";
    private readonly RegIds2 _regs = RegIds2.Of(rt, rd);

    public ReadOnlySpan<RegisterName> Src => this._regs[..1];
    public ReadOnlySpan<RegisterName> Dest => this._regs[1..];

    public string Text => Syntax.Of(Mnemonic, this._regs);

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands is [GprId t, GprId n] && Cp0.FromNumber(n) is RName d ? new Mtc0(t, d) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        dest[0] = src[0];
        return new Effect(new Next());
    }
}
