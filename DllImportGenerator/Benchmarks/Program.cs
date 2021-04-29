using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = DefaultConfig.Instance
                .AddJob(Job.Default.WithId("Generated"))
                .AddJob(Job.Default
                    .WithCustomBuildConfiguration("Release_Forwarders")
                    .WithId("Built-in")
                    .AsBaseline())
                .AddDiagnoser(MemoryDiagnoser.Default)
                .WithOptions(ConfigOptions.KeepBenchmarkFiles);

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}
