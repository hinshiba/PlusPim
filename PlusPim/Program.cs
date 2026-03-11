using PlusPim.EditorController.DebugAdapter;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Net;
using System.Net.Sockets;
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
            StreamWriter logWritter = new("error.log");
            foreach(ParseError parseError in parseResult.Errors) {
                logWritter.WriteLine(parseError.Message);
            }
            return 1;
        }

        if(parseResult.GetValue(verboseArg)) {
            Console.WriteLine("PlusPim: Verbose mode enabled");
            Console.WriteLine("PlusPim: PlusPim version 0.1.1");
        }

        if(parseResult.GetValue(debugArg)) {
            // デバッガモードで起動する
            if(parseResult.GetValue(verboseArg)) {
                Console.WriteLine("PlusPim: Debug Launch");
            }

            FileInfo[] files = parseResult.GetValue(fileArg) ?? throw new ArgumentException("file is not set");
            Application.Application app = new(true, files);

            Socket dapSocket = new(SocketType.Stream, ProtocolType.Tcp);
            dapSocket.Bind(new IPEndPoint(IPAddress.Loopback, parseResult.GetValue(portArg)));
            dapSocket.Listen();
            if(parseResult.GetValue(verboseArg)) {
                Console.WriteLine("PlusPim: Socket created");
            }

            // プローブ接続（waitForPort）を読み飛ばし，本番DAP接続を待つ
            Socket clientSocket;
            while(true) {
                clientSocket = await dapSocket.AcceptAsync();
                await Task.Delay(50);
                if(clientSocket.Poll(0, SelectMode.SelectRead) && clientSocket.Available == 0) {
                    clientSocket.Dispose();
                    if(parseResult.GetValue(verboseArg)) {
                        Console.WriteLine("PlusPim: Probe connection discarded");
                    }
                    continue;
                }
                break;
            }

            using Socket _ = clientSocket;
            await using NetworkStream stream = new(clientSocket, ownsSocket: true);

            if(parseResult.GetValue(verboseArg)) {
                Console.WriteLine("PlusPim: Socket connected");
            }

            DebugAdapter adapter = new(stream, stream, app);
            await adapter.WaitForSessionEnd();
        } else {
            // 実行するだけ
            FileInfo[] files = parseResult.GetValue(fileArg) ?? throw new ArgumentException("file is not set");
            Application.Application app = new(false, files);
        }

        Console.WriteLine("PlusPim: Exit.");
        return 0;
    }
}
