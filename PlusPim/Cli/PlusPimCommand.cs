using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace PlusPim.Cli;

internal enum EstablishMethod {
    Connect,
    Listen
}

/// <summary>
/// 共通する引数
/// </summary>
/// <param name="Files">読み込むアセンブリファイル．1つ以上</param>
/// <param name="Verbose">ログを標準エラー出力へ出すか</param>
/// <param name="Strict">解析できない行や未対応の指令をエラーにするか</param>
/// <param name="ForceTerminalUtf8">ターミナルを UTF-8 に切り替えるか</param>
/// <param name="Attach">起動時に .NET デバッガをアタッチするか</param>
internal sealed record CommonArgs(FileInfo[] Files, bool Verbose, bool Strict, bool ForceTerminalUtf8, bool Attach);

/// <summary>
/// <c>debug</c> サブコマンドの引数
/// </summary>
/// <param name="Common">共通の引数</param>
/// <param name="EstablishMethod">接続方法</param>
internal sealed record DebugArgs(CommonArgs Common, (EstablishMethod Method, IPEndPoint Endpoint) EstablishMethod);

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
    public static RootCommand Create(string version, Func<DebugArgs, CancellationToken, Task<int>> debug, Func<CommonArgs, int> runtime) {
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

        Argument<FileInfo[]> files = new("file") {
            Arity = ArgumentArity.OneOrMore,
            Description = "Path to the MIPS ASM file",
            Validators = {
                r => {
                    foreach(Token token in r.Tokens.Where(t => t.Value.StartsWith('-'))) {
                        r.AddError($"Unrecognized option '{token.Value}'.");
                    }
                },
            }
        };

        CommonArgs ReadCommon(ParseResult r) {
            return new(r.GetRequiredValue(files), r.GetValue(verbose), r.GetValue(strict), r.GetValue(forceTerminalUtf8), r.GetValue(attach));
        }

        Option<IPEndPoint?> EndpointOption(string name, ushort minPort, string description) {
            return new(name) {
                HelpName = "ip:port",
                Description = description,
                CustomParser = r => {
                    string text = r.Tokens[0].Value;
                    IPEndPoint? endpoint = ParseEndpoint(text, minPort);
                    if(endpoint is null) {
                        r.AddError($"Invalid {name} value '{text}': expected IPv4:port or [IPv6]:port (port {minPort}-{ushort.MaxValue}).");
                    }
                    return endpoint;
                }
            };
        }

        Option<IPEndPoint?> connect = EndpointOption("--connect", 1, "Connect to the DAP client listening at ip:port");
        Option<IPEndPoint?> listen = EndpointOption("--listen", 0, "Listen for the DAP client at ip:port; port 0 picks a free port and the address is announced on stderr");
        Command debugCommand = new("debug", "Start as a debugger driven by a DAP client") {
            files, connect, listen
        };
        debugCommand.Validators.Add(c => {
            bool hasConnect = c.GetResult(connect) is not null;
            bool hasListen = c.GetResult(listen) is not null;
            switch(hasConnect, hasListen) {
                case (false, false):
                    c.AddError("Either --connect or --listen must be specified.");
                    break;
                case (true, true):
                    c.AddError("--connect and --listen cannot be specified together.");
                    break;
            }
        });

        (EstablishMethod method, IPEndPoint endpoint) ReadEstablishMethod(ParseResult r) {
            return (r.GetValue(connect), r.GetValue(listen)) switch {
                (IPEndPoint c, null) => (EstablishMethod.Connect, c),
                (null, IPEndPoint l) => (EstablishMethod.Listen, l),
                _ => throw new UnreachableException("Validator should have prevented this"),
            };
        }

        debugCommand.SetAction((r, ct) => debug(new DebugArgs(ReadCommon(r), ReadEstablishMethod(r)), ct));

        Command runtimeCommand = new("runtime", "Run the program to the end without the debugger (not implemented)") {
            files
        };
        runtimeCommand.SetAction(r => runtime(ReadCommon(r)));

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
        configuration.Error.WriteLine("Run 'PlusPim --help' for usage.");
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
        if(!bracketed && address.ToString() != literal) {
            return null;
        }
        return !ushort.TryParse(text[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out ushort port) || port < minPort
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
