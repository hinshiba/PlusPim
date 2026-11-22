using PlusPim.Instructions;

namespace PlusPim.Dbg;

/// <summary>
/// 呼び出しまたは例外で積んだフレーム
/// </summary>
/// <param name="Entry">呼び出し先 (例外ならハンドラ) の先頭</param>
/// <param name="CallSite">呼び出した命令 (例外なら EPC) のアドレス</param>
/// <param name="CallerRegs">積んだ時点のレジスタ</param>
/// <param name="Code">例外のフレームならその例外. 呼び出しなら <see langword="null"/></param>
public sealed record Frame(Address Entry, Address CallSite, Registers CallerRegs, ExcCode? Code);
