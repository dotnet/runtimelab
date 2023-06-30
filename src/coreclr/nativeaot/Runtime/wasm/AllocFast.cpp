// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "CommonMacros.inl"
#include "PalRedhawk.h"
#include "rhassert.h"

#include "slist.h"
#include "rhbinder.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "threadstore.h"
#include "threadstore.inl"

#include "MethodTable.h"
#include "TypeManager.h"
#include "MethodTable.inl"
#include "ObjectLayout.h"

//
// WASM-specific allocators: we define them to use a shadow stack argument to avoid saving it on the fast path.
//
extern "C" void* RhpGcAlloc(MethodTable* pEEType, uint32_t uFlags, uintptr_t numElements, void* pTransitionFrame);
extern "C" void* RhpGetShadowStackTop();
extern "C" void RhpSetShadowStackTop(void* pShadowStack);
extern "C" void RhExceptionHandling_FailedAllocation_Managed(void* pShadowStack, MethodTable* pEEType, bool isOverflow);

static Object* AllocateObject(void* pShadowStack, MethodTable* pEEType, uint32_t uFlags, uintptr_t numElements)
{
    // Save the current shadow stack before calling into GC; we may need to scan it for live references.
    RhpSetShadowStackTop(pShadowStack);

    Object* pObject = (Object*)RhpGcAlloc(pEEType, uFlags, numElements, nullptr);
    if (pObject == nullptr)
    {
        RhExceptionHandling_FailedAllocation_Managed(pShadowStack, pEEType, /* isOverflow */ false);
    }

    return pObject;
}

static void ThrowOverflowException(void* pShadowStack, MethodTable* pEEType)
{
    RhExceptionHandling_FailedAllocation_Managed(pShadowStack, pEEType, /* isOverflow */ true);
}

struct gc_alloc_context
{
    uint8_t* alloc_ptr;
    uint8_t* alloc_limit;
};

#define GC_ALLOC_FINALIZE    0x1 // TODO: Defined in gc.h
#define GC_ALLOC_ALIGN8_BIAS 0x4 // TODO: Defined in gc.h
#define GC_ALLOC_ALIGN8      0x8 // TODO: Defined in gc.h

//
// Allocations
//
COOP_PINVOKE_HELPER(Object*, RhpNewFast, (void* pShadowStack, MethodTable* pEEType))
{
    ASSERT(!pEEType->HasFinalizer());

    Thread* pCurThread = ThreadStore::GetCurrentThread();
    gc_alloc_context* acontext = pCurThread->GetAllocContext();
    size_t size = pEEType->get_BaseSize();

    uint8_t* alloc_ptr = acontext->alloc_ptr;
    ASSERT(alloc_ptr <= acontext->alloc_limit);
    if ((size_t)(acontext->alloc_limit - alloc_ptr) >= size)
    {
        acontext->alloc_ptr = alloc_ptr + size;
        Object* pObject = (Object*)alloc_ptr;
        pObject->set_EEType(pEEType);
        return pObject;
    }

    return AllocateObject(pShadowStack, pEEType, 0, 0);
}

COOP_PINVOKE_HELPER(Object*, RhpNewFinalizable, (void* pShadowStack, MethodTable* pEEType))
{
    ASSERT(pEEType->HasFinalizer());
    return AllocateObject(pShadowStack, pEEType, GC_ALLOC_FINALIZE, 0);
}

COOP_PINVOKE_HELPER(Array*, RhpNewArray, (void* pShadowStack, MethodTable* pArrayEEType, int numElements))
{
    Thread* pCurThread = ThreadStore::GetCurrentThread();
    gc_alloc_context* acontext = pCurThread->GetAllocContext();

    if (numElements < 0)
    {
        ThrowOverflowException(pShadowStack, pArrayEEType);
    }

    size_t size;
#ifndef HOST_64BIT
    // if the element count is <= 0x10000, no overflow is possible because the component size is
    // <= 0xffff, and thus the product is <= 0xffff0000, and the base size is only ~12 bytes
    if (numElements > 0x10000)
    {
        // Perform the size computation using 64-bit integeres to detect overflow
        uint64_t size64 = (uint64_t)pArrayEEType->get_BaseSize() + ((uint64_t)numElements * (uint64_t)pArrayEEType->RawGetComponentSize());
        size64 = (size64 + (sizeof(uintptr_t)-1)) & ~(sizeof(uintptr_t)-1);

        size = (size_t)size64;
        if (size != size64)
        {
            ThrowOverflowException(pShadowStack, pArrayEEType);
        }
    }
    else
#endif // !HOST_64BIT
    {
        size = (size_t)pArrayEEType->get_BaseSize() + ((size_t)numElements * (size_t)pArrayEEType->RawGetComponentSize());
        size = ALIGN_UP(size, sizeof(uintptr_t));
    }

    uint8_t* alloc_ptr = acontext->alloc_ptr;
    ASSERT(alloc_ptr <= acontext->alloc_limit);
    if ((size_t)(acontext->alloc_limit - alloc_ptr) >= size)
    {
        acontext->alloc_ptr = alloc_ptr + size;
        Array* pObject = (Array *)alloc_ptr;
        pObject->set_EEType(pArrayEEType);
        pObject->InitArrayLength((uint32_t)numElements);
        return pObject;
    }

    return (Array*)AllocateObject(pShadowStack, pArrayEEType, 0, numElements);
}

COOP_PINVOKE_HELPER(String*, RhNewString, (MethodTable* pArrayEEType, int numElements))
{
    // TODO: Implement. We call RhpNewArray for now since there's a bunch of TODOs in the places that matter anyway.
    void* pShadowStack = RhpGetShadowStackTop();
    return (String*)RhpNewArray(pShadowStack, pArrayEEType, numElements);
}

#if defined(FEATURE_64BIT_ALIGNMENT)
GPTR_DECL(MethodTable, g_pFreeObjectEEType);

COOP_PINVOKE_HELPER(Object*, RhpNewFinalizableAlign8, (void* pShadowStack, MethodTable* pEEType))
{
    return AllocateObject(pShadowStack, pEEType, GC_ALLOC_FINALIZE | GC_ALLOC_ALIGN8, 0);
}

COOP_PINVOKE_HELPER(Object*, RhpNewFastAlign8, (void* pShadowStack, MethodTable* pEEType))
{
    ASSERT(!pEEType->HasFinalizer());

    Thread* pCurThread = ThreadStore::GetCurrentThread();
    gc_alloc_context* acontext = pCurThread->GetAllocContext();

    size_t size = pEEType->get_BaseSize();
    size = (size + (sizeof(uintptr_t) - 1)) & ~(sizeof(uintptr_t) - 1);

    uint8_t* result = acontext->alloc_ptr;

    int requiresPadding = ((uint32_t)result) & 7;
    size_t paddedSize = size;
    if (requiresPadding)
    {
        if (paddedSize > SIZE_MAX - 12)
        {
            ThrowOverflowException(pShadowStack, pEEType);
        }
        paddedSize += 12;
    }

    uint8_t* alloc_ptr = acontext->alloc_ptr;
    ASSERT(alloc_ptr <= acontext->alloc_limit);
    if ((size_t)(acontext->alloc_limit - alloc_ptr) >= paddedSize)
    {
        acontext->alloc_ptr = alloc_ptr + paddedSize;
        if (requiresPadding)
        {
            Object* dummy = (Object*)alloc_ptr;
            dummy->set_EEType(g_pFreeObjectEEType);
            alloc_ptr += 12; // if result + paddedSize was ok, then cant overflow
        }
        Object* pObject = (Object*)alloc_ptr;
        pObject->set_EEType(pEEType);
        return pObject;
    }

    return AllocateObject(pShadowStack, pEEType, GC_ALLOC_ALIGN8, 0);
}

COOP_PINVOKE_HELPER(Object*, RhpNewFastMisalign, (void* pShadowStack, MethodTable* pEEType))
{
    Thread* pCurThread = ThreadStore::GetCurrentThread();
    gc_alloc_context* acontext = pCurThread->GetAllocContext();

    size_t size = pEEType->get_BaseSize();
    uint8_t* result = acontext->alloc_ptr;

    int requiresPadding = (((uint32_t)result) & 7) != 4;
    size_t paddedSize = size;
    if (requiresPadding)
    {
        if (paddedSize > SIZE_MAX - 12)
        {
            ThrowOverflowException(pShadowStack, pEEType);
        }
        paddedSize += 12;
    }

    uint8_t* alloc_ptr = acontext->alloc_ptr;
    ASSERT(alloc_ptr <= acontext->alloc_limit);
    if ((size_t)(acontext->alloc_limit - alloc_ptr) >= paddedSize)
    {
        acontext->alloc_ptr = alloc_ptr + paddedSize;
        if (requiresPadding)
        {
            Object* dummy = (Object*)alloc_ptr;
            dummy->set_EEType(g_pFreeObjectEEType);
            alloc_ptr += 12; // if result + paddedSize was ok, then cant overflow
        }
        Object* pObject = (Object *)alloc_ptr;
        pObject->set_EEType(pEEType);
        return pObject;
    }

    return AllocateObject(pShadowStack, pEEType, GC_ALLOC_ALIGN8 | GC_ALLOC_ALIGN8_BIAS, 0);
}

COOP_PINVOKE_HELPER(Array*, RhpNewArrayAlign8, (void* pShadowStack, MethodTable* pArrayEEType, int numElements))
{
    Thread* pCurThread = ThreadStore::GetCurrentThread();
    gc_alloc_context* acontext = pCurThread->GetAllocContext();

    if (numElements < 0)
    {
        ThrowOverflowException(pShadowStack, pArrayEEType);
    }

    size_t size;
    uint32_t baseSize = pArrayEEType->get_BaseSize();
    // if the element count is <= 0x10000, no overflow is possible because the component size is
    // <= 0xffff, and thus the product is <= 0xffff0000, and the base size is only ~12 bytes
    if (numElements > 0x10000)
    {
        // Perform the size computation using 64-bit integeres to detect overflow
        uint64_t size64 = (uint64_t)baseSize + ((uint64_t)numElements * (uint64_t)pArrayEEType->RawGetComponentSize());
        size64 = (size64 + (sizeof(uintptr_t) - 1)) & ~(sizeof(uintptr_t) - 1);

        size = (size_t)size64;
        if (size != size64)
        {
            ThrowOverflowException(pShadowStack, pArrayEEType);
        }
    }
    else
    {
        size = (size_t)baseSize + ((size_t)numElements * (size_t)pArrayEEType->RawGetComponentSize());
        size = ALIGN_UP(size, sizeof(uintptr_t));
    }

    uint8_t* result = acontext->alloc_ptr;
    int requiresAlignObject = ((uint32_t)result) & 7;
    size_t paddedSize = size;
    if (requiresAlignObject)
    {
        if (paddedSize > SIZE_MAX - 12)
        {
            ASSERT_UNCONDITIONALLY("NYI");  // TODO: Throw overflow
        }
        paddedSize += 12;
    }

    uint8_t* alloc_ptr = acontext->alloc_ptr;
    ASSERT(alloc_ptr <= acontext->alloc_limit);
    if ((size_t)(acontext->alloc_limit - alloc_ptr) >= paddedSize)
    {
        acontext->alloc_ptr = alloc_ptr + paddedSize;
        if (requiresAlignObject)
        {
            Object* dummy = (Object*)alloc_ptr;
            dummy->set_EEType(g_pFreeObjectEEType);
            alloc_ptr += 12; // if result + paddedSize was ok, then cant overflow
        }
        Array* pObject = (Array*)alloc_ptr;
        pObject->set_EEType(pArrayEEType);
        pObject->InitArrayLength((uint32_t)numElements);
        return pObject;
    }

    return (Array*)AllocateObject(pShadowStack, pArrayEEType, GC_ALLOC_ALIGN8, numElements);
}
#endif // FEATURE_64BIT_ALIGNMENT
