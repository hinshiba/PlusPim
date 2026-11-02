using PlusPim.Application;
using PlusPim.Cli;
using PlusPim.EditorController.DebugAdapter;
using PlusPim.EditorController.Transport;
using PlusPim.Logging;
using PlusPim.Terminal;
using System.CommandLine;
using System.Net;
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
        LogLevel minLevel = common.Verbose ? LogLevel.Debug : LogLevel.Info;
        Logger logger = new(minLevel);
        // stderrはデバッギーのものなので，verboseモードのときだけログを出す
        if(common.Verbose) {
            logger.AddSink((LogLevel level, string source, string msg) => Console.Error.WriteLine($"[{level}][{source}] {msg}"));
        }
        logger.Debug("Program", "Verbose mode enabled");
        logger.Info("Program", $"PlusPim version {version}");

        if(common.Attach) {
            _ = System.Diagnostics.Debugger.Launch();
        }

        logger.Debug("Program", "Debug Launch");

        IDapTransport transport = args.EstablishMethod switch {
            (EstablishMethod.Connect, IPEndPoint endpoint) => new ConnectTransport(endpoint, logger),
            (EstablishMethod.Listen, IPEndPoint endpoint) => new ListenTransport(endpoint, logger, Console.Error),
        };

        // 端末の符号化を切り替える
        using TerminalUtf8Guard terminalGuard = TerminalUtf8Guard.Apply(TerminalUtf8Backends.Create(), common.ForceTerminalUtf8);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => terminalGuard.Restore();

        Application.Application app = new(common.Files, logger, StreamDebuggeeIO.OpenConsole(), common.Strict);

        // stdin/stdoutはデバッギが使う
        Stream dap;
        try {
            dap = await transport.OpenAsync(ct);
        } catch(DapTransportException e) {
            Console.Error.WriteLine(e.Message);
            return 1;
        } catch(OperationCanceledException) {
            logger.Info("Program", "Operation canceled.");
            return 130;
        }
        await using Stream dapStream = dap;
        logger.Debug("Program", "DAP transport opened");

        DebugAdapter adapter = new(dapStream, dapStream, app, logger);
        await adapter.WaitForSessionEnd();

        logger.Info("Program", "Exit.");
        return 0;
    }

    private static int RunRuntime(CommonArgs _) {
        Console.Error.WriteLine("runtime is not implemented.");
        return 1;
    }
}
