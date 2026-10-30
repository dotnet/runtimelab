// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

internal class HighMemoryPercent
{
    public static int Main(string[] args)
    {
        if (!GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object? satori) || satori is not true)
        {
            return 100;
        }

        int percent = int.Parse(args[0]);
        bool configurationMatches = GC.GetConfigurationVariables().TryGetValue("GCHighMemPercent", out object? value)
            && value is long configuredPercent && configuredPercent == percent;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GCMemoryInfo info = GC.GetGCMemoryInfo();
        long expected = info.TotalAvailableMemoryBytes * percent / 100;
        Console.WriteLine($"Total available: {info.TotalAvailableMemoryBytes}, threshold: {info.HighMemoryLoadThresholdBytes}, expected: {expected}");
        return configurationMatches && info.TotalAvailableMemoryBytes > 0 && info.HighMemoryLoadThresholdBytes == expected ? 100 : 1;
    }
}
