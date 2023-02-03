// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifdef FEATURE_CACHED_INTERFACE_DISPATCH

#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "rhbinder.h"
#include "MethodTable.h"
#include "ObjectLayout.h"
#include "CachedInterfaceDispatch.h"

//
// WASM uses a modified version of the regular cached interface dispatch mechanism. While ordinarily the dispatch
// stubs would directly call the target when it has been found (in the cache or otherwise), for WASM we cannot do
// this because there is no signature-agnostic way to transfer control. Stated otherwise, the stubs would need to
// be specialized on a per-signature basis, resulting in significant code size overhead with intrusive changes to
// the rest of dispatch code, which expects globally unique stubs. Thus, we leave calling the target to codegen and
// here only resolve it.
//
// TODO-LLVM-CQ: explore using one static stub for all cache sizes, using the count of entries stored in the cache.
// This would allow codegen to emit direct calls to this stub instead of loading it from the cell. It would also
// make the "stub" portion of the stub cell unnecessary, though actually removing it would imply very intrusive
// changes to the shared portion of the dispatch code, or maintaining its subset in a copy.
//

extern "C" void* RhpCidResolveWasm_Managed(void* pShadowStack, void* pCell);

// Cache miss case, call the runtime to resolve the target and update the cache.
static void* ResolveTarget(void* pShadowStack, Object* pObject, InterfaceDispatchCell* pCell)
{
    *(Object**)pShadowStack = pObject;
    void* pTarget = RhpCidResolveWasm_Managed(pShadowStack, pCell);

    return pTarget;
}

// Initial dispatch on an interface when we don't have a cache yet.
COOP_PINVOKE_HELPER(void*, RhpInitialInterfaceDispatch, (void* pShadowStack, Object* pObject, InterfaceDispatchCell* pCell))
{
    return ResolveTarget(pShadowStack, pObject, pCell);
}

COOP_PINVOKE_HELPER(void*, RhpInitialDynamicInterfaceDispatch, (void* pShadowStack, Object* pObject, InterfaceDispatchCell* pCell))
{
    return ResolveTarget(pShadowStack, pObject, pCell);
}

template <int CacheEntryCount>
FORCEINLINE static void* InterfaceDispatchStub(void* pShadowStack, Object* pObject, InterfaceDispatchCell* pCell)
{
    MethodTable* pObjectType = pObject->get_EEType();
    InterfaceDispatchCache* pCache = reinterpret_cast<InterfaceDispatchCache*>(pCell->m_pCache);
    for (size_t i = 0; i < CacheEntryCount; i++)
    {
        InterfaceDispatchCacheEntry* pEntry = &pCache->m_rgEntries[i];
        if (pEntry->m_pInstanceType == pObjectType)
        {
            return pEntry->m_pTargetCode;
        }
    }

    return ResolveTarget(pShadowStack, pObject, pCell);
}

// Stubs specialized for the supported cache sizes.
COOP_PINVOKE_HELPER(void*, RhpInterfaceDispatch1, (void* pShadowStack, Object* pObject, InterfaceDispatchCell* pCell)) { return InterfaceDispatchStub<1>(pShadowStack, pObject, pCell); }
COOP_PINVOKE_HELPER(void*, RhpInterfaceDispatch2, (void* pShadowStack, Object* pObject, InterfaceDispatchCell* pCell)) { return InterfaceDispatchStub<2>(pShadowStack, pObject, pCell); }
COOP_PINVOKE_HELPER(void*, RhpInterfaceDispatch4, (void* pShadowStack, Object* pObject, InterfaceDispatchCell* pCell)) { return InterfaceDispatchStub<4>(pShadowStack, pObject, pCell); }
COOP_PINVOKE_HELPER(void*, RhpInterfaceDispatch8, (void* pShadowStack, Object* pObject, InterfaceDispatchCell* pCell)) { return InterfaceDispatchStub<8>(pShadowStack, pObject, pCell); }
COOP_PINVOKE_HELPER(void*, RhpInterfaceDispatch16, (void* pShadowStack, Object* pObject, InterfaceDispatchCell* pCell)) { return InterfaceDispatchStub<16>(pShadowStack, pObject, pCell); }
COOP_PINVOKE_HELPER(void*, RhpInterfaceDispatch32, (void* pShadowStack, Object* pObject, InterfaceDispatchCell* pCell)) { return InterfaceDispatchStub<32>(pShadowStack, pObject, pCell); }
COOP_PINVOKE_HELPER(void*, RhpInterfaceDispatch64, (void* pShadowStack, Object* pObject, InterfaceDispatchCell* pCell)) { return InterfaceDispatchStub<64>(pShadowStack, pObject, pCell); }

// Stub dispatch routine for dispatch to a vtable slot.
COOP_PINVOKE_HELPER(void*, RhpVTableOffsetDispatch, (void* pShadowStack, class Object* pObject, InterfaceDispatchCell* pCell))
{
    uintptr_t pVTable = reinterpret_cast<uintptr_t>(pObject->get_EEType());
    uintptr_t offset = pCell->m_pCache;

    return *(void**)(pVTable + offset);
}
#endif // FEATURE_CACHED_INTERFACE_DISPATCH
