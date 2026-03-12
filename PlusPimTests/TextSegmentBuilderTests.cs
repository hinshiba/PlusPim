using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Logging;
using Xunit;

namespace PlusPimTests;

public class TextSegmentBuilderTests {
    private static readonly SymbolTable EmptySymbolTable = new();

    [Fact]
    public void AddLine_ValidInstruction_AddsToSegment() {
        TextSegmentBuilder b = new(Logger.Null);
        b.AddLine("add $t0, $t1, $t2", 0, EmptySymbolTable);
        _ = Assert.Single(b.Build().Instructions.ToArray());
    }

    [Fact]
    public void AddLine_DotDirective_IsSkipped() {
        TextSegmentBuilder b = new(Logger.Null);
        b.AddLine(".globl main", 0, EmptySymbolTable);
        Assert.Empty(b.Build().Instructions.ToArray());
    }

    [Fact]
    public void CurrentInstructionIndex_MatchesInstructionCount() {
        TextSegmentBuilder b = new(Logger.Null);
        Assert.Equal(0, b.CurrentInstructionIndex().Idx);
        b.AddLine("add $t0, $t1, $t2", 0, EmptySymbolTable);
        Assert.Equal(1, b.CurrentInstructionIndex().Idx);
        b.AddLine("sub $t0, $t1, $t2", 1, EmptySymbolTable);
        Assert.Equal(2, b.CurrentInstructionIndex().Idx);
    }

    [Fact]
    public void FreshBuilder_HasZeroInstructions() {
        TextSegmentBuilder b = new(Logger.Null);
        Assert.Empty(b.Build().Instructions.ToArray());
    }

    [Fact]
    public void AddLine_MultipleInstructions_AllAdded() {
        TextSegmentBuilder b = new(Logger.Null);
        b.AddLine("add $t0, $t1, $t2", 0, EmptySymbolTable);
        b.AddLine("sub $t0, $t1, $t2", 1, EmptySymbolTable);
        b.AddLine("add $t0, $t1, $t2", 2, EmptySymbolTable);
        Assert.Equal(3, b.Build().Instructions.Length);
    }

    [Fact]
    public void AddLine_DataDirective_IsSkipped() {
        TextSegmentBuilder b = new(Logger.Null);
        b.AddLine(".data", 1, EmptySymbolTable);
        Assert.Empty(b.Build().Instructions.ToArray());
    }

    [Fact]
    public void AddLine_TextDirective_IsSkipped() {
        TextSegmentBuilder b = new(Logger.Null);
        b.AddLine(".text", 1, EmptySymbolTable);
        Assert.Empty(b.Build().Instructions.ToArray());
    }

    [Fact]
    public void AddLine_UnknownMnemonic_DoesNotAddInstruction() {
        TextSegmentBuilder b = new(Logger.Null);
        b.AddLine("notamnemonic $t0, $t1, $t2", 0, EmptySymbolTable);
        Assert.Empty(b.Build().Instructions.ToArray());
    }

    [Fact]
    public void AddLine_UnknownMnemonic_DoesNotAdvanceInstructionIndex() {
        TextSegmentBuilder b = new(Logger.Null);
        b.AddLine("notamnemonic $t0, $t1, $t2", 0, EmptySymbolTable);
        Assert.Equal(0, b.CurrentInstructionIndex().Idx);
    }

    [Fact]
    public void AddLine_DotDirective_DoesNotAdvanceInstructionIndex() {
        TextSegmentBuilder b = new(Logger.Null);
        b.AddLine(".globl main", 1, EmptySymbolTable);
        Assert.Equal(0, b.CurrentInstructionIndex().Idx);
    }
}
