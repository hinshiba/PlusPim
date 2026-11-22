namespace PlusPim.Instructions.Formats;

/// <summary>
/// カーネルモードでのみ実行できる命令. ユーザーモードでは <see cref="ExcCode.CpU"/> を発生させる
/// </summary>
/// <typeparam name="TInst">特権検査を除いた命令</typeparam>
/// <remarks><see cref="Src"/> の先頭に Status を足し, 残りを <typeparamref name="TInst"/> へ渡す</remarks>
internal sealed class Privileged<TInst>: IAssemblerable, IExecute
    where TInst : IAssemblerable {

    public static string Mnemonic => TInst.Mnemonic;
    private readonly IExecute _inner;
    private readonly RegisterName[] _src;

    private Privileged(IExecute inner) {
        this._inner = inner;
        this._src = [RName.Status, .. inner.Src];
    }

    public ReadOnlySpan<RegisterName> Src => this._src;
    public ReadOnlySpan<RegisterName> Dest => this._inner.Dest;

    public string Text => this._inner.Text;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return TInst.FromOperands(operands) is IExecute inner ? new Privileged<TInst>(inner) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return Cp0.IsKernel(src[0]) ? this._inner.Eval(src[1..], dest) : new Effect(new Raise(ExcCode.CpU));
    }
}
