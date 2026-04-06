namespace PlusPim.Application;

/// <summary>
/// ブレークポイント設定の結果
/// </summary>
/// <param name="Line">1-indexedの行番号</param>
/// <param name="Verified">有効な命令アドレスに対応する場合はtrue</param>
public readonly record struct BreakpointResult(int Line, bool Verified);
