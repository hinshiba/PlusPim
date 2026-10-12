using PlusPim.Application;
using PlusPim.EditorController.DebugAdapter;
using PlusPim.EditorController.Transport;
using PlusPim.Logging;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;
namespace PlusPim;

internal class Program {

    private static async Task<int> Main(string[] args) {
        // コマンドライン引数処理
        RootCommand cmd = new("PlusPim - A MIPS runtime and debugger");

        Argument<FileInfo[]> fileArg = new(
            name: "file"
        ) {
            Description = "Path to the MIPS ASM File"
        };

        // 位置引数でないものはOption
        Option<bool> verboseArg = new(
            name: "--verbose",
            aliases: ["-v"]
            ) {
            Required = false,
            Description = "Start in verbose mode (default: false)",
            DefaultValueFactory = (_) => false
        };

        // 指定されている場合はデバッガモードで起動する
        Option<bool> debugArg = new(
            name: "--debug",
            aliases: ["-d"]
            ) {
            Required = false,
            Description = "Start in debug mode (default: false)",
            DefaultValueFactory = (_) => false
        };

        // --debug のときはどちらか一方が必須．検証は --debug の分岐で行う
        Option<string?> connectArg = new(
            name: "--connect"
            ) {
            Required = false,
            Description = "Connect to the DAP client listening at host:port (debug mode)"
        };

        Option<string?> listenArg = new(
            name: "--listen"
            ) {
            Required = false,
            Description = "Listen for the DAP client at host:port; port 0 picks a free port and the address is announced on stderr (debug mode)"
        };

        Option<bool> attachArg = new(
            name: "--attach"
            ) {
            Required = false,
            Description = "Attach a .NET debugger on startup (default: false)",
            DefaultValueFactory = (_) => false
        };

        Option<bool> strictArg = new(
            name: "--strict"
            ) {
            Required = false,
            Description = "Fail to load when a line cannot be parsed or a directive is unsupported (default: false)",
            DefaultValueFactory = (_) => false
        };

        cmd.Arguments.Add(fileArg);
        cmd.Options.Add(verboseArg);
        cmd.Options.Add(debugArg);
        cmd.Options.Add(connectArg);
        cmd.Options.Add(listenArg);
        cmd.Options.Add(attachArg);
        cmd.Options.Add(strictArg);


        // 実際に解析
        ParseResult parseResult = cmd.Parse(args);
        if(parseResult.Errors.Count != 0) {
            foreach(ParseError parseError in parseResult.Errors) {
                Console.Error.WriteLine(parseError.Message);
            }
            return 1;
        }

        LogLevel minLevel = parseResult.GetValue(verboseArg) ? LogLevel.Debug : LogLevel.Info;
        Logger logger = new(minLevel);
        // stderrはデバッギーのものなので，verboseモードのときだけログを出す
        if(parseResult.GetValue(verboseArg)) {
            logger.AddSink((LogLevel level, string source, string msg) => Console.Error.WriteLine($"[{level}][{source}] {msg}"));
        }
        logger.Debug("Program", "Verbose mode enabled");
        string version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        logger.Info("Program", $"PlusPim version {version}");

        if(parseResult.GetValue(attachArg)) {
            _ = System.Diagnostics.Debugger.Launch();
        }

        if(parseResult.GetValue(debugArg)) {
            // デバッガモードで起動する
            logger.Debug("Program", "Debug Launch");

            if(!DapTransportFactory.TryCreate(parseResult.GetValue(connectArg), parseResult.GetValue(listenArg), logger, Console.Error, out IDapTransport? transport, out string? error)) {
                Console.Error.WriteLine(error);
                return 1;
            }

            FileInfo[] files = parseResult.GetValue(fileArg) ?? throw new ArgumentException("file is not set");
            Application.Application app = new(files, logger, StreamDebuggeeIO.OpenConsole(), parseResult.GetValue(strictArg));

            // DAPはデバッギの標準入出力とは別の通信路で話す．stdin/stdoutはデバッギがそのまま使う
            Stream dap;
            try {
                dap = await transport.OpenAsync();
            } catch(DapTransportException e) {
                Console.Error.WriteLine(e.Message);
                return 1;
            }
            await using Stream dapStream = dap;
            logger.Debug("Program", "DAP transport opened");

            DebugAdapter adapter = new(dapStream, dapStream, app, logger);
            await adapter.WaitForSessionEnd();
        } else {
            Console.Error.WriteLine("Non-debug mode is not implemented. Use --debug.");
            return 1;
        }

        logger.Info("Program", "Exit.");
        return 0;
    }
}
