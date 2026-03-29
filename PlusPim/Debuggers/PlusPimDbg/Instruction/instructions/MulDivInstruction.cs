using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;

/// <summary>
/// MIPSにおいて乗除算の命令を表すクラス
/// </summary>
internal sealed class MulDivInstruction(
    RegisterID rs, RegisterID rt, int sourceLine,
    string mnemonic, Func<uint, uint, (uint hi, uint lo)> compute
): IInstruction {

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = sourceLine;

    // 逆操作のためのHiLoレジスタの以前の値
    // ループ内では複数回書き込まれる可能性があるためスタックで管理
    // (Hi, Lo)である．
    private readonly Stack<(uint, uint)> _prevHiLoValues = new();

    public void Execute(RuntimeContext context) {
        uint rsVal = context.Registers[rs];
        uint rtVal = context.Registers[rt];
        (uint hi, uint lo) = compute(rsVal, rtVal);
        this.WriteHiLo(context, hi, lo);
        context.Log($"{mnemonic} ${rs}, ${rt}: 0x{rsVal:X8}, 0x{rtVal:X8} => HI=0x{hi:X8}, LO=0x{lo:X8}");
    }

    /// <summary>
    /// 命令の逆操作だが，乗除算命令ではHi Loに書き込んだ値を元に戻すだけで良い
    /// </summary>
    public void Undo(RuntimeContext context) {
        (uint prevHi, uint prevLo) = this._prevHiLoValues.Pop();
        context.HI = prevHi;
        context.LO = prevLo;
    }

    /// <summary>
    /// コンテキスト内のHI/LOレジスタに値を書き込むと同時に，逆操作のために以前の値を保存する
    /// </summary>
    private void WriteHiLo(RuntimeContext context, uint hi, uint lo) {
        this._prevHiLoValues.Push((context.HI, context.LO));
        context.HI = hi;
        context.LO = lo;
    }

    /// <summary>
    /// 乗除算命令のパーサーを生成するファクトリ (mult, div)
    /// </summary>
    internal static Func<string, IInstructionParser> CreateParser(Func<uint, uint, (uint hi, uint lo)> compute) {
        return mnemonic => new Factories.FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return OperandParser.TryParse2RegOperands(operands, out RegisterID rs, out RegisterID rt)
                ? new MulDivInstruction(rs, rt, lineIndex, mnemonic, compute)
                : (IInstruction?)null;
        });
    }
}
