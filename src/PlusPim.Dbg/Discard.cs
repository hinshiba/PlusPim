using System.Buffers;

namespace PlusPim.Dbg;

/// <summary>
/// 書かれた内容を捨てる出力先.
/// </summary>
internal sealed class Discard: IBufferWriter<byte> {
    public static readonly Discard Instance = new();
    private byte[] _buffer = [];

    private Discard() { }

    public void Advance(int count) { }

    public Memory<byte> GetMemory(int sizeHint = 0) {
        if(this._buffer.Length < Math.Max(sizeHint, 1)) {
            this._buffer = new byte[Math.Max(sizeHint, 256)];
        }
        return this._buffer;
    }

    public Span<byte> GetSpan(int sizeHint = 0) {
        return this.GetMemory(sizeHint).Span;
    }
}
