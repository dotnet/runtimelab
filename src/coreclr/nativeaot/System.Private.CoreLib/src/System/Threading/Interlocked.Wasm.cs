// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

using Internal.Runtime.CompilerHelpers;

namespace System.Threading
{
    //
    // WASM atomics trap on unaligned access so we must manually check the alignment of locations before
    // calling the underlying instructions, to throw a managed misalignment exception. Likewise with null
    // reference exception. We delegate this to codegen where possible, so most of methods below are
    // always-expand intrinsics.
    //
    // TODO-LLVM: once/if upstream factoring of atomics changes, reconsider the need for a separate file.
    //
    public static partial class Interlocked
    {
        [Intrinsic]
        public static unsafe byte CompareExchange(ref byte location1, byte value, byte comparand) => CompareExchange(ref location1, value, comparand);

        [Intrinsic]
        public static unsafe int CompareExchange(ref int location1, int value, int comparand) => CompareExchange(ref location1, value, comparand);

        [Intrinsic]
        public static unsafe long CompareExchange(ref long location1, long value, long comparand) => CompareExchange(ref location1, value, comparand);

        [Intrinsic]
        [return: NotNullIfNotNull(nameof(location1))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CompareExchange<T>(ref T location1, T value, T comparand) where T : class?
        {
            return Unsafe.As<T>(CompareExchange(ref Unsafe.As<T, object?>(ref location1), value, comparand));
        }

        [Intrinsic]
        [return: NotNullIfNotNull(nameof(location1))]
        public static unsafe object? CompareExchange(ref object? location1, object? value, object? comparand)
        {
            if (Unsafe.IsNullRef(ref location1))
            {
                ThrowHelpers.ThrowNullReferenceException();
            }
            return RuntimeImports.InterlockedCompareExchange(ref location1, value, comparand);
        }

        [Intrinsic]
        public static byte Exchange(ref byte location1, byte value) => Exchange(ref location1, value);

        [Intrinsic]
        public static short Exchange(ref short location1, short value) => Exchange(ref location1, value);

        [Intrinsic]
        public static int Exchange(ref int location1, int value) => Exchange(ref location1, value);

        [Intrinsic]
        public static long Exchange(ref long location1, long value) => Exchange(ref location1, value);

        [Intrinsic]
        [return: NotNullIfNotNull(nameof(location1))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Exchange<T>([NotNullIfNotNull(nameof(value))] ref T location1, T value) where T : class?
        {
            return Unsafe.As<T>(Exchange(ref Unsafe.As<T, object?>(ref location1), value));
        }

        [Intrinsic]
        [return: NotNullIfNotNull(nameof(location1))]
        public static unsafe object? Exchange([NotNullIfNotNull(nameof(value))] ref object? location1, object? value)
        {
            if (Unsafe.IsNullRef(ref location1))
            {
                ThrowHelpers.ThrowNullReferenceException();
            }
            return RuntimeImports.InterlockedExchange(ref location1, value);
        }

        [Intrinsic]
        private static int ExchangeAdd(ref int location1, int value) => ExchangeAdd(ref location1, value);

        [Intrinsic]
        private static long ExchangeAdd(ref long location1, long value) => ExchangeAdd(ref location1, value);
    }
}
