// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "CommonTypes.h"
#include "CommonMacros.h"

#include "../wasm.h"

struct SparseVirtualUnwindFrame
{
    SparseVirtualUnwindFrame* Prev;
    void* UnwindTable;
    size_t UnwindIndex;
};

// This variable is defined here in native code because:
//  1) Unmanaged thread locals are currently much more efficient than managed ones.
//  2) Push/pop functions do not need the shadow stack argument.
//
thread_local SparseVirtualUnwindFrame* t_pLastSparseVirtualUnwindFrame = nullptr;

FCIMPL_NO_SS(void, RhpPushSparseVirtualUnwindFrame, SparseVirtualUnwindFrame* pFrame, void* pUnwindTable, size_t unwindIndex)
{
    ASSERT(t_pLastSparseVirtualUnwindFrame < pFrame);
    pFrame->Prev = t_pLastSparseVirtualUnwindFrame;
    pFrame->UnwindTable = pUnwindTable;
    pFrame->UnwindIndex = unwindIndex;

    t_pLastSparseVirtualUnwindFrame = pFrame;
}
FCIMPLEND

FCIMPL_NO_SS(void, RhpPopSparseVirtualUnwindFrame)
{
    ASSERT(t_pLastSparseVirtualUnwindFrame != nullptr);
    t_pLastSparseVirtualUnwindFrame = t_pLastSparseVirtualUnwindFrame->Prev;
}
FCIMPLEND

FCIMPL0(SparseVirtualUnwindFrame**, RhpGetLastSparseVirtualUnwindFrameRef)
{
    return &t_pLastSparseVirtualUnwindFrame;
}
FCIMPLEND

FCIMPL0(void*, RhpGetLastPreciseVirtualUnwindFrame)
{
    return static_cast<uint8_t*>(pShadowStack) - sizeof(void*);
}
FCIMPLEND

// We do not use these helpers. TODO-LLVM: exclude them from the WASM build.
FCIMPL4(void*, RhpCallCatchFunclet, void*, void*, void*, void*) { abort(); } FCIMPLEND
FCIMPL3(bool, RhpCallFilterFunclet, void*, void*, void*) { abort(); } FCIMPLEND
FCIMPL2(void, RhpCallFinallyFunclet, void*, void*) { abort(); } FCIMPLEND
