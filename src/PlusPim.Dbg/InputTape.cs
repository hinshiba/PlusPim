using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PlusPim.Dbg;

/// <summary>
/// デバッギへ与えた入力のすべて. 追記のみで, 読み取り位置は <see cref="State"/> が持つ
/// </summary>
/// <remarks>過去の内容が変わらないので, 同じ位置からの再実行は同じ入力を読む</remarks>
internal sealed class InputTape {
    private readonly List<byte> _bytes = [];

    /// <summary>終端が記録されたか. 以降は追記できない</summary>
    public bool Closed { get; private set; }

    /// <summary><paramref name="cursor"/> から末尾までの未読の入力</summary>
    public ReadOnlySpan<byte> From(int cursor) {
        return CollectionsMarshal.AsSpan(this._bytes)[cursor..];
    }

    /// <summary>末尾に入力を追記する</summary>
    /// <param name="input">追記するバイト列</param>
    public void Append(ReadOnlySpan<byte> input) {
        if(this.Closed) {
            throw new UnreachableException("Input is already closed.");
        }
        this._bytes.AddRange(input);
    }

    /// <summary>EOFを入力する</summary>
    public void Close() {
        this.Closed = true;
    }
}
