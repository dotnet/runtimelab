// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Internal calls specific to the WASM target. These can have unusual calling convention conversion
// requirements and so are defined/implemented here separately from the rest. Specifically, some of
// the underlying FCalls have the shadow stack argument, which we don't want to explicitly spell out
// in the managed signature (to keep it cross-target) and so create wrappers that pass it implicitly.
//
using System.Runtime.CompilerServices;

using Internal.Runtime;

#pragma warning disable 8500 // Cannot take the address of, get the size of, or declare a pointer to a managed type

namespace System.Runtime
{
    internal static partial class InternalCalls
    {
        internal static unsafe void RhpThrowNativeException(object* pManagedException)
        {
            [RuntimeImport(Redhawk.BaseName, "RhpThrowNativeException")]
            [MethodImpl(MethodImplOptions.InternalCall)]
            static extern unsafe void Impl(void* pDispatcherShadowFrame, object* pManagedException);

            void* pImpl = (delegate*<void*, object*, void>)&Impl;
            ((delegate*<object*, void>)pImpl)(pManagedException);
        }

        [RuntimeImport(Redhawk.BaseName, "RhpReleaseNativeException")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern unsafe void RhpReleaseNativeException(void* pDispatchData);

        internal static unsafe int RhpCallCatchOrFilterFunclet(void* pOriginalShadowFrame, object exception, void* pFunclet)
        {
            [RuntimeImport(Redhawk.BaseName, "RhpCallCatchOrFilterFunclet")]
            [MethodImpl(MethodImplOptions.InternalCall)]
            static extern int Impl(void* pShadowFrame, void* pOriginalShadowFrame, object exception, void* pFunclet);

            void* pImpl = (delegate*<void*, void*, object, void*, int>)&Impl;
            return ((delegate*<void*, object, void*, int>)pImpl)(pOriginalShadowFrame, exception, pFunclet);
        }

        internal static unsafe object RhpNewFast(MethodTable* pEEType) // BEWARE: not for finalizable objects!
        {
            [RuntimeImport(Redhawk.BaseName, "RhpNewFast")]
            [MethodImpl(MethodImplOptions.InternalCall)]
            static extern object Impl(void* pShadowStack, MethodTable* pEEType);

            void* pImpl = (delegate*<void*, MethodTable*, object>)&Impl;
            return ((delegate*<MethodTable*, object>)pImpl)(pEEType);
        }

        internal static unsafe object RhpNewFinalizable(MethodTable* pEEType)
        {
            [RuntimeImport(Redhawk.BaseName, "RhpNewFinalizable")]
            [MethodImpl(MethodImplOptions.InternalCall)]
            static extern object Impl(void* pShadowStack, MethodTable* pEEType);

            void* pImpl = (delegate*<void*, MethodTable*, object>)&Impl;
            return ((delegate*<MethodTable*, object>)pImpl)(pEEType);
        }

        internal static unsafe object RhpNewArray(MethodTable* pEEType, int length)
        {
            [RuntimeImport(Redhawk.BaseName, "RhpNewArray")]
            [MethodImpl(MethodImplOptions.InternalCall)]
            static extern object Impl(void* pShadowStack, MethodTable* pEEType, int length);

            void* pImpl = (delegate*<void*, MethodTable*, int, object>)&Impl;
            return ((delegate*<MethodTable*, int, object>)pImpl)(pEEType, length);
        }

#if FEATURE_64BIT_ALIGNMENT
        internal static unsafe object RhpNewFastAlign8(MethodTable* pEEType) // BEWARE: not for finalizable objects!
        {
            [RuntimeImport(Redhawk.BaseName, "RhpNewFastAlign8")]
            [MethodImpl(MethodImplOptions.InternalCall)]
            static extern object Impl(void* pShadowStack, MethodTable* pEEType);

            void* pImpl = (delegate*<void*, MethodTable*, object>)&Impl;
            return ((delegate*<MethodTable*, object>)pImpl)(pEEType);

        }

        internal static unsafe object RhpNewFinalizableAlign8(MethodTable* pEEType)
        {
            [RuntimeImport(Redhawk.BaseName, "RhpNewFinalizableAlign8")]
            [MethodImpl(MethodImplOptions.InternalCall)]
            static extern object Impl(void* pShadowStack, MethodTable* pEEType);

            void* pImpl = (delegate*<void*, MethodTable*, object>)&Impl;
            return ((delegate*<MethodTable*, object>)pImpl)(pEEType);

        }

        internal static unsafe object RhpNewArrayAlign8(MethodTable* pEEType, int length)
        {
            [RuntimeImport(Redhawk.BaseName, "RhpNewArrayAlign8")]
            [MethodImpl(MethodImplOptions.InternalCall)]
            static extern object Impl(void* pShadowStack, MethodTable* pEEType, int length);

            void* pImpl = (delegate*<void*, MethodTable*, int, object>)&Impl;
            return ((delegate*<MethodTable*, int, object>)pImpl)(pEEType, length);
        }

        internal static unsafe object RhpNewFastMisalign(MethodTable* pEEType)
        {
            [RuntimeImport(Redhawk.BaseName, "RhpNewFastMisalign")]
            [MethodImpl(MethodImplOptions.InternalCall)]
            static extern object Impl(void* pShadowStack, MethodTable* pEEType);

            void* pImpl = (delegate*<void*, MethodTable*, object>)&Impl;
            return ((delegate*<MethodTable*, object>)pImpl)(pEEType);
        }
#endif // FEATURE_64BIT_ALIGNMENT
    }
}
