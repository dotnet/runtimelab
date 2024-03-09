// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.DotNet.XHarness.TestRunners.Common;
using Microsoft.DotNet.XHarness.TestRunners.Xunit;

public class SimpleWasmTestRunner : WasmApplicationEntryPoint
{
    public static async Task<int> Main(string[] args)
    {
        int index = 0;

#if SINGLE_FILE_TEST_RUNNER
        // This runner is also used for NativeAOT testing, which defines SINGLE_FILE_TEST_RUNNER.
        var testAssembly = typeof(SimpleWasmTestRunner).Assembly.GetName().Name;
#else
        if (args.Length == 0)
        {
            Console.WriteLine ($"No args given");
            return -1;
        }

        var testAssembly = args[index++];
#endif

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
                    excludedTraits.Add (args[i + 1]);
                    i++;
                    break;
                case "-trait":
                    includedTraits.Add (args[i + 1]);
                    i++;
                    break;
                case "-namespace":
                    includedNamespaces.Add (args[i + 1]);
                    i++;
                    break;
                case "-class":
                    includedClasses.Add (args[i + 1]);
                    i++;
                    break;
                case "-method":
                    includedMethods.Add (args[i + 1]);
                    i++;
                    break;
                case "-backgroundExec":
                    backgroundExec = true;
                    break;
                case "-untilFailed":
                    untilFailed = true;
                    break;
                default:
                    throw new ArgumentException($"Invalid argument '{option}'.");
            }
        }

        var runner = new SimpleWasmTestRunner()
        {
            TestAssembly = testAssembly,
            ExcludedTraits = excludedTraits,
            IncludedTraits = includedTraits,
            IncludedNamespaces = includedNamespaces,
            IncludedClasses = includedClasses,
            IncludedMethods = includedMethods
        };

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

#if SINGLE_FILE_TEST_RUNNER
    protected override IEnumerable<TestAssemblyInfo> GetTestAssemblies()
        => new[] { new TestAssemblyInfo(typeof(SimpleWasmTestRunner).Assembly, typeof(SimpleWasmTestRunner).Assembly.GetName().Name) };
#endif
}
