// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;

bool success = true;

#if !CODEGEN_WASM
success &= RunTest(BasicThreading.Run);
#endif
success &= RunTest(Delegates.Run);
success &= RunTest(Generics.Run);
success &= RunTest(Interfaces.Run);
#if !CODEGEN_WASM
success &= RunTest(Threading.Run);
#endif
success &= RunTest(Devirtualization.Run);
#if !CODEGEN_WASM
success &= RunTest(StackTraces.Run);
#endif

return success ? 100 : 1;

static bool RunTest(Func<int> t, [CallerArgumentExpression("t")] string name = null)
{
    Console.WriteLine($"===== Running test {name} =====");
    bool success = true;
    try
    {
        success = t() == 100;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
        success = false;
    }
    Console.WriteLine($"===== Test {name} {(success ? "succeeded" : "failed")} =====");
    return success;
}
