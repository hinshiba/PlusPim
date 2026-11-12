using PlusPim.Instructions;

namespace PlusPim.Image;

/// <summary>
/// 実行に必要とするリンク済みのプログラム
/// </summary>
public sealed class ProgramImage {
    private readonly uint[] _textBases;
    private readonly IExecute[][] _text;

    public Address Entry { get; }

    /// <summary>
    /// 昇順で重なりのないデータセグメント. 
    /// </summary>
    public IReadOnlyList<(Address Base, ReadOnlyMemory<byte> Bytes)> Data { get; }

    private ProgramImage(Address entry, uint[] textBases, IExecute[][] text, (Address, ReadOnlyMemory<byte>)[] data) {
        this.Entry = entry;
        this._textBases = textBases;
        this._text = text;
        this.Data = data;
    }

    /// <summary>
    /// 区間を整列し, 不変条件を検査して作る. 空の区間は捨てる
    /// </summary>
    /// <exception cref="ArgumentException">
    /// 区間の重なり, テキストの非整列, セグメントの範囲外. ユーザの誤りは Asm が先に診断するので, これはプログラムの誤りである
    /// </exception>
    public static ProgramImage Create(
        Address entry,
        IEnumerable<(Address Base, IExecute[] Code)> text,
        IEnumerable<(Address Base, ReadOnlyMemory<byte> Bytes)> data) {

        (Address Base, IExecute[] Code)[] t = [.. text.Where(s => s.Code.Length > 0).OrderBy(s => s.Base.Addr)];
        (Address Base, ReadOnlyMemory<byte> Bytes)[] d = [.. data.Where(s => !s.Bytes.IsEmpty).OrderBy(s => s.Base.Addr)];

        foreach((Address b, IExecute[] code) in t) {
            ulong size = (ulong)code.Length * 4;
            if(b % 4 != 0) {
                throw new ArgumentException($"Text section at {b} is not word-aligned.", nameof(text));
            }
            if(!MemoryMap.Text.Contains(b, size) && !MemoryMap.KText.Contains(b, size)) {
                throw new ArgumentException($"Text section at {b} is out of the text segments.", nameof(text));
            }
        }
        foreach((Address b, ReadOnlyMemory<byte> bytes) in d) {
            if(!MemoryMap.Data.Contains(b, (ulong)bytes.Length)) {
                throw new ArgumentException($"Data section at {b} is out of the data segment.", nameof(data));
            }
        }

        // セグメントは互いに素なので, 重なりは同じ種類の隣どうしでしか起こらない
        CheckOverlap(t.Select(s => (s.Base.Addr, (ulong)s.Code.Length * 4)), nameof(text));
        CheckOverlap(d.Select(s => (s.Base.Addr, (ulong)s.Bytes.Length)), nameof(data));

        return new ProgramImage(
            entry,
            [.. t.Select(s => s.Base.Addr)],
            [.. t.Select(s => s.Code.ToArray())],
            [.. d.Select(s => (s.Base, (ReadOnlyMemory<byte>)s.Bytes.ToArray()))]);
    }

    private static void CheckOverlap(IEnumerable<(uint Base, ulong Size)> sorted, string param) {
        ulong end = 0;
        foreach((uint b, ulong size) in sorted) {
            if(b < end) {
                throw new ArgumentException($"Section at {new Address(b)} overlaps the previous one.", param);
            }
            end = b + size;
        }
    }

    /// <summary>
    /// <paramref name="addr"/> に存在する命令. なければ <see langword="null"/>
    /// </summary>
    public IExecute? Fetch(Address addr) {
        int i = Sorted.Floor(this._textBases, addr.Addr);
        if(i < 0) {
            return null;
        }
        uint offset = addr.Addr - this._textBases[i];
        IExecute[] code = this._text[i];
        return offset % 4 == 0 && offset / 4 < (uint)code.Length ? code[offset / 4] : null;
    }
}
