using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

// In process: the default toolchain builds a generated project under bin/, which would inherit the repository's
// Directory.* files (central versions, locked restore) and fail.
var config = DefaultConfig.Instance.AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
var summaries = BenchmarkSwitcher.FromAssembly(typeof(Ducky.Benchmarks.DispatchBenchmarks).Assembly).Run(args, config);
return summaries.Any(s => s.HasCriticalValidationErrors || s.Reports.Any(r => !r.Success)) ? 1 : 0;
