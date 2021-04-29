using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using BenchmarkDotNet.Toolchains.Results;
using System;

namespace Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = DefaultConfig.Instance
                .AddJob(GetDefaultJob())
                .AddJob(GetForwarderJob())
                .AddDiagnoser(MemoryDiagnoser.Default)
                .WithOptions(ConfigOptions.KeepBenchmarkFiles);

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }

        private static Job GetDefaultJob()
        {
            var settings = NetCoreAppSettings.NetCoreApp50;

            return Job.Default.WithToolchain(new Toolchain(".NET 6.0",
                new CsProjGenerator(settings.TargetFrameworkMoniker, settings.CustomDotNetCliPath, settings.PackagesPath, "$(MicrosoftNETCoreAppVersion)"),
                new DotNetCliBuilder(settings.TargetFrameworkMoniker, settings.CustomDotNetCliPath, settings.Timeout),
                new DotNetCliExecutor(settings.CustomDotNetCliPath)))
                .WithId("Generated");
        }

        private static Job GetForwarderJob()
        {
            var settings = NetCoreAppSettings.NetCoreApp50;
            return Job.Default
                .WithToolchain(new Toolchain(".NET 6.0 with Forwarders",
                     new CsProjGenerator(settings.TargetFrameworkMoniker, settings.CustomDotNetCliPath, settings.PackagesPath, "$(MicrosoftNETCoreAppVersion)"),
                    new GenerateForwarderBuilder(settings),
                   new DotNetCliExecutor(settings.CustomDotNetCliPath)))
                .WithId("Built-in")
                .WithArguments(new []{ new MsBuildArgument("/p:DllImportGenerator_GenerateForwarders=true") })
                .AsBaseline();
        }
    }

    internal class GenerateForwarderBuilder : IBuilder
    {
        private TimeSpan Timeout { get; }

        private string TargetFrameworkMoniker { get; }

        private string CustomDotNetCliPath { get; }

        public GenerateForwarderBuilder(NetCoreAppSettings settings)
        {
            TargetFrameworkMoniker = settings.TargetFrameworkMoniker;
            CustomDotNetCliPath = settings.CustomDotNetCliPath;
            Timeout = settings.Timeout;
        }

        public BuildResult Build(GenerateResult generateResult, BuildPartition buildPartition, ILogger logger)
            => new DotNetCliCommand(
                    CustomDotNetCliPath,
                    "/p:DllImportGenerator_GenerateForwarders=true",
                    generateResult,
                    logger,
                    buildPartition,
                    Array.Empty<EnvironmentVariable>(),
                    Timeout)
                .RestoreThenBuild();
    }
}
