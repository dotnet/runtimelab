using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using System;

namespace Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            IConfig config = DefaultConfig.Instance;

            if (args.Length > 0)
            {
                string builtinOption = "builtin";
                string marshallerOption = "marshaller";

                if (args[0] is "-h" or "--help")
                {
                    Console.WriteLine($"Select benchmark group by passing either '{builtinOption}' or '{marshallerOption}' as the first argument. The default is '{builtinOption}'");
                }
                else if (args[0] == marshallerOption)
                {
                    args = args[1..];
                    config = DefaultConfig.Instance
                        .AddDiagnoser(MemoryDiagnoser.Default);
                }
                else
                {
                    if (args[0] == builtinOption)
                    {
                        args = args[1..];
                    }

                    config = DefaultConfig.Instance
                        .AddJob(Job.Default.WithId("Generated"))
                        .AddJob(Job.Default
                            .WithCustomBuildConfiguration("Release_Forwarders")
                            .WithId("Built-in")
                            .AsBaseline())
                        .AddDiagnoser(MemoryDiagnoser.Default)
                        .AddFilter(new BuiltinCompareFilter());
                }
            }

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}
