using BenchmarkDotNet.Running;
using Fatewake.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(CoreBenchmarks).Assembly).Run(args);
