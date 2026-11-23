using PlusPim.Image;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace PlusPim;


/// <summary>
/// 待ち受けている DAP クライアントへ接続する
/// </summary>
/// <param name="Endpoint">接続先</param>
internal readonly record struct Connect(IPEndPoint Endpoint);

/// <summary>
/// DAP クライアントからの接続を待ち受ける
/// </summary>
/// <param name="Endpoint">待ち受けるアドレス．ポート 0 なら空きポートを使う</param>
internal readonly record struct Listen(IPEndPoint Endpoint);

/// <summary>
/// DAP クライアントとの接続方法
/// </summary>
internal readonly union EstablishMethod(
    Connect,
    Listen
);


/// <summary>
/// 共通する引数
/// </summary>
/// <param name="Verbose">ログを標準エラー出力へ出すか</param>
/// <param name="Strict">解析できない行や未対応の指令をエラーにするか</param>
/// <param name="ForceTerminalUtf8">ターミナルを UTF-8 に切り替えるか</param>
/// <param name="Attach">起動時に .NET デバッガをアタッチするか</param>
internal sealed record CommonArgs(bool Verbose, bool Strict, bool ForceTerminalUtf8, bool Attach);

/// <summary>
/// <c>debug</c> サブコマンドの引数
/// </summary>
/// <param name="Common">共通の引数</param>
/// <param name="Method">接続方法</param>
internal sealed record DebugArgs(CommonArgs Common, EstablishMethod Method);

/// <summary>
/// <c>runtime</c> サブコマンドの引数
/// </summary>
/// <param name="Common">共通の引数</param>
/// <param name="Files">読み込むアセンブリファイル．1つ以上</param>
internal sealed record RuntimeArgs(CommonArgs Common, SourcePath[] Files);

/// <summary>
/// PlusPim のコマンドラインの定義
/// </summary>
internal static class PlusPimCommand {
    /// <summary>
    /// CLI のルートコマンドを定義する
    /// </summary>
    /// <param name="version">表示するバージョン</param>
    /// <param name="debug">サブコマンド <c>debug</c> のアクション</param>
    /// <param name="runtime">サブコマンド <c>runtime</c> のアクション</param>
    public static RootCommand Create(string version, Func<DebugArgs, CancellationToken, Task<int>> debug, Func<RuntimeArgs, int> runtime) {
        RootCommand root = new("PlusPim - A MIPS runtime and debugger");
        string versionLine = $"PlusPim {version}";

        HelpOption help = root.Options.OfType<HelpOption>().Single();
        help.Action = new VersionLineAction(versionLine, (SynchronousCommandLineAction)help.Action!);
        VersionOption versionOption = root.Options.OfType<VersionOption>().Single();
        versionOption.Aliases.Add("-V");
        versionOption.Action = new VersionLineAction(versionLine, null);

        // 共通オプション
        Option<bool> verbose = new("--verbose", "-v") {
            Recursive = true, // サブコマンドでも使えるようにする
            Description = "Write logs to stderr"
        };
        Option<bool> strict = new("--strict") {
            Recursive = true,
            Description = "Fail to load when a line cannot be parsed or a directive is unsupported"
        };
        Option<bool> forceTerminalUtf8 = new("--force-terminal-utf8") {
            Recursive = true,
            Description = "Switch the terminal to UTF-8 while PlusPim runs, where supported (currently Windows only)"
        };
        Option<bool> attach = new("--attach") {
            Recursive = true,
            Description = "Attach a .NET debugger on startup (for development)"
        };
        root.Options.Add(verbose);
        root.Options.Add(strict);
        root.Options.Add(forceTerminalUtf8);
        root.Options.Add(attach);

        Argument<SourcePath[]> files = new("file") {
            Arity = ArgumentArity.OneOrMore,
            Description = "Path to the MIPS ASM file",
            CustomParser = r => {
                List<SourcePath> paths = new(r.Tokens.Count);
                foreach(Token token in r.Tokens) {
                    if(token.Value.StartsWith('-')) {
                        r.AddError($"Unrecognized option '{token.Value}'.");
                    } else if(SourcePath.TryResolve(token.Value, Environment.CurrentDirectory, out SourcePath path)) {
                        paths.Add(path);
                    } else {
                        r.AddError($"Invalid file path '{token.Value}'.");
                    }
                }
                return [.. paths];
            }
        };

        CommonArgs ReadCommon(ParseResult r) {
            return new(r.GetValue(verbose), r.GetValue(strict), r.GetValue(forceTerminalUtf8), r.GetValue(attach));
        }

        Command Mode(string name, ushort minPort, string description, Func<IPEndPoint, EstablishMethod> make) {
            string format = $"IPv4:port or [IPv6]:port (port {minPort}-{ushort.MaxValue})";
            Argument<IPEndPoint> endpoint = new("ip:port") {
                Description = format,
                CustomParser = r => {
                    string text = r.Tokens[0].Value;
                    IPEndPoint? parsed = ParseEndpoint(text, minPort);
                    if(parsed is null) {
                        r.AddError($"Invalid ip:port value '{text}': expected {format}.");
                    }
                    return parsed;
                }
            };
            Command command = new(name, description) { endpoint };
            command.SetAction((r, ct) => debug(new DebugArgs(ReadCommon(r), make(r.GetRequiredValue(endpoint))), ct));
            return command;
        }

        Command debugCommand = new("debug", "Start as a debugger driven by a DAP client") {
            Mode("connect", 1, "Connect to the DAP client listening at ip:port", e => new Connect(e)),
            Mode("listen", 0, "Listen for the DAP client at ip:port; port 0 picks a free port and the address is announced on stderr", e => new Listen(e))
        };

        Command runtimeCommand = new("runtime", "Run the program to the end without the debugger (not implemented)") {
            files
        };
        runtimeCommand.SetAction(r => runtime(new RuntimeArgs(ReadCommon(r), r.GetRequiredValue(files))));

        root.Subcommands.Add(debugCommand);
        root.Subcommands.Add(runtimeCommand);
        return root;
    }

    /// <summary>
    /// 引数を解析してアクションを呼ぶ
    /// </summary>
    /// <remarks>パースエラーのときはエラーと <c>--help</c> すると良い旨をを出力する</remarks>
    /// <param name="root"><see cref="Create"/> で作ったコマンド</param>
    /// <param name="args">コマンドライン引数</param>
    /// <param name="configuration">出力先</param>
    /// <returns>終了コード</returns>
    public static Task<int> InvokeAsync(RootCommand root, string[] args, InvocationConfiguration configuration) {
        ParseResult parseResult = root.Parse(args);
        if(parseResult.Action is not ParseErrorAction) {
            return parseResult.InvokeAsync(configuration);
        }
        foreach(ParseError error in parseResult.Errors) {
            configuration.Error.WriteLine(error.Message);
        }
        configuration.Error.WriteLine("Run 'pluspim --help' for usage.");
        return Task.FromResult(1);
    }

    /// <summary>
    /// <c>IPv4:port</c> と <c>[IPv6]:port</c> を解釈する
    /// </summary>
    /// <param name="text">解釈する文字列</param>
    /// <param name="minPort">許す最小のポート</param>
    /// <returns>解釈した接続先．書式が不正ならnull</returns>
    private static IPEndPoint? ParseEndpoint(string text, ushort minPort) {
        int separator = text.LastIndexOf(':');
        if(separator < 0) {
            return null;
        }
        string ip = text[..separator];
        // IPv6は括弧で囲ませる仕様
        bool bracketed = ip.StartsWith('[') && ip.EndsWith(']');
        string literal = bracketed ? ip[1..^1] : ip;
        AddressFamily family = bracketed ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
        if(!IPAddress.TryParse(literal, out IPAddress? address) || address.AddressFamily != family) {
            return null;
        }
        // IPAddress.TryParse は 127.1 のような省略形も受け付ける
        // これを拒否する
        return !bracketed && address.ToString() != literal
            ? null
            : !ushort.TryParse(text[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out ushort port) || port < minPort
            ? null
            : new IPEndPoint(address, port);
    }

    /// <summary>
    /// バージョン行を出し，続けて <paramref name="next"/> を呼ぶ
    /// </summary>
    private sealed class VersionLineAction(string versionLine, SynchronousCommandLineAction? next): SynchronousCommandLineAction {
        public override bool ClearsParseErrors => true;

        public override int Invoke(ParseResult parseResult) {
            parseResult.InvocationConfiguration.Output.WriteLine(versionLine);
            return next?.Invoke(parseResult) ?? 0;
        }
    }
}
