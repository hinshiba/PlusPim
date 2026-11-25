using PlusPim.Instructions.Formats;

namespace PlusPim.Instructions.Instructions;

/// <summary>
/// 戻りアドレスをrdに保存して, レジスタの値をアドレスとしてジャンプする
/// </summary>
/// <param name="rd">戻りアドレスを書くレジスタ番号. 省略時は$ra</param>
/// <param name="rs">呼び出し先を持つレジスタ番号</param>
/// <remarks>遅延スロットの有無が必要であるため戻りアドレスはランタイムが書き込む</remarks>
public sealed class Jalr(GprId rd, GprId rs): IAssemblerable, IExecute {
    public static string Mnemonic => "jalr";
    private readonly RegIds2 _regs = RegIds2.Of(rd, rs);

    public ReadOnlySpan<RegisterName> Src => this._regs[1..];
    public ReadOnlySpan<RegisterName> Dest => this._regs[..1];

    public string Text => Syntax.Of(Mnemonic, this._regs[0].AsmName(), this._regs[1].AsmName());

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands) {
        return operands switch {
            [GprId s] => new Jalr(GprId.Ra, s),
            [GprId d, GprId s] => new Jalr(d, s),
            _ => null,
        };
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        return new Effect(new Call(new Address(src[0])));
    }
}
