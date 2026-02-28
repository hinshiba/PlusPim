using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using Xunit;

namespace PlusPimTests;

public class DataSegmentBuilderTests {
    private static DataSegmentBuilder MakeBuilder(out List<string> logs) {
        List<string> l = [];
        logs = l;
        return new DataSegmentBuilder(l.Add);
    }

    [Fact]
    public void Byte_HexValue_WritesCorrectByte() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".byte 0x41");
        DataSegment seg = b.Build();
        Assert.Equal(0x41, seg.MemoryImage[DataSegment.DataSegmentBase]);
    }

    [Fact]
    public void Word_Value1_WritesLittleEndian() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".word 1");
        DataSegment seg = b.Build();
        Address @base = DataSegment.DataSegmentBase;
        Assert.Equal(0x01, seg.MemoryImage[@base]);
        Assert.Equal(0x00, seg.MemoryImage[new Address(@base.Addr + 1)]);
        Assert.Equal(0x00, seg.MemoryImage[new Address(@base.Addr + 2)]);
        Assert.Equal(0x00, seg.MemoryImage[new Address(@base.Addr + 3)]);
    }

    [Fact]
    public void Ascii_TwoChars_WritesWithoutNull() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".ascii \"AB\"");
        DataSegment seg = b.Build();
        Address @base = DataSegment.DataSegmentBase;
        Assert.Equal(0x41, seg.MemoryImage[@base]);
        Assert.Equal(0x42, seg.MemoryImage[new Address(@base.Addr + 1)]);
        Assert.False(seg.MemoryImage.ContainsKey(new Address(@base.Addr + 2)));
    }

    [Fact]
    public void Asciiz_OneChar_WritesWithNull() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".asciiz \"A\"");
        DataSegment seg = b.Build();
        Address @base = DataSegment.DataSegmentBase;
        Assert.Equal(0x41, seg.MemoryImage[@base]);
        Assert.Equal(0x00, seg.MemoryImage[new Address(@base.Addr + 1)]);
    }

    [Fact]
    public void Ascii_EscapeNewline_WritesLineFeed() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".ascii \"\\n\"");
        DataSegment seg = b.Build();
        Assert.Equal((byte)'\n', seg.MemoryImage[DataSegment.DataSegmentBase]);
    }

    [Fact]
    public void Ascii_EscapeTab_WritesTab() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".ascii \"\\t\"");
        DataSegment seg = b.Build();
        Assert.Equal((byte)'\t', seg.MemoryImage[DataSegment.DataSegmentBase]);
    }

    [Fact]
    public void Ascii_EscapeNull_WritesZero() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".ascii \"\\0\"");
        DataSegment seg = b.Build();
        Assert.Equal(0x00, seg.MemoryImage[DataSegment.DataSegmentBase]);
    }

    [Fact]
    public void Ascii_EscapeBackslash_WritesBackslash() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".ascii \"\\\\\"");
        DataSegment seg = b.Build();
        Assert.Equal((byte)'\\', seg.MemoryImage[DataSegment.DataSegmentBase]);
    }

    [Fact]
    public void Align_AlreadyAligned_NoChange() {
        DataSegmentBuilder b = MakeBuilder(out _);
        Address before = b.NextDataAddres;
        b.AddLine(".align 2");
        Assert.Equal(before, b.NextDataAddres);
    }

    [Fact]
    public void Align_AfterOneByte_PadsTo4ByteBoundary() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".byte 1");
        b.AddLine(".align 2");
        Assert.Equal(0, b.NextDataAddres.Addr % 4);
    }

    [Fact]
    public void Align_AfterTwoBytes_PadsTo4ByteBoundary() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".byte 1");
        b.AddLine(".byte 2");
        b.AddLine(".align 2");
        Assert.Equal(0, b.NextDataAddres.Addr % 4);
    }

    [Fact]
    public void Align_AfterThreeBytes_PadsTo4ByteBoundary() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".byte 1");
        b.AddLine(".byte 2");
        b.AddLine(".byte 3");
        b.AddLine(".align 2");
        Assert.Equal(0, b.NextDataAddres.Addr % 4);
    }

    [Fact]
    public void Byte_InvalidValue_LogsWarning() {
        DataSegmentBuilder b = MakeBuilder(out List<string> logs);
        b.AddLine(".byte notanumber");
        Assert.Contains(logs, l => l.Contains("invalid .byte value"));
    }

    [Fact]
    public void UnknownDirective_LogsWarning() {
        DataSegmentBuilder b = MakeBuilder(out List<string> logs);
        b.AddLine(".unknown 123");
        Assert.Contains(logs, l => l.Contains("unknown data directive"));
    }

    [Fact]
    public void Byte_DecimalValue_WritesCorrectByte() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".byte 65");   // decimal 65 == 0x41 == 'A'
        DataSegment seg = b.Build();
        Assert.Equal(0x41, seg.MemoryImage[DataSegment.DataSegmentBase]);
    }

    [Fact]
    public void Byte_MultipleValuesCommaList_WritesEachByte() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".byte 1, 2, 3");
        DataSegment seg = b.Build();
        Address @base = DataSegment.DataSegmentBase;
        Assert.Equal(0x01, seg.MemoryImage[@base]);
        Assert.Equal(0x02, seg.MemoryImage[new Address(@base.Addr + 1)]);
        Assert.Equal(0x03, seg.MemoryImage[new Address(@base.Addr + 2)]);
    }

    [Fact]
    public void Byte_MultipleValues_AdvancesNextDataAddresCorrectly() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".byte 1, 2, 3");
        Assert.Equal(DataSegment.DataSegmentBase.Addr + 3, b.NextDataAddres.Addr);
    }

    [Fact]
    public void Word_HexValue_WritesLittleEndian() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".word 0xFF");
        DataSegment seg = b.Build();
        Address @base = DataSegment.DataSegmentBase;
        Assert.Equal(0xFF, seg.MemoryImage[@base]);
        Assert.Equal(0x00, seg.MemoryImage[new Address(@base.Addr + 1)]);
        Assert.Equal(0x00, seg.MemoryImage[new Address(@base.Addr + 2)]);
        Assert.Equal(0x00, seg.MemoryImage[new Address(@base.Addr + 3)]);
    }

    [Fact]
    public void Word_MultipleValues_WritesEachWordLittleEndian() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".word 1, 2");
        DataSegment seg = b.Build();
        Address @base = DataSegment.DataSegmentBase;
        Assert.Equal(0x01, seg.MemoryImage[@base]);
        Assert.Equal(0x00, seg.MemoryImage[new Address(@base.Addr + 1)]);
        Assert.Equal(0x02, seg.MemoryImage[new Address(@base.Addr + 4)]);
        Assert.Equal(0x00, seg.MemoryImage[new Address(@base.Addr + 5)]);
    }

    [Fact]
    public void Word_InvalidValue_LogsWarning() {
        DataSegmentBuilder b = MakeBuilder(out List<string> logs);
        b.AddLine(".word notanumber");
        Assert.Contains(logs, l => l.Contains("invalid .word value"));
    }

    [Fact]
    public void Asciiz_MultipleChars_WritesAllCharsAndNull() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".asciiz \"AB\"");
        DataSegment seg = b.Build();
        Address @base = DataSegment.DataSegmentBase;
        Assert.Equal(0x41, seg.MemoryImage[@base]);
        Assert.Equal(0x42, seg.MemoryImage[new Address(@base.Addr + 1)]);
        Assert.Equal(0x00, seg.MemoryImage[new Address(@base.Addr + 2)]);
    }

    [Fact]
    public void Ascii_EmptyString_WritesNoBytes() {
        DataSegmentBuilder b = MakeBuilder(out _);
        b.AddLine(".ascii \"\"");
        DataSegment seg = b.Build();
        Assert.Empty(seg.MemoryImage);
    }

    [Fact]
    public void SequentialWrites_AdvanceNextDataAddresMonotonically() {
        DataSegmentBuilder b = MakeBuilder(out _);
        Address start = b.NextDataAddres;
        b.AddLine(".byte 0x01");
        Assert.Equal(start.Addr + 1, b.NextDataAddres.Addr);
        b.AddLine(".byte 0x02");
        Assert.Equal(start.Addr + 2, b.NextDataAddres.Addr);
        b.AddLine(".byte 0x03");
        Assert.Equal(start.Addr + 3, b.NextDataAddres.Addr);
    }

    [Fact]
    public void EmptyBuilder_BuildReturnsEmptyMemoryImage() {
        DataSegmentBuilder b = MakeBuilder(out _);
        Assert.Empty(b.Build().MemoryImage);
    }

    [Fact]
    public void EmptyBuilder_NextDataAddresIsBase() {
        DataSegmentBuilder b = MakeBuilder(out _);
        Assert.Equal(DataSegment.DataSegmentBase, b.NextDataAddres);
    }
}
