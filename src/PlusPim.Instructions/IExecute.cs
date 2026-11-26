namespace PlusPim.Instructions;

/// <summary>
/// 命令の実行の抽象化
/// </summary>
public interface IExecute {

    /// <summary>
    /// 宣言順の読み込むレジスタ
    /// </summary>
    ReadOnlySpan<RegisterName> Src { get; }

    /// <summary>
    /// 宣言順の書き込むレジスタ
    /// </summary>
    ReadOnlySpan<RegisterName> Dest { get; }

    /// <summary>
    /// アセンブリでの表記. 例 <c>addu $t0, $t1, $zero</c>
    /// </summary>
    /// <remarks>分岐先とジャンプ先はラベルでなく絶対アドレスで表す</remarks>
    string Text { get; }

    /// <summary>
    /// 値を評価してランタイムへの副作用を返す
    /// </summary>
    /// <param name="src"><see cref="Src"/> の各レジスタの値</param>
    /// <param name="dest"><see cref="Dest"/> の各レジスタへ書く値</param>
    /// <remarks><see cref="RuntimeRequest"/> の結果はランタイムが <c>dest[0]</c> へ書く</remarks>
    Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest);
}
