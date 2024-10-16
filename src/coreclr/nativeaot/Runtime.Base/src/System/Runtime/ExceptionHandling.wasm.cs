// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

using Internal.Runtime;
using Internal.Runtime.Augments;

// Disable: Filter expression is a constant. We know. We just can't do an unfiltered catch.
#pragma warning disable 7095

//
// WASM exception handling.
// See: https://github.com/dotnet/runtimelab/blob/feature/NativeAOT-LLVM/docs/design/coreclr/botr/nativeaot-wasm-exception-handling.md.
//
namespace System.Runtime
{
    internal static unsafe partial class EH
    {
        private const nuint UnwindIndexNotInTry = 0;
        private const nuint UnwindIndexBase = 2;

        [ThreadStatic]
        private static ExceptionDispatchData? t_lastDispatchedException;
        [ThreadStatic]
        private static CallFilterFrame* t_pLastCallFilterFrame;

        [RuntimeExport("RhpThrowEx")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RhpThrowEx(object exception)
        {
#if INPLACE_RUNTIME
            // Turn "throw null" into "throw new NullReferenceException()".
            exception ??= new NullReferenceException();
#else
#error Implement "throw null" in non-INPLACE_RUNTIME builds
#endif
            DispatchException(exception, RhEHFrameType.RH_EH_FIRST_FRAME);
        }

        [RuntimeExport("RhpRethrow")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RhpRethrow(object exception)
        {
            DispatchException(exception, RhEHFrameType.RH_EH_FIRST_FRAME | RhEHFrameType.RH_EH_FIRST_RETHROW_FRAME);
        }

        // Note that this method cannot have any catch handlers as it manipulates the virtual unwind frames directly
        // and exits via native unwind (it would not pop the frame it would push). This is accomplished by calling
        // all user code via separate noinline methods. It also cannot throw any exceptions as that would lead to
        // infinite recursion.
        //
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DispatchException(object exception, RhEHFrameType flags)
        {
            WasmEHLogFirstPassEnter(exception, (flags & RhEHFrameType.RH_EH_FIRST_RETHROW_FRAME) != 0);
            OnFirstChanceExceptionNoInline(exception);

            // Find the handler for this exception by virtually unwinding the stack of active protected regions.
            nuint unwindCount = 0;
            void* pFrameLimit = GetVirtualUnwindFrameLimit();
            VirtualUnwindFrame* pFrame = GetLastVirtualUnwindFrame(skip: 2, pFrameLimit);
            VirtualUnwindFrame* pFirstCatchFrame = null;
            for (; pFrame != null; pFrame = GetPrevious(pFrame, pFrameLimit))
            {
                nint ip = GetStackTraceIp(pFrame);
                GetAppendStackFrame(exception)(exception, ip, (int)flags);
                flags = 0;

                void* pEHInfo = GetEHInfo(pFrame);
                if (pEHInfo == null)
                {
                    continue;
                }

                nuint index = GetUnwindIndex(pFrame);
                if (IsCatchUnwindIndex(index) && pFirstCatchFrame == null)
                {
                    pFirstCatchFrame = pFrame;
                }

                EHTable table = new EHTable(pEHInfo);
                while (IsCatchUnwindIndex(index))
                {
                    EHClause clause;
                    nuint enclosingIndex = table.GetClauseInfo(index, &clause);
                    WasmEHLogEHTableEntry(pFrame, index, &clause);

                    if (clause.Filter != null)
                    {
                        if (CallFilterFunclet(clause.Filter, exception, pFrame, pFrameLimit))
                        {
                            goto FoundHandler;
                        }
                    }
                    else
                    {
                        if (ShouldTypedClauseCatchThisException(exception, clause.ClauseType, false /* tryUnwrapException, not used for NATIVEAOT */))
                        {
                            goto FoundHandler;
                        }
                    }

                    index = enclosingIndex;
                    unwindCount++;
                }
            }

        FoundHandler:
            // We currently install an unhandled exception handler for RPI frames in codegen and so will never fail to
            // find one. We could handle unhandled exceptions here, with the caveat being that virtual unwinding would
            // need to become aware of RPI. Notably, we still check for a null frame, to get reliable failure modes.
            if (pFrame == null)
            {
                FallbackFailFast(RhFailFastReason.InternalError, exception);
            }
            WasmEHLogFirstPassExit(pFrame, unwindCount);

            // Thread this exception onto the list of currently active exceptions. We need to keep the managed exception
            // object alive during the second pass and using a thread static is the most straightforward way to achive
            // this. Additionally, not having to inspect the native exception in the second pass is better for code size.
            t_lastDispatchedException = new() // TODO-LLVM: this can fail with an OOM and lead to infinite recursion.
            {
                Prev = t_lastDispatchedException,
                ExceptionObject = exception,
                RemainingUnwindCount = unwindCount,
                NextCatchFrame = pFirstCatchFrame,
                NextCatchIndex = GetUnwindIndex(pFirstCatchFrame)
            };

            if (!RuntimeAugments.PreciseVirtualUnwind)
            {
                SparseVirtualUnwindFrame** pLastFrameRef = SparseVirtualUnwindFrame.GetLastRef();
                *pLastFrameRef = UnlinkSparseNotInTryFrames(*pLastFrameRef);
            }

            // Initiate the second pass by throwing a native exception.
            WasmEHLog("Initiating the second pass via native throw", 2);
            InternalCalls.RhpThrowNativeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)] // We avoid modifying common code with this noinline wrapper.
        private static void OnFirstChanceExceptionNoInline(object exception) => OnFirstChanceExceptionViaClassLib(exception);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CallFilterFunclet(void* pFunclet, object exception, VirtualUnwindFrame* pFrame, void* pFrameLimit)
        {
            void* pOriginalShadowStack = GetOriginalShadowStack(pFrame);
            WasmEHLogFilterEnter(pFunclet, RhEHClauseKind.RH_EH_CLAUSE_FILTER, pOriginalShadowStack);

            CallFilterFrame callFilterFrame;
            if (RuntimeAugments.PreciseVirtualUnwind)
            {
                callFilterFrame.CallFilterFuncletFrame = PreciseVirtualUnwindFrame.GetLast(0, pFrameLimit);
                callFilterFrame.HandlerMethodFrame = (PreciseVirtualUnwindFrame*)pFrame;
                callFilterFrame.Prev = t_pLastCallFilterFrame;
                t_pLastCallFilterFrame = &callFilterFrame;
            }
            bool result;
            try
            {
                result = ((delegate*<void*, object, int>)pFunclet)(pOriginalShadowStack, exception) != 0;
            }
            catch when (true)
            {
                result = false; // A filter that throws is treated as if it returned "continue search".
            }
            if (RuntimeAugments.PreciseVirtualUnwind)
            {
                t_pLastCallFilterFrame = t_pLastCallFilterFrame->Prev;
            }

            WasmEHLogFilterExit(RhEHClauseKind.RH_EH_CLAUSE_FILTER, result, pOriginalShadowStack);
            return result;
        }

        // This helper is called by codegen at the beginning of catch handlers. It should return the exception object
        // if control is to be transferred to the handler and null if unwinding should continue. Like the first pass
        // method above, it cannot push/pop virtual unwind frames due to the manual chain manipulation.
        //
        [MethodImpl(MethodImplOptions.NoInlining)]
        [RuntimeExport("RhpHandleExceptionWasmCatch")]
        private static object RhpHandleExceptionWasmCatch(nuint catchUnwindIndex)
        {
            Debug.Assert(IsCatchUnwindIndex(catchUnwindIndex));
            ref ExceptionDispatchData? lastExceptionRef = ref t_lastDispatchedException;
            ExceptionDispatchData? lastException = lastExceptionRef;
            Debug.Assert(lastException != null);

            VirtualUnwindFrame* pCatchFrame = lastException.NextCatchFrame;
            Debug.Assert(pCatchFrame == GetLastVirtualUnwindFrame(skip: 1, GetVirtualUnwindFrameLimit()));

            // Have we reached the unwind destination of this exception?
            if (lastException.RemainingUnwindCount == 0)
            {
                WasmEHLog("Exception caught at [" + ToHex(pCatchFrame) + "][" + ToDec(catchUnwindIndex) + "]", 2);
                object exceptionObject = lastException.ExceptionObject;

                // Release the native and managed memory used for this dispatch.
                InternalCalls.RhpReleaseNativeException();
                lastException = lastException.Prev;

                // In nested dispatch - when an exception is thrown inside the active fault handler's call stack,
                // exceptions can go "abandoned", i. e. replaced by the nested one. This happens when the nested
                // exception escapes from the fault handler of its upstream cousin:
                //
                // [try                        ][catch C1] ; Will catch the nested exception
                // ...
                // [try              ][catch C0]           ; Would have caught the original exception
                // ...
                // [try][active fault]                     ; Triggered by the original exception
                // /|\
                //  |       /|\ ; The nested exception is unwinding upwards
                //  |        |
                //           |
                //      [nested throw]
                //
                // It is hence critical that we unlink all abandoned exceptions from the active exception list,
                // so that upstream handlers do not catch them. To this end we maintan the "next catch" fields
                // during the second pass: if the upstream exception is yet to unwind this catch handler, that
                // means the nested one (which, recall, is being caught here) ended up replacing it. This works
                // because all fault handlers that trigger nested dispatch always lie below the next catch and
                // all catches that would **not** result in abandonment (thus "containing" the nested exception)
                // lie below those faults.
                //
                while (lastException != null && IsBelowOrSame(lastException.NextCatchFrame, lastException.NextCatchIndex, pCatchFrame, catchUnwindIndex))
                {
                    WasmEHLog("Abandoning an exception (next catch was at " +
                        "[" + ToHex(lastException.NextCatchFrame) + "][" + ToDec(lastException.NextCatchIndex) + "])", 2);
                    InternalCalls.RhpReleaseNativeException();
                    lastException = lastException.Prev;
                }
                lastExceptionRef = lastException;

                return exceptionObject;
            }

            // Maintain the consistency of the virtual unwind stack if we are unwinding out of this frame.
            nuint enclosingCatchIndex = new EHTable(GetEHInfo(pCatchFrame)).GetClauseInfo(catchUnwindIndex);
            if (!RuntimeAugments.PreciseVirtualUnwind && enclosingCatchIndex == UnwindIndexNotInTry)
            {
                RhpPopUnwoundSparseVirtualFrames();
            }

            if (!IsCatchUnwindIndex(enclosingCatchIndex))
            {
                // This next frame is yet to be unwound, hence its index represents the actual unwind destination.
                VirtualUnwindFrame* pNextCatchFrame = UnwindToNextCatchFrame(pCatchFrame);
                lastException.NextCatchFrame = pNextCatchFrame;
                lastException.NextCatchIndex = GetUnwindIndex(pNextCatchFrame);
            }
            else
            {
                lastException.NextCatchIndex = enclosingCatchIndex;
            }

            WasmEHLog("Continuing to unwind from [" + ToHex(pCatchFrame) + "][" + ToDec(catchUnwindIndex) + "] to " +
                "[" + ToHex(lastException.NextCatchFrame) + "][" + ToDec(lastException.NextCatchIndex) + "]", 2);
            lastException.RemainingUnwindCount--;
            return null;
        }

        private static bool IsBelowOrSame(VirtualUnwindFrame* pNextCatchFrame, nuint nextCatchIndex, VirtualUnwindFrame* pCurrentFrame, nuint currentIndex)
        {
            // Frames are allocated on the shadow stack, which grows upwards.
            if (pNextCatchFrame > pCurrentFrame)
            {
                return true;
            }

            // The indices are constructed such that enclosed regions come before enclosing ones and this method does
            // assume that a nesting relashionship exists between the two indices. Note that the "next catch" index,
            // if it does refer to a mutually protecting region, will always refer to the "innermost" one, since they
            // are unwound in an uninterrupted succession of each other. The current index, however, may be one from
            // the same run of handlers but "outer". In such a case, our answer does not depend on which index from
            // this run we pick - all will return "true". Hence, no special handling is needed.
            if (pNextCatchFrame == pCurrentFrame)
            {
                return nextCatchIndex <= currentIndex;
            }

            return false;
        }

        private static VirtualUnwindFrame* UnwindToNextCatchFrame(VirtualUnwindFrame* pThisCatchFrame)
        {
            void* pFrameLimit = GetVirtualUnwindFrameLimit();
            VirtualUnwindFrame* pFrame = GetPrevious(pThisCatchFrame, pFrameLimit);
            Debug.Assert(pFrame != null);
            while (GetEHInfo(pFrame) == null || !IsCatchUnwindIndex(GetUnwindIndex(pFrame)))
            {
                pFrame = GetPrevious(pFrame, pFrameLimit);
            }

            Debug.Assert(pFrame != null);
            return pFrame;
        }

        [RuntimeExport("RhpPopUnwoundSparseVirtualFrames")]
        private static void RhpPopUnwoundSparseVirtualFrames()
        {
            SparseVirtualUnwindFrame** pLastFrameRef = SparseVirtualUnwindFrame.GetLastRef();
            SparseVirtualUnwindFrame* pFrame = *pLastFrameRef;
            WasmEHLog("Unlinking [" + ToHex(pFrame) + "] - top-level unwind", 2);
            pFrame = pFrame->Prev;
            pFrame = UnlinkSparseNotInTryFrames(pFrame);

            *pLastFrameRef = pFrame;
        }

        private static SparseVirtualUnwindFrame* UnlinkSparseNotInTryFrames(SparseVirtualUnwindFrame* pFrame)
        {
            Debug.Assert(!RuntimeAugments.PreciseVirtualUnwind);
            Debug.Assert(pFrame != null);
            while (pFrame->UnwindIndex == UnwindIndexNotInTry)
            {
                WasmEHLog("Unlinking [" + ToHex(pFrame) + "] - NotInTry", 2);
                pFrame = pFrame->Prev;
            }

            return pFrame;
        }

        // There are two special unwind indices:
        //  1. IndexNotInTry      (0) - the code is outside of any protected regions.
        //  2. IndexNotInTryCatch (1) - the code is outside of a region protected by a catch handler, i. e. it is in
        //                              a region protected by a fault or finally.
        //
        // For the purposes of finding handlers in the first pass, both can be taken to mean the same thing, however,
        // while in the second pass, only "IndexNotInTry" virtual unwind frames can (and must) be popped eagerly, as
        // the handlers in "IndexNotInTryCatch" frames may access the frame and are responsible for freeing it when
        // exiting by calling "RhpPopUnwoundVirtualFrames".
        //
        private static bool IsCatchUnwindIndex(nuint unwindIndex) => unwindIndex >= UnwindIndexBase;

        // This handler is called by codegen for exceptions that escape from RPI methods (i. e. unhandled exceptions).
        //
        [RuntimeExport("RhpHandleUnhandledException")]
        private static void HandleUnhandledException(object exception)
        {
            GetAppendStackFrame(exception)(exception, 0, 0); // Append JS frames to this unhandled exception for better diagnostics.
            OnUnhandledExceptionViaClassLib(exception);

            // We have to duplicate "UnhandledExceptionFailFastViaClasslib" because we cannot use code addresses to get helpers.
            IntPtr pFailFastFunction = exception.GetMethodTable()->GetClasslibFunction(ClassLibFunctionId.FailFast);

            if (pFailFastFunction == IntPtr.Zero)
            {
                FallbackFailFast(RhFailFastReason.UnhandledException, exception);
            }

            try
            {
                ((delegate*<RhFailFastReason, object, IntPtr, void*, void>)pFailFastFunction)
                    (RhFailFastReason.UnhandledException, exception, 0, null);
            }
            catch when (true)
            {
                // disallow all exceptions leaking out of callbacks
            }

            // The classlib's function should never return and should not throw. If it does, then we fail our way...
            FallbackFailFast(RhFailFastReason.UnhandledException, exception);
        }

        // Stack trace support.
        //
        private static delegate*<object, IntPtr, int, void> GetAppendStackFrame(object exception)
        {
            // We use this more direct way of invoking "AppendExceptionStackFrame" to preserve more user frames in truncated traces.
            nint pAppendStackFrame = exception.GetMethodTable()->GetClasslibFunction(ClassLibFunctionId.AppendExceptionStackFrame);
            return (delegate*<object, IntPtr, int, void>)pAppendStackFrame;
        }

        [RuntimeExport("RhGetCurrentThreadStackTrace")]
        [MethodImpl(MethodImplOptions.NoInlining)] // Ensures that the RhGetCurrentThreadStackTrace frame is always present
        private static unsafe int RhGetCurrentThreadStackTrace(IntPtr[] outputBuffer)
        {
            Debug.Assert(RuntimeAugments.PreciseVirtualUnwind);

            int count = 0;
            void* pFrameLimit = PreciseVirtualUnwindFrame.GetLimit();
            PreciseVirtualUnwindFrame* pFrame = PreciseVirtualUnwindFrame.GetLast(skipCount: 1, pFrameLimit);
            CallFilterFrame* pFilterFrame = t_pLastCallFilterFrame;
            while (pFrame != null)
            {
                nint ip;
                if (pFilterFrame != null && pFrame == pFilterFrame->CallFilterFuncletFrame)
                {
                    // We need to report the filter funclets in stack traces. They will not show up as a frame on
                    // the shadow stack because funclets don't have a shadow frame of their own. We also want to
                    // skip the dispatcher frames.
                    ip = PreciseVirtualUnwindFrame.GetStackTraceIp(pFilterFrame->HandlerMethodFrame);
                    pFrame = PreciseVirtualUnwindFrame.GetPrevious(pFrame, pFrameLimit); // DispatchException.
                    pFrame = PreciseVirtualUnwindFrame.GetPrevious(pFrame, pFrameLimit); // RhpThrowEx/RhpRethrow.
                    pFilterFrame = pFilterFrame->Prev;
                }
                else
                {
                    ip = PreciseVirtualUnwindFrame.GetStackTraceIp(pFrame);
                }

                if (ip != 0)
                {
                    if (count < outputBuffer.Length)
                    {
                        outputBuffer[count] = ip;
                    }
                    count++;
                }
                pFrame = PreciseVirtualUnwindFrame.GetPrevious(pFrame, pFrameLimit);
            }

            return count <= outputBuffer.Length ? count : -count;
        }

        // Abstraction over the two types of virtual unwind frames.
        //
        private struct VirtualUnwindFrame { }

        private static void* GetVirtualUnwindFrameLimit()
        {
            return RuntimeAugments.PreciseVirtualUnwind
                ? PreciseVirtualUnwindFrame.GetLimit()
                : SparseVirtualUnwindFrame.GetLimit();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static VirtualUnwindFrame* GetLastVirtualUnwindFrame(int skip, void* pFrameLimit)
        {
            return RuntimeAugments.PreciseVirtualUnwind
                ? (VirtualUnwindFrame*)PreciseVirtualUnwindFrame.GetLast(skip + 1, pFrameLimit)
                : (VirtualUnwindFrame*)SparseVirtualUnwindFrame.GetLast();
        }

        private static VirtualUnwindFrame* GetPrevious(VirtualUnwindFrame* pFrame, void* pFrameLimit)
        {
            void* pPrevFrame = RuntimeAugments.PreciseVirtualUnwind
                ? PreciseVirtualUnwindFrame.GetPrevious((PreciseVirtualUnwindFrame*)pFrame, pFrameLimit)
                : SparseVirtualUnwindFrame.GetPrevious((SparseVirtualUnwindFrame*)pFrame);

            Debug.Assert(pFrame != pPrevFrame);
            return (VirtualUnwindFrame*)pPrevFrame;
        }

        private static void* GetEHInfo(VirtualUnwindFrame* pFrame)
        {
            return RuntimeAugments.PreciseVirtualUnwind
                ? PreciseVirtualUnwindFrame.GetEHInfo((PreciseVirtualUnwindFrame*)pFrame)
                : SparseVirtualUnwindFrame.GetEHInfo((SparseVirtualUnwindFrame*)pFrame);
        }

        private static nuint GetUnwindIndex(VirtualUnwindFrame* pFrame)
        {
            return RuntimeAugments.PreciseVirtualUnwind
                ? PreciseVirtualUnwindFrame.GetUnwindIndex((PreciseVirtualUnwindFrame*)pFrame)
                : SparseVirtualUnwindFrame.GetUnwindIndex((SparseVirtualUnwindFrame*)pFrame);
        }

        private static void* GetOriginalShadowStack(VirtualUnwindFrame* pFrame)
        {
            return RuntimeAugments.PreciseVirtualUnwind
                ? PreciseVirtualUnwindFrame.GetOriginalShadowStack((PreciseVirtualUnwindFrame*)pFrame)
                : SparseVirtualUnwindFrame.GetOriginalShadowStack((SparseVirtualUnwindFrame*)pFrame);
        }

        private static nint GetStackTraceIp(VirtualUnwindFrame* pFrame)
        {
            return RuntimeAugments.PreciseVirtualUnwind
                ? PreciseVirtualUnwindFrame.GetStackTraceIp((PreciseVirtualUnwindFrame*)pFrame)
                : SparseVirtualUnwindFrame.GetStackTraceIp((SparseVirtualUnwindFrame*)pFrame);
        }

        // These are present in each virtually unwindable frame (not just those with EH).
        //
        private struct PreciseVirtualUnwindFrame
        {
            public byte* UnwindInfo;

            public static void* GetLimit()
            {
                return InternalCalls.RhpGetCurrentThreadShadowStackBottom();
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static PreciseVirtualUnwindFrame* GetLast(int skipCount, void* pFrameLimit)
            {
                skipCount++; // Include this method.

                var lastFrame = (PreciseVirtualUnwindFrame*)InternalCalls.RhpGetLastPreciseVirtualUnwindFrame();
                while (skipCount > 0)
                {
                    lastFrame = GetPrevious(lastFrame, pFrameLimit);
                    skipCount--;
                }

                return lastFrame;
            }

            public static PreciseVirtualUnwindFrame* GetPrevious(PreciseVirtualUnwindFrame* pFrame, void* pFrameLimit)
            {
                Debug.Assert(pFrame > pFrameLimit);

                uint shadowFrameSize;
                PreciseVirtualUnwindInfo.Parse(pFrame->UnwindInfo, &shadowFrameSize);
                byte* pPreviousFrame = (byte*)pFrame - shadowFrameSize;
                if (pPreviousFrame <= pFrameLimit)
                {
                    // We have reached top of the stack. Because the initial shadow stack alignment is large enough
                    // to satisfy any overaligned frame, the first frame will never have any padding.
                    Debug.Assert((nuint)pFrameLimit % sizeof(double) == 0);
                    Debug.Assert(pFrameLimit == pPreviousFrame + sizeof(PreciseVirtualUnwindFrame));
                    return null;
                }
                while (*(nuint*)pPreviousFrame == 0)
                {
                    pPreviousFrame -= sizeof(nuint); // Skip padding introduced by overaligned frames.
                }

                return (PreciseVirtualUnwindFrame*)pPreviousFrame;
            }

            public static void* GetEHInfo(PreciseVirtualUnwindFrame* pFrame)
            {
                void* pEHInfo;
                PreciseVirtualUnwindInfo.Parse(pFrame->UnwindInfo, ppEHInfo: &pEHInfo);
                return pEHInfo;
            }

            public static nuint GetUnwindIndex(PreciseVirtualUnwindFrame* pFrame)
            {
                Debug.Assert(GetEHInfo(pFrame) != null); // Only EH frames have the unwind index.
                return *((nuint*)pFrame - 1);
            }

            public static void* GetOriginalShadowStack(PreciseVirtualUnwindFrame* pFrame)
            {
                // The precise frame is allocated on the logical bottom of the frame, while the original shadow stack
                // points at its top.
                uint shadowFrameSize;
                PreciseVirtualUnwindInfo.Parse(pFrame->UnwindInfo, &shadowFrameSize);
                return (byte*)pFrame + sizeof(PreciseVirtualUnwindFrame) - shadowFrameSize;
            }

            public static nint GetStackTraceIp(PreciseVirtualUnwindFrame* pFrame)
            {
                // The unwind info is our "IP" in the precise model (for visible frames).
                bool hasStackTraceIp;
                byte* pUnwindInfo = pFrame->UnwindInfo;
                PreciseVirtualUnwindInfo.Parse(pUnwindInfo, pHasStackTraceIp: &hasStackTraceIp);
                return hasStackTraceIp ? (nint)pUnwindInfo : 0;
            }
        }

        // These are pushed by codegen on the shadow stack for frames that have at least one region protected by a catch.
        //
        private struct SparseVirtualUnwindFrame
        {
            public SparseVirtualUnwindFrame* Prev;
            public void* UnwindTable;
            public nuint UnwindIndex;

            public static void* GetLimit()
            {
                return null;
            }

            public static SparseVirtualUnwindFrame** GetLastRef()
            {
                return (SparseVirtualUnwindFrame**)InternalCalls.RhpGetLastSparseVirtualUnwindFrameRef();
            }

            public static SparseVirtualUnwindFrame* GetLast()
            {
                return *GetLastRef();
            }

            public static SparseVirtualUnwindFrame* GetPrevious(SparseVirtualUnwindFrame* pFrame)
            {
                return pFrame->Prev;
            }

            public static void* GetEHInfo(SparseVirtualUnwindFrame* pFrame)
            {
                return pFrame->UnwindTable;
            }

            public static nuint GetUnwindIndex(SparseVirtualUnwindFrame* pFrame)
            {
                return pFrame->UnwindIndex;
            }

            public static void* GetOriginalShadowStack(SparseVirtualUnwindFrame* pFrame)
            {
                // The sparse virtual unwind frame is always allocated at the top of the frame by codegen.
                return pFrame;
            }

            public static nint GetStackTraceIp(SparseVirtualUnwindFrame* pFrame)
            {
#if TARGET_BROWSER
                int wasmFunctionIndex = Unsafe.ReadUnaligned<int>((byte*)pFrame->UnwindTable - sizeof(int));
                int wasmFunctionIndexWithBias = Exception.GetBiasedWasmFunctionIndex(wasmFunctionIndex);
                return wasmFunctionIndexWithBias;
#else
                return 0;
#endif
            }
        }

        private sealed class ExceptionDispatchData
        {
            public ExceptionDispatchData? Prev;
            public object ExceptionObject;
            public nuint RemainingUnwindCount;
            public VirtualUnwindFrame* NextCatchFrame;
            public nuint NextCatchIndex;
        }

        private unsafe struct CallFilterFrame
        {
            public PreciseVirtualUnwindFrame* CallFilterFuncletFrame;
            public PreciseVirtualUnwindFrame* HandlerMethodFrame;
            public CallFilterFrame* Prev;
        }

        private unsafe struct EHClause
        {
            public MethodTable* ClauseType;
            public void* Filter;
        }

        private unsafe struct EHTable
        {
            private const nuint MetadataLargeFormat = 1;
            private const nuint MetadataFilter = 1 << 1;
            private const int MetadataShift = 2;

            private readonly byte* _pEHTable;
            private readonly bool _isLargeFormat;

            public EHTable(void* pEHInfo)
            {
                _pEHTable = (byte*)pEHInfo;
                _isLargeFormat = (*(byte*)pEHInfo & MetadataLargeFormat) != 0;
            }

            public readonly nuint GetClauseInfo(nuint index, EHClause* pClause = null)
            {
                Debug.Assert(IsCatchUnwindIndex(index));
                nuint largeFormatEntrySize = (nuint)(sizeof(uint) + sizeof(void*));
                nuint smallFormatEntrySize = (nuint)(sizeof(byte) + sizeof(void*));

                nuint zeroBasedIndex = index - UnwindIndexBase;
                nuint metadata = _isLargeFormat
                    ? Unsafe.ReadUnaligned<uint>(_pEHTable + zeroBasedIndex * largeFormatEntrySize)
                    : Unsafe.ReadUnaligned<byte>(_pEHTable + zeroBasedIndex * smallFormatEntrySize);

                if (pClause != null)
                {
                    nuint value = _isLargeFormat
                        ? Unsafe.ReadUnaligned<nuint>(_pEHTable + zeroBasedIndex * largeFormatEntrySize + sizeof(uint))
                        : Unsafe.ReadUnaligned<nuint>(_pEHTable + zeroBasedIndex * smallFormatEntrySize + sizeof(byte));

                    if ((metadata & MetadataFilter) != 0)
                    {
                        pClause->Filter = (void*)value;
                        pClause->ClauseType = null;
                    }
                    else
                    {
                        pClause->Filter = null;
                        pClause->ClauseType = (MethodTable*)value;
                    }
                }

                nuint enclosingIndex = metadata >> MetadataShift;
                return enclosingIndex;
            }
        }

        [Conditional("ENABLE_NOISY_WASM_EH_LOG")]
        private static void WasmEHLog(string message, int pass, string prefix = "")
        {
            int dispatchIndex = 0;
            for (ExceptionDispatchData? exception = t_lastDispatchedException; exception != null; exception = exception.Prev)
            {
                dispatchIndex++;
            }
            if (pass != 1)
            {
                dispatchIndex--;
            }

            string log = prefix + "WASM EH";
            log += " [N: " + ToDec(dispatchIndex) + "]";
            log += " [" + ToDec(pass) + "]";
            log += ": " + message + Environment.NewLineConst;

            byte[] bytes = new byte[log.Length + 1];
            for (int i = 0; i < log.Length; i++)
            {
                bytes[i] = (byte)log[i];
            }

            fixed (byte* p = bytes)
            {
                Interop.Sys.Log(p, bytes.Length);
            }
        }

        [Conditional("ENABLE_NOISY_WASM_EH_LOG")]
        private static void WasmEHLogFirstPassEnter(object exception, bool isFirstRethrowFrame)
        {
            string kind = isFirstRethrowFrame ? "Rethrowing" : "Throwing";
            WasmEHLog(kind + ": [" + exception.GetType() + "]", 1, "\n");
        }

        [Conditional("ENABLE_NOISY_WASM_EH_LOG")]
        private static void WasmEHLogEHTableEntry(VirtualUnwindFrame* pClauseFrame, nuint clauseUnwindIndex, EHClause* pClause)
        {
            string description = pClause->Filter != null
                ? "filtered catch, filter at [" + ToHex(pClause->Filter) + "]"
                : "catch, class [" + Type.GetTypeFromMethodTable(pClause->ClauseType) + "]";

            WasmEHLog("Candidate clause [" + ToHex(pClauseFrame) + "][" + ToDec(clauseUnwindIndex) + "]: " + description, 1);
        }

        [Conditional("ENABLE_NOISY_WASM_EH_LOG")]
        private static void WasmEHLogFilterEnter(void* pFilter, RhEHClauseKind kind, void* pShadowFrame)
        {
            WasmEHLog("Calling filter funclet at [" + ToHex(pFilter) + "] on SF [" + ToHex(pShadowFrame) + "]", 1);
        }

        [Conditional("ENABLE_NOISY_WASM_EH_LOG")]
        private static void WasmEHLogFilterExit(RhEHClauseKind kind, bool result, void* pShadowFrame)
        {
            WasmEHLog("Funclet returned: " + (result ? "true" : "false"), 1);
        }

        [Conditional("ENABLE_NOISY_WASM_EH_LOG")]
        private static void WasmEHLogFirstPassExit(VirtualUnwindFrame* pHandlingFrame, nuint unwindCount)
        {
            WasmEHLog("Handler found at [" + ToHex(pHandlingFrame) + "], unwind count: " + ToDec((nint)unwindCount), 1);
        }

        private static string ToHex(void* value) => "0x" + ToHex((nint)value);

        private static string ToHex(nint value)
        {
            int length = 2 * sizeof(nint);
            char* chars = stackalloc char[length];
            for (int i = length - 1, j = 0; j < length; i--, j++)
            {
                chars[j] = "0123456789ABCDEF"[(int)((value >> (i * 4)) & 0xF)];
            }

            return new string(chars, 0, length);
        }

        private static string ToDec(nint value) => ToDec((nuint)value);

        private static string ToDec(nuint value)
        {
            const int MaxLength = 20; // $"{ulong.MaxValue}".Length.
            char* chars = stackalloc char[MaxLength];

            char* pLast = &chars[MaxLength - 1];
            char* pCurrent = pLast;
            do
            {
                *pCurrent-- = "0123456789"[(int)(value % 10)];
                value /= 10;
            }
            while (value != 0);

            return new string(pCurrent + 1, 0, (int)(pLast - pCurrent));
        }
    }

    internal static unsafe class PreciseVirtualUnwindInfo
    {
        private static nuint GetUnwindInfoViaAbsoluteValueLimit() => /* This value will be substituted by the ILC. */ 0;

        public static int Parse(byte* pUnwindInfo, uint* pShadowFrameSize = null, void** pFunctionPointer = null, void** ppEHInfo = null, bool* pHasStackTraceIp = null)
        {
            Debug.Assert(GetUnwindInfoViaAbsoluteValueLimit() > 0);

            if ((nuint)pUnwindInfo < GetUnwindInfoViaAbsoluteValueLimit())
            {
                if (pShadowFrameSize != null)
                {
                    *pShadowFrameSize = (uint)pUnwindInfo * (uint)sizeof(void*);
                }
                if (pFunctionPointer != null)
                {
                    *pFunctionPointer = null;
                }
                if (ppEHInfo != null)
                {
                    *ppEHInfo = null;
                }
                if (pHasStackTraceIp != null)
                {
                    *pHasStackTraceIp = false;
                }
                return 0;
            }

            const uint HasExtendedInfoFlag = 1 << 7;
            const uint HasFunctionPointerFlag = 1 << 6;
            const uint AllFlags = HasExtendedInfoFlag | HasFunctionPointerFlag;
            const uint SmallShadowFrameSizeInSlotsLimit = byte.MaxValue & ~AllFlags;
            const uint IsHiddenExtendedFlag = 1 << 7;
            const uint SmallEHInfoSizeLimit = byte.MaxValue & ~IsHiddenExtendedFlag;

            byte* pCurrent = pUnwindInfo;
            uint smallShadowFrameSizeAndFlags = *pCurrent++;
            uint shadowFrameSizeInSlots = smallShadowFrameSizeAndFlags & ~AllFlags;
            if (shadowFrameSizeInSlots >= SmallShadowFrameSizeInSlotsLimit)
            {
                shadowFrameSizeInSlots = Unsafe.ReadUnaligned<uint>(pCurrent);
                pCurrent += sizeof(uint);
            }
            if (pShadowFrameSize != null)
            {
                *pShadowFrameSize = shadowFrameSizeInSlots * (uint)sizeof(void*);
            }

            void* functionPointer = null;
            if ((smallShadowFrameSizeAndFlags & HasFunctionPointerFlag) != 0)
            {
                functionPointer = (void*)Unsafe.ReadUnaligned<nuint>(pCurrent);
                pCurrent += sizeof(void*);
            }
            if (pFunctionPointer != null)
            {
                *pFunctionPointer = functionPointer;
            }

            void* pEHInfo = null;
            bool hasStackTraceIp = true;
            if ((smallShadowFrameSizeAndFlags & HasExtendedInfoFlag) != 0)
            {
                uint smallEHInfoSizeAndExtendedFlags = *pCurrent++;
                uint ehInfoSize = smallEHInfoSizeAndExtendedFlags & ~IsHiddenExtendedFlag;
                if (ehInfoSize >= SmallEHInfoSizeLimit)
                {
                    ehInfoSize = Unsafe.ReadUnaligned<uint>(pCurrent);
                    pCurrent += sizeof(uint);
                }
                if (ehInfoSize != 0)
                {
                    pEHInfo = pCurrent;
                    pCurrent += ehInfoSize;
                }
                hasStackTraceIp = (smallEHInfoSizeAndExtendedFlags & IsHiddenExtendedFlag) == 0;
            }
            if (ppEHInfo != null)
            {
                *ppEHInfo = pEHInfo;
            }
            if (pHasStackTraceIp != null)
            {
                *pHasStackTraceIp = hasStackTraceIp;
            }

            return (int)(pCurrent - pUnwindInfo);
        }
    }
}
