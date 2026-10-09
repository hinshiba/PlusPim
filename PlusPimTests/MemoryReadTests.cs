using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Logging;
using Xunit;

namespace PlusPimTests;

/// <summary>
/// メモリビューのためのメモリの読み取りとデータセグメントの範囲のテスト
/// </summary>
public class MemoryReadTests {
    private const string DataProgram = """
        .data
        value:
          .word 0x11223344
          .byte 0x55
        .text
        main:
          la $t0, value
        """;

    [Fact]
    public void ReadMemory_ReturnsLittleEndianBytesAndZerosForUnwrittenMemory() {
        (PlusPimDbg debugger, FileInfo file) = TestHelpers.CreateDebugger(DataProgram);
        try {
            Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11, 0x55, 0, 0, 0 }, debugger.ReadMemory(0x10000000, 8));
            Assert.Empty(debugger.ReadMemory(0x10000000, 0));
        } finally {
            file.Delete();
        }
    }

    [Fact]
    public void ReadMemory_StopsAtEndOfAddressSpace() {
        (PlusPimDbg debugger, FileInfo file) = TestHelpers.CreateDebugger(DataProgram);
        try {
            Assert.Equal(4, debugger.ReadMemory(0xFFFFFFFC, 8).Length);
        } finally {
            file.Delete();
        }
    }

    [Fact]
    public void DataSegmentRange_SumsAllFiles() {
        FileInfo first = TestHelpers.WriteTempAsm(DataProgram);
        FileInfo second = TestHelpers.WriteTempAsm("""
            .data
            other:
              .space 16
            """);
        try {
            PlusPimDbg debugger = new([first, second], Logger.Null);
            Assert.Equal((0x10000000u, 5u + 16u), debugger.DataSegmentRange);
        } finally {
            first.Delete();
            second.Delete();
        }
    }
}
