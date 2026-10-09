using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions;

/// <summary>
/// MIPSにおいてI形式の命令のほとんどを表すクラス
/// </summary>
/// <remarks>
/// メモリアクセス命令, ブランチ命令, トラップ命令を含まない．
/// ラムダ内で <c>checked</c> を使えば算術オーバーフロー時に
/// <see cref="OverflowException"/> が発生し，MIPS例外 <see cref="ExcCode.Ov"/> として処理される．
/// </remarks>
internal sealed class ITypeInstruction(
    RegisterID rt, RegisterID rs, Immediate imm, int sourceLine,
    string mnemonic, Func<uint, Immediate, uint> compute
): IInstruction {

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = sourceLine;

    private readonly RegisterWriteHistory _rt = new(rt);

    public ExecuteResult Execute(RuntimeContext context) {
        uint rsVal = context.Registers[rs];
        uint result;
        try {
            result = compute(rsVal, imm);
        } catch(OverflowException) {
            // Rtは変更せず，Undo用の情報も積まない
            return ExecuteResult.Raise(ExcCode.Ov);
        }
        this._rt.Write(context, result);
        context.Log($"{mnemonic} ${rt}, ${rs}, {imm}: 0x{rsVal:X8}, {imm} => 0x{result:X8}");
        return ExecuteResult.Next;
    }

    /// <summary>
    /// 命令の逆操作だが，ほとんどのI形式命令ではRtに書き込んだ値を元に戻すだけで良い
    /// </summary>
    public void Undo(RuntimeContext context) {
        this._rt.Undo(context);
    }

    /// <summary>
    /// 標準I形式命令のパーサーを生成するファクトリ (addiu, andi, ori, xori, slti, sltiu)
    /// </summary>
    internal static Func<string, IInstructionParser> CreateParser(Func<uint, Immediate, uint> compute) {
        return mnemonic => new Factories.FuncInstructionParser(mnemonic, (operands, lineNumber) => {
            return OperandParser.TryParseITypeOperands(operands, out RegisterID rt, out RegisterID rs, out Immediate? imm)
                ? new ITypeInstruction(rt, rs, imm, lineNumber, mnemonic, compute)
                : (IInstruction?)null;
        });
    }

    /// <summary>
    /// レジスタ+即値のみのI形式命令のパーサーを生成するファクトリ (lui)
    /// </summary>
    internal static Func<string, IInstructionParser> CreateRegImmParser(Func<Immediate, uint> compute) {
        return mnemonic => new Factories.FuncInstructionParser(mnemonic, (operands, lineNumber) => {
            return OperandParser.TryParseRegImmOperands(operands, out RegisterID rt, out Immediate? imm)
                ? new ITypeInstruction(rt, RegisterID.Zero, imm, lineNumber, mnemonic,
                    (_, immVal) => compute(immVal))
                : (IInstruction?)null;
        });
    }
}
