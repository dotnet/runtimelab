// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// ZeroGCHandles.cpp - a trivial, never-shrinking GC handle table.
//
// Because ZeroGC never determines reachability, weak handles behave exactly
// like strong handles from the caller's point of view: whatever was stored
// last is what you get back, for as long as the process runs.
//
#include "ZeroGC.h"

// ---------------------------------------------------------------------------
// ZeroGCHandleStore
// ---------------------------------------------------------------------------
ZeroGCHandleStore::ZeroGCHandleStore()
{
    InitializeCriticalSection(&m_lock);
}

ZeroGCHandleStore::~ZeroGCHandleStore()
{
    DeleteCriticalSection(&m_lock);
}

void ZeroGCHandleStore::Uproot() { }

bool ZeroGCHandleStore::ContainsHandle(OBJECTHANDLE handle)
{
    // We only ever hand out handles from our own slot allocator, so any
    // non-null handle passed back to us is considered ours.
    return handle != nullptr;
}

OBJECTHANDLE ZeroGCHandleStore::AllocSlot(Object* value, HandleType type)
{
    EnterCriticalSection(&m_lock);

    Slot* slot;
    if (m_freeList != nullptr)
    {
        slot = m_freeList;
        m_freeList = slot->NextFree;
    }
    else
    {
        slot = new (nothrow) Slot();
    }

    LeaveCriticalSection(&m_lock);

    if (slot == nullptr)
        return nullptr;

    slot->Value = value;
    slot->Secondary = nullptr;
    slot->ExtraInfo = nullptr;
    slot->Type = type;
    slot->InUse = true;

    int64_t live = InterlockedIncrement64(&g_zeroGCCounters.LiveHandleCount);
    int64_t peak = g_zeroGCCounters.PeakHandleCount;
    while (live > peak)
    {
        int64_t prev = InterlockedCompareExchange64(&g_zeroGCCounters.PeakHandleCount, live, peak);
        if (prev == peak)
            break;
        peak = prev;
    }

    return (OBJECTHANDLE)slot;
}

void ZeroGCHandleStore::FreeSlot(OBJECTHANDLE handle)
{
    if (handle == nullptr)
        return;

    Slot* slot = SlotFromHandle(handle);
    if (!slot->InUse)
        return;

    slot->InUse = false;
    slot->Value = nullptr;
    slot->Secondary = nullptr;

    EnterCriticalSection(&m_lock);
    slot->NextFree = m_freeList;
    m_freeList = slot;
    LeaveCriticalSection(&m_lock);

    InterlockedDecrement64(&g_zeroGCCounters.LiveHandleCount);
}

OBJECTHANDLE ZeroGCHandleStore::CreateHandleOfType(Object* object, HandleType type)
{
    return AllocSlot(object, type);
}

OBJECTHANDLE ZeroGCHandleStore::CreateHandleOfType(Object* object, HandleType type, int heapToAffinitizeTo)
{
    return AllocSlot(object, type);
}

OBJECTHANDLE ZeroGCHandleStore::CreateHandleWithExtraInfo(Object* object, HandleType type, void* pExtraInfo)
{
    OBJECTHANDLE handle = AllocSlot(object, type);
    if (handle != nullptr)
        SlotFromHandle(handle)->ExtraInfo = pExtraInfo;
    return handle;
}

OBJECTHANDLE ZeroGCHandleStore::CreateDependentHandle(Object* primary, Object* secondary)
{
    OBJECTHANDLE handle = AllocSlot(primary, HNDTYPE_DEPENDENT);
    if (handle != nullptr)
        SlotFromHandle(handle)->Secondary = secondary;
    return handle;
}

// ---------------------------------------------------------------------------
// ZeroGCHandleManager
// ---------------------------------------------------------------------------
bool ZeroGCHandleManager::Initialize()
{
    m_globalStore = new (nothrow) ZeroGCHandleStore();
    return m_globalStore != nullptr;
}

void ZeroGCHandleManager::Shutdown() { }

IGCHandleStore* ZeroGCHandleManager::GetGlobalHandleStore()
{
    return m_globalStore;
}

IGCHandleStore* ZeroGCHandleManager::CreateHandleStore()
{
    // Some hosts (e.g. AssemblyLoadContext unload tracking) create secondary
    // handle stores. Since ZeroGC never collects, and collectible-assembly
    // teardown is out of scope for a "never reclaim anything" GC, we simply
    // hand back a new table with the same trivial semantics.
    return new (nothrow) ZeroGCHandleStore();
}

void ZeroGCHandleManager::DestroyHandleStore(IGCHandleStore* store)
{
    delete store;
}

OBJECTHANDLE ZeroGCHandleManager::CreateGlobalHandleOfType(Object* object, HandleType type)
{
    return m_globalStore->CreateHandleOfType(object, type);
}

OBJECTHANDLE ZeroGCHandleManager::CreateDuplicateHandle(OBJECTHANDLE handle)
{
    ZeroGCHandleStore::Slot* slot = ZeroGCHandleStore::SlotFromHandle(handle);
    return m_globalStore->CreateHandleOfType(slot->Value, slot->Type);
}

void ZeroGCHandleManager::DestroyHandleOfType(OBJECTHANDLE handle, HandleType type)
{
    m_globalStore->FreeSlot(handle);
}

void ZeroGCHandleManager::DestroyHandleOfUnknownType(OBJECTHANDLE handle)
{
    m_globalStore->FreeSlot(handle);
}

void ZeroGCHandleManager::SetExtraInfoForHandle(OBJECTHANDLE handle, HandleType type, void* pExtraInfo)
{
    ZeroGCHandleStore::SlotFromHandle(handle)->ExtraInfo = pExtraInfo;
}

void* ZeroGCHandleManager::GetExtraInfoFromHandle(OBJECTHANDLE handle)
{
    return ZeroGCHandleStore::SlotFromHandle(handle)->ExtraInfo;
}

void ZeroGCHandleManager::StoreObjectInHandle(OBJECTHANDLE handle, Object* object)
{
    ZeroGCHandleStore::SlotFromHandle(handle)->Value = object;
}

bool ZeroGCHandleManager::StoreObjectInHandleIfNull(OBJECTHANDLE handle, Object* object)
{
    ZeroGCHandleStore::Slot* slot = ZeroGCHandleStore::SlotFromHandle(handle);
    if (slot->Value != nullptr)
        return false;
    slot->Value = object;
    return true;
}

void ZeroGCHandleManager::SetDependentHandleSecondary(OBJECTHANDLE handle, Object* object)
{
    ZeroGCHandleStore::SlotFromHandle(handle)->Secondary = object;
}

Object* ZeroGCHandleManager::GetDependentHandleSecondary(OBJECTHANDLE handle)
{
    return ZeroGCHandleStore::SlotFromHandle(handle)->Secondary;
}

Object* ZeroGCHandleManager::InterlockedCompareExchangeObjectInHandle(OBJECTHANDLE handle, Object* object, Object* comparandObject)
{
    ZeroGCHandleStore::Slot* slot = ZeroGCHandleStore::SlotFromHandle(handle);
    return (Object*)InterlockedCompareExchangePointer((void* volatile*)&slot->Value, object, comparandObject);
}

HandleType ZeroGCHandleManager::HandleFetchType(OBJECTHANDLE handle)
{
    return ZeroGCHandleStore::SlotFromHandle(handle)->Type;
}

void ZeroGCHandleManager::TraceRefCountedHandles(HANDLESCANPROC callback, uintptr_t param1, uintptr_t param2)
{
    // ZeroGC never scans handles for promotion, so ref-counted COM handle
    // tracing is a no-op: their targets are always considered reachable.
}
