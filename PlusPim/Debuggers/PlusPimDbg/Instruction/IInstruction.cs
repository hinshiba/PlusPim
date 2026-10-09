using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction;

/// <summary>
/// 命令を意味するインターフェース
/// </summary>
internal interface IInstruction {

    /// <summary>
    /// 命令を実行し，コンテキストを変更する
    /// </summary>
    /// <remarks>
    /// 例外を要求する (<see cref="ExecuteResult.Raise"/> を返す) 場合と，
    /// ランタイムエラーを要求する (<see cref="ExecuteResult.Fail"/> を返す) 場合はコンテキストを一切変更しないこと．
    /// </remarks>
    /// <param name="context">レジスタ，メモリ状態等を示す</param>
    /// <returns>例外・ランタイムエラーの要求と，命令自身が PC を設定したか</returns>
    ExecuteResult Execute(RuntimeContext context);

    /// <summary>
    /// 命令の逆操作を実行し，コンテキストを元に戻す
    /// </summary>
    /// <remarks>例外もランタイムエラーも要求しなかった実行に対してのみ呼ぶこと．</remarks>
    /// <param name="context">レジスタ，メモリ状態等を示す</param>
    void Undo(RuntimeContext context);

    /// <summary>
    /// その命令のファイル上での行番号(1-index)
    /// </summary>
    int SourceLine { get; }

    /// <summary>
    /// 命令のアセンブリでの表記 (<c>lui $t0, 0x1000</c> など)．即値は解決済みの値である
    /// </summary>
    /// <remarks>表記に未対応の命令は<see langword="null"/></remarks>
    string? Disassembly => null;
}
