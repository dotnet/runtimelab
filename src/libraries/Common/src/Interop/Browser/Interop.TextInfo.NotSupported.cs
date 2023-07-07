// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable IDE0060 // Remove unused parameter

using System;

internal static partial class Interop
{
    internal static unsafe partial class JsGlobalization
    {
        internal static unsafe void ChangeCaseInvariant(char* src, int srcLen, char* dstBuffer, int dstBufferCapacity, bool bToUpper, out int exceptionalResult, out object result) => throw new PlatformNotSupportedException();
        internal static unsafe void ChangeCase(in string culture, char* src, int srcLen, char* dstBuffer, int dstBufferCapacity, bool bToUpper, out int exceptionalResult, out object result) => throw new PlatformNotSupportedException();
    }
}
