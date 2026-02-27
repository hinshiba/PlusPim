using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

/// <summary>
/// ラベルとアドレスの対応を管理する
/// ラベル名から全情報(名前, アドレス)を持っているラベル型との相互変換
/// </summary>
internal sealed class SymbolTable {
    private readonly Dictionary<string, Label> _forwardTable = [];

    /// <summary>
    /// ラベルを追加する
    /// </summary>
    /// <param name="label">追加するラベル</param>
    /// <remarks>重複がある場合は上書きされる</remarks>
    public void Add(Label label) {
        this._forwardTable[label.Name] = label;
    }

    /// <summary>
    /// ラベル名からラベルを解決する
    /// </summary>
    /// <param name="name">ラベル名</param>
    /// <returns>解決できた場合はラベル．そうでない場合は<see langword="null"/></returns>
    public Label? Resolve(string name) {
        return this._forwardTable.TryGetValue(name, out Label label) ? label : null;
    }
}
