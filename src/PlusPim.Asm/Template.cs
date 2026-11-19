using PlusPim.Instructions;

namespace PlusPim.Asm;

/// <summary>シンボルのアドレス</summary>
internal readonly record struct SymbolRef(string Name);

/// <summary>シンボルのアドレスの上位16bit</summary>
internal readonly record struct SymbolHi(string Name);

/// <summary>シンボルのアドレスの下位16bit</summary>
internal readonly record struct SymbolLo(string Name);

/// <summary>
/// シンボル解決前のオペランド.
/// </summary>
internal readonly union AsmOperand(GprId, Immediate, Memory, SymbolRef, SymbolHi, SymbolLo);

/// <summary>
/// シンボル解決前の命令.
/// </summary>
/// <param name="Factory">命令パーサ</param>
/// <param name="Operands">オペランド</param>
internal readonly record struct Template(FromOperands Factory, AsmOperand[] Operands) {
    /// <summary>
    /// シンボルを解決して命令を作る
    /// </summary>
    /// <param name="symbol">シンボルテーブル</param>
    /// <returns>オペランドの形が合わなければ <see langword="null"/></returns>
    public IExecute? Resolve(Func<string, Address> symbol) {
        Operand[] ops = new Operand[this.Operands.Length];
        for(int i = 0; i < ops.Length; i++) {
            ops[i] = this.Operands[i] switch {
                SymbolRef r => symbol(r.Name),
                SymbolHi h => new Immediate((ushort)(symbol(h.Name).Addr >> 16)),
                SymbolLo l => new Immediate((ushort)symbol(l.Name).Addr),
                // シンボル解決に影響しないのでそのまま
                GprId g => g,
                Immediate imm => imm,
                Memory m => m,

            };
        }
        return this.Factory(ops);
    }
}
