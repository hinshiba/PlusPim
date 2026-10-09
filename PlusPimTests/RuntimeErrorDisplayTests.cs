using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.EditorController.DebugAdapter;
using Xunit;

namespace PlusPimTests;

/// <summary>
/// ランタイムエラーと MIPS の例外の表示の区別のテスト
/// </summary>
public class RuntimeErrorDisplayTests {
    private static RuntimeErrorInfo DivByZero(FileInfo? file = null, int line = 0) {
        return new RuntimeErrorInfo {
            Kind = RuntimeErrorKind.DivisionByZero,
            Description = "division by zero",
            Address = 0x00400010,
            SourceFile = file,
            Line = line
        };
    }

    private static ExceptionInfo Overflow(bool isDouble) {
        return new ExceptionInfo {
            Reason = ExcCode.Ov,
            ExceptionId = nameof(ExcCode.Ov),
            Description = "MIPS exception: Ov",
            IsDouble = isDouble
        };
    }

    [Fact]
    public void RuntimeError_TitleAndStateLabelSayPlusPimRuntimeError() {
        Assert.Equal("PlusPim runtime error (DivisionByZero)", RuntimeErrorDisplay.ExceptionId(DivByZero()));
        Assert.Equal("Paused on PlusPim runtime error", RuntimeErrorDisplay.StateLabel);
    }

    [Fact]
    public void RuntimeError_WidgetDescriptionExplainsItIsNotAMipsException() {
        string description = RuntimeErrorDisplay.WidgetDescription(DivByZero());

        Assert.StartsWith("division by zero", description);
        Assert.Contains("not a MIPS exception", description);
        Assert.Contains("cannot continue", description);
        Assert.Contains("Step Back", description);
    }

    [Fact]
    public void RuntimeError_ConsoleLineIncludesSourceLocation() {
        FileInfo file = new(Path.Combine(Path.GetTempPath(), "main.asm"));

        Assert.Equal(
            "[PlusPim runtime error] DivisionByZero at 0x00400010 (main.asm:12): division by zero. Execution cannot continue; use Step Back.\n",
            RuntimeErrorDisplay.ConsoleLine(DivByZero(file, 12))
        );
    }

    [Fact]
    public void RuntimeError_ConsoleLineOmitsUnknownSourceLocation() {
        Assert.Equal(
            "[PlusPim runtime error] DivisionByZero at 0x00400010: division by zero. Execution cannot continue; use Step Back.\n",
            RuntimeErrorDisplay.ConsoleLine(DivByZero())
        );
    }

    [Theory]
    [InlineData(false, "MIPS exception (Ov)", "Paused on MIPS exception")]
    [InlineData(true, "MIPS double exception (Ov)", "Paused on MIPS double exception")]
    public void MipsException_TitleAndStateLabelSayMipsException(bool isDouble, string expectedId, string expectedLabel) {
        ExceptionInfo info = Overflow(isDouble);

        Assert.Equal(expectedId, RuntimeErrorDisplay.MipsExceptionId(info));
        Assert.Equal(expectedLabel, RuntimeErrorDisplay.MipsExceptionStateLabel(info));
    }

    [Fact]
    public void GetRuntimeError_HasSourceLocationOfFaultingInstruction() {
        const string asm = """
            .text
            main:
              addiu $t0, $zero, 7
              div $t0, $zero
            """;
        (PlusPimDbg debugger, FileInfo tempFile) = TestHelpers.CreateDebugger(asm);
        try {
            _ = debugger.Step(); // addiu
            Assert.Equal(StopReason.RuntimeError, debugger.Step());

            RuntimeErrorInfo? error = debugger.GetRuntimeError();
            Assert.NotNull(error);
            Assert.Equal(tempFile.FullName, error.SourceFile?.FullName);
            Assert.Equal(4, error.Line);
            Assert.Contains($"({tempFile.Name}:4)", RuntimeErrorDisplay.ConsoleLine(error));
        } finally {
            tempFile.Delete();
        }
    }
}
