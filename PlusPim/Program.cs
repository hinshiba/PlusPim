using PlusPim.EditorController.DebugAdapter;
using System.CommandLine;
using System.CommandLine.Parsing;
namespace PlusPim;

internal class Program {


    private static int Main(string[] args) {
        // コマンドライン引数処理
        RootCommand cmd = new("PlusPim - A MIPS runtime and debugger");

        Argument<FileInfo> fileArg = new(
            name: "file"
        ) {
            Description = "Path to the MIPS ASM File"
        };

        // 指定されている場合はデバッグモードで起動する
        // 位置引数でないものはOption
        Option<int> portArg = new(
            name: "--port"
            ) {
            Required = false,
            Description = "Port to listen on for debug adapter connections (default: 4711)",
            DefaultValueFactory = (_) => 4711
        };

        cmd.Arguments.Add(fileArg);
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

        Application.Application app = new();
        _ = new DebugAdapter(Console.OpenStandardInput(), Console.OpenStandardOutput(), app);
        return 0;
    }
}
