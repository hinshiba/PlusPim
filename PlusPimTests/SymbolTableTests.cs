using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using Xunit;

namespace PlusPimTests;

public class SymbolTableTests {
    [Fact]
    public void Resolve_AddedLabel_ReturnsLabel() {
        SymbolTable table = new();
        table.Add(new Label("main", new Address(0x400000)));
        Label? result = table.Resolve("main");
        _ = Assert.NotNull(result);
        Assert.Equal("main", result.Value.Name);
        Assert.Equal(0x400000, result.Value.Addr.Addr);
    }

    [Fact]
    public void Resolve_MissingLabel_ReturnsNull() {
        SymbolTable table = new();
        Assert.Null(table.Resolve("missing"));
    }

    [Fact]
    public void Add_DuplicateLabel_LastWins() {
        SymbolTable table = new();
        table.Add(new Label("foo", new Address(0x10)));
        table.Add(new Label("foo", new Address(0x20)));
        Label? result = table.Resolve("foo");
        _ = Assert.NotNull(result);
        Assert.Equal(0x20, result.Value.Addr.Addr);
    }

    [Fact]
    public void Resolve_MultipleDistinctLabels_AllReturnCorrectAddress() {
        SymbolTable table = new();
        table.Add(new Label("alpha", new Address(0x100)));
        table.Add(new Label("beta", new Address(0x200)));
        table.Add(new Label("gamma", new Address(0x300)));
        Label? alpha = table.Resolve("alpha");
        Label? beta = table.Resolve("beta");
        Label? gamma = table.Resolve("gamma");
        _ = Assert.NotNull(alpha);
        _ = Assert.NotNull(beta);
        _ = Assert.NotNull(gamma);
        Assert.Equal(0x100, alpha.Value.Addr.Addr);
        Assert.Equal(0x200, beta.Value.Addr.Addr);
        Assert.Equal(0x300, gamma.Value.Addr.Addr);
    }

    [Fact]
    public void Resolve_IsCaseSensitive_DifferentCaseReturnsNull() {
        SymbolTable table = new();
        table.Add(new Label("main", new Address(0x400000)));
        Assert.Null(table.Resolve("Main"));
        Assert.Null(table.Resolve("MAIN"));
    }

    [Fact]
    public void Add_ThenResolve_LabelNamePreserved() {
        SymbolTable table = new();
        table.Add(new Label("loop", new Address(0x400008)));
        Label? result = table.Resolve("loop");
        _ = Assert.NotNull(result);
        Assert.Equal("loop", result.Value.Name);
    }

    [Fact]
    public void Add_ThenResolve_AddressPreserved() {
        SymbolTable table = new();
        table.Add(new Label("exit", new Address(0xABCD)));
        Label? result = table.Resolve("exit");
        _ = Assert.NotNull(result);
        Assert.Equal(0xABCD, result.Value.Addr.Addr);
    }
}
