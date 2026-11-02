using PlusPim.Cli;
using System.CommandLine;
using Xunit;

namespace PlusPimTests;

/// <summary>
/// コマンドラインの定義 <see cref="PlusPimCommand"/> のテスト (doc/tests/commandline/commandline_model.md)
/// </summary>
/// <remarks>引数は空白区切りの1文字列で与える．<c>&lt;file&gt;</c> は存在しなくてよい</remarks>
public class CommandLineTests {
    private const string Version = "9.8.7-test+abc";

    private const string VersionLine = $"PlusPim {Version}";

    private const string Hint = "Run 'pluspim --help' for usage.";

    private static readonly string[] CommonOptions = ["--verbose", "-v", "--strict", "--force-terminal-utf8", "--attach"];

    /// <summary>
    /// 呼び出しの結果とアクションが受け取った引数．呼ばれなかったアクションの引数はnull
    /// </summary>
    private sealed record Invocation(int ExitCode, string Output, string Error, DebugArgs? Debug, CommonArgs? Runtime);

    /// <summary>
    /// パースエラーになる引数
    /// </summary>
    public static TheoryData<string> InvalidArgs => new() {
        // サブコマンドなし
        "",
        "a.s",
        "--verbose a.s",
        // 未知のサブコマンド
        "foo a.s",
        // <file> が0個
        "debug --listen 127.0.0.1:0",
        "runtime",
        "runtime --strict",
        // runtime に --connect / --listen
        "runtime --connect 127.0.0.1:1 a.s",
        "runtime --listen 127.0.0.1:0 a.s",
        // debug に --connect / --listen がどちらもなし，または両方
        "debug a.s",
        "debug ./-main.s",
        "debug --connect 127.0.0.1:1 --listen 127.0.0.1:0 a.s",
        // --connect / --listen の値が不正
        "debug --connect",
        "debug --connect nonsense a.s",
        "debug --connect 127.0.0.1:0 a.s",
        "debug --connect --listen 127.0.0.1:0 a.s",
        "debug --listen 127.0.0.1:x a.s",
        "debug --connect 127.0.0.1 a.s",
        "debug --connect 127.0.0.1:65536 a.s",
        "debug --connect 127.0.0.1:+1 a.s",
        "debug --connect [::1] a.s",
        "debug --connect []:1 a.s",
        // ip は IP アドレスのリテラルに限る．IPv6 は括弧が必須，IPv4 は省略形を許さない
        "debug --connect localhost:1 a.s",
        "debug --connect example.com:1 a.s",
        "debug --listen localhost:0 a.s",
        "debug --connect ::1:5 a.s",
        "debug --connect [127.0.0.1]:1 a.s",
        "debug --connect 127.1:1 a.s",
        // 未知のオプションと廃止された -d / --debug
        "--foo debug a.s",
        "debug --foo a.s",
        "runtime a.s --foo",
        "-d debug a.s",
        "debug -d a.s",
        "runtime --debug a.s",
        // '-' で始まる <file>
        "debug -main.s",
        "runtime a.s -main.s",
    };

    private static string[] Split(string args) {
        return args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static RootCommand Create(int exitCode, Action<DebugArgs>? onDebug = null, Action<CommonArgs>? onRuntime = null) {
        return PlusPimCommand.Create(
            Version,
            (a, _) => {
                onDebug?.Invoke(a);
                return Task.FromResult(exitCode);
            },
            a => {
                onRuntime?.Invoke(a);
                return exitCode;
            });
    }

    private static async Task<Invocation> InvokeAsync(string args, int exitCode = 0) {
        DebugArgs? debug = null;
        CommonArgs? runtime = null;
        RootCommand root = Create(exitCode, a => debug = a, a => runtime = a);
        StringWriter output = new();
        StringWriter error = new();
        // 既定では Ctrl+C のハンドラを実際のコンソールへ登録してしまう
        InvocationConfiguration configuration = new() { Output = output, Error = error, ProcessTerminationTimeout = null };
        int code = await PlusPimCommand.InvokeAsync(root, Split(args), configuration);
        return new Invocation(code, output.ToString(), error.ToString(), debug, runtime);
    }

    private static void AssertNoAction(Invocation result) {
        Assert.Null(result.Debug);
        Assert.Null(result.Runtime);
    }

    private static void AssertFiles(string expected, FileInfo[] actual) {
        Assert.Equal(Split(expected).Select(Path.GetFullPath), actual.Select(f => f.FullName));
    }

    [Theory]
    [MemberData(nameof(InvalidArgs))]
    public void Parse_InvalidArgs_HasErrors(string args) {
        Assert.NotEmpty(Create(0).Parse(Split(args)).Errors);
    }

    [Theory]
    [InlineData("debug --connect 127.0.0.1:1 a.s")]
    [InlineData("debug --listen 127.0.0.1:0 a.s")]
    [InlineData("runtime a.s b.s")]
    [InlineData("debug --listen 127.0.0.1:0 ./-main.s")]
    [InlineData("runtime ./-main.s")]
    public void Parse_ValidArgs_HasNoErrors(string args) {
        Assert.Empty(Create(0).Parse(Split(args)).Errors);
    }

    [Theory]
    [InlineData("debug --connect 127.0.0.1:1 c.s a.s b.s", "Connect", "127.0.0.1:1", "c.s a.s b.s")]
    [InlineData("debug --connect [::1]:5 a.s", "Connect", "[::1]:5", "a.s")]
    [InlineData("debug b.s --listen 127.0.0.1:0 a.s", "Listen", "127.0.0.1:0", "b.s a.s")]
    [InlineData("debug --listen 127.0.0.1:4711 ./-main.s", "Listen", "127.0.0.1:4711", "./-main.s")]
    [InlineData("debug --listen [::1]:0 a.s", "Listen", "[::1]:0", "a.s")]
    [InlineData("debug --connect 127.0.0.1:65535 a.s", "Connect", "127.0.0.1:65535", "a.s")]
    public async Task Invoke_Debug_PassesDebugArgs(string args, string method, string endpoint, string files) {
        Invocation result = await InvokeAsync(args);
        Assert.Null(result.Runtime);
        Assert.NotNull(result.Debug);
        AssertFiles(files, result.Debug.Common.Files);
        Assert.Equal(method, result.Debug.EstablishMethod.Method.ToString());
        Assert.Equal(endpoint, result.Debug.EstablishMethod.Endpoint.ToString());
    }

    [Theory]
    [InlineData("debug a.s")]
    [InlineData("debug --connect 127.0.0.1:1 --listen 127.0.0.1:0 a.s")]
    [InlineData("debug --connect nonsense a.s")]
    [InlineData("debug --connect 127.0.0.1:0 a.s")]
    [InlineData("debug --listen 127.0.0.1:x a.s")]
    [InlineData("debug --listen localhost:0 a.s")]
    public void Parse_InvalidTransport_HasSingleError(string args) {
        _ = Assert.Single(Create(0).Parse(Split(args)).Errors);
    }

    [Theory]
    [InlineData("runtime b.s a.s", "b.s a.s")]
    [InlineData("runtime ./-main.s", "./-main.s")]
    public async Task Invoke_Runtime_PassesCommonArgs(string args, string files) {
        Invocation result = await InvokeAsync(args);
        Assert.Null(result.Debug);
        Assert.NotNull(result.Runtime);
        AssertFiles(files, result.Runtime.Files);
    }

    [Theory]
    [InlineData("debug --listen 127.0.0.1:0 a.s", false, false, false, false)]
    [InlineData("runtime a.s", false, false, false, false)]
    [InlineData("-v --strict debug --listen 127.0.0.1:0 a.s", true, true, false, false)]
    [InlineData("--force-terminal-utf8 --attach runtime a.s", false, false, true, true)]
    [InlineData("debug --verbose --attach --listen 127.0.0.1:0 a.s", true, false, false, true)]
    [InlineData("runtime --strict --force-terminal-utf8 a.s", false, true, true, false)]
    [InlineData("debug --listen 127.0.0.1:0 a.s -v --force-terminal-utf8", true, false, true, false)]
    [InlineData("runtime a.s --strict --attach", false, true, false, true)]
    [InlineData("--attach debug --strict --listen 127.0.0.1:0 a.s --verbose --force-terminal-utf8", true, true, true, true)]
    [InlineData("--verbose runtime --attach a.s --strict --force-terminal-utf8", true, true, true, true)]
    public async Task Invoke_CommonOptions_SetFlagsAtAnyPosition(string args, bool verbose, bool strict, bool forceTerminalUtf8, bool attach) {
        Invocation result = await InvokeAsync(args);
        CommonArgs? common = result.Debug?.Common ?? result.Runtime;
        Assert.NotNull(common);
        Assert.Equal((verbose, strict, forceTerminalUtf8, attach), (common.Verbose, common.Strict, common.ForceTerminalUtf8, common.Attach));
    }

    [Theory]
    [InlineData("debug --listen 127.0.0.1:0 a.s", 0)]
    [InlineData("debug --listen 127.0.0.1:0 a.s", 3)]
    [InlineData("runtime a.s", 0)]
    [InlineData("runtime a.s", 42)]
    public async Task Invoke_ReturnsActionExitCode(string args, int exitCode) {
        Invocation result = await InvokeAsync(args, exitCode);
        Assert.Equal(exitCode, result.ExitCode);
    }

    [Theory]
    [MemberData(nameof(InvalidArgs))]
    public async Task Invoke_ParseError_PrintsHintAndReturnsOne(string args) {
        // 既定の終了コードを 1 以外にして，アクションの戻り値と区別する
        Invocation result = await InvokeAsync(args, exitCode: 7);
        Assert.Equal(1, result.ExitCode);
        AssertNoAction(result);
        Assert.Empty(result.Output);
        string[] lines = result.Error.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2, result.Error);
        Assert.Equal(Hint, lines[^1]);
    }

    [Theory]
    [InlineData("-V")]
    [InlineData("--version")]
    public async Task Invoke_Version_PrintsVersionLineOnly(string args) {
        Invocation result = await InvokeAsync(args, exitCode: 7);
        Assert.Equal(0, result.ExitCode);
        AssertNoAction(result);
        Assert.Equal(VersionLine + Environment.NewLine, result.Output);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("-?")]
    public async Task Invoke_RootHelp_ListsSubcommands(string args) {
        Invocation result = await InvokeAsync(args, exitCode: 7);
        Assert.Equal(0, result.ExitCode);
        AssertNoAction(result);
        Assert.StartsWith(VersionLine + Environment.NewLine, result.Output);
        Assert.Contains("debug", result.Output);
        Assert.Contains("runtime", result.Output);
    }

    [Theory]
    [InlineData("debug -h", true)]
    [InlineData("debug --help", true)]
    [InlineData("debug -?", true)]
    [InlineData("runtime -h", false)]
    [InlineData("runtime --help", false)]
    [InlineData("runtime -?", false)]
    public async Task Invoke_SubcommandHelp_ListsOptions(string args, bool hasTransport) {
        Invocation result = await InvokeAsync(args, exitCode: 7);
        Assert.Equal(0, result.ExitCode);
        AssertNoAction(result);
        Assert.StartsWith(VersionLine + Environment.NewLine, result.Output);
        Assert.All(CommonOptions, o => Assert.Contains(o, result.Output));
        Assert.Equal(hasTransport, result.Output.Contains("--connect"));
        Assert.Equal(hasTransport, result.Output.Contains("--listen"));
    }
}
