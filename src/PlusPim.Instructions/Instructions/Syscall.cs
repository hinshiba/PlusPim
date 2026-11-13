namespace PlusPim.Instructions.Instructions;

/// <summary>
/// syscall: システムコール例外を発生させる
/// </summary>
public sealed class Syscall: IAssemblerable, IExecute {
    public static string Mnemonic => "syscall";

    public ReadOnlySpan<RegisterName> Src => [];
    public ReadOnlySpan<RegisterName> Dest => [];

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands.IsEmpty ? new Syscall() : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return new Effect(new Raise(ExcCode.Sys));
    }
}
