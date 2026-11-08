using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

/// <summary>
/// 次の命令のアドレスをrdに保存して, レジスタの値をアドレスとしてジャンプする
/// </summary>
/// <param name="rd">戻りアドレスを書くレジスタ番号. 省略時は$ra</param>
/// <param name="rs">呼び出し先を持つレジスタ番号</param>
/// <param name="sourceLine">行番号</param>
/// <remarks>遅延スロットを持たないとしたときの戻りアドレスである</remarks>
public sealed class Jalr(GprId rd, GprId rs, int sourceLine): IAssemblerable, IExecute {
    public static string Mnemonic => "jalr";
    private readonly RegIds3 _regs = RegIds3.Of(rd, RName.Pc, rs);

    public ReadOnlySpan<RegisterName> Src => this._regs[1..];
    public ReadOnlySpan<RegisterName> Dest => this._regs[..1];
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands switch {
            [GprId s] => new Jalr(GprId.Ra, s, sourceLine),
            [GprId d, GprId s] => new Jalr(d, s, sourceLine),
            _ => null,
        };
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        Address target = new(src[1]);
        dest[0] = unchecked(src[0] + 4);
        return new Effect(new Call(target));
    }
}
