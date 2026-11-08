using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

/// <summary>
/// EXLをクリアしてEPCへ復帰する. カーネルモードでのみ実行できる
/// </summary>
/// <param name="sourceLine">行番号</param>
public sealed class Eret(int sourceLine): IAssemblerable, IExecute {
    public static string Mnemonic => "eret";
    private static readonly RegIds3 Regs = RegIds3.Of(RName.Status, RName.Status, RName.Epc);

    public ReadOnlySpan<RegisterName> Src => Regs[1..];
    public ReadOnlySpan<RegisterName> Dest => Regs[..1];
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands.IsEmpty ? new Eret(sourceLine) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        if(!Cp0.IsKernel(src[0])) {
            return new Effect(new Raise(ExcCode.CpU));
        }
        dest[0] = src[0] & ~Cp0.Exl;
        return new Effect(new ExceptionReturn(new Address(src[1])));
    }
}
