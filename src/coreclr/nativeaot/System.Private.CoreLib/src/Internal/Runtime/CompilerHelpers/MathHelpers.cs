// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// Math helpers for generated code. The helpers here are referenced by the runtime.
    /// </summary>
    [StackTraceHidden]
    internal static partial class MathHelpers
    {
#if !TARGET_64BIT
        private const string RuntimeLibrary = "*";

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        private static partial ulong RhpULMod(ulong dividend, ulong divisor);

        public static ulong ULMod(ulong dividend, ulong divisor)
        {
            if (divisor == 0)
                ThrowHelper.ThrowDivideByZeroException();

            return RhpULMod(dividend, divisor);
        }

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        private static partial long RhpLMod(long dividend, long divisor);

        public static long LMod(long dividend, long divisor)
        {
            if (divisor == 0)
                ThrowHelper.ThrowDivideByZeroException();
            if (divisor == -1 && dividend == long.MinValue)
                ThrowHelper.ThrowOverflowException();

            return RhpLMod(dividend, divisor);
        }

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        private static partial ulong RhpULDiv(ulong dividend, ulong divisor);

        public static ulong ULDiv(ulong dividend, ulong divisor)
        {
            if (divisor == 0)
                ThrowHelper.ThrowDivideByZeroException();

            return RhpULDiv(dividend, divisor);
        }

        [LibraryImport(RuntimeLibrary)]
        [SuppressGCTransition]
        private static partial long RhpLDiv(long dividend, long divisor);

        public static long LDiv(long dividend, long divisor)
        {
            if (divisor == 0)
                ThrowHelper.ThrowDivideByZeroException();
            if (divisor == -1 && dividend == long.MinValue)
                ThrowHelper.ThrowOverflowException();

            return RhpLDiv(dividend, divisor);
        }

#if TARGET_ARM || TARGET_WASM // TODO-LLVM: include TARGET_WASM at least until we copy over the implementations from IL to RyuJit
        [RuntimeImport(RuntimeLibrary, "RhpIDiv")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int RhpIDiv(int dividend, int divisor);

        public static int IDiv(int dividend, int divisor)
        {
            if (divisor == 0)
                ThrowHelper.ThrowDivideByZeroException();
            if (divisor == -1 && dividend == int.MinValue)
                ThrowHelper.ThrowOverflowException();

            return RhpIDiv(dividend, divisor);
        }

        [RuntimeImport(RuntimeLibrary, "RhpUDiv")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern uint RhpUDiv(uint dividend, uint divisor);

        public static long UDiv(uint dividend, uint divisor)
        {
            if (divisor == 0)
                ThrowHelper.ThrowDivideByZeroException();

            return RhpUDiv(dividend, divisor);
        }

        [RuntimeImport(RuntimeLibrary, "RhpIMod")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int RhpIMod(int dividend, int divisor);

        public static int IMod(int dividend, int divisor)
        {
            if (divisor == 0)
                ThrowHelper.ThrowDivideByZeroException();
            if (divisor == -1 && dividend == int.MinValue)
                ThrowHelper.ThrowOverflowException();

            return RhpIMod(dividend, divisor);
        }

        [RuntimeImport(RuntimeLibrary, "RhpUMod")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern uint RhpUMod(uint dividend, uint divisor);

        public static long UMod(uint dividend, uint divisor)
        {
            if (divisor == 0)
                return ThrowUIntDivByZero();
            else
                return RhpUMod(dividend, divisor);
        }
#endif // TARGET_ARM || TARGET_WASM

        //
        // Matching return types of throw helpers enables tailcalling them. It improves performance
        // of the hot path because of it does not need to raise full stackframe.
        //

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ThrowIntOvf()
        {
            throw new OverflowException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static uint ThrowUIntOvf()
        {
            throw new OverflowException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long ThrowLngOvf()
        {
            throw new OverflowException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ulong ThrowULngOvf()
        {
            throw new OverflowException();
        }

#if TARGET_ARM || TARGET_WASM // TODO-LLVM: include TARGET_WASM at least until we copy over the implementations from IL to RyuJit
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ThrowIntDivByZero()
        {
            throw new DivideByZeroException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static uint ThrowUIntDivByZero()
        {
            throw new DivideByZeroException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ThrowIntArithExc()
        {
            throw new ArithmeticException();
        }
#endif // TARGET_ARM || TARGET_WASM
#endif // TARGET_64BIT
    }
}
