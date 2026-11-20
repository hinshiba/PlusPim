namespace PlusPim.Image.Tests;

public class SourcePathTests {
    private static readonly string Root = Path.GetTempPath();

    [Theory]
    [InlineData("")]
    [InlineData("a\0.asm")]
    public void TryResolve_RejectsEmptyAndNul(string raw) {
        Assert.False(SourcePath.TryResolve(raw, Root, out _));
    }

    [Fact]
    public void TryResolve_ResolvesRelativeAgainstBase() {
        Assert.True(SourcePath.TryResolve(Path.Combine("dir", "a.asm"), Root, out SourcePath relative));
        Assert.True(SourcePath.TryResolve(Path.Combine(Root, "dir", "a.asm"), Root, out SourcePath absolute));
        Assert.Equal(absolute, relative);
    }

    [Fact]
    public void TryResolve_IgnoresBaseForQualified() {
        string other = Path.Combine(Root, "other");
        Assert.True(SourcePath.TryResolve(Path.Combine(Root, "a.asm"), other, out SourcePath p));
        Assert.Equal(Path.Combine(Root, "a.asm"), p.Value);
    }

    [Fact]
    public void TryResolve_ThrowsOnNonQualifiedBase() {
        Assert.Throws<ArgumentException>(() => SourcePath.TryResolve("a.asm", "dir", out _));
    }

    [Fact]
    public void TryResolve_CollapsesDotSegments() {
        Assert.True(SourcePath.TryResolve(Path.Combine("x", "..", "a.asm"), Root, out SourcePath dotted));
        Assert.True(SourcePath.TryResolve("a.asm", Root, out SourcePath plain));
        Assert.Equal(plain, dotted);
    }

    [Fact]
    public void TryResolve_UppercasesDriveLetter() {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Drive letters exist only on Windows.");
        Assert.True(SourcePath.TryResolve(@"d:\dev\a.asm", Root, out SourcePath lower));
        Assert.True(SourcePath.TryResolve(@"D:\dev\a.asm", Root, out SourcePath upper));
        Assert.Equal(upper, lower);
        Assert.Equal(@"D:\dev\a.asm", lower.Value);
    }
}
