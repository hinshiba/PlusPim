using PlusPim.Instructions;

namespace PlusPim.Image.Tests;

public class SourceMapTests {
    private static readonly SourcePath A = Path("a.asm");
    private static readonly SourcePath B = Path("b.asm");
    private static readonly Address T = MemoryMap.Text.Base;

    private static readonly LineSpan Add = new(new(A, 3), T, 1, null);
    private static readonly LineSpan La = new(new(A, 4), T + 4u, 2, "la");
    private static readonly LineSpan Li = new(new(B, 1), T + 0x100u, 1, "li");

    private static readonly SourceMap Map = SourceMap.Create([Li, La, Add]);

    private static SourcePath Path(string name) {
        Assert.True(SourcePath.TryResolve(name, System.IO.Path.GetTempPath(), out SourcePath p));
        return p;
    }

    [Fact]
    public void LocationOf_CoversWholeExpansion() {
        Assert.Equal(Add.Location, Map.LocationOf(T));
        Assert.Equal(La.Location, Map.LocationOf(T + 4u));
        Assert.Equal(La.Location, Map.LocationOf(T + 8u));
        Assert.Null(Map.LocationOf(T + 12u));
        Assert.Null(Map.LocationOf(T - 4u));
    }

    [Fact]
    public void StartsLine_OnlyFirstInstructionOfEachLine() {
        Assert.True(Map.StartsLine(T));
        Assert.True(Map.StartsLine(T + 4u));
        Assert.False(Map.StartsLine(T + 8u));
        Assert.False(Map.StartsLine(T + 12u));
        Assert.False(Map.StartsLine(T - 4u));
    }

    [Fact]
    public void AddressOf_ReturnsFirstInstructionOfLine() {
        Assert.Equal(T + 4u, Map.AddressOf(La.Location));
        Assert.Null(Map.AddressOf(new(A, 5)));
    }

    [Fact]
    public void PseudosIn_FiltersByFile() {
        Assert.Equal([La], Map.PseudosIn(A));
        Assert.Equal([Li], Map.PseudosIn(B));
    }

    [Fact]
    public void Create_RejectsBrokenInvariants() {
        Assert.Throws<ArgumentException>(() => SourceMap.Create([La, Add with { Start = T + 8u }]));
        Assert.Throws<ArgumentException>(() => SourceMap.Create([Add with { Count = 0 }]));
        Assert.Throws<ArgumentException>(() => SourceMap.Create([Add, Li with { Location = Add.Location }]));
    }
}
