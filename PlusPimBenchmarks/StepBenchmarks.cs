using BenchmarkDotNet.Attributes;
using PlusPim.Application;
using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.Logging;

namespace PlusPimBenchmarks;

/// <summary>
/// 命令のステップ実行と逆実行の性能を計測する
/// </summary>
/// <remarks>
/// 実行: <c>dotnet run -c Release --project PlusPimBenchmarks -- --filter '*StepBenchmarks*'</c>
/// </remarks>
[MemoryDiagnoser]
public class StepBenchmarks {
    /// <summary>
    /// ループの反復回数．1反復で4命令を実行する
    /// </summary>
    private const int LoopCount = 300_000;

    private static readonly string Source = $"""
        .text
        main:
          li $t0, {LoopCount}
        loop:
          addu $t1, $t1, $t0
          sll $t2, $t1, 1
          addiu $t0, $t0, -1
          bne $t0, $zero, loop
          jr $ra
        """;

    private FileInfo? _file;
    private PlusPimDbg? _debugger;

    /// <summary>
    /// Debugログを有効にするかどうか．<c>--verbose</c>の有無に相当する
    /// </summary>
    [Params(false, true)]
    public bool Verbose { get; set; }

    [GlobalSetup]
    public void GlobalSetup() {
        string path = Path.ChangeExtension(Path.GetTempFileName(), ".s");
        File.WriteAllText(path, Source);
        this._file = new FileInfo(path);
        this._debugger = this.CreateDebugger();
    }

    [GlobalCleanup]
    public void GlobalCleanup() {
        this._file?.Delete();
    }

    [IterationSetup(Target = nameof(RunToEnd))]
    public void RunToEndSetup() {
        this._debugger = this.CreateDebugger();
    }

    /// <summary>
    /// 最後まで実行してから先頭まで戻す．状態が元に戻るため，同じデバッガを繰り返し使える
    /// </summary>
    [Benchmark]
    public int RunToEndAndBack() {
        PlusPimDbg debugger = this._debugger!;
        int steps = RunToTermination(debugger);
        while(debugger.Back()) {
            steps++;
        }
        return steps;
    }

    /// <summary>
    /// 最後まで実行する．毎回新しいデバッガを使う
    /// </summary>
    [Benchmark]
    public int RunToEnd() {
        return RunToTermination(this._debugger!);
    }

    private PlusPimDbg CreateDebugger() {
        // DAPのシンクの代わりに何もしないシンクを使う
        Logger logger = new(this.Verbose ? LogLevel.Debug : LogLevel.Info);
        logger.AddSink((_, _, _) => { });
        return new PlusPimDbg([this._file!], logger);
    }

    private static int RunToTermination(PlusPimDbg debugger) {
        int steps = 0;
        StopReason reason;
        do {
            reason = debugger.Step();
            steps++;
        } while(reason == StopReason.Step);

        return reason == StopReason.Terminated
            ? steps
            : throw new InvalidOperationException($"Unexpected stop reason: {reason}");
    }
}
