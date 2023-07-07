// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable IDE0060 // Remove unused parameter

using System;

internal static partial class Interop
{
    internal static unsafe partial class JsGlobalization
    {
        internal static unsafe int CompareString(in string culture, char* str1, int str1Len, char* str2, int str2Len, global::System.Globalization.CompareOptions options, out int exceptionalResult, out object result) => throw new PlatformNotSupportedException();
        internal static unsafe bool StartsWith(in string culture, char* str1, int str1Len, char* str2, int str2Len, global::System.Globalization.CompareOptions options, out int exceptionalResult, out object result) => throw new PlatformNotSupportedException();
        internal static unsafe bool EndsWith(in string culture, char* str1, int str1Len, char* str2, int str2Len, global::System.Globalization.CompareOptions options, out int exceptionalResult, out object result) => throw new PlatformNotSupportedException();
        internal static unsafe int IndexOf(in string culture, char* str1, int str1Len, char* str2, int str2Len, global::System.Globalization.CompareOptions options, bool fromBeginning, out int exceptionalResult, out object result) => throw new PlatformNotSupportedException();
    }
}
