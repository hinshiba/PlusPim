namespace PlusPim.Instructions.Instructions;

/// <summary>
/// 無条件にジャンプする
/// </summary>
/// <param name="target">ジャンプ先</param>
/// <param name="sourceLine">行番号</param>
public sealed class J(Address target, int sourceLine): IAssemblerable, IExecute {
    public static string Mnemonic => "j";

    public ReadOnlySpan<RegisterName> Src => [];
    public ReadOnlySpan<RegisterName> Dest => [];
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands is [Address a] ? new J(a, sourceLine) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return new Effect(new Jump(target));
    }
}
