// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifdef TARGET_WINDOWS
#define DLL_EXPORT extern "C" __declspec(dllexport)
#else
#define DLL_EXPORT extern "C" __attribute((visibility("default")))
#endif

DLL_EXPORT int GetNativeFunctionToCall()
{
    return 1;
}

DLL_EXPORT double NativeIntToDouble(int a)
{
    return a;
}

DLL_EXPORT int GetMemCpyLength()
{
    return 255;
}

// Marked as DirectPInvoke.
DLL_EXPORT int SimpleDirectPInvokeTestFunc(int a)
{
    return a;
}

// Also used for the common ABI test.
DLL_EXPORT int CommonStaticFunctionName(int a)
{
    return a;
}
