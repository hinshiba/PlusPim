using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;

/// <summary>
/// 疑似命令のパーサーを表すインターフェース
/// </summary>
/// <remarks>
/// 疑似命令は複数の実命令に展開される
/// </remarks>
internal interface IPseudoInstructionParser {
    /// <summary>
    /// 疑似命令のニーモニック
    /// </summary>
    string Mnemonic { get; }

    /// <summary>
    /// 疑似命令のオペランドを解析する
    /// </summary>
    /// <param name="operands">オペランド文字列</param>
    /// <param name="lineNumber">ソースファイル上の行番号(1-based)</param>
    /// <param name="line">成功の場合は展開後の命令数が確定した行</param>
    /// <returns>成功なら<see langword="true"/></returns>
    bool TryParse(string operands, int lineNumber, [MaybeNullWhen(false)] out ParsedLine line);
}
