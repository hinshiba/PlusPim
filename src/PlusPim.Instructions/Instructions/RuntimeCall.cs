using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

/// <summary>
/// $v0が示す機能をランタイムへ要求する
/// </summary>
/// <remarks>特権検査は <see cref="Privileged{TInst}"/> が行う</remarks>
internal sealed class RuntimeCall: IAssemblerable, IExecute {
    public static string Mnemonic => "runtime_call!";
    private static readonly RegIds3 Regs = RegIds3.Of(GprId.V0, GprId.A0, GprId.A1);

    public ReadOnlySpan<RegisterName> Src => Regs;
    public ReadOnlySpan<RegisterName> Dest => Regs[..1];

    public string Text => Mnemonic;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands.IsEmpty ? new RuntimeCall() : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        // byteへの切り詰めで未知の番号が既知の機能にならないように検査する
        if(byte.MaxValue < src[0] || !Enum.IsDefined((RuntimeRequestKind)src[0])) {
            return new Effect(new RaiseRuntimeError(RuntimeErrorKind.UnknownRuntimeCall));
        }
        dest[0] = src[0];
        return new Effect(new RuntimeRequest((RuntimeRequestKind)src[0], src[1], src[2]));
    }
}
