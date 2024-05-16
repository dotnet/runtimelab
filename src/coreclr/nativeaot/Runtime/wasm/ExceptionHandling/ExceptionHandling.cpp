// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "CommonTypes.h"
#include "CommonMacros.h"

#include "../wasm.h"

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

FCIMPL_NO_SS(void, RhpPushVirtualUnwindFrame, VirtualUnwindFrame* pFrame, void* pUnwindTable, size_t unwindIndex)
{
    ASSERT(t_pLastVirtualUnwindFrame < pFrame);
    pFrame->Prev = t_pLastVirtualUnwindFrame;
    pFrame->UnwindTable = pUnwindTable;
    pFrame->UnwindIndex = unwindIndex;

    t_pLastVirtualUnwindFrame = pFrame;
}
FCIMPLEND

FCIMPL_NO_SS(void, RhpPopVirtualUnwindFrame)
{
    ASSERT(t_pLastVirtualUnwindFrame != nullptr);
    t_pLastVirtualUnwindFrame = t_pLastVirtualUnwindFrame->Prev;
}
FCIMPLEND

FCIMPL0(void*, RhpGetRawLastVirtualUnwindFrameRef)
{
    return &t_pLastVirtualUnwindFrame;
}
FCIMPLEND

// We do not use these helpers. TODO-LLVM: exclude them from the WASM build.
FCIMPL4(void*, RhpCallCatchFunclet, void*, void*, void*, void*) { abort(); } FCIMPLEND
FCIMPL3(bool, RhpCallFilterFunclet, void*, void*, void*) { abort(); } FCIMPLEND
FCIMPL2(void, RhpCallFinallyFunclet, void*, void*) { abort(); } FCIMPLEND
