using PlusPim.Adapter.Controller.Dap;
using PlusPim.Adapter.Logging;
using PlusPim.Terminal;
using PlusPim.Usecase;
using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
namespace PlusPim;

internal class Program {

    private static Task<int> Main(string[] args) {
        string version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        RootCommand cmd = PlusPimCommand.Create(version, (a, ct) => RunDebugAsync(a, version, ct), RunRuntime);
        return PlusPimCommand.InvokeAsync(cmd, args, new InvocationConfiguration());
    }

    private static async Task<int> RunDebugAsync(DebugArgs args, string version, CancellationToken ct) {
        CommonArgs common = args.Common;
        Logger logger = new(common.Verbose ? LogLevel.Debug : LogLevel.Info);
        // stderrはデバッギのものなので, verboseのときだけログを出す
        if(common.Verbose) {
            logger.AddSink((level, source, message) => Console.Error.WriteLine($"[{level}][{source}] {message}"));
        }
        logger.Log(LogLevel.Info, nameof(Program), $"PlusPim version {version}");

        if(common.Attach) {
            _ = Debugger.Launch();
        }

        // 端末の符号化を切り替える
        using TerminalUtf8Guard terminalGuard = TerminalUtf8Guard.Apply(TerminalUtf8Backends.Create(), common.ForceTerminalUtf8);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => terminalGuard.Restore();

        Stream dap;
        try {
            dap = await (args.Method switch {
                Connect c => Transport.ConnectAsync(c.Endpoint, Transport.ConnectTimeout, ct),
                Listen l => Transport.ListenAsync(l.Endpoint, Console.Error, ct),
            });
        } catch(DapTransportException e) {
            Console.Error.WriteLine(e.Message);
            return 1;
        } catch(OperationCanceledException) {
            logger.Log(LogLevel.Info, nameof(Program), "Operation canceled.");
            return 130;
        }
        await using Stream dapStream = dap;

        // stdin/stdoutはデバッギが使う
        Stream input = Console.OpenStandardInput();
        Stream output = Console.OpenStandardOutput();
        DapConnection connection = new(dapStream, files => Session.Load(files, FileSourceReader.Instance, common.Strict, common.Slots, input, output));
        logger.AddSink(connection.PostLog);
        logger.Log(LogLevel.Debug, nameof(Program), "DAP transport opened");
        await connection.RunAsync();

        logger.Log(LogLevel.Info, nameof(Program), "Exit.");
        return 0;
    }

    private static int RunRuntime(RuntimeArgs _) {
        Console.Error.WriteLine("runtime is not implemented.");
        return 1;
    }
}
