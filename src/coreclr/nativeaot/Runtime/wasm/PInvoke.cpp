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

FCIMPL0(void*, RhpGetOrInitShadowStackTop)
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

FCIMPL0(void*, RhpGetShadowStackTop)
{
    return t_pShadowStackTop;
}
FCIMPLEND

FCIMPL1(void, RhpSetShadowStackTop, void* pShadowStack)
{
    t_pShadowStackTop = pShadowStack;
}
FCIMPLEND

FCIMPL2(void, RhpPInvoke, void* pShadowStack, PInvokeTransitionFrame* pFrame)
{
    RhpSetShadowStackTop(pShadowStack);
    Thread* pCurThread = ThreadStore::RawGetCurrentThread();
    pCurThread->InlinePInvoke(pFrame);
}
FCIMPLEND
