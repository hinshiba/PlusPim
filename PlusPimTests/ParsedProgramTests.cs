using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Logging;
using Xunit;

namespace PlusPimTests;

public class ParsedProgramTests {
    private static string WriteTempAsm(string content) {
        string path = Path.ChangeExtension(Path.GetTempFileName(), ".s");
        File.WriteAllText(path, content);
        return path;
    }

    // RemoveComment indirect tests

    [Fact]
    public void InstructionWithComment_IsParsedAsInstruction() {
        string path = WriteTempAsm("add $t0, $t1, $t2 # comment\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Assert.Equal(1, prog.InstructionCount);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void LineWithOnlyComment_IsSkipped() {
        string path = WriteTempAsm("# only comment\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Assert.Equal(0, prog.InstructionCount);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void HashInsideStringLiteral_IsNotRemovedAsComment() {
        string path = WriteTempAsm(".data\n.ascii \"hello # world\"\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            // "hello # world" is 13 chars; # and everything after must not be stripped
            Assert.Equal(13, prog.DataSegment.MemoryImage.Count);
        } finally {
            File.Delete(path);
        }
    }

    // IsLabel indirect tests

    [Fact]
    public void Label_IsRegisteredInSymbolTable() {
        string path = WriteTempAsm("main:\nadd $t0, $t1, $t2\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            _ = Assert.NotNull(prog.SymbolTable.Resolve("main"));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void LineWithColonAndSpace_IsNotTreatedAsLabel() {
        string path = WriteTempAsm("add $t0:\n");
        try {
            // "add $t0:" has a space → IsLabel returns false → not added to symbol table
            ParsedProgram prog = new(path, Logger.Null);
            Assert.Null(prog.SymbolTable.Resolve("add $t0"));
        } finally {
            File.Delete(path);
        }
    }

    // Integration tests

    [Fact]
    public void TextOnlyProgram_InstructionCountMatchesLines() {
        string path = WriteTempAsm("add $t0, $t1, $t2\nsub $t0, $t1, $t2\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Assert.Equal(2, prog.InstructionCount);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void TextAndDataSegments_AreSeparatedCorrectly() {
        string path = WriteTempAsm(".text\nadd $t0, $t1, $t2\n.data\n.byte 0x42\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Assert.Equal(1, prog.InstructionCount);
            _ = Assert.Single(prog.DataSegment.MemoryImage);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void TextLabel_PointsToFirstInstruction() {
        string path = WriteTempAsm("main:\nadd $t0, $t1, $t2\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Label? label = prog.SymbolTable.Resolve("main");
            _ = Assert.NotNull(label);
            Address expected = Address.FromInstructionIndex(new InstructionIndex(0));
            Assert.Equal(expected, label.Value.Addr);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void TextLabel_AfterFirstInstruction_PointsToSecond() {
        string path = WriteTempAsm("add $t0, $t1, $t2\nhoge:\nsub $t0, $t1, $t2\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Label? label = prog.SymbolTable.Resolve("hoge");
            _ = Assert.NotNull(label);
            Address expected = Address.FromInstructionIndex(new InstructionIndex(1));
            Assert.Equal(expected, label.Value.Addr);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void DataLabel_HasDataSegmentBaseAddress() {
        string path = WriteTempAsm(".data\nhello:\n.byte 0x41\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Label? label = prog.SymbolTable.Resolve("hello");
            _ = Assert.NotNull(label);
            Assert.Equal(DataSegment.DataSegmentBase, label.Value.Addr);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void EmptyFile_HasZeroInstructions() {
        string path = WriteTempAsm("");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Assert.Equal(0, prog.InstructionCount);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void EmptyFile_HasEmptyDataSegment() {
        string path = WriteTempAsm("");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Assert.Empty(prog.DataSegment.MemoryImage);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void BlankOnlyLines_AreSkipped() {
        string path = WriteTempAsm("   \n\n   \n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Assert.Equal(0, prog.InstructionCount);
            Assert.Empty(prog.DataSegment.MemoryImage);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void TextDirectiveAlone_CountsNoInstructions() {
        string path = WriteTempAsm(".text\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Assert.Equal(0, prog.InstructionCount);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void DataDirectiveAlone_HasEmptyMemoryImage() {
        string path = WriteTempAsm(".data\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Assert.Empty(prog.DataSegment.MemoryImage);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ConsecutiveLabels_BothPointToSameInstructionIndex() {
        string path = WriteTempAsm("foo:\nbar:\nadd $t0, $t1, $t2\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Label? foo = prog.SymbolTable.Resolve("foo");
            Label? bar = prog.SymbolTable.Resolve("bar");
            _ = Assert.NotNull(foo);
            _ = Assert.NotNull(bar);
            Assert.Equal(foo.Value.Addr, bar.Value.Addr);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void DataLabel_AfterFirstByte_PointsToOffsetAddress() {
        string path = WriteTempAsm(".data\nfirst:\n.byte 0x41\nsecond:\n.byte 0x42\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Label? second = prog.SymbolTable.Resolve("second");
            _ = Assert.NotNull(second);
            Assert.Equal(DataSegment.DataSegmentBase.Addr + 1, second.Value.Addr.Addr);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void MultipleTextLabels_AllResolvable() {
        string path = WriteTempAsm("alpha:\nadd $t0, $t1, $t2\nbeta:\nsub $t0, $t1, $t2\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            _ = Assert.NotNull(prog.SymbolTable.Resolve("alpha"));
            _ = Assert.NotNull(prog.SymbolTable.Resolve("beta"));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void InterleavedSections_TextInstructionsCountedCorrectly() {
        string path = WriteTempAsm(".text\nadd $t0, $t1, $t2\n.data\n.byte 0x01\n.text\nsub $t0, $t1, $t2\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Assert.Equal(2, prog.InstructionCount);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void InterleavedSections_DataBytesCountedCorrectly() {
        string path = WriteTempAsm(".text\nadd $t0, $t1, $t2\n.data\n.byte 0x01\n.text\nsub $t0, $t1, $t2\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            _ = Assert.Single(prog.DataSegment.MemoryImage);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void CommentOnlyFile_HasZeroInstructionsAndEmptyData() {
        string path = WriteTempAsm("# this is a comment\n# another comment\n");
        try {
            ParsedProgram prog = new(path, Logger.Null);
            Assert.Equal(0, prog.InstructionCount);
            Assert.Empty(prog.DataSegment.MemoryImage);
        } finally {
            File.Delete(path);
        }
    }
}
