// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "CommonTypes.h"
#include "CommonMacros.h"

struct VirtualUnwindFrame
{
    VirtualUnwindFrame* Prev;
    void* UnwindTable;
    size_t UnwindIndex;
};

// This variable is defined here in native code because:
//  1) Unmanaged thread locals are currently much more efficient than managed ones.
//  2) Push/pop functions do not need the shadow stack argument.
//
thread_local VirtualUnwindFrame* t_pLastVirtualUnwindFrame = nullptr;

COOP_PINVOKE_HELPER(void, RhpPushVirtualUnwindFrame, (VirtualUnwindFrame* pFrame, void* pUnwindTable, size_t unwindIndex))
{
    ASSERT(t_pLastVirtualUnwindFrame < pFrame);
    pFrame->Prev = t_pLastVirtualUnwindFrame;
    pFrame->UnwindTable = pUnwindTable;
    pFrame->UnwindIndex = unwindIndex;

    t_pLastVirtualUnwindFrame = pFrame;
}

COOP_PINVOKE_HELPER(void, RhpPopVirtualUnwindFrame, ())
{
    ASSERT(t_pLastVirtualUnwindFrame != nullptr);
    t_pLastVirtualUnwindFrame = t_pLastVirtualUnwindFrame->Prev;
}

COOP_PINVOKE_HELPER(void*, RhpGetRawLastVirtualUnwindFrameRef, ())
{
    return &t_pLastVirtualUnwindFrame;
}

// We do not use these helpers. TODO-LLVM: exclude them from the WASM build.
COOP_PINVOKE_HELPER(void*, RhpCallCatchFunclet, (void*, void*, void*, void*)) { abort(); }
COOP_PINVOKE_HELPER(bool, RhpCallFilterFunclet, (void*, void*, void*)) { abort(); }
COOP_PINVOKE_HELPER(void, RhpCallFinallyFunclet, (void*, void*)) { abort(); }
