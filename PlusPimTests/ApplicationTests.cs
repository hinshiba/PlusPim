using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using PlusPim.Logging;
using Xunit;
using App = PlusPim.Application.Application;

namespace PlusPimTests;

/// <summary>
/// <see cref="App"/> の実行操作がランタイムエラーで停止することのテスト
/// (doc/tests/instructions/runtime_error_model.md)
/// </summary>
public class ApplicationTests {
    private const uint TextBase = 0x00400000;

    /// <summary>
    /// サブルーチン <c>sub</c> の中でゼロ除算する．<c>div</c> は <see cref="TextBase"/> + 12 にある
    /// </summary>
    private const string DivByZeroInSub = """
        .text
        main:
          addiu $t0, $zero, 7
          jal sub
          addiu $t1, $zero, 1
        sub:
          div $t0, $zero
          jr $ra
        """;

    private const uint DivAddress = TextBase + 12;

    private static void WithApplication(string asm, Action<App> test) {
        FileInfo tempFile = TestHelpers.WriteTempAsm(asm);
        try {
            App app = new([tempFile], Logger.Null);
            Assert.True(app.Load());
            test(app);
        } finally {
            tempFile.Delete();
        }
    }

    private static void AssertStoppedAtDiv(App app) {
        RuntimeErrorInfo? error = app.GetRuntimeError();
        Assert.NotNull(error);
        Assert.Equal(RuntimeErrorKind.DivisionByZero, error.Kind);
        Assert.Equal(DivAddress, error.Address);
        Assert.Equal(DivAddress, app.GetCallStack()[0].PC);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Continue_StopsAtRuntimeError(bool emptyFilters) {
        WithApplication(DivByZeroInSub, app => {
            if(emptyFilters) {
                // 例外フィルタによらず停止する
                app.SetExceptionFilters([]);
            }

            Assert.Equal(StopReason.RuntimeError, app.Continue());
            AssertStoppedAtDiv(app);

            // 先へは進めない
            Assert.Equal(StopReason.RuntimeError, app.Continue());
            AssertStoppedAtDiv(app);
        });
    }

    [Fact]
    public void StepOver_StopsAtRuntimeError() {
        WithApplication(DivByZeroInSub, app => {
            Assert.Equal(StopReason.Step, app.StepIn()); // addiu

            // jal をステップオーバーすると，サブルーチン内のランタイムエラーで止まる
            Assert.Equal(StopReason.RuntimeError, app.StepOver());
            AssertStoppedAtDiv(app);
        });
    }

    [Fact]
    public void StepOut_StopsAtRuntimeError() {
        WithApplication(DivByZeroInSub, app => {
            Assert.Equal(StopReason.Step, app.StepIn()); // addiu
            Assert.Equal(StopReason.Step, app.StepIn()); // jal

            Assert.Equal(StopReason.RuntimeError, app.StepOut());
            AssertStoppedAtDiv(app);
        });
    }

    [Fact]
    public void StepIn_ReturnsRuntimeError() {
        WithApplication(DivByZeroInSub, app => {
            Assert.Equal(StopReason.Step, app.StepIn()); // addiu
            Assert.Equal(StopReason.Step, app.StepIn()); // jal

            Assert.Equal(StopReason.RuntimeError, app.StepIn());
            AssertStoppedAtDiv(app);

            // StepBack でランタイムエラーを取り消せる
            Assert.True(app.StepBack());
            Assert.Null(app.GetRuntimeError());
            Assert.Equal(DivAddress, app.GetCallStack()[0].PC);
        });
    }
}
