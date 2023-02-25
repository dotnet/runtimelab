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