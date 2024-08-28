// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdlib.h>

#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "thread.h"
#include "threadstore.h"
#include "thread.inl"
#include "threadstore.inl"

#include "wasm.h"

FCIMPL_NO_SS(void*, RhpGetOrInitShadowStackTop)
{
    Thread* pCurThread = ThreadStore::RawGetCurrentThread();

    void* pShadowStack = pCurThread->GetShadowStackTop();

    if (pShadowStack == nullptr)
    {
        pShadowStack = malloc(1000000); // ~1MB.
        if (pShadowStack == nullptr)
        {
            RhFailFast(); // Fatal OOM.
        }

        pCurThread->SetShadowStackBottom(pShadowStack);
    }

    return pShadowStack;
}
FCIMPLEND

EXTERN_C NOINLINE void FASTCALL RhpReversePInvokeAttachOrTrapThread2(ReversePInvokeFrame* pFrame);

FCIMPL1(void, RhpReversePInvoke, ReversePInvokeFrame* pFrame)
{
    Thread* pCurThread = ThreadStore::RawGetCurrentThread();
    pFrame->m_savedThread = pCurThread;
    if (pCurThread->InlineTryFastReversePInvoke(pFrame))
        return;

    // The slow path may invoke runtime initialization, which runs managed code.
    pCurThread->SetShadowStackTop(pShadowStack);
    RhpReversePInvokeAttachOrTrapThread2(pFrame);
}
FCIMPLEND

FCIMPL_NO_SS(void, RhpReversePInvokeReturn, void* pPreviousShadowStackTop, ReversePInvokeFrame* pFrame)
{
    pFrame->m_savedThread->InlineReversePInvokeReturn(pFrame);
    pFrame->m_savedThread->SetShadowStackTop(pPreviousShadowStackTop);
}
FCIMPLEND

FCIMPL1(void, RhpPInvoke, PInvokeTransitionFrame* pFrame)
{
    Thread* pCurThread = ThreadStore::RawGetCurrentThread();
    pCurThread->InlinePInvoke(pFrame);
    pCurThread->SetShadowStackTop(pShadowStack);
}
FCIMPLEND

FCIMPL_NO_SS(void, RhpPInvokeReturn, PInvokeTransitionFrame* pFrame)
{
    //reenter cooperative mode
    pFrame->m_pThread->InlinePInvokeReturn(pFrame);
}
FCIMPLEND
