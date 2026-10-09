using PlusPim.Debuggers.PlusPimDbg.Program.Records;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// ラベル名からラベルを解決する
/// </summary>
internal interface ISymbolResolver {
    /// <summary>
    /// ラベル名からラベルを解決する
    /// </summary>
    /// <param name="name">ラベル名</param>
    /// <returns>解決できた場合はラベル．そうでない場合は<see langword="null"/></returns>
    Label? Resolve(string name);
}
