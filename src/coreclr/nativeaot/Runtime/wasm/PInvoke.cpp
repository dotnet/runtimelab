// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdlib.h>

#include "CommonTypes.h"
#include "CommonMacros.h"

extern "C" thread_local void* t_pShadowStackBottom = nullptr;
extern "C" thread_local void* t_pShadowStackTop = nullptr;

COOP_PINVOKE_HELPER(void*, RhpGetOrInitShadowStackTop, ())
{
    void* pShadowStack = t_pShadowStackTop;
    if (pShadowStack == nullptr)
    {
        pShadowStack = malloc(1000000); // ~1MB.
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

void* GetShadowStackBottom()
{
    return t_pShadowStackBottom;
}

void* GetShadowStackTop()
{
    return t_pShadowStackTop;
}
