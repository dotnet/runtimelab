// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <cstdio>

#include "common.h"
#include "gcenv.h"
#include "gcenv.ee.h"
#include "gcheaputilities.h"
#include "gchandleutilities.h"

#include "thread.h"
#include "threadstore.h"
#include "threadstore.inl"
#include "thread.inl"

COOP_PINVOKE_HELPER(void*, RhpGcStressOnce, (void* obj, uint8_t* pFlag))
{
    if (*pFlag)
    {
        // This helper will only stress each safe point once.
        return obj;
    }

    // The GarbageCollect operation below may trash the last win32 error. We save the error here so that it can be
    // restored after the GC operation;
    int32_t lastErrorOnEntry = PalGetLastError();

    Thread* pThread = ThreadStore::GetCurrentThread();
    if (!pThread->IsSuppressGcStressSet() && !pThread->IsDoNotTriggerGcSet())
    {
        // GC-protect our exposed object.
        GCFrameRegistration gc;
        if (obj != nullptr)
        {
            gc.m_pThread = pThread;
            gc.m_pObjRefs = &obj;
            gc.m_numObjRefs = 1;
            gc.m_MaybeInterior = 1;
            pThread->PushGCFrameRegistration(&gc);
        }

        GCHeapUtilities::GetGCHeap()->GarbageCollect();

        if (obj != nullptr)
        {
            pThread->PopGCFrameRegistration(&gc);
        }
        *pFlag = true;
    }

    // Restore the saved error
    PalSetLastError(lastErrorOnEntry);
    return obj;
}

COOP_PINVOKE_HELPER(Object*, RhpCheckObj, (Object* obj))
{
    if (obj != nullptr)
    {
        MethodTable* pMT = obj->GetMethodTable();
        if (!pMT->Validate())
        {
            printf("Corrupt object/pMT: [%p]/[%p]\n", obj, pMT);
            RhFailFast();
        }
    }

    return obj;
}
