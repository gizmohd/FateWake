using BenchmarkDotNet.Running;
using Fatewake.Benchmarks;

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FATEWAKE_AUTH_BENCHMARK_CONNECTION")))
    BenchmarkSwitcher.FromTypes([typeof(CoreBenchmarks)]).Run(args);
else
    BenchmarkSwitcher.FromAssembly(typeof(CoreBenchmarks).Assembly).Run(args);
