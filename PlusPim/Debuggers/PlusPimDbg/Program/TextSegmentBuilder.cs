using PlusPim.Debuggers.PlusPimDbg.Instructions;
using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Program;

internal sealed class TextSegmentBuilder(Action<string> log) {
    private readonly List<IInstruction> _instructions = [];
    private readonly List<int> _sourceLines = [];
    private readonly SymbolTable _symbolTable = new();
    public void AddLine(string Line) {


    }


}





/// <summary>
/// ラベルと実行インデックスの対応を管理する
/// </summary>
internal sealed class TextSymbolTable {
    private readonly Dictionary<string, int> _forwardTable = [];
    private readonly Dictionary<int, string> _reverseTable = [];

    /// <summary>
    /// ラベルを追加する
    /// </summary>
    /// <param name="label">追加するラベル</param>
    /// <remarks>重複がある場合は上書きされる</remarks>
    public void Add(Label label) {
        // 既存のエントリがある場合は逆引きテーブルからも削除する
        if(this._forwardTable.TryGetValue(label.Name, out int oldIndex) && oldIndex != label.ExecutionIndex) {
            _ = this._reverseTable.Remove(oldIndex);
        }
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
