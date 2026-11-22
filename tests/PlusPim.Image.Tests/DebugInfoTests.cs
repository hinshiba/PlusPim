using PlusPim.Instructions;

namespace PlusPim.Image.Tests;

public class DebugInfoTests {
    private static readonly Address T = MemoryMap.Text.Base;

    private static DebugInfo Create(params Symbol[] symbols) {
        return DebugInfo.Create(SourceMap.Create([]), symbols);
    }

    [Fact]
    public void SymbolAt_PrefersGlobalThenFirstDefinition() {
        DebugInfo info = Create(
            new("local1", T, false),
            new("local2", T, false),
            new("global1", T, true),
            new("global2", T, true),
            new("only", T + 4u, false));

        Assert.Equal("global1", info.SymbolAt(T));
        Assert.Equal("only", info.SymbolAt(T + 4u));
        Assert.Null(info.SymbolAt(T + 8u));
    }
}
