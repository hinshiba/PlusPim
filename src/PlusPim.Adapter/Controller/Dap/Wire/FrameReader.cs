using System.Buffers;
using System.Globalization;
using System.Text;

namespace PlusPim.Adapter.Controller.Dap.Wire;

/// <summary>
/// Content-Lengthで区切られたメッセージの本体を読む
/// </summary>
/// <param name="stream">DAPの接続</param>
internal sealed class FrameReader(Stream stream): IDisposable {
    /// <summary>ヘッダの上限のバイト数. VS Code拡張のdapFraming.tsのMAX_HEADER_BYTESと同じ</summary>
    public const int MaxHeaderBytes = 1024;

    /// <summary>本体の上限のバイト数. VS Code拡張の上限(MAX_BODY_BYTES)はこれとは別に送る側の制約である</summary>
    public const int MaxBodyBytes = 1 << 20;

    private static ReadOnlySpan<byte> HeaderEnd => "\r\n\r\n"u8;

    // ArrayPoolは2のべき乗に切り上げるので, 上限で借りずに小さく始める
    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(4096);

    // 未読のバイトは[_start, _end)にある
    private int _start;
    private int _end;

    /// <summary>次の本体を読む. 結果は1度だけ待つ</summary>
    /// <param name="cancellation">読み取りの取り消し</param>
    /// <returns>本体. 次の呼び出しまで有効. メッセージの境界で接続が終われば <see langword="null"/></returns>
    /// <exception cref="FramingException">ヘッダが不正か, 上限を超えた</exception>
    public async ValueTask<ReadOnlyMemory<byte>?> ReadAsync(CancellationToken cancellation) {
        // 前の本体はもう使われないので, 残りを先頭へ寄せる
        this._buffer.AsSpan(this._start, this._end - this._start).CopyTo(this._buffer);
        this._end -= this._start;
        this._start = 0;

        int headerLength;
        while((headerLength = this._buffer.AsSpan(0, this._end).IndexOf(HeaderEnd)) < 0) {
            if(MaxHeaderBytes + HeaderEnd.Length <= this._end) {
                throw new FramingException("The DAP header is too long.");
            }
            if(!await this.FillAsync(cancellation)) {
                return this._end == 0 ? null : throw new FramingException("The connection ended in the middle of a DAP header.");
            }
        }
        if(MaxHeaderBytes < headerLength) {
            throw new FramingException("The DAP header is too long.");
        }

        int bodyStart = headerLength + HeaderEnd.Length;
        int bodyEnd = bodyStart + ParseContentLength(this._buffer.AsSpan(0, headerLength));
        if(this._buffer.Length < bodyEnd) {
            byte[] larger = ArrayPool<byte>.Shared.Rent(bodyEnd);
            this._buffer.AsSpan(0, this._end).CopyTo(larger);
            ArrayPool<byte>.Shared.Return(this._buffer);
            this._buffer = larger;
        }
        while(this._end < bodyEnd) {
            if(!await this.FillAsync(cancellation)) {
                throw new FramingException("The connection ended in the middle of a DAP body.");
            }
        }
        this._start = bodyEnd;
        return this._buffer.AsMemory(bodyStart, bodyEnd - bodyStart);
    }

    private async ValueTask<bool> FillAsync(CancellationToken cancellation) {
        int read = await stream.ReadAsync(this._buffer.AsMemory(this._end), cancellation);
        this._end += read;
        return read != 0;
    }

    /// <summary>
    /// <c>名前: 値</c> の行の並びから, ちょうど1つのContent-Lengthを読む. ほかの名前の行は読み飛ばす
    /// </summary>
    /// <remarks>拡張は <c>Content-Length: N</c> と空白を1つ置いて送るので, コロンの後の空白だけ読み飛ばす</remarks>
    private static int ParseContentLength(ReadOnlySpan<byte> header) {
        int length = -1;
        foreach(Range range in header.Split("\r\n"u8)) {
            ReadOnlySpan<byte> line = header[range];
            int colon = line.IndexOf((byte)':');
            if(colon < 0) {
                throw new FramingException("A DAP header line has no colon.");
            }
            if(!Ascii.EqualsIgnoreCase(line[..colon], "Content-Length"u8)) {
                continue;
            }
            // 7桁あれば上限の1 MiBを表せ, intの桁あふれも起きない
            ReadOnlySpan<byte> value = line[(colon + 1)..].TrimStart((byte)' ');
            if(0 <= length || value.Length == 0 || 7 < value.Length || value.ContainsAnyExceptInRange((byte)'0', (byte)'9')) {
                throw new FramingException("The Content-Length header is invalid or duplicated.");
            }
            length = int.Parse(value, CultureInfo.InvariantCulture);
        }
        if(length < 0) {
            throw new FramingException("The Content-Length header is missing.");
        }
        return length <= MaxBodyBytes ? length : throw new FramingException($"The DAP body is too large: {length} bytes.");
    }

    /// <inheritdoc/>
    public void Dispose() {
        ArrayPool<byte>.Shared.Return(this._buffer);
        this._buffer = [];
    }
}
