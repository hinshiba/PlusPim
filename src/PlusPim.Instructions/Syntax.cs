namespace PlusPim.Instructions;

/// <summary>
/// 命令のアセンブリでの表記を組み立てる
/// </summary>
internal static class Syntax {
    /// <summary><c>mnemonic op, op, ...</c>を結合してアセンブリ表記を生成する</summary>
    public static string Of(string mnemonic, params ReadOnlySpan<string> operands) {
        return operands.IsEmpty ? mnemonic : mnemonic + " " + string.Join(", ", operands);
    }

    /// <summary>レジスタだけをオペランドに持つ命令のアセンブリ表記を生成する</summary>
    public static string Of(string mnemonic, ReadOnlySpan<RegisterName> regs) {
        string[] ops = new string[regs.Length];
        for(int i = 0; i < ops.Length; i++) {
            ops[i] = regs[i].AsmName();
        }
        return Of(mnemonic, ops);
    }

    /// <summary>汎用レジスタを名前, CP0レジスタを番号の表記に変換する</summary>
    public static string AsmName(this RegisterName r) {
        return r switch {
            GprId g => g.AsmName(),
            RName n => "$" + Cp0.ToNumber(n),
        };
    }

    /// <summary><c>offset($base)</c>をアセンブリ表記に変換する</summary>
    public static string Mem(RegisterName baseReg, uint offset) {
        return ImmKind.Signed.Format(offset) + "(" + baseReg.AsmName() + ")";
    }
}
