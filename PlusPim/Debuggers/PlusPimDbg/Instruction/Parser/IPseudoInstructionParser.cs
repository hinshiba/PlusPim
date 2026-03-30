using PlusPim.Debuggers.PlusPimDbg.Program;
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
    /// 展開後の命令数を返す
    /// </summary>
    /// <param name="operands">オペランド文字列</param>
    /// <returns>展開後の実命令数</returns>
    int GetExpansionSize(string operands);

    /// <summary>
    /// 疑似命令を実命令列に展開する
    /// </summary>
    /// <param name="operands">オペランド文字列</param>
    /// <param name="lineIndex">ソースファイル上の行番号(1-based)</param>
    /// <param name="symbolTable">解決済みのシンボルテーブル</param>
    /// <param name="instructions">展開後の命令列</param>
    /// <returns>成功なら<see langword="true"/></returns>
    bool TryExpand(string operands, int lineIndex, SymbolTable symbolTable,
                   [MaybeNullWhen(false)] out IInstruction[] instructions);
}
