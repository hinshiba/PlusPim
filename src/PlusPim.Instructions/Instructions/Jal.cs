namespace PlusPim.Instructions.Instructions;

/// <summary>
/// 次の命令のアドレスを$raに保存してジャンプする
/// </summary>
/// <param name="target">呼び出し先</param>
/// <param name="sourceLine">行番号</param>
/// <remarks>遅延スロットを持たないとしたときの戻りアドレスである</remarks>
public sealed class Jal(Address target, int sourceLine): IAssemblerable, IExecute {
    public static string Mnemonic => "jal";
    private static readonly RegisterName Pc = RName.Pc;
    private static readonly RegisterName Ra = GprId.Ra;

    public ReadOnlySpan<RegisterName> Src => new(in Pc);
    public ReadOnlySpan<RegisterName> Dest => new(in Ra);
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands is [Address a] ? new Jal(a, sourceLine) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        dest[0] = unchecked(src[0] + 4);
        return new Effect(new Call(target));
    }
}
