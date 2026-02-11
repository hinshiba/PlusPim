namespace PlusPim.Debuggers.PlusPimDbg;

/// <summary>
/// ラベルを表す値型
/// </summary>
/// <param name="Name">ラベルのシンボル名</param>
/// <param name="ExecutionIndex">対応する実行インデックス</param>
internal readonly record struct Label(string Name, int ExecutionIndex);

/// <summary>
/// ラベルと実行インデックスの対応を管理する
/// </summary>
internal sealed class SymbolTable {
    private readonly Dictionary<string, int> _forwardTable = [];
    private readonly Dictionary<int, string> _reverseTable = [];

    /// <summary>
    /// ラベルを追加する
    /// </summary>
    /// <param name="label">追加するラベル</param>
    /// <remarks>重複がある場合は上書きされる</remarks>
    public void Add(Label label) {
        this._forwardTable[label.Name] = label.ExecutionIndex;
        this._reverseTable[label.ExecutionIndex] = label.Name;
    }

    /// <summary>
    /// ラベル名から実行インデックスを解決する
    /// </summary>
    /// <param name="name">ラベル名</param>
    /// <returns>解決できた場合は実行インデックス．そうでない場合は<see langword="null"/></returns>
    public int? Resolve(string name) {
        return this._forwardTable.TryGetValue(name, out int idx) ? idx : null;
    }

    /// <summary>
    /// ExecutionIndexからラベル名を逆引きする
    /// </summary>
    /// <param name="executionIndex">実行インデックス</param>
    /// <returns>完全一致するラベルがある場合はそのラベル名．そうでない場合はexecutionIndexより前で最も近いラベル名．それもない場合は<see langword="null"/></returns>
    public string? FindByIndex(int executionIndex) {
        // 完全一致
        if(this._reverseTable.TryGetValue(executionIndex, out string? label)) {
            return label;
        }
        // 線形探索であり，改善はTODO
        // indexより前で最も近いラベルを返す
        string? closest = null;
        int closestIndex = -1;
        foreach(KeyValuePair<int, string> kvp in this._reverseTable) {
            if(kvp.Key <= executionIndex && kvp.Key > closestIndex) {
                closestIndex = kvp.Key;
                closest = kvp.Value;
            }
        }
        return closest;
    }
}
