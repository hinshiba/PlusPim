namespace PlusPim.Dbg;

/// <summary>
/// デバッガの可変状態
/// </summary>
/// <remarks>再実行で履歴を生成するので状態はここに集約すること.</remarks>
internal sealed class State(Memory memory) {
    public Core Core = new();
    public Memory Memory { get; } = memory;

    /// <summary>内容が等しい独立した状態を作る. ページ数に比例した時間がかかる</summary>
    public State Fork() {
        return new(this.Memory.Fork()) { Core = this.Core };
    }

    /// <summary>現在の状態へ <see cref="Rewind"/> で戻るための印. 定数時間</summary>
    public Mark Save() {
        return new(this.Core, this.Memory.Journal);
    }

    /// <summary>
    /// <paramref name="m"/> を作った時点へ戻す. その後の書き込みの量に比例した時間がかかる
    /// </summary>
    /// <remarks><see cref="Memory.Commit"/> より前の印へは戻せない.</remarks>
    public void Rewind(in Mark m) {
        this.Core = m.Core;
        this.Memory.Rollback(m.Journal);
    }
}

/// <summary>
/// ある位置の状態の印
/// </summary>
/// <param name="core">その位置のメモリを除く状態</param>
/// <param name="journal">その位置での <see cref="Memory.Journal"/></param>
internal readonly struct Mark(Core core, int journal) {
    // 判定へ複製せずに参照で渡せるようフィールドにする
    public readonly Core Core = core;
    public readonly int Journal = journal;
}
