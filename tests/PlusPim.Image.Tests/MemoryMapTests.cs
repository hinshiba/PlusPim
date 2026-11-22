using PlusPim.Instructions;

namespace PlusPim.Image.Tests;

public class MemoryMapTests {
    [Theory]
    [InlineData(SegmentKind.Text, 0x003FFFFFu, false)]
    [InlineData(SegmentKind.Text, 0x00400000u, true)]
    [InlineData(SegmentKind.Text, 0x0FFFFFFFu, true)]
    [InlineData(SegmentKind.Data, 0x10000000u, true)]
    [InlineData(SegmentKind.Data, 0x7FFFFFFFu, true)]
    [InlineData(SegmentKind.Data, 0x80000000u, false)]
    [InlineData(SegmentKind.KText, 0x80000000u, true)]
    [InlineData(SegmentKind.KText, 0x8FFFFFFFu, true)]
    [InlineData(SegmentKind.KText, 0x90000000u, false)]
    public void Contains_FollowsBoundaries(SegmentKind kind, uint addr, bool inside) {
        Assert.Equal(inside, MemoryMap.Of(kind).Contains(new Address(addr), 1));
    }

    [Fact]
    public void Of_ReturnsSegmentContainingItsBase() {
        foreach(SegmentKind kind in Enum.GetValues<SegmentKind>()) {
            Segment s = MemoryMap.Of(kind);
            Assert.True(s.Contains(s.Base, 1));
        }
    }

    [Fact]
    public void ExceptionVector_IsInKText() {
        Assert.True(MemoryMap.KText.Contains(MemoryMap.ExceptionVector, 4));
    }
}
