namespace PlusPim.Instructions.Instructions;

/// <summary>
/// break: ブレークポイント例外を発生させる
/// </summary>
public sealed class Break: IAssemblerable, IExecute {
    public static string Mnemonic => "break";

    public ReadOnlySpan<RegisterName> Src => [];
    public ReadOnlySpan<RegisterName> Dest => [];

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands.IsEmpty ? new Break() : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return new Effect(new Raise(ExcCode.Bp));
    }
}
