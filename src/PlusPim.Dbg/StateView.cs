using PlusPim.Instructions;
using System.Collections.Immutable;

namespace PlusPim.Dbg;

/// <summary>
/// 状態の読み取り専用ビュー
/// </summary>
public readonly struct StateView {
    private readonly State _s;

    internal StateView(State s) {
        this._s = s;
    }

    public Status Status => this._s.Status;

    public ref readonly Registers Registers => ref this._s.Registers;

    public Address Pc => this._s.Registers.Pc;

    /// <summary>ライブフレームを含まないコールスタック</summary>
    public ImmutableStack<Frame> CallStack => this._s.CallStack;

    /// <summary><see cref="CallStack"/> のフレームの数</summary>
    public int Depth => this._s.Depth;

    /// <summary>
    /// <paramref name="destination"/> のすべてのバイトを書く. 書き込まれていないメモリは未定義.
    /// </summary>
    public void ReadMemory(Address start, Span<byte> destination) {
        this._s.Memory.Read(start, destination);
    }
}
