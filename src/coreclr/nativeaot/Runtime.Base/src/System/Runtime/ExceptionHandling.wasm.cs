// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Internal.Runtime;

// Disable: Filter expression is a constant. We know. We just can't do an unfiltered catch.
#pragma warning disable 7095
#pragma warning disable 8500 // Cannot take the address of, get the size of, or declare a pointer to a managed type

namespace System.Runtime
{
    // Due to the inability to perform manual unwind, WASM uses a customized exception handling scheme where unwinding
    // is performed by throwing and catching native exceptions and EH-live state is maintained on the shadow stack.
    //
    // ; First pass
    //
    //   ; Shadow frames                            ; Native frames
    //
    //   [Filtering F0] (with C0 catch)             [Filtering F0]
    //   [Finally   S0]                                            [Dispatcher]  ^ ; Progression of the native exception
    //   [Filtering F1]                             [Filtering F1] [F0 frames]   | ; stops once we find a filter which
    //   [Finally   S1]                                            [Dispatcher]  | ; accepts its managed counterpart.
    //   [Filtering F2]                             [Filtering F2] [F1 frames]   |
    //   [Finally   S2]                                            [Dispatcher]  |
    //   [Throw]                                    [Throw]        [F2 frames]   |
    //   [Dispatcher(s)]
    //   [F2 frames] [F1 frames] ... ; Native exception carries the dispatcher's shadow stack
    //
    // ; Second pass
    //
    //   ; Shadow frames                            ; Native frames
    //
    //   [Filtering F0] <-------------------------| [Filtering F0] <---------------------------------------------|
    //   [Finally   S0]                           |                [Dispatcher]  ; The handler was found         |
    //   [Filtering F1]                           |                [S2 frames] [S1 frames] ... [C0 frames]-------|
    //   [Finally   S1]                           |
    //   [Filtering F2]                           |
    //   [Finally   S2]                           |
    //   [Throw]                                  |
    //   [Dispatcher]                             |
    //   [S2 frames] [S1 frames] ... [C0 frames]--| ; Normal "ret" from the dispatcher
    //
    internal static unsafe partial class EH
    {
        private const int ContinueSearch = 0;

        // The layout of this struct must match the native version in "wasm/ExceptionHandling.cpp" exactly.
        private struct ExceptionDispatchData
        {
            public void* DispatchShadowFrameAddress; // Shadow stack to use when calling managed dispatchers.
            public object* ManagedExceptionAddress; // Address of the managed exception on the shadow stack.
            public FaultNode* LastFault; // Half-circular linked list of fault funclets to run before calling catch.
        }

        private struct FaultNode
        {
            public void* Funclet;
            public void* ShadowFrameAddress;
            public FaultNode* Next;
        }

        // These per-clause handlers are invoked by the native dispatcher code, using a shadow stack extracted from the thrown exception.
        //
        [RuntimeExport("RhpHandleExceptionWasmMutuallyProtectingCatches")]
        private static int RhpHandleExceptionWasmMutuallyProtectingCatches(void* pOriginalShadowFrame, ExceptionDispatchData* pDispatchData, void** pEHTable)
        {
            WasmEHLogDispatcherEnter(RhEHClauseKind.RH_EH_CLAUSE_UNUSED, pEHTable, pOriginalShadowFrame);

            object exception = *pDispatchData->ManagedExceptionAddress;
            EHClauseIteratorWasm clauseIter = new EHClauseIteratorWasm(pEHTable);
            EHClauseWasm clause;
            while (clauseIter.Next(&clause))
            {
                WasmEHLogEHTableEntry(clause, pOriginalShadowFrame);

                bool foundHandler = false;
                if (clause.Filter != null)
                {
                    if (CallFilterFunclet(clause.Filter, exception, pOriginalShadowFrame))
                    {
                        foundHandler = true;
                    }
                }
                else
                {
                    if (ShouldTypedClauseCatchThisException(exception, clause.ClauseType))
                    {
                        foundHandler = true;
                    }
                }

                if (foundHandler)
                {
                    return EndDispatchAndCallSecondPassHandlers(clause.Handler, pDispatchData, pOriginalShadowFrame);
                }
            }

            return ContinueSearch;
        }

        [RuntimeExport("RhpHandleExceptionWasmFilteredCatch")]
        private static int RhpHandleExceptionWasmFilteredCatch(void* pOriginalShadowFrame, ExceptionDispatchData* pDispatchData, void* pHandler, void* pFilter)
        {
            WasmEHLogDispatcherEnter(RhEHClauseKind.RH_EH_CLAUSE_FILTER, pFilter, pOriginalShadowFrame);

            if (CallFilterFunclet(pFilter, *pDispatchData->ManagedExceptionAddress, pOriginalShadowFrame))
            {
                return EndDispatchAndCallSecondPassHandlers(pHandler, pDispatchData, pOriginalShadowFrame);
            }

            return ContinueSearch;
        }

        [RuntimeExport("RhpHandleExceptionWasmCatch")]
        private static int RhpHandleExceptionWasmCatch(void* pOriginalShadowFrame, ExceptionDispatchData* pDispatchData, void* pHandler, MethodTable* pClauseType)
        {
            WasmEHLogDispatcherEnter(RhEHClauseKind.RH_EH_CLAUSE_TYPED, pClauseType, pOriginalShadowFrame);

            if (ShouldTypedClauseCatchThisException(*pDispatchData->ManagedExceptionAddress, pClauseType))
            {
                return EndDispatchAndCallSecondPassHandlers(pHandler, pDispatchData, pOriginalShadowFrame);
            }

            return ContinueSearch;
        }

        [RuntimeExport("RhpHandleExceptionWasmFault")]
        private static void RhpHandleExceptionWasmFault(void* pOriginalShadowFrame, ExceptionDispatchData* pDispatchData, void* pHandler)
        {
            WasmEHLogDispatcherEnter(RhEHClauseKind.RH_EH_CLAUSE_FAULT, null, pOriginalShadowFrame);

            FaultNode* lastFault = pDispatchData->LastFault;
            FaultNode* nextFault = (FaultNode*)NativeMemory.Alloc((nuint)sizeof(FaultNode));
            nextFault->Funclet = pHandler;
            nextFault->ShadowFrameAddress = pOriginalShadowFrame;

            if (lastFault != null)
            {
                nextFault->Next = lastFault->Next; // The last "Next" entry always points to the first.
                lastFault->Next = nextFault;
            }
            else
            {
                nextFault->Next = nextFault;
            }

            pDispatchData->LastFault = nextFault;
        }

        // This handler is called by codegen for exceptions that escape from RPI methods (i. e. unhandled exceptions).
        //
        [RuntimeExport("RhpHandleUnhandledException")]
        private static void HandleUnhandledException(object exception)
        {
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

        private static int EndDispatchAndCallSecondPassHandlers(void* pCatchFunclet, ExceptionDispatchData* pDispatchData, void* pCatchShadowFrame)
        {
            // Make sure to get the data we need before releasing the native exception.
            FaultNode* lastFault = pDispatchData->LastFault;
            object exception = *pDispatchData->ManagedExceptionAddress;

            // Note that the first pass will never let exceptions escape out of the dispatcher, and so we can guarantee that no
            // native exceptions will be leaked. This also depends on us not using native rethrow in the catch handler below.
            InternalCalls.RhpReleaseNativeException(pDispatchData);

            if (lastFault != null)
            {
                for (FaultNode* fault = lastFault->Next, nextFault; ; fault = nextFault)
                {
                    CallFinallyFunclet(fault->Funclet, fault->ShadowFrameAddress);

                    nextFault = fault->Next;
                    NativeMemory.Free(fault);

                    if (fault == lastFault)
                    {
                        break;
                    }
                }
            }

            WasmEHLogFunletEnter(pCatchFunclet, RhEHClauseKind.RH_EH_CLAUSE_TYPED, pCatchShadowFrame);
            int catchRetIdx = ((delegate*<object, void*, int>)pCatchFunclet)(exception, pCatchShadowFrame);
            WasmEHLogFunletExit(RhEHClauseKind.RH_EH_CLAUSE_TYPED, catchRetIdx, pCatchShadowFrame);

            return catchRetIdx;
        }

        private static bool CallFilterFunclet(void* pFunclet, object exception, void* pShadowFrame)
        {
            WasmEHLogFunletEnter(pFunclet, RhEHClauseKind.RH_EH_CLAUSE_FILTER, pShadowFrame);
            bool result;
            try
            {
                result = ((delegate*<object, void*, int>)pFunclet)(exception, pShadowFrame) != 0;
            }
            catch when (true)
            {
                result = false; // A filter that throws is treated as if it returned "continue search".
            }
            WasmEHLogFunletExit(RhEHClauseKind.RH_EH_CLAUSE_FILTER, result ? 1 : 0, pShadowFrame);

            return result;
        }

        private static void CallFinallyFunclet(void* pFunclet, void* pShadowFrame)
        {
            WasmEHLogFunletEnter(pFunclet, RhEHClauseKind.RH_EH_CLAUSE_FAULT, pShadowFrame);
            ((delegate*<void*, void>)pFunclet)(pShadowFrame);
            WasmEHLogFunletExit(RhEHClauseKind.RH_EH_CLAUSE_FAULT, 0, pShadowFrame);
        }

        [RuntimeExport("RhpThrowEx")]
        private static void RhpThrowEx(object exception)
        {
#if INPLACE_RUNTIME
            // Turn "throw null" into "throw new NullReferenceException()".
            exception ??= new NullReferenceException();
#endif
            Exception.DispatchExLLVM(exception);
            ThrowException(exception);
        }

        [RuntimeExport("RhpRethrow")]
        private static void RhpRethrow(object* pException)
        {
            ThrowException(*pException);
        }

        private static void ThrowException(object exception)
        {
            WasmEHLog("Throwing: [" + exception.GetType() + "]", &exception, "1");
            OnFirstChanceExceptionViaClassLib(exception);

            // We will pass around the managed exception address in the native exception to avoid having to report it
            // explicitly to the GC (or having a hole, or using a GCHandle). This will work as intended as the shadow
            // stack associated with this method will only be freed after the last (catch) handler returns.
            void* pFunc = (delegate*<void*, object*, void>)&InternalCalls.RhpThrowNativeException;
            ((delegate*<object*, void>)pFunc)(&exception); // Implicitly pass the callee's shadow stack.
        }

        [Conditional("ENABLE_NOISY_WASM_EH_LOG")]
        private static void WasmEHLog(string message, void* pShadowFrame, string pass)
        {
            string log = "WASM EH";
            log += " [SF: " + ToHex(pShadowFrame) + "]";
            log += " [" + pass + "]";
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
        private static void WasmEHLogDispatcherEnter(RhEHClauseKind kind, void* data, void* pShadowFrame)
        {
            string description = GetClauseDescription(kind, data);
            string pass = kind == RhEHClauseKind.RH_EH_CLAUSE_FAULT ? "2" : "1";
            WasmEHLog("Handling" + ": " + description, pShadowFrame, pass);
        }

        [Conditional("ENABLE_NOISY_WASM_EH_LOG")]
        private static void WasmEHLogEHTableEntry(EHClauseWasm clause, void* pShadowFrame)
        {
            string description = clause.Filter != null ? GetClauseDescription(RhEHClauseKind.RH_EH_CLAUSE_FILTER, clause.Filter)
                                                       : GetClauseDescription(RhEHClauseKind.RH_EH_CLAUSE_TYPED, clause.ClauseType);
            WasmEHLog("Clause: " + description, pShadowFrame, "1");
        }

        private static string GetClauseDescription(RhEHClauseKind kind, void* data) => kind switch
        {
            RhEHClauseKind.RH_EH_CLAUSE_TYPED => "catch, class [" + Type.GetTypeFromMethodTable((MethodTable*)data) + "]",
            RhEHClauseKind.RH_EH_CLAUSE_FILTER => "filtered catch",
            RhEHClauseKind.RH_EH_CLAUSE_UNUSED => "mutually protecting catches, table at [" + ToHex(data) + "]",
            _ => "fault",
        };

        [Conditional("ENABLE_NOISY_WASM_EH_LOG")]
        private static void WasmEHLogFunletEnter(void* pHandler, RhEHClauseKind kind, void* pShadowFrame)
        {
            (string name, string pass) = kind switch
            {
                RhEHClauseKind.RH_EH_CLAUSE_FILTER => ("filter", "1"),
                RhEHClauseKind.RH_EH_CLAUSE_FAULT => ("fault", "2"),
                _ => ("catch", "2")
            };

            WasmEHLog("Calling " + name + " funclet at [" + ToHex(pHandler) + "]", pShadowFrame, pass);
        }

        [Conditional("ENABLE_NOISY_WASM_EH_LOG")]
        private static void WasmEHLogFunletExit(RhEHClauseKind kind, int result, void* pShadowFrame)
        {
            (string resultString, string pass) = kind switch
            {
                RhEHClauseKind.RH_EH_CLAUSE_FILTER => (result == 1 ? "true" : "false", "1"),
                RhEHClauseKind.RH_EH_CLAUSE_FAULT => ("success", "2"),
                _ => (ToHex(result), "2")
            };

            WasmEHLog("Funclet returned: " + resultString, pShadowFrame, pass);
        }

        private static string ToHex(uint value) => ToHex((int)value);
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

        // This iterator is used for EH tables produces by codegen for runs of mutually protecting catch handlers.
        //
        internal unsafe struct EHClauseWasm
        {
            public void* Handler;
            public void* Filter;
            public MethodTable* ClauseType;
        }

        // See codegen code ("jit/llvmcodegen.cpp, generateEHDispatchTable") for details on the format of the table.
        //
        internal unsafe struct EHClauseIteratorWasm
        {
            private const nuint HeaderRecordSize = 1;
            private const nuint ClauseRecordSize = 2;
            private static nuint FirstSectionSize => HeaderRecordSize + (nuint)sizeof(nuint) / 2 * 8 * ClauseRecordSize;
            private static nuint LargeSectionSize => HeaderRecordSize + (nuint)sizeof(nuint) * 8 * ClauseRecordSize;

            private readonly void** _pTableEnd;
            private void** _pCurrentSectionClauses;
            private void** _pNextSection;
            private nuint _currentIndex;
            private nuint _clauseKindMask;

            public EHClauseIteratorWasm(void** pEHTable)
            {
                _pCurrentSectionClauses = pEHTable + HeaderRecordSize;
                _pNextSection = pEHTable + FirstSectionSize;
                _currentIndex = 0;
#if TARGET_32BIT
                _clauseKindMask = ((ushort*)pEHTable)[1];
                nuint tableSize = ((ushort*)pEHTable)[0];
#else
                _clauseKindMask = ((uint*)pEHTable)[1];
                nuint tableSize = ((uint*)pEHTable)[0];
#endif
                _pTableEnd = pEHTable + tableSize;
            }

            public bool Next(EHClauseWasm* pClause)
            {
                void** pCurrent = _pCurrentSectionClauses + _currentIndex * ClauseRecordSize;
                if (pCurrent >= _pTableEnd)
                {
                    return false;
                }

                if ((_clauseKindMask & ((nuint)1 << (int)_currentIndex)) != 0)
                {
                    pClause->Filter = pCurrent[0];
                    pClause->ClauseType = null;
                }
                else
                {
                    pClause->Filter = null;
                    pClause->ClauseType = (MethodTable*)pCurrent[0];
                }

                pClause->Handler = pCurrent[1];

                // Initialize the state for the next iteration.
                void** pCurrentNext = pCurrent + ClauseRecordSize;
                if ((pCurrentNext != _pTableEnd) && (pCurrentNext == _pNextSection))
                {
                    _pCurrentSectionClauses = pCurrentNext + HeaderRecordSize;
                    _pNextSection += LargeSectionSize;
                    _currentIndex = 0;
                    _clauseKindMask = (nuint)pCurrentNext[0];
                }
                else
                {
                    _currentIndex++;
                }

                return true;
            }
        }
    }
}
