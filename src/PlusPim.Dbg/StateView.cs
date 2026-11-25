using PlusPim.Instructions;

namespace PlusPim.Dbg;

/// <summary>
/// 状態の読み取り専用ビュー
/// </summary>
public readonly struct StateView {
    private readonly State _s;

    internal StateView(State s) {
        this._s = s;
    }

    /// <summary>メモリを除く状態</summary>
    public ref readonly Core Core => ref this._s.Core;

    /// <summary>
    /// <paramref name="destination"/> のすべてのバイトを書く. 書き込まれていないメモリは未定義.
    /// </summary>
    public void ReadMemory(Address start, Span<byte> destination) {
        this._s.Memory.Read(start, destination);
    }
}
