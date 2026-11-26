using BenchmarkDotNet.Attributes;
using PlusPim.Asm;
using PlusPim.Dbg;
using PlusPim.Image;
using PlusPim.Usecase;

/// <summary>
/// 履歴を積む前進と巻き戻しの速度，および履歴が保持するメモリ
/// </summary>
/// <remarks>v0.5.0 のworktreeにも同名・同パラメータのクラスがあり，結果を突き合わせる</remarks>
[Config(typeof(StepConfig))]
public class History {
    /// <summary>ループで実行する命令数のおおよその値</summary>
    [Params(10_000, 25_000, 50_000, 75_000, 100_000)]
    public int N;

    private string _path = "";
    private int _doneLine;
    private Session _session = null!;
    private long _baseline;

    [GlobalSetup]
    public void Setup() {
        (this._path, this._doneLine) = Workload.Write(this.N);
        // done で止まらなければ命令数が想定と異なる
        using Session s = this.Load();
        if(s.RunAsync(new Continue()).GetAwaiter().GetResult() is not Breakpoint) {
            throw new InvalidOperationException("loop.asm did not stop at done.");
        }
    }

    [IterationSetup(Target = nameof(Continue))]
    public void SetupContinue() {
        this._session = this.Load();
        this._baseline = GC.GetTotalMemory(true);
    }

    [IterationSetup(Target = nameof(ReverseContinue))]
    public void SetupReverse() {
        this._session = this.Load();
        _ = this._session.RunAsync(new Continue()).GetAwaiter().GetResult();
    }

    /// <summary>入口から done のブレークポイントまで進む</summary>
    [Benchmark]
    public Stop Continue() {
        return this._session.RunAsync(new Continue()).GetAwaiter().GetResult();
    }

    /// <summary>done から履歴の先頭まで戻る</summary>
    [Benchmark]
    public Stop ReverseContinue() {
        return this._session.RunAsync(new ReverseContinue()).GetAwaiter().GetResult();
    }

    [IterationCleanup(Target = nameof(Continue))]
    public void CleanupContinue() {
        Samples.Record(nameof(History), nameof(this.Continue), this.N.ToString(), GC.GetTotalMemory(true) - this._baseline);
        this._session.Dispose();
    }

    [IterationCleanup(Target = nameof(ReverseContinue))]
    public void CleanupReverse() {
        this._session.Dispose();
    }

    private Session Load() {
        _ = SourcePath.TryResolve(this._path, Environment.CurrentDirectory, out SourcePath path);
        Session s = Session.Load([path], new FileReader(), false, DelaySlotsConfig.All, Stream.Null, Stream.Null).Session!;
        _ = s.SetBreakpoints(path, [this._doneLine]);
        return s;
    }

    private sealed class FileReader: ISourceReader {
        public string? Read(SourcePath path) {
            return File.ReadAllText(path.Value);
        }
    }
}
