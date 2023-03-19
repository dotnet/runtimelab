// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Unmanaged portion of finalization implementation for a single-threaded environment.
// Currently, only supports explicit finalization via WaitForPendingFinalizers.
//
#include "common.h"
#include "gcenv.h"
#include "gcheaputilities.h"
#include "gcrhinterface.h"

#include "thread.h"
#include "threadstore.h"
#include "threadstore.inl"
#include "thread.inl"

// Finalizer method implemented by the managed runtime.
extern "C" __cdecl void RhpProcessFinalizersAndReturn();

static void ProcessFinalizersAndReturn()
{
    static bool s_finalizing = false;

    // Recursive wait on finalization is a no-op.
    if (!s_finalizing)
    {
        s_finalizing = true;
        RhpProcessFinalizersAndReturn();
        s_finalizing = false;
    }
}

bool RhInitializeFinalization()
{
    return true;
}

// This method is called at the end of GC in case finalizable objects were present.
void RhEnableFinalization()
{
    // TODO: Implement automatic finalization. Cannot just call "ProcessFinalizersAndReturn"
    // here as it will deadlock the GC.
}

EXTERN_C NATIVEAOT_API void __cdecl RhWaitForPendingFinalizers(UInt32_BOOL allowReentrantWait)
{
    // Must be called in cooperative mode as "ProcessFinalizersAndReturn" RPIs back into managed.
    ASSERT(!ThreadStore::GetCurrentThread()->IsCurrentThreadInCooperativeMode());

    ProcessFinalizersAndReturn();
}

// Fetch next object which needs finalization or return null if we've reached the end of the list.
COOP_PINVOKE_HELPER(OBJECTREF, RhpGetNextFinalizableObject, ())
{
    while (true)
    {
        // Get the next finalizable object. If we get back NULL we've reached the end of the list.
        OBJECTREF refNext = GCHeapUtilities::GetGCHeap()->GetNextFinalizable();
        if (refNext == NULL)
            return NULL;

        // The queue may contain objects which have been marked as finalized already (via GC.SuppressFinalize()
        // for instance). Skip finalization for these but reset the flag so that the object can be put back on
        // the list with RegisterForFinalization().
        if (refNext->GetHeader()->GetBits() & BIT_SBLK_FINALIZER_RUN)
        {
            refNext->GetHeader()->ClrBit(BIT_SBLK_FINALIZER_RUN);
            continue;
        }

        // We've found the first finalizable object, return it to the caller.
        return refNext;
    }
}
