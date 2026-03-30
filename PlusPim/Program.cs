using PlusPim.EditorController.DebugAdapter;
using PlusPim.Logging;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Net;
using System.Net.Sockets;
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

        Option<int> portArg = new(
            name: "--port"
            ) {
            Required = false,
            Description = "Port to listen on for debug adapter connections (default: 4711)",
            DefaultValueFactory = (_) => 4711
        };

        cmd.Arguments.Add(fileArg);
        cmd.Options.Add(verboseArg);
        cmd.Options.Add(debugArg);
        cmd.Options.Add(portArg);


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

#if DEBUG
        _ = System.Diagnostics.Debugger.Launch();
#endif

        if(parseResult.GetValue(debugArg)) {
            // デバッガモードで起動する
            logger.Debug("Program", "Debug Launch");

            FileInfo[] files = parseResult.GetValue(fileArg) ?? throw new ArgumentException("file is not set");
            Application.Application app = new(true, files, logger);

            Socket dapSocket = new(SocketType.Stream, ProtocolType.Tcp);
            dapSocket.Bind(new IPEndPoint(IPAddress.Loopback, parseResult.GetValue(portArg)));
            dapSocket.Listen();
            logger.Debug("Program", "Socket created");

            // プローブ接続（waitForPort）を読み飛ばし，本番DAP接続を待つ
            Socket clientSocket;
            while(true) {
                clientSocket = await dapSocket.AcceptAsync();
                await Task.Delay(50);
                if(clientSocket.Poll(0, SelectMode.SelectRead) && clientSocket.Available == 0) {
                    clientSocket.Dispose();
                    logger.Debug("Program", "Probe connection discarded");
                    continue;
                }
                break;
            }

            await using NetworkStream stream = new(clientSocket, ownsSocket: true);

            logger.Debug("Program", "Socket connected");

            DebugAdapter adapter = new(stream, stream, app, logger);
            await adapter.WaitForSessionEnd();
        } else {
            // 実行するだけ
            throw new NotImplementedException("Non-debug mode is not implemented yet");
            FileInfo[] files = parseResult.GetValue(fileArg) ?? throw new ArgumentException("file is not set");
            Application.Application app = new(false, files, logger);
        }

        logger.Info("Program", "Exit.");
        return 0;
    }
}
