using PlusPim.EditorController.DebugAdapter;
using PlusPim.Logging;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
namespace PlusPim;

internal class Program {
    /// <summary>
    /// DAP のソケットが接続を待ち始めたときに標準出力へ書く行の接頭辞．後にポート番号と LF が続く
    /// </summary>
    /// <remarks>拡張機能 (<c>adapterProcess.ts</c> の <c>LISTENING_MARKER</c>) と一致させること</remarks>
    internal const string ListeningMarker = "PLUSPIM_DAP_LISTENING 127.0.0.1:";

    /// <summary>
    /// 指定したポートが使用中のときの終了コード
    /// </summary>
    internal const int PortInUseExitCode = 2;

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
            Description = "Port to listen on for a debug adapter connection; 0 picks a free port and prints it to stdout (default: 4711)",
            DefaultValueFactory = (_) => 4711
        };

        Option<bool> attachArg = new(
            name: "--attach"
            ) {
            Required = false,
            Description = "Attach a .NET debugger on startup (default: false)",
            DefaultValueFactory = (_) => false
        };

        Option<bool> stdioArg = new(
            name: "--stdio"
            ) {
            Required = false,
            Description = "Use stdin/stdout for DAP transport instead of TCP (default: false)",
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
        cmd.Options.Add(portArg);
        cmd.Options.Add(attachArg);
        cmd.Options.Add(stdioArg);
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
            // 起動の計測のため，各行にプロセスの開始からの経過時間 [+<ms>ms] を付ける
            TimeSpan sinceStart = DateTime.Now - Process.GetCurrentProcess().StartTime;
            Stopwatch stopwatch = Stopwatch.StartNew();
            logger.AddSink((LogLevel level, string source, string msg) =>
                Console.Error.WriteLine($"[+{(long)(sinceStart + stopwatch.Elapsed).TotalMilliseconds}ms][{level}][{source}] {msg}"));
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

            FileInfo[] files = parseResult.GetValue(fileArg) ?? throw new ArgumentException("file is not set");
            Application.Application app = new(files, logger, parseResult.GetValue(strictArg));

            if(parseResult.GetValue(stdioArg)) {
                // stdio経由でDAPを話す．DAPに渡す前に元のstdin/stdoutストリームを確保する
                Stream stdin = Console.OpenStandardInput();
                Stream stdout = Console.OpenStandardOutput();
                logger.Debug("Program", "Using stdio transport for DAP");

                DebugAdapter adapter = new(stdin, stdout, app, logger);

                // デバッギのConsole.WriteがDAPストリームを汚染しないようにOutputEventへ転送
                Console.SetOut(new DebuggeeOutputWriter(adapter));
                // stdinはDAPが占有するため，デバッギからの読み取りは空文字列扱いに劣化させる
                Console.SetIn(TextReader.Null);

                await adapter.WaitForSessionEnd();
            } else {
                UseUtf8ForRedirectedStdio();

                Socket? clientSocket = await AcceptSingleClientAsync(parseResult.GetValue(portArg), Console.Out, Console.Error, logger);
                if(clientSocket is null) {
                    return PortInUseExitCode;
                }

                await using NetworkStream stream = new(clientSocket, ownsSocket: true);

                logger.Debug("Program", "Socket connected");

                DebugAdapter adapter = new(stream, stream, app, logger);
                await adapter.WaitForSessionEnd();
            }
        } else {
            Console.Error.WriteLine("Non-debug mode is not implemented. Use --debug.");
            return 1;
        }

        logger.Info("Program", "Exit.");
        return 0;
    }

    /// <summary>
    /// パイプなどにリダイレクトされた標準入出力を UTF-8 で読み書きする
    /// </summary>
    /// <remarks>
    /// 既定ではコンソールのコードページ (例: 932) が使われ，拡張機能が UTF-8 として読めない．
    /// <see cref="Console.OutputEncoding"/> は共有しているコンソールのコードページまで変えるため使わず，
    /// リダイレクトされたストリームだけを差し替える
    /// </remarks>
    private static void UseUtf8ForRedirectedStdio() {
        UTF8Encoding utf8 = new(encoderShouldEmitUTF8Identifier: false);
        if(Console.IsOutputRedirected) {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
        }
        if(Console.IsErrorRedirected) {
            Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });
        }
        if(Console.IsInputRedirected) {
            Console.SetIn(new StreamReader(Console.OpenStandardInput(), utf8));
        }
    }

    /// <summary>
    /// 127.0.0.1 の <paramref name="port"/> で接続を待ち，<see cref="ListeningMarker"/> とポート番号の行を
    /// <paramref name="announce"/> に書いてから，1つだけ接続を受け付ける
    /// </summary>
    /// <param name="port">待ち受けるポート．0 なら OS が空いているポートを選ぶ</param>
    /// <param name="announce">待ち受けを知らせる行を書く先 (標準出力)．接続前に書くのはこの1行だけである</param>
    /// <param name="error">ポートが使用中のときのメッセージを書く先 (標準エラー出力)</param>
    /// <param name="logger">ロガー</param>
    /// <returns>接続したクライアントのソケット．ポートが使用中なら null</returns>
    /// <remarks>受け付けた後は待ち受けを閉じるため，2つ目以降の接続は拒否される</remarks>
    internal static async Task<Socket?> AcceptSingleClientAsync(int port, TextWriter announce, TextWriter error, ILogger logger) {
        // 拡張機能は 127.0.0.1 に接続する．localhost は ::1 に解決されうるため IPv4 に限る
        using Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try {
            listener.Bind(new IPEndPoint(IPAddress.Loopback, port));
        } catch(SocketException e) when(e.SocketErrorCode == SocketError.AddressAlreadyInUse) {
            error.WriteLine($"Port {port} is already in use. Use --port 0 to pick a free port.");
            return null;
        }
        listener.Listen(1);
        int boundPort = ((IPEndPoint)listener.LocalEndPoint!).Port;
        logger.Debug("Program", $"Listening on 127.0.0.1:{boundPort}");

        announce.Write($"{ListeningMarker}{boundPort}\n");
        announce.Flush();

        return await listener.AcceptAsync();
    }
}
