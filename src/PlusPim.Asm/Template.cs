using PlusPim.Instructions;

namespace PlusPim.Asm;

/// <summary>ラベル参照のアドレス</summary>
internal readonly record struct LabelRef(string Name);

/// <summary>ラベル参照のアドレスの上位16bit</summary>
internal readonly record struct LabelHi(string Name);

/// <summary>ラベル参照のアドレスの下位16bit</summary>
internal readonly record struct LabelLo(string Name);

/// <summary>
/// ラベル参照の解決前のオペランド.
/// </summary>
internal readonly union AsmOperand(GprId, Immediate, Memory, LabelRef, LabelHi, LabelLo);

/// <summary>
/// ラベル参照の解決前の命令.
/// </summary>
/// <param name="Factory">命令パーサ</param>
/// <param name="Operands">オペランド</param>
internal readonly record struct Template(FromOperands Factory, AsmOperand[] Operands) {
    /// <summary>
    /// ラベル参照を解決して命令を作る
    /// </summary>
    /// <param name="label">ラベルの名前からアドレスを引く</param>
    /// <returns>オペランドの形が合わなければ <see langword="null"/></returns>
    public IExecute? Resolve(Func<string, Address> label) {
        Operand[] ops = new Operand[this.Operands.Length];
        for(int i = 0; i < ops.Length; i++) {
            ops[i] = this.Operands[i] switch {
                LabelRef r => label(r.Name),
                LabelHi h => new Immediate((ushort)(label(h.Name).Addr >> 16)),
                LabelLo l => new Immediate((ushort)label(l.Name).Addr),
                // ラベル参照を含まないのでそのまま
                GprId g => g,
                Immediate imm => imm,
                Memory m => m,

            };
        }
        return this.Factory(ops);
    }
}
