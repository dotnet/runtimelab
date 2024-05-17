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

thread_local void* t_pShadowStackBottom = nullptr;
thread_local void* t_pShadowStackTop = nullptr;

void* GetShadowStackBottom()
{
    return t_pShadowStackBottom;
}

void* GetShadowStackTop()
{
    return t_pShadowStackTop;
}

void SetShadowStackTop(void* pShadowStack)
{
    t_pShadowStackTop = pShadowStack;
}

FCIMPL_NO_SS(void*, RhpGetOrInitShadowStackTop)
{
    void* pShadowStack = t_pShadowStackTop;
    if (pShadowStack == nullptr)
    {
        pShadowStack = malloc(1000000); // ~1MB.
        if (pShadowStack == nullptr)
        {
            RhFailFast(); // Fatal OOM.
        }

        ASSERT(t_pShadowStackBottom == nullptr);
        t_pShadowStackBottom = pShadowStack;
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
    SetShadowStackTop(pShadowStack);
    RhpReversePInvokeAttachOrTrapThread2(pFrame);
}
FCIMPLEND

FCIMPL_NO_SS(void, RhpReversePInvokeReturn, void* pPreviousShadowStackTop, ReversePInvokeFrame* pFrame)
{
    pFrame->m_savedThread->InlineReversePInvokeReturn(pFrame);
    SetShadowStackTop(pPreviousShadowStackTop);
}
FCIMPLEND

FCIMPL1(void, RhpPInvoke, PInvokeTransitionFrame* pFrame)
{
    SetShadowStackTop(pShadowStack);
    Thread* pCurThread = ThreadStore::RawGetCurrentThread();
    pCurThread->InlinePInvoke(pFrame);
}
FCIMPLEND

FCIMPL_NO_SS(void, RhpPInvokeReturn, PInvokeTransitionFrame* pFrame)
{
    //reenter cooperative mode
    pFrame->m_pThread->InlinePInvokeReturn(pFrame);
}
FCIMPLEND
