namespace PlusPim.Application;

/// <summary>
/// 疑似命令の1行と，その展開先の命令
/// </summary>
/// <param name="Line">1始まりの行番号</param>
/// <param name="Mnemonic">疑似命令のニーモニック (小文字)</param>
/// <param name="Instructions">展開先の命令 (アドレス順)</param>
public sealed record PseudoExpansionInfo(int Line, string Mnemonic, PseudoExpandedInstruction[] Instructions);

/// <summary>
/// 疑似命令の展開先の1命令
/// </summary>
/// <param name="Address">命令のアドレス</param>
/// <param name="Text">命令のアセンブリでの表記．表記に未対応の命令は <c>?</c></param>
public sealed record PseudoExpandedInstruction(uint Address, string Text);
