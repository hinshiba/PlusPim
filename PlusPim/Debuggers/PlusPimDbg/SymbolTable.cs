namespace PlusPim.Debuggers.PlusPimDbg;

/// <summary>
/// ラベルを表すレコード
/// </summary>
internal readonly record struct Label(string Name, int ExecutionIndex);

/// <summary>
/// ラベルの正引き（名前→インデックス）・逆引き（インデックス→ラベル）を一元管理する
/// </summary>
internal sealed class SymbolTable {
    private readonly Dictionary<string, int> _forwardTable = [];
    private readonly Dictionary<int, string> _reverseTable = [];

    public void Add(Label label) {
        this._forwardTable[label.Name] = label.ExecutionIndex;
        this._reverseTable[label.ExecutionIndex] = label.Name;
    }

    /// <summary>
    /// ラベル名からExecutionIndexを解決する
    /// </summary>
    public int? Resolve(string name) {
        return this._forwardTable.TryGetValue(name, out int idx) ? idx : null;
    }

    /// <summary>
    /// ExecutionIndexからラベル名を逆引きする（完全一致 + 最近傍）
    /// </summary>
    public string? FindByIndex(int executionIndex) {
        // 完全一致
        if(this._reverseTable.TryGetValue(executionIndex, out string? label))
            return label;
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

    /// <summary>
    /// IReadOnlyDictionaryとしてのビュー（後方互換用）
    /// </summary>
    public IReadOnlyDictionary<string, int> ForwardTable => this._forwardTable;
}
