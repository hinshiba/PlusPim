using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;

/// <summary>
/// MIPSにおいてLo Hiレジスタ間の転送を行う命令のクラス
/// </summary>
internal sealed class LoHiRegisterInstruction(RegisterID reg, bool isHi, bool isFrom, int sourceLine): IInstruction {

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = sourceLine;

    // 逆操作のためのレジスタの以前の値
    // ループ内では複数回書き込まれる可能性があるためスタックで管理
    private readonly Stack<uint> _prevRegValues = new();

    public void Execute(RuntimeContext context) {
        if(isFrom) {
            this.ExecuteFrom(context);
        } else {
            this.ExecuteTo(context);
        }
    }

    private void ExecuteFrom(RuntimeContext context) {
        // まず書き込まれる汎用レジスタを保存
        this._prevRegValues.Push(context.Registers[reg]);
        // 書き込みを実施
        context.Registers[reg] = isHi ? context.HI : context.LO;
    }

    private void ExecuteTo(RuntimeContext context) {
        if(isHi) {
            // まず書き込まれるHiレジスタを保存
            this._prevRegValues.Push(context.HI);
            // 書き込みを実施
            context.HI = context.Registers[reg];
        } else {
            this._prevRegValues.Push(context.LO);

            context.LO = context.Registers[reg];
        }
    }

    /// <summary>
    /// 命令の逆操作
    /// </summary>
    public void Undo(RuntimeContext context) {
        if(isFrom) {
            context.Registers[reg] = this._prevRegValues.Pop();
        } else {
            if(isHi) {
                context.HI = this._prevRegValues.Pop();
            } else {
                context.LO = this._prevRegValues.Pop();
            }
        }
    }


    /// <summary>
    /// Lo Hiレジスタ間の転送を行う命令のパーサーを生成するファクトリ
    /// </summary>
    /// <param name="isHi">Hiレジスタへの読み書きかどうか．<see langword="false"/>の場合はLoレジスタが選択される</param>
    /// <param name="isFrom"><see langword="true"/>の場合はHi/Loレジスタからの転送となる </param>
    /// <returns></returns>
    internal static Func<string, IInstructionParser> CreateParser(bool isHi, bool isFrom) {
        return mnemonic => new Factories.FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return OperandParser.TryParseSingleRegOperand(operands, out RegisterID reg)
                ? new LoHiRegisterInstruction(reg, isHi, isFrom, lineIndex)
                : (IInstruction?)null;
        });
    }
}
