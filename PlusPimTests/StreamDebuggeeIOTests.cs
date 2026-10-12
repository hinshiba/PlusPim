using PlusPim.Application;
using Xunit;

namespace PlusPimTests;

public class StreamDebuggeeIOTests {
    [Fact]
    public void Write_PassesBytesVerbatim() {
        using MemoryStream output = new();
        StreamDebuggeeIO io = new(new MemoryStream(), output);

        // 不正なUTF-8も変換されない
        byte[] data = [0xFF, 0xFE, 0x61, 0xE3, 0x81];
        io.Write(data);
        io.Write([0x00]);

        Assert.Equal([.. data, 0x00], output.ToArray());
    }

    [Fact]
    public void ReadByte_ReturnsBytesThenEof() {
        StreamDebuggeeIO io = new(new MemoryStream([0x00, 0x7F, 0x80, 0xFF]), new MemoryStream());

        Assert.Equal(0x00, io.ReadByte());
        Assert.Equal(0x7F, io.ReadByte());
        Assert.Equal(0x80, io.ReadByte());
        Assert.Equal(0xFF, io.ReadByte());
        Assert.Equal(-1, io.ReadByte());
        Assert.Equal(-1, io.ReadByte());
    }

    [Fact]
    public void ReadByte_DoesNotReadAhead() {
        MemoryStream input = new([1, 2, 3]);
        StreamDebuggeeIO io = new(input, new MemoryStream());

        _ = io.ReadByte();

        Assert.Equal(1, input.Position);
    }
}
