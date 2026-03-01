using PlusPim.Debuggers.PlusPimDbg.Program;
using Xunit;

namespace PlusPimTests;

public class TextSegmentBuilderTests {
    [Fact]
    public void AddLine_ValidInstruction_AddsToSegment() {
        TextSegmentBuilder b = new(_ => { });
        b.AddLine("add $t0, $t1, $t2", 0);
        _ = Assert.Single(b.Build().Instructions.ToArray());
    }

    [Fact]
    public void AddLine_DotDirective_IsSkipped() {
        TextSegmentBuilder b = new(_ => { });
        b.AddLine(".globl main", 0);
        Assert.Empty(b.Build().Instructions.ToArray());
    }

    [Fact]
    public void CurrentInstructionIndex_MatchesInstructionCount() {
        TextSegmentBuilder b = new(_ => { });
        Assert.Equal(0, b.CurrentInstructionIndex().Idx);
        b.AddLine("add $t0, $t1, $t2", 0);
        Assert.Equal(1, b.CurrentInstructionIndex().Idx);
        b.AddLine("sub $t0, $t1, $t2", 1);
        Assert.Equal(2, b.CurrentInstructionIndex().Idx);
    }

    [Fact]
    public void FreshBuilder_HasZeroInstructions() {
        TextSegmentBuilder b = new(_ => { });
        Assert.Empty(b.Build().Instructions.ToArray());
    }

    [Fact]
    public void AddLine_MultipleInstructions_AllAdded() {
        TextSegmentBuilder b = new(_ => { });
        b.AddLine("add $t0, $t1, $t2", 0);
        b.AddLine("sub $t0, $t1, $t2", 1);
        b.AddLine("add $t0, $t1, $t2", 2);
        Assert.Equal(3, b.Build().Instructions.Length);
    }

    [Fact]
    public void AddLine_DataDirective_IsSkipped() {
        TextSegmentBuilder b = new(_ => { });
        b.AddLine(".data", 1);
        Assert.Empty(b.Build().Instructions.ToArray());
    }

    [Fact]
    public void AddLine_TextDirective_IsSkipped() {
        TextSegmentBuilder b = new(_ => { });
        b.AddLine(".text", 1);
        Assert.Empty(b.Build().Instructions.ToArray());
    }

    [Fact]
    public void AddLine_UnknownMnemonic_DoesNotAddInstruction() {
        TextSegmentBuilder b = new(_ => { });
        b.AddLine("notamnemonic $t0, $t1, $t2", 0);
        Assert.Empty(b.Build().Instructions.ToArray());
    }

    [Fact]
    public void AddLine_UnknownMnemonic_DoesNotAdvanceInstructionIndex() {
        TextSegmentBuilder b = new(_ => { });
        b.AddLine("notamnemonic $t0, $t1, $t2", 0);
        Assert.Equal(0, b.CurrentInstructionIndex().Idx);
    }

    [Fact]
    public void AddLine_DotDirective_DoesNotAdvanceInstructionIndex() {
        TextSegmentBuilder b = new(_ => { });
        b.AddLine(".globl main", 1);
        Assert.Equal(0, b.CurrentInstructionIndex().Idx);
    }
}
