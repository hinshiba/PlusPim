using PlusPim.Instructions;

namespace PlusPim.Image.Tests;

public class MemoryMapTests {
    [Theory]
    [InlineData(0x003FFFFFu, null)]
    [InlineData(0x00400000u, SegmentKind.Text)]
    [InlineData(0x0FFFFFFFu, SegmentKind.Text)]
    [InlineData(0x10000000u, SegmentKind.Data)]
    [InlineData(0x7FFFFFFFu, SegmentKind.Data)]
    [InlineData(0x80000000u, SegmentKind.KText)]
    [InlineData(0x8FFFFFFFu, SegmentKind.KText)]
    [InlineData(0x90000000u, null)]
    public void KindOf_FollowsBoundaries(uint addr, SegmentKind? kind) {
        Assert.Equal(kind, MemoryMap.KindOf(new Address(addr)));
    }

    [Fact]
    public void Of_ReturnsSegmentContainingItsBase() {
        foreach(SegmentKind kind in Enum.GetValues<SegmentKind>()) {
            Assert.Equal(kind, MemoryMap.KindOf(MemoryMap.Of(kind).Base));
        }
    }
}
