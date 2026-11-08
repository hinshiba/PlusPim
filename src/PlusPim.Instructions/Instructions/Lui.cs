namespace PlusPim.Instructions.Instructions;

/// <summary>
/// lui: 即値を上位16bitへ置き, 下位16bitを0とする
/// </summary>
/// <param name="rt">rt レジスタ番号</param>
/// <param name="value">シフト済みの書き込む値</param>
/// <param name="sourceLine">行番号</param>
public sealed class Lui(GprId rt, uint value, int sourceLine): IAssemblerable, IExecute {
    public static string Mnemonic => "lui";
    private readonly RegisterName _rt = rt;

    public ReadOnlySpan<RegisterName> Src => [];
    public ReadOnlySpan<RegisterName> Dest => new(in this._rt);
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands is [GprId t, Immediate i] ? new Lui(t, i.ZeroExtended << 16, sourceLine) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        dest[0] = value;
        return new Effect(new Next());
    }
}
