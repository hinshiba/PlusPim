using PlusPim.Instructions;

namespace PlusPim.Dbg;

/// <summary>
/// 32ビットのアドレス空間. 書き込まれていない領域は未定義.
/// </summary>
/// <remarks>
/// ページを <see cref="Fork"/> の前後で共有し, 書き込み時に所有していなければ複製する (CoW).
/// </remarks>
internal sealed class Memory {
    private const int PageBits = 12;
    private const uint OffsetMask = (1u << PageBits) - 1;

    private sealed record Page(byte[] Bytes, object Owner);

    private readonly Dictionary<uint, Page> _pages;
    private object _owner = new();

    public Memory() : this([]) { }

    private Memory(Dictionary<uint, Page> pages) {
        this._pages = pages;
    }

    public byte Read(Address a) {
        return this._pages.TryGetValue(a.Addr >> PageBits, out Page? p) ? p.Bytes[a.Addr & OffsetMask] : (byte)0;
    }

    /// <summary><paramref name="start"/> から <paramref name="destination"/> のすべてのバイトを読む. 末尾を超えると0番地へ回り込む</summary>
    public void Read(Address start, Span<byte> destination) {
        for(int i = 0; i < destination.Length; i++) {
            destination[i] = this.Read(start + (uint)i);
        }
    }

    public void Write(Address a, byte value) {
        uint key = a.Addr >> PageBits;
        if(!this._pages.TryGetValue(key, out Page? p) || !ReferenceEquals(p.Owner, this._owner)) {
            p = new Page(p is null ? new byte[1 << PageBits] : (byte[])p.Bytes.Clone(), this._owner);
            this._pages[key] = p;
        }
        p.Bytes[a.Addr & OffsetMask] = value;
    }

    /// <summary>
    /// 内容が等しい独立したメモリを作る. ページ数に比例した時間がかかる.
    /// </summary>
    public Memory Fork() {
        // 既存のページを双方にとって共有にする
        this._owner = new();
        return new Memory([with(this._pages)]);
    }
}
