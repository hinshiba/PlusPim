using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions;

/// <summary>
/// MIPSにおいて乗除算の命令を表すクラス
/// </summary>
/// <remarks><c>check</c> は計算前に入力を検査し，ランタイムエラーとなる場合はその種類を返す．<see langword="null"/>なら検査しない</remarks>
internal sealed class MulDivInstruction(
    RegisterID rs, RegisterID rt, int sourceLine,
    string mnemonic, Func<uint, uint, (uint hi, uint lo)> compute,
    Func<uint, uint, RuntimeErrorKind?>? check
): IInstruction {

    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = sourceLine;

    // 逆操作のためのHiLoレジスタの以前の値
    // ループ内では複数回書き込まれる可能性があるためスタックで管理
    // (Hi, Lo)である．
    private readonly Stack<(uint, uint)> _prevHiLoValues = new();

    public ExecuteResult Execute(RuntimeContext context) {
        uint rsVal = context.Registers[rs];
        uint rtVal = context.Registers[rt];
        if(check?.Invoke(rsVal, rtVal) is RuntimeErrorKind kind) {
            // ランタイムエラー．HI/LO は以前の値のままで，Undo用の情報も積まない
            return ExecuteResult.Fail(kind, this.ErrorMessage(kind, rsVal, rtVal));
        }
        (uint hi, uint lo) = compute(rsVal, rtVal);
        this.WriteHiLo(context, hi, lo);
        context.Log($"{mnemonic} ${rs}, ${rt}: 0x{rsVal:X8}, 0x{rtVal:X8} => HI=0x{hi:X8}, LO=0x{lo:X8}");
        return ExecuteResult.Next;
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
    /// ランタイムエラーの説明文 (例: <c>div $t0, $t1: division by zero ($t0 = 0x00000007, $t1 = 0x00000000)</c>)
    /// </summary>
    private string ErrorMessage(RuntimeErrorKind kind, uint rsVal, uint rtVal) {
        string reason = kind switch {
            RuntimeErrorKind.DivisionByZero => "division by zero",
            RuntimeErrorKind.DivisionOverflow => "quotient overflow (0x80000000 / -1)",
            _ => kind.ToString()
        };
        string rsName = $"${rs.ToString().ToLowerInvariant()}";
        string rtName = $"${rt.ToString().ToLowerInvariant()}";
        return $"{mnemonic} {rsName}, {rtName}: {reason} ({rsName} = 0x{rsVal:X8}, {rtName} = 0x{rtVal:X8})";
    }

    /// <summary>
    /// 乗除算命令のパーサーを生成するファクトリ (mult, div)
    /// </summary>
    /// <param name="compute">(rs, rt) から (HI, LO) を計算する</param>
    /// <param name="check">計算前の入力の検査．ランタイムエラーとなる場合はその種類を返す</param>
    internal static Func<string, IInstructionParser> CreateParser(
        Func<uint, uint, (uint hi, uint lo)> compute,
        Func<uint, uint, RuntimeErrorKind?>? check = null
    ) {
        return mnemonic => new Factories.FuncInstructionParser(mnemonic, (operands, lineNumber) => {
            return OperandParser.TryParse2RegOperands(operands, out RegisterID rs, out RegisterID rt)
                ? new MulDivInstruction(rs, rt, lineNumber, mnemonic, compute, check)
                : (IInstruction?)null;
        });
    }
}
