using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// 標準入出力を差し替えるテストのコレクション (doc/tests/instructions/runtime_call_model.md「テストの前提」)．
/// 標準入出力はプロセス全体で共有されるため，このコレクションは他のテストと並列実行しない
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleCollection {
    /// <summary>
    /// <c>[Collection(ConsoleCollection.Name)]</c> で参照するコレクション名
    /// </summary>
    public const string Name = "Console";
}

/// <summary>
/// <see cref="Console.In"/> と <see cref="Console.Out"/> を差し替え，<see cref="Dispose"/> で元に戻す
/// </summary>
/// <remarks><see cref="ConsoleCollection"/> に属するテストクラスのコンストラクタで生成し，Dispose で破棄する</remarks>
internal sealed class ConsoleRedirect: IDisposable {
    private readonly TextReader _originalIn;
    private readonly TextWriter _originalOut;
    private readonly StringWriter _output = new();
    private StringReader _input = new("");

    public ConsoleRedirect() {
        this._originalIn = Console.In;
        this._originalOut = Console.Out;
        Console.SetIn(this._input);
        Console.SetOut(this._output);
    }

    /// <summary>
    /// これまでに標準出力へ書き込まれた文字列
    /// </summary>
    public string Output => this._output.ToString();

    /// <summary>
    /// 標準入力の内容を <paramref name="input"/> に置き換える
    /// </summary>
    public void SetInput(string input) {
        this._input = new StringReader(input);
        Console.SetIn(this._input);
    }

    /// <summary>
    /// 標準入力のうち，まだ読まれていない残りをすべて読み出す
    /// </summary>
    public string ReadRemainingInput() {
        return this._input.ReadToEnd();
    }

    public void Dispose() {
        Console.SetIn(this._originalIn);
        Console.SetOut(this._originalOut);
        this._input.Dispose();
        this._output.Dispose();
    }
}
