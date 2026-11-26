using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(StepConfig).Assembly).Run(args);
Samples.Export();
