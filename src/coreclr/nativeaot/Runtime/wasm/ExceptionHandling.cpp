// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>
#include <stdlib.h>
#include <exception>

#include "CommonMacros.h"

class Object;
struct ExceptionDispatchData
{
    ExceptionDispatchData(void* pDispatcherShadowFrame, Object** pManagedException)
        : DispatchShadowFrameAddress(pDispatcherShadowFrame)
        , ManagedExceptionAddress(pManagedException)
        , LastFault(nullptr)
    {
        ASSERT(pDispatcherShadowFrame != nullptr);
    }

    // The layout of this struct must match the managed version in "ExceptionHandling.wasm.cs" exactly.
    void* DispatchShadowFrameAddress;
    Object** ManagedExceptionAddress;
    void* LastFault;
};

struct ManagedExceptionWrapper : std::exception
{
    ManagedExceptionWrapper(ExceptionDispatchData dispatchData) : DispatchData(dispatchData)
    {
    }

    ExceptionDispatchData DispatchData;
};

// The layout of this struct must match what codegen expects (see "jit/llvmcodegen.cpp, generateEHDispatch").
// Instances of it are shared between dispatchers across a single native frame.
//
struct FrameDispatchData
{
    struct {
        void* ExceptionData;
        int Selector;
    } CppExceptionTuple; // Owned by codegen.

    ExceptionDispatchData* DispatchData; // Owned by runtime.
};

static const int CONTINUE_SEARCH = 0;

extern "C" int RhpHandleExceptionWasmMutuallyProtectingCatches_Managed(void* pDispatchShadowFrame, void* pOriginalShadowFrame, ExceptionDispatchData * pDispatchData, void** pEHTable);
extern "C" int RhpHandleExceptionWasmFilteredCatch_Managed(void* pDispatchShadowFrame, void* pOriginalShadowFrame, ExceptionDispatchData* pDispatchData, void* pHandler, void* pFilter);
extern "C" int RhpHandleExceptionWasmCatch_Managed(void* pDispatchShadowFrame, void* pOriginalShadowFrame, ExceptionDispatchData* pDispatchData, void* pHandler, void* pClauseType);
extern "C" void RhpHandleExceptionWasmFault_Managed(void* pDispatchShadowFrame, void* pOriginalShadowFrame, ExceptionDispatchData* pDispatchData, void* pHandler);
extern "C" void RhpDynamicStackRelease(void* pShadowFrame);
extern "C" void* __cxa_begin_catch(void* pExceptionData);

static ExceptionDispatchData* BeginFrameDispatch(FrameDispatchData* pFrameDispatchData)
{
    if (pFrameDispatchData->DispatchData == nullptr)
    {
        ManagedExceptionWrapper* pException = (ManagedExceptionWrapper*)__cxa_begin_catch(pFrameDispatchData->CppExceptionTuple.ExceptionData);
        pFrameDispatchData->DispatchData = &pException->DispatchData;
    }

    return pFrameDispatchData->DispatchData;
}

// These per-clause handlers are invoked by RyuJit-generated LLVM code. The general dispatcher machinery is split into two parts: the managed and
// native portions. Here, in the native portion, we handle "activating" the dispatch (i. e. calling "__cxa_begin_catch") and extracting the shadow
// stack for managed dispatchers from the exception. We also handle releasing the dynamic shadow stack. The latter is a choice made from a tradeoff
// between keeping the managed dispatcher code free of assumptions that no dynamic stack state is allocated on it and the general desire to have
// as much code as possible in managed. Note as well we could have technically released the shadow stack using the original shadow frame, this too
// would assume that dispatchers have no dynamic stack state as otherwise, in a nested dispatch across a single original frame, the bottom (first
// to return) catch handler would release state of dispatchers still active above it.
//
COOP_PINVOKE_HELPER(int, RhpDispatchHandleExceptionWasmMutuallyProtectingCatches,
                    (void* pShadowFrame, void* pOriginalShadowFrame, FrameDispatchData* pFrameDispatchData, void** pEHTable))
{
    ExceptionDispatchData* pData = BeginFrameDispatch(pFrameDispatchData);
    int catchRetIdx = RhpHandleExceptionWasmMutuallyProtectingCatches_Managed(pData->DispatchShadowFrameAddress, pOriginalShadowFrame, pData, pEHTable);
    if (catchRetIdx != CONTINUE_SEARCH)
    {
        RhpDynamicStackRelease(pShadowFrame);
    }
    return catchRetIdx;
}

COOP_PINVOKE_HELPER(int, RhpDispatchHandleExceptionWasmFilteredCatch,
                    (void* pShadowFrame, void* pOriginalShadowFrame, FrameDispatchData* pFrameDispatchData, void* pHandler, void* pFilter))
{
    ExceptionDispatchData* pData = BeginFrameDispatch(pFrameDispatchData);
    int catchRetIdx = RhpHandleExceptionWasmFilteredCatch_Managed(pData->DispatchShadowFrameAddress, pOriginalShadowFrame, pData, pHandler, pFilter);
    if (catchRetIdx != CONTINUE_SEARCH)
    {
        RhpDynamicStackRelease(pShadowFrame);
    }
    return catchRetIdx;
}

COOP_PINVOKE_HELPER(int, RhpDispatchHandleExceptionWasmCatch,
                    (void* pShadowFrame, void* pOriginalShadowFrame, FrameDispatchData* pFrameDispatchData, void* pHandler, void* pClauseType))
{
    ExceptionDispatchData* pData = BeginFrameDispatch(pFrameDispatchData);
    int catchRetIdx = RhpHandleExceptionWasmCatch_Managed(pData->DispatchShadowFrameAddress, pOriginalShadowFrame, pData, pHandler, pClauseType);
    if (catchRetIdx != CONTINUE_SEARCH)
    {
        RhpDynamicStackRelease(pShadowFrame);
    }
    return catchRetIdx;
}

COOP_PINVOKE_HELPER(void, RhpDispatchHandleExceptionWasmFault, (void* pOriginalShadowFrame, FrameDispatchData* pFrameDispatchData, void* pHandler))
{
    ExceptionDispatchData* pData = BeginFrameDispatch(pFrameDispatchData);
    RhpHandleExceptionWasmFault_Managed(pData->DispatchShadowFrameAddress, pOriginalShadowFrame, pData, pHandler);
}

COOP_PINVOKE_HELPER(void, RhpThrowNativeException, (void* pDispatcherShadowFrame, Object** pManagedException))
{
    throw ManagedExceptionWrapper(ExceptionDispatchData(pDispatcherShadowFrame, pManagedException));
}

// We do not use these helpers, but we also do not exclude code referencing them from the
// build, and so define these stubs to avoid undefined symbol warnings. TODO-LLVM: exclude
// the general EH code from WASM-targeting runtime build.
//
COOP_PINVOKE_HELPER(void*, RhpCallCatchFunclet, (void*, void*, void*, void*)) { abort(); }
COOP_PINVOKE_HELPER(bool, RhpCallFilterFunclet, (void*, void*, void*)) { abort(); }
COOP_PINVOKE_HELPER(void, RhpCallFinallyFunclet, (void*, void*)) { abort(); }
