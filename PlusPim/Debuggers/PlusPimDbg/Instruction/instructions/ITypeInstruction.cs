using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions;

/// <summary>
/// MIPSにおいてI形式の命令のほとんどを表すクラス
/// </summary>
/// <remarks>メモリアクセス命令, ブランチ命令, トラップ命令を含まない</remarks>
internal sealed class ITypeInstruction(
    RegisterID rt, RegisterID rs, Immediate imm, int sourceLine,
    string mnemonic, Func<uint, Immediate, uint> compute
): IInstruction {

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = sourceLine;

    // 逆操作のためのRtの以前の値
    // ループ内では複数回書き込まれる可能性があるためスタックで管理
    private readonly Stack<uint> _prevRtValues = new();

    public void Execute(RuntimeContext context) {
        uint rsVal = context.Registers[rs];
        uint result = compute(rsVal, imm);
        this.WriteRt(context, result);
        context.Log($"{mnemonic} ${rt}, ${rs}, {imm}: 0x{rsVal:X8}, {imm} => 0x{result:X8}");
    }

    /// <summary>
    /// 命令の逆操作だが，ほとんどのI形式命令ではRtに書き込んだ値を元に戻すだけで良い
    /// </summary>
    public void Undo(RuntimeContext context) {
        if(rt == RegisterID.Zero) {
            return;
        }
        if(this._prevRtValues.Count == 0) {
            throw new InvalidOperationException("No previous value to undo.");
        }
        context.Registers[rt] = this._prevRtValues.Pop();
    }

    /// <summary>
    /// コンテキスト内のTargetレジスタに値を書き込むと同時に，逆操作のために以前の値を保存する
    /// </summary>
    /// <remarks>
    /// これを呼び出すと逆操作のためにTargetレジスタの値は保存される
    /// </remarks>
    /// <param name="context">レジスタを含むコンテキスト</param>
    /// <param name="value">書き込む値</param>
    private void WriteRt(RuntimeContext context, uint value) {
        // $zero保護
        if(rt == RegisterID.Zero) {
            return;
        }
        // 逆操作のために保存
        this._prevRtValues.Push(context.Registers[rt]);
        context.Registers[rt] = value;
    }

    /// <summary>
    /// 標準I形式命令のパーサーを生成するファクトリ (addiu, andi, ori, xori, slti, sltiu)
    /// </summary>
    internal static Func<string, IInstructionParser> CreateParser(Func<uint, Immediate, uint> compute) {
        return mnemonic => new Factories.FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return OperandParser.TryParseITypeOperands(operands, out RegisterID rt, out RegisterID rs, out Immediate? imm)
                ? new ITypeInstruction(rt, rs, imm, lineIndex, mnemonic, compute)
                : (IInstruction?)null;
        });
    }

    /// <summary>
    /// レジスタ+即値のみのI形式命令のパーサーを生成するファクトリ (lui)
    /// </summary>
    internal static Func<string, IInstructionParser> CreateRegImmParser(Func<Immediate, uint> compute) {
        return mnemonic => new Factories.FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return OperandParser.TryParseRegImmOperands(operands, out RegisterID rt, out Immediate? imm)
                ? new ITypeInstruction(rt, RegisterID.Zero, imm, lineIndex, mnemonic,
                    (_, immVal) => compute(immVal))
                : (IInstruction?)null;
        });
    }
}
