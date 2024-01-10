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

COOP_PINVOKE_HELPER(void*, RhpGetOrInitShadowStackTop, ())
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

COOP_PINVOKE_HELPER(void*, RhpGetShadowStackTop, ())
{
    return t_pShadowStackTop;
}

COOP_PINVOKE_HELPER(void, RhpSetShadowStackTop, (void* pShadowStack))
{
    t_pShadowStackTop = pShadowStack;
}

COOP_PINVOKE_HELPER(void, RhpPInvoke, (void* pShadowStack, PInvokeTransitionFrame* pFrame))
{
    RhpSetShadowStackTop(pShadowStack);
    Thread* pCurThread = ThreadStore::RawGetCurrentThread();
    pCurThread->InlinePInvoke(pFrame);
}
