using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

/// <summary>
/// $v0が示すの機能をランタイムへ要求する. カーネルモードでのみ実行できる
/// </summary>
/// <param name="sourceLine">行番号</param>
public sealed class RuntimeCall(int sourceLine): IAssemblerable, IExecute {
    public static string Mnemonic => "runtime_call!";
    private static readonly RegIds4 Regs = RegIds4.Of(RName.Status, GprId.V0, GprId.A0, GprId.A1);

    public ReadOnlySpan<RegisterName> Src => Regs;
    public ReadOnlySpan<RegisterName> Dest => Regs[1..2];
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands.IsEmpty ? new RuntimeCall(sourceLine) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        if(!Cp0.IsKernel(src[0])) {
            return new Effect(new Raise(ExcCode.CpU));
        }
        // byteへの切り詰めで未知の番号が既知の機能にならないように検査する
        if(src[1] > byte.MaxValue || !Enum.IsDefined((RuntimeRequestKind)src[1])) {
            return new Effect(new Fail(RuntimeErrorKind.UnknownRuntimeCall));
        }
        dest[0] = src[1];
        return new Effect(new RuntimeRequest((RuntimeRequestKind)src[1], src[2], src[3]));
    }
}
