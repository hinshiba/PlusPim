using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

/// <summary>
/// 汎用レジスタの値をCP0レジスタへコピーする. カーネルモードでのみ実行できる
/// </summary>
/// <param name="rt">読み込むレジスタ番号</param>
/// <param name="rd">書き込むCP0レジスタ</param>
public sealed class Mtc0(GprId rt, RName rd): IAssemblerable, IExecute {
    public static string Mnemonic => "mtc0";
    private readonly RegIds3 _regs = RegIds3.Of(rd, RName.Status, rt);

    public ReadOnlySpan<RegisterName> Src => this._regs[1..];
    public ReadOnlySpan<RegisterName> Dest => this._regs[..1];

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands is [GprId t, GprId n] && Cp0.FromNumber(n) is RName d ? new Mtc0(t, d) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        if(!Cp0.IsKernel(src[0])) {
            return new Effect(new Raise(ExcCode.CpU));
        }
        dest[0] = src[1];
        return new Effect(new Next());
    }
}
