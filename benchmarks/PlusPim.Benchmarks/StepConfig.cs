using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

/// <summary>
/// 1回の呼び出しが重く，呼び出しごとに状態を作り直すベンチマークの設定
/// </summary>
public sealed class StepConfig: ManualConfig {
    public StepConfig() {
        _ = this.AddJob(Job.Default
            .WithToolchain(InProcessEmitToolchain.Instance)
            .WithWarmupCount(2)
            .WithIterationCount(10)
            .WithInvocationCount(1)
            .WithUnrollFactor(1));
        _ = this.AddDiagnoser(MemoryDiagnoser.Default);
        // RPlotExporter は CsvMeasurementsExporter を含む
        _ = this.AddExporter(RPlotExporter.Default);
        _ = this.AddColumn(
            new SampleColumn("Retained", (m, c) => Is(c, "Continue") ? $"{m / 1024:N0} KB" : null),
            new SampleColumn("Retained/N", (m, c) => Is(c, "Continue") && c.Parameters["N"] is int n ? $"{m / n:N1} B" : null),
            new SampleColumn("PeakPrivate", (m, c) => Is(c, "Fact") ? $"{m / (1024 * 1024):N1} MB" : null));
    }

    private static bool Is(BenchmarkDotNet.Running.BenchmarkCase c, string method) {
        return c.Descriptor.WorkloadMethod.Name == method;
    }
}
