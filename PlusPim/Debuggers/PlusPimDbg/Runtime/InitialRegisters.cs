namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// プログラムの読み込み時のレジスタの初期値 (SPIM/MARS と同じ値)
/// </summary>
/// <remarks>
/// ローダーの役割なので <see cref="RuntimeContext"/> の生成時には設定しない．
/// 実行の履歴に含まれないので，StepBack で先頭まで戻っても初期値のまま残る
/// </remarks>
internal static class InitialRegisters {
    /// <summary>
    /// <c>$sp</c> の初期値．<c>0x80000000 - 4 - 4096</c>
    /// </summary>
    public const uint StackPointer = 0x7FFFEFFC;

    /// <summary>
    /// <c>$gp</c> の初期値．データセグメントの先頭 <c>0x10000000</c> + <c>0x8000</c>
    /// </summary>
    public const uint GlobalPointer = 0x10008000;

    /// <summary>
    /// <c>$sp</c> と <c>$gp</c> に初期値を設定する
    /// </summary>
    public static void Apply(RegisterFile registers) {
        registers[RegisterID.Sp] = StackPointer;
        registers[RegisterID.Gp] = GlobalPointer;
    }
}
