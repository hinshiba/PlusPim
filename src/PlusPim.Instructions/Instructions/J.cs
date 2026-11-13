namespace PlusPim.Instructions.Instructions;

/// <summary>
/// 無条件にジャンプする
/// </summary>
/// <param name="target">ジャンプ先</param>
public sealed class J(Address target): IAssemblerable, IExecute {
    public static string Mnemonic => "j";

    public ReadOnlySpan<RegisterName> Src => [];
    public ReadOnlySpan<RegisterName> Dest => [];

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands is [Address a] ? new J(a) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return new Effect(new Jump(target));
    }
}
