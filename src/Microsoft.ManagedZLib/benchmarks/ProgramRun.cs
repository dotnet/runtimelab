// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Running;
using System;

namespace Microsoft.ManagedZLib.Benchmarks;

class ProgramRun
{
    static void Main(string[] args)
    {
        BenchmarkRunner.Run<ManagedZLibBenchmarks>();
    }
}   
