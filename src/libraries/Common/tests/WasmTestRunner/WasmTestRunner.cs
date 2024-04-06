// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
<<<<<<< HEAD

=======
>>>>>>> runtime/main
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Microsoft.DotNet.XHarness.TestRunners.Xunit;

public class WasmTestRunner : WasmApplicationEntryPoint
{
    protected int MaxParallelThreadsFromArg { get; set; }
    protected override int? MaxParallelThreads => RunInParallel ? MaxParallelThreadsFromArg : base.MaxParallelThreads;

    public static async Task<int> Main(string[] args)
    {
        int index = 0;

#if SINGLE_FILE_TEST_RUNNER
        // This runner is also used for NativeAOT testing, which defines SINGLE_FILE_TEST_RUNNER.
        var testAssembly = typeof(SimpleWasmTestRunner).Assembly.GetName().Name;
#else
        if (args.Length == 0)
        {
            Console.WriteLine($"No args given");
            return -1;
        }

<<<<<<< HEAD
        var testAssembly = args[index++];
#endif
=======
        var runner = new WasmTestRunner();

        runner.TestAssembly = args[0];
>>>>>>> runtime/main

        var excludedTraits = new List<string>();
        var includedTraits = new List<string>();
        var includedNamespaces = new List<string>();
        var includedClasses = new List<string>();
        var includedMethods = new List<string>();
        var backgroundExec = false;
        var untilFailed = false;

        for (int i = index; i < args.Length; i++)
        {
            var option = args[i];
            switch (option)
            {
                case "-notrait":
                    excludedTraits.Add(args[i + 1]);
                    i++;
                    break;
                case "-trait":
                    includedTraits.Add(args[i + 1]);
                    i++;
                    break;
                case "-namespace":
                    includedNamespaces.Add(args[i + 1]);
                    i++;
                    break;
                case "-class":
                    includedClasses.Add(args[i + 1]);
                    i++;
                    break;
                case "-method":
                    includedMethods.Add(args[i + 1]);
                    i++;
                    break;
                case "-backgroundExec":
                    backgroundExec = true;
                    break;
                case "-untilFailed":
                    untilFailed = true;
                    break;
                case "-threads":
                    runner.IsThreadless = false;
                    break;
                case "-parallelThreads":
                    runner.MaxParallelThreadsFromArg = Math.Max(1, int.Parse(args[i + 1]));
                    runner.RunInParallel = runner.MaxParallelThreadsFromArg > 1;
                    i++;
                    break;
                case "-verbosity":
                    runner.MinimumLogLevel = Enum.Parse<MinimumLogLevel>(args[i + 1]);
                    i++;
                    break;
                default:
                    throw new ArgumentException($"Invalid argument '{option}'.");
            }
        }

        runner.ExcludedTraits = excludedTraits;
        runner.IncludedTraits = includedTraits;
        runner.IncludedNamespaces = includedNamespaces;
        runner.IncludedClasses = includedClasses;
        runner.IncludedMethods = includedMethods;

#if !SINGLE_FILE_TEST_RUNNER
        if (OperatingSystem.IsBrowser())
        {
            await Task.Yield();
        }
#endif

        var res = 0;
        do
        {
            if (backgroundExec)
            {
                res = await Task.Run(() => runner.Run());
            }
            else
            {
                res = await runner.Run();
            }
        }
        while(res == 0 && untilFailed);

        return res;
    }

<<<<<<< HEAD
#if SINGLE_FILE_TEST_RUNNER
    protected override IEnumerable<TestAssemblyInfo> GetTestAssemblies()
        => new[] { new TestAssemblyInfo(typeof(SimpleWasmTestRunner).Assembly, typeof(SimpleWasmTestRunner).Assembly.GetName().Name) };
#endif
=======
    public override Task RunAsync()
    {
        if (RunInParallel)
            Console.WriteLine($"Running in parallel with {MaxParallelThreads} threads.");

        return base.RunAsync();
    }
>>>>>>> runtime/main
}
