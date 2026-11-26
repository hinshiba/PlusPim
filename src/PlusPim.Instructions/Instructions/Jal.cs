namespace PlusPim.Instructions.Instructions;

/// <summary>
/// 戻りアドレスを$raに保存してジャンプする
/// </summary>
/// <param name="target">呼び出し先</param>
/// <remarks>遅延スロットの有無が必要であるため戻りアドレスはランタイムが書き込む</remarks>
public sealed class Jal(Address target): IAssemblerable, IExecute {
    public static string Mnemonic => "jal";
    private static readonly RegisterName Ra = GprId.Ra;

    public ReadOnlySpan<RegisterName> Src => [];
    public ReadOnlySpan<RegisterName> Dest => new(in Ra);

    public string Text => Syntax.Of(Mnemonic, target.ToString());

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands is [Address a] ? new Jal(a) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return new Effect(new Call(target));
    }
}
