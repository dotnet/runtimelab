// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "ExceptionHandling.h"

static const int CONTINUE_SEARCH = 0;

extern "C" int RhpHandleExceptionWasmMutuallyProtectingCatches_Managed(void* pDispatchShadowFrame, void* pOriginalShadowFrame, ExceptionDispatchData * pDispatchData, void** pEHTable);
extern "C" int RhpHandleExceptionWasmFilteredCatch_Managed(void* pDispatchShadowFrame, void* pOriginalShadowFrame, ExceptionDispatchData* pDispatchData, void* pHandler, void* pFilter);
extern "C" int RhpHandleExceptionWasmCatch_Managed(void* pDispatchShadowFrame, void* pOriginalShadowFrame, ExceptionDispatchData* pDispatchData, void* pHandler, void* pClauseType);
extern "C" void RhpHandleExceptionWasmFault_Managed(void* pDispatchShadowFrame, void* pOriginalShadowFrame, ExceptionDispatchData* pDispatchData, void* pHandler);
extern "C" void RhpDynamicStackRelease(void* pShadowFrame);

// These per-clause handlers are invoked by RyuJit-generated LLVM code. The general dispatcher machinery is split into two parts: the managed and
// native portions. Here, in the native portion, we handle "activating" the dispatch (i. e. calling "__cxa_begin_catch") and extracting the shadow
// stack for managed dispatchers from the exception. We also handle releasing the dynamic shadow stack. The latter is a choice made from a tradeoff
// between keeping the managed dispatcher code free of assumptions that no dynamic stack state is allocated on it and the general desire to have
// as much code as possible in managed. Note as well we could have technically released the shadow stack using the original shadow frame, this too
// would assume that dispatchers have no dynamic stack state as otherwise, in a nested dispatch across a single original frame, the bottom (first
// to return) catch handler would release state of dispatchers still active above it.
//
COOP_PINVOKE_HELPER(int, RhpDispatchHandleExceptionWasmMutuallyProtectingCatches,
                    (void* pShadowFrame, void* pOriginalShadowFrame, void* pFrameDispatchData, void** pEHTable))
{
    ExceptionDispatchData* pData = BeginSingleFrameDispatch(pFrameDispatchData);
    int catchRetIdx = RhpHandleExceptionWasmMutuallyProtectingCatches_Managed(pData->DispatchShadowFrameAddress, pOriginalShadowFrame, pData, pEHTable);
    if (catchRetIdx != CONTINUE_SEARCH)
    {
        RhpDynamicStackRelease(pShadowFrame);
    }
    return catchRetIdx;
}

COOP_PINVOKE_HELPER(int, RhpDispatchHandleExceptionWasmFilteredCatch,
                    (void* pShadowFrame, void* pOriginalShadowFrame, void* pFrameDispatchData, void* pHandler, void* pFilter))
{
    ExceptionDispatchData* pData = BeginSingleFrameDispatch(pFrameDispatchData);
    int catchRetIdx = RhpHandleExceptionWasmFilteredCatch_Managed(pData->DispatchShadowFrameAddress, pOriginalShadowFrame, pData, pHandler, pFilter);
    if (catchRetIdx != CONTINUE_SEARCH)
    {
        RhpDynamicStackRelease(pShadowFrame);
    }
    return catchRetIdx;
}

COOP_PINVOKE_HELPER(int, RhpDispatchHandleExceptionWasmCatch,
                    (void* pShadowFrame, void* pOriginalShadowFrame, void* pFrameDispatchData, void* pHandler, void* pClauseType))
{
    ExceptionDispatchData* pData = BeginSingleFrameDispatch(pFrameDispatchData);
    int catchRetIdx = RhpHandleExceptionWasmCatch_Managed(pData->DispatchShadowFrameAddress, pOriginalShadowFrame, pData, pHandler, pClauseType);
    if (catchRetIdx != CONTINUE_SEARCH)
    {
        RhpDynamicStackRelease(pShadowFrame);
    }
    return catchRetIdx;
}

COOP_PINVOKE_HELPER(void, RhpDispatchHandleExceptionWasmFault, (void* pOriginalShadowFrame, void* pFrameDispatchData, void* pHandler))
{
    ExceptionDispatchData* pData = BeginSingleFrameDispatch(pFrameDispatchData);
    RhpHandleExceptionWasmFault_Managed(pData->DispatchShadowFrameAddress, pOriginalShadowFrame, pData, pHandler);
}

// Catch and filter funclets have a special calling convention which saves the exception object to the shadow stack.
// This is intended to optimize for size: the exception object comes "pre-spilled". It also makes implementing rethrow simple.
//
COOP_PINVOKE_HELPER(int, RhpCallCatchOrFilterFunclet, (void* pShadowFrame, void* pOriginalShadowFrame, Object* exception, int (*pFunclet)(void*, void*)))
{
    *((Object**)pShadowFrame) = exception;
    return pFunclet(pShadowFrame, pOriginalShadowFrame);
}

// We do not use these helpers. TODO-LLVM: exclude them from the WASM build.
COOP_PINVOKE_HELPER(void*, RhpCallCatchFunclet, (void*, void*, void*, void*)) { abort(); }
COOP_PINVOKE_HELPER(bool, RhpCallFilterFunclet, (void*, void*, void*)) { abort(); }
COOP_PINVOKE_HELPER(void, RhpCallFinallyFunclet, (void*, void*)) { abort(); }
