// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// The handle store of Laxori. It is a copy of gc/gchandletable.cpp. The only change: every call that gives a handle an
// object first calls LaxoriEscapeObject. A handle is a root that the owner thread of a request region cannot track, so an
// object of a request-private region that gets a handle (of any type, weak and dependent included) publishes its region.

#include "common.h"
#include "gcenv.h"
#include "gchandletableimpl.h"
#include "objecthandle.h"
#include "handletablepriv.h"

// Defined in laxori_gc.cpp. Does nothing when the object is not in a request-private region.
void LaxoriEscapeObject(Object* object);

GCHandleStore* g_gcGlobalHandleStore;

IGCHandleManager* CreateGCHandleManager()
{
    return new (nothrow) GCHandleManager();
}

void GCHandleStore::Uproot()
{
    // Dead path. The vtable slot is preserved for GC_INTERFACE_MAJOR_VERSION 5
    // ABI compatibility with external standalone GC implementations.
    assert(!"Uproot is not in use");
}

bool GCHandleStore::ContainsHandle(OBJECTHANDLE handle)
{
    // Dead path. The vtable slot is preserved for GC_INTERFACE_MAJOR_VERSION 5
    // ABI compatibility with external standalone GC implementations.
    assert(!"ContainsHandle is not in use");
    return false;
}

// this is the number of handles we allocate in a handle table before we switch to the next table.
#define HANDLE_THRESHOLD (15)
static int s_numTableSlots = 0;

HHANDLETABLE GCHandleStore::GetTable()
{
    if (!s_numTableSlots)
    {
        s_numTableSlots = getNumberOfSlots();
    }

    if (s_numTableSlots == 1)
        return _underlyingBucket.pTable[0];

    gc_alloc_context* ctx = GetCurrentThreadAllocContext();
    if (!ctx)
        return _underlyingBucket.pTable[0];

    // high 16-bits are for the handle info.
    int handleInfo = (uint32_t)(ctx->alloc_count) >> 16;

    // high 10 bits store the cpu index.
    // low 6 bits store the # of handles allocated so far (before the next reset).
    int numHandles = handleInfo & 0x3f;
    int cpuIndex = handleInfo >> 6;
    if (numHandles == HANDLE_THRESHOLD)
    {
        numHandles = 0;
        cpuIndex++;
        cpuIndex = (cpuIndex > (s_numTableSlots - 1)) ? 0 : cpuIndex;
    }
    else
    {
        numHandles++;
    }

    int newHandleInfo = numHandles | (cpuIndex << 6);

    int low_16_bits = ctx->alloc_count & 0xffff;
    ctx->alloc_count = low_16_bits | (newHandleInfo << 16);

    HHANDLETABLE handletable = _underlyingBucket.pTable[cpuIndex];
    return handletable;
}

OBJECTHANDLE GCHandleStore::CreateHandleOfType(Object* object, HandleType type)
{
    LaxoriEscapeObject(object);
    HHANDLETABLE handletable = GetTable();
    return ::HndCreateHandle(handletable, type, ObjectToOBJECTREF(object));
}

OBJECTHANDLE GCHandleStore::CreateHandleOfType(Object* object, HandleType type, int heapToAffinitizeTo)
{
    LaxoriEscapeObject(object);
    HHANDLETABLE handletable = _underlyingBucket.pTable[heapToAffinitizeTo];
    return ::HndCreateHandle(handletable, type, ObjectToOBJECTREF(object));
}

OBJECTHANDLE GCHandleStore::CreateHandleWithExtraInfo(Object* object, HandleType type, void* pExtraInfo)
{
    LaxoriEscapeObject(object);
    HHANDLETABLE handletable = _underlyingBucket.pTable[GetCurrentThreadHomeHeapNumber()];
    return ::HndCreateHandle(handletable, type, ObjectToOBJECTREF(object), reinterpret_cast<uintptr_t>(pExtraInfo));
}

OBJECTHANDLE GCHandleStore::CreateDependentHandle(Object* primary, Object* secondary)
{
    LaxoriEscapeObject(primary);
    LaxoriEscapeObject(secondary);
    HHANDLETABLE handletable = _underlyingBucket.pTable[GetCurrentThreadHomeHeapNumber()];
    OBJECTHANDLE handle = ::HndCreateHandle(handletable, HNDTYPE_DEPENDENT, ObjectToOBJECTREF(primary));
    if (!handle)
    {
        return nullptr;
    }

    ::SetDependentHandleSecondary(handle, ObjectToOBJECTREF(secondary));
    return handle;
}

GCHandleStore::~GCHandleStore()
{
    ::Ref_DestroyHandleTableBucket(&_underlyingBucket);
}

bool GCHandleManager::Initialize()
{
    return Ref_Initialize();
}

void GCHandleManager::Shutdown()
{
    if (g_gcGlobalHandleStore != nullptr)
    {
        DestroyHandleStore(g_gcGlobalHandleStore);
    }

    ::Ref_Shutdown();
}

IGCHandleStore* GCHandleManager::GetGlobalHandleStore()
{
    return g_gcGlobalHandleStore;
}

IGCHandleStore* GCHandleManager::CreateHandleStore()
{
    // Dead path. The vtable slot is preserved for GC_INTERFACE_MAJOR_VERSION 5
    // ABI compatibility with external standalone GC implementations.
    assert(!"CreateHandleStore is not in use");
    return nullptr;
}

void GCHandleManager::DestroyHandleStore(IGCHandleStore* store)
{
    delete store;
}

OBJECTHANDLE GCHandleManager::CreateGlobalHandleOfType(Object* object, HandleType type)
{
    LaxoriEscapeObject(object);
    return ::HndCreateHandle(g_HandleTableMap.pBuckets[0]->pTable[GetCurrentThreadHomeHeapNumber()], type, ObjectToOBJECTREF(object));
}

OBJECTHANDLE GCHandleManager::CreateDuplicateHandle(OBJECTHANDLE handle)
{
    return ::HndCreateHandle(HndGetHandleTable(handle), HNDTYPE_DEFAULT, ::HndFetchHandle(handle));
}

void GCHandleManager::DestroyHandleOfType(OBJECTHANDLE handle, HandleType type)
{
    ::HndDestroyHandle(::HndGetHandleTable(handle), type, handle);
}

void GCHandleManager::DestroyHandleOfUnknownType(OBJECTHANDLE handle)
{
    ::HndDestroyHandleOfUnknownType(::HndGetHandleTable(handle), handle);
}

void GCHandleManager::SetExtraInfoForHandle(OBJECTHANDLE  handle, HandleType type, void* pExtraInfo)
{
    ::HndSetHandleExtraInfo(handle, type, (uintptr_t)pExtraInfo);
}

void* GCHandleManager::GetExtraInfoFromHandle(OBJECTHANDLE handle)
{
    return (void*)::HndGetHandleExtraInfo(handle);
}

void GCHandleManager::StoreObjectInHandle(OBJECTHANDLE handle, Object* object)
{
    LaxoriEscapeObject(object);
    ::HndAssignHandle(handle, ObjectToOBJECTREF(object));
}

bool GCHandleManager::StoreObjectInHandleIfNull(OBJECTHANDLE handle, Object* object)
{
    LaxoriEscapeObject(object);
    return !!::HndFirstAssignHandle(handle, ObjectToOBJECTREF(object));
}

void GCHandleManager::SetDependentHandleSecondary(OBJECTHANDLE handle, Object* object)
{
    LaxoriEscapeObject(object);
    ::SetDependentHandleSecondary(handle, ObjectToOBJECTREF(object));
}

Object* GCHandleManager::GetDependentHandleSecondary(OBJECTHANDLE handle)
{
    return OBJECTREFToObject(::GetDependentHandleSecondary(handle));
}

Object* GCHandleManager::InterlockedCompareExchangeObjectInHandle(OBJECTHANDLE handle, Object* object, Object* comparandObject)
{
    LaxoriEscapeObject(object);
    return (Object*)::HndInterlockedCompareExchangeHandle(handle, ObjectToOBJECTREF(object), ObjectToOBJECTREF(comparandObject));
}

HandleType GCHandleManager::HandleFetchType(OBJECTHANDLE handle)
{
    uint32_t type = ::HandleFetchType(handle);
    assert(type >= HNDTYPE_WEAK_SHORT && type <= HNDTYPE_CROSSREFERENCE);
    return static_cast<HandleType>(type);
}

void GCHandleManager::TraceRefCountedHandles(HANDLESCANPROC callback, uintptr_t param1, uintptr_t param2)
{
    ::Ref_TraceRefCountHandles(callback, param1, param2);
}
