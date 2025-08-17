// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

partial class Program
{
    private static void TestMappedSourceFileResolved()
    {
        Debugger.Break(); // This source code should be resolved and visible.
    }
}
