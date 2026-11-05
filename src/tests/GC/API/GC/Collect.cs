// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Tests GC.Collect()

using System;
using Xunit;

public class Test_Collect {
    public static bool IsSatori => GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object value) && value is true;

    [SkipOnCoreClr("This test requires uncontested GC state.", RuntimeTestModes.AnyGCStress)]
    [ConditionalFact(typeof(TestLibrary.Utilities), nameof(TestLibrary.Utilities.IsNotMonoRuntime))]
    public static void CollectionCompletesBeforeReturning()
    {
        int before = GC.CollectionCount(GC.MaxGeneration);
        GC.Collect();
        Assert.True(GC.CollectionCount(GC.MaxGeneration) > before);
        Assert.False(GC.GetGCMemoryInfo().Concurrent);
        Console.WriteLine("Default blocking collection completed before returning.");
    }

    [SkipOnCoreClr("This test is not compatible with GC stress.", RuntimeTestModes.AnyGCStress)]
    [SkipOnCoreClr("GC.Collect does not exhibit the generational behavior this test validates under the CoreCLR interpreter. See https://github.com/dotnet/runtime/issues/118618.", RuntimeTestModes.InterpreterActive)]
    [ActiveIssue("Satori can demote regions after a full GC; generation advancement is not guaranteed.", typeof(Test_Collect), nameof(IsSatori))]
    [Fact]
    public static int TestEntryPoint()
    {

        Object obj1 = new Object();
        int[] array = new int[25];
        
        int gen1 = GC.GetGeneration(array);

        Console.WriteLine("Array is in generation: " + gen1);
        GC.Collect();

        int gen2 = GC.GetGeneration(array);
        Console.WriteLine("Array is in generation: " + gen2);

        if(((gen1==2) && (gen2==2)) || (gen2>gen1)) {    // was already in gen 2!
            Console.WriteLine("Test for GC.Collect() passed!");
            return 100;
        }

        else {
            Console.WriteLine("Test for GC.Collect() failed!");
            return 1;
        }
    }
}
