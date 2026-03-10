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

        // 指定されている場合はデバッグモードで起動する
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
            Console.WriteLine("PlusPim: PlusPim version 0.1.0");
        }

        if(parseResult.GetValue(debugArg)) {
            // デバッグモードで起動する
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

            using Socket clientSocket = await dapSocket.AcceptAsync();
            await using NetworkStream stream = new(clientSocket, ownsSocket: true);

            if(parseResult.GetValue(verboseArg)) {
                Console.WriteLine("PlusPim: Socket connected");
            }

            _ = new DebugAdapter(stream, stream, app);
        } else {
            // 実行するだけ
            FileInfo[] files = parseResult.GetValue(fileArg) ?? throw new ArgumentException("file is not set");
            Application.Application app = new(false, files);
        }


        return 0;
    }
}
