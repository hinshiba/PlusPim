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
    /// <summary>最後の <see cref="Commit"/> 以降に書き込んだ番地と, 書き込む前の値</summary>
    private readonly List<(Address At, byte Old)> _journal = [];

    public Memory() : this([]) { }

    private Memory(Dictionary<uint, Page> pages) {
        this._pages = pages;
    }

    /// <summary>記録した書き込みの数. <see cref="Rollback"/> の戻り先に使う</summary>
    public int Journal => this._journal.Count;

    public byte Read(Address a) {
        return this._pages.TryGetValue(a.Addr >> PageBits, out Page? p) ? p.Bytes[a.Addr & OffsetMask] : (byte)0;
    }

    /// <summary><paramref name="start"/> から <paramref name="destination"/> のすべてのバイトを読む. 末尾を超えると0番地へ回り込む</summary>
    public void Read(Address start, Span<byte> destination) {
        for(int i = 0; i < destination.Length; i++) {
            destination[i] = this.Read(start + (uint)i);
        }
    }

    /// <summary>書き込む前の値を記録してから書く</summary>
    public void Write(Address a, byte value) {
        ref byte b = ref this.At(a);
        this._journal.Add((a, b));
        b = value;
    }

    /// <summary>
    /// <see cref="Journal"/> が <paramref name="mark"/> だった時点の内容に戻す
    /// </summary>
    /// <remarks>新しい順に書き戻すので, 同じ番地への複数回の書き込みも最も古い値に戻る.</remarks>
    public void Rollback(int mark) {
        for(int i = this._journal.Count - 1; mark <= i; i--) {
            (Address at, byte old) = this._journal[i];
            this.At(at) = old;
        }
        this._journal.RemoveRange(mark, this._journal.Count - mark);
    }

    /// <summary>記録を捨てる. 以降はこの時点より前へ戻せない</summary>
    public void Commit() {
        this._journal.Clear();
    }

    /// <summary>
    /// 内容が等しい独立したメモリを作る. ページ数に比例した時間がかかる. 作ったメモリの記録は空
    /// </summary>
    public Memory Fork() {
        // 既存のページを双方にとって共有にする
        this._owner = new();
        return new Memory([with(this._pages)]);
    }

    /// <summary>書き込める <paramref name="a"/> のバイト. 所有していないページは複製する</summary>
    private ref byte At(Address a) {
        uint key = a.Addr >> PageBits;
        if(!this._pages.TryGetValue(key, out Page? p) || !ReferenceEquals(p.Owner, this._owner)) {
            p = new Page(p is null ? new byte[1 << PageBits] : (byte[])p.Bytes.Clone(), this._owner);
            this._pages[key] = p;
        }
        return ref p.Bytes[a.Addr & OffsetMask];
    }
}
