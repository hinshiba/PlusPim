using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

/// <summary>
/// EXLをクリアしてEPCへ復帰する
/// </summary>
/// <remarks>特権検査は <see cref="Privileged{TInst}"/> が行う</remarks>
internal sealed class Eret: IAssemblerable, IExecute {
    public static string Mnemonic => "eret";
    private static readonly RegIds2 Regs = RegIds2.Of(RName.Status, RName.Epc);

    public ReadOnlySpan<RegisterName> Src => Regs;
    public ReadOnlySpan<RegisterName> Dest => Regs[..1];

    public string Text => Mnemonic;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands.IsEmpty ? new Eret() : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        dest[0] = src[0] & ~Cp0.Exl;
        return new Effect(new ExceptionReturn(new Address(src[1])));
    }
}
