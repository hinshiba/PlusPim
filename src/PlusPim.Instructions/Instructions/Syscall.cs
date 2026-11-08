namespace PlusPim.Instructions.Instructions;

/// <summary>
/// syscall: システムコール例外を発生させる
/// </summary>
/// <param name="sourceLine">行番号</param>
public sealed class Syscall(int sourceLine): IAssemblerable, IExecute {
    public static string Mnemonic => "syscall";

    public ReadOnlySpan<RegisterId> Src => [];
    public ReadOnlySpan<RegisterId> Dest => [];
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands.IsEmpty ? new Syscall(sourceLine) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return new Effect(new Raise(ExcCode.Sys));
    }
}
