// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Runtime imports specific to WASM. Currently these are mostly PInvoke-based
// redefinitions of the math-related methods, as the runtime import mechanism
// assumes a managed calling convention (with a shadow stack), while those
// functions are native.

using System.Runtime.InteropServices;

namespace System.Runtime
{
    internal static partial class RuntimeImports
    {
        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double acos(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float acosf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double acosh(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float acoshf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double asin(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float asinf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double asinh(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float asinhf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double atan(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float atanf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double atan2(double y, double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float atan2f(float y, float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double atanh(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float atanhf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double cbrt(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float cbrtf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double ceil(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float ceilf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double cos(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float cosf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double cosh(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float coshf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double exp(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float expf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double floor(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float floorf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double log(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float logf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double log2(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float log2f(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double log10(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float log10f(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double pow(double x, double y);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float powf(float x, float y);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double sin(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float sinf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double sinh(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float sinhf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double sqrt(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float sqrtf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double tan(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float tanf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double tanh(double x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float tanhf(float x);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double fmod(double x, double y);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float fmodf(float x, float y);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial double fma(double x, double y, double z);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static partial float fmaf(float x, float y, float z);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static unsafe partial double modf(double x, double* intptr);

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        internal static unsafe partial float modff(float x, float* intptr);
    }
}
