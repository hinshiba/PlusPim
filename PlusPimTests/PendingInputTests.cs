using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests;

public class PendingInputTests {
    private readonly InMemoryDebuggeeIO _io = new();

    private PendingInput Create() {
        return new PendingInput(this._io);
    }

    [Fact]
    public void ReadByte_WithoutPushedBytes_ReadsFromIo() {
        PendingInput pending = this.Create();
        this._io.SetInput(0x00, 0xFF);

        Assert.Equal(0x00, pending.ReadByte());
        Assert.Equal(0xFF, pending.ReadByte());
        Assert.Equal(-1, pending.ReadByte());
    }

    [Fact]
    public void PushFront_FirstByteIsReadFirst() {
        PendingInput pending = this.Create();
        this._io.SetInput(0x09);

        pending.PushFront([0x01, 0x02, 0x03]);

        Assert.Equal([0x01, 0x02, 0x03], pending.ToArray());
        Assert.Equal(0x01, pending.ReadByte());
        Assert.Equal(0x02, pending.ReadByte());
        Assert.Equal(0x03, pending.ReadByte());
        // 戻したバイトが尽きたらIoから読む
        Assert.Equal(0x09, pending.ReadByte());
        Assert.Equal(-1, pending.ReadByte());
    }

    [Fact]
    public void PushFront_Twice_LaterPushIsReadFirst() {
        PendingInput pending = this.Create();

        pending.PushFront([0x03, 0x04]);
        pending.PushFront([0x01, 0x02]);

        Assert.Equal([0x01, 0x02, 0x03, 0x04], pending.ToArray());
    }

    [Fact]
    public void PushFront_Empty_ChangesNothing() {
        PendingInput pending = this.Create();

        pending.PushFront([]);

        Assert.Empty(pending.ToArray());
    }

    [Fact]
    public void ToArray_DoesNotConsume() {
        PendingInput pending = this.Create();
        pending.PushFront([0x01, 0x02]);

        _ = pending.ToArray();

        Assert.Equal(0x01, pending.ReadByte());
        Assert.Equal([0x02], pending.ToArray());
    }

    [Fact]
    public void ReadLine_IncludesNewlineAndStopsThere() {
        PendingInput pending = this.Create();
        this._io.SetInput("abc\ndef\n");

        Assert.Equal("abc\n"u8.ToArray(), pending.ReadLine());
        Assert.Equal("def\n"u8.ToArray(), pending.ReadLine());
    }

    [Fact]
    public void ReadLine_KeepsCarriageReturn() {
        PendingInput pending = this.Create();
        this._io.SetInput("12\r\nrest");

        Assert.Equal("12\r\n"u8.ToArray(), pending.ReadLine());
        Assert.Equal("rest"u8.ToArray(), pending.ReadLine());
    }

    [Fact]
    public void ReadLine_AtEof_ReturnsNull() {
        PendingInput pending = this.Create();

        Assert.Null(pending.ReadLine());
    }

    [Fact]
    public void ReadLine_PushedBytesWithoutNewline_ContinuesFromIo() {
        PendingInput pending = this.Create();
        pending.PushFront("ab"u8);
        this._io.SetInput("cd\nef");

        Assert.Equal("abcd\n"u8.ToArray(), pending.ReadLine());
        Assert.Equal("ef"u8.ToArray(), pending.ReadLine());
        Assert.Null(pending.ReadLine());
    }

    [Fact]
    public void ReadLine_PushedLineStopsAtItsNewline() {
        PendingInput pending = this.Create();
        pending.PushFront("ab\ncd"u8);

        Assert.Equal("ab\n"u8.ToArray(), pending.ReadLine());
        Assert.Equal("cd"u8.ToArray(), pending.ToArray());
    }
}
