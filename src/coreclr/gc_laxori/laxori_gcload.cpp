// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"
#include "gcenv.h"
#include "laxori_gc.h"

#ifdef TARGET_WINDOWS
#define LAXORI_EXPORT extern "C" __declspec(dllexport)
#else
#define LAXORI_EXPORT extern "C" __attribute__((visibility("default")))
#endif

namespace
{
    LaxoriGcHeap* Heap = nullptr;
}

LAXORI_EXPORT void LOCALGC_CALLCONV GC_VersionInfo(VersionInfo* info)
{
    g_runtimeSupportedVersion = *info;
    g_oldMethodTableFlags = info->MajorVersion < 2;
    info->MajorVersion = GC_INTERFACE_MAJOR_VERSION;
    info->MinorVersion = GC_INTERFACE_MINOR_VERSION;
    info->BuildVersion = 10;
    info->Name = "Laxori prototype";
}

LAXORI_EXPORT HRESULT LOCALGC_CALLCONV GC_Initialize(
    IGCToCLR* runtime, IGCHeap** heap, IGCHandleManager** handles, GcDacVars* dac)
{
    if (runtime == nullptr || heap == nullptr || handles == nullptr || dac == nullptr || Heap != nullptr)
    {
        return E_FAIL;
    }
    // Old loaders accept higher collector minors but supply only their old FastGCFunctions buffer.
    if (g_runtimeSupportedVersion.MajorVersion != EE_INTERFACE_MAJOR_VERSION ||
        g_runtimeSupportedVersion.MinorVersion < EE_INTERFACE_MINOR_VERSION)
    {
        runtime->LogErrorToHost("LAXORI shared write barriers require EE interface 6.1 or later.");
        return E_FAIL;
    }
    g_theGCToCLR = runtime;
    GCConfig::Initialize();
    if (!GCToOSInterface::Initialize())
    {
        GCToEEInterface::LogErrorToHost("LAXORI OS initialization failed.");
        return E_FAIL;
    }
    IGCHandleManager* manager = CreateGCHandleManager();
    LaxoriGcHeap* collector = new (std::nothrow) LaxoriGcHeap;
    if (manager == nullptr || collector == nullptr)
    {
        return E_OUTOFMEMORY;
    }
    // This existing EE category selects single-heap compatibility, not the stock WKS collector.
    g_gc_heap_type = GC_HEAP_WKS;
    g_num_processors = 1;
    g_max_generation = 2;
    g_theGCHeap = collector;
    g_theGCHandleManager = manager;
    Heap = collector;
    *heap = collector;
    *handles = manager;
    // Stock WKS/SVR DAC layouts are not LAXORI layouts. Version zero fails closed in DAC.
    *dac = {};
    return S_OK;
}

LAXORI_EXPORT uint64_t GC_LaxoriPrototype_GetMetric(uint32_t metric)
{
    return Heap == nullptr ? (metric == 0 ? 0 : UINT64_MAX) : Heap->GetPrototypeMetric(metric);
}

// The benchmark driver looks for GC_LxrPrototype_GetMetric and for GC_Laxori_GetMetric. Both names read the same metrics.
LAXORI_EXPORT uint64_t GC_Laxori_GetMetric(uint32_t metric)
{
    return GC_LaxoriPrototype_GetMetric(metric);
}

LAXORI_EXPORT uint64_t GC_LxrPrototype_GetMetric(uint32_t metric)
{
    return GC_LaxoriPrototype_GetMetric(metric);
}

LAXORI_EXPORT const char* GC_LaxoriPrototype_GetIdentity()
{
    return "LAXORI (thread-local GC regions over an LXR engine); GC interface 6.8; shared-write-barriers-v1";
}

LAXORI_EXPORT uintptr_t GC_LaxoriPrototype_GetOwnedStart()
{
    return Heap == nullptr ? 0 : Heap->GetOwnedStart();
}

LAXORI_EXPORT size_t GC_LaxoriPrototype_GetOwnedBytes()
{
    return Heap == nullptr ? 0 : Heap->GetOwnedBytes();
}

LAXORI_EXPORT uintptr_t GC_LaxoriPrototype_GetHandleAddress(OBJECTHANDLE handle)
{
    return Heap == nullptr ? 0 : Heap->GetHandleAddress(handle);
}

LAXORI_EXPORT size_t GC_LaxoriPrototype_GetAddressExtent(uintptr_t address)
{
    return Heap == nullptr ? 0 : Heap->GetAddressExtent(address);
}

LAXORI_EXPORT int32_t GC_LaxoriPrototype_IsOwnedHandle(OBJECTHANDLE handle)
{
    return Heap != nullptr && Heap->IsOwnedHandle(handle) ? 1 : 0;
}

LAXORI_EXPORT int32_t GC_LaxoriPrototype_Collect(uint32_t kind)
{
    if (Heap == nullptr)
    {
        return static_cast<int32_t>(LaxoriRcResult::NotInitialized);
    }
    if (kind >= static_cast<uint32_t>(LaxoriPauseKind::Count))
    {
        return static_cast<int32_t>(LaxoriRcResult::InvalidArgument);
    }
    // P26 (F8). This entry point is a synchronous collection for tests, like a user collection: it returns after the lazy work.
    return static_cast<int32_t>(Heap->CollectPrototype(static_cast<LaxoriPauseKind>(kind), true));
}

LAXORI_EXPORT int32_t GC_LaxoriPrototype_HoldTrace(int32_t hold)
{
    return Heap == nullptr ? static_cast<int32_t>(LaxoriRcResult::NotInitialized) :
        static_cast<int32_t>(Heap->HoldPrototypeTrace(hold != 0));
}

LAXORI_EXPORT int32_t GC_LaxoriPrototype_ArmScanCheckpoint(OBJECTHANDLE handle)
{
    return Heap == nullptr ? static_cast<int32_t>(LaxoriRcResult::NotInitialized) :
        static_cast<int32_t>(Heap->ArmPrototypeScan(handle));
}

LAXORI_EXPORT int32_t GC_LaxoriPrototype_ArmLifetimeProbe(OBJECTHANDLE handle)
{
    return Heap == nullptr ? static_cast<int32_t>(LaxoriRcResult::NotInitialized) :
        static_cast<int32_t>(Heap->ArmPrototypeLifetime(handle));
}

LAXORI_EXPORT int32_t GC_LaxoriPrototype_EnableStorageDiagnostics()
{
    return Heap == nullptr ? static_cast<int32_t>(LaxoriRcResult::NotInitialized) :
        static_cast<int32_t>(Heap->EnablePrototypeStorageDiagnostics());
}

LAXORI_EXPORT int32_t GC_LaxoriPrototype_GetStorageRange(uint32_t component, uintptr_t* start, size_t* bytes)
{
    return Heap == nullptr ? static_cast<int32_t>(LaxoriRcResult::NotInitialized) :
        static_cast<int32_t>(Heap->GetPrototypeStorageRange(component, start, bytes));
}

LAXORI_EXPORT HRESULT GC_LaxoriPrototype_WaitForTrace(int32_t timeout)
{
    return Heap == nullptr ? E_FAIL : Heap->WaitUntilConcurrentGCCompleteAsync(timeout);
}

LAXORI_EXPORT segment_handle GC_LaxoriPrototype_RegisterFrozenArray(
    void* memory, size_t capacity, OBJECTHANDLE array, OBJECTHANDLE target)
{
    return Heap == nullptr ? nullptr : Heap->RegisterPrototypeFrozenArray(memory, capacity, array, target);
}

LAXORI_EXPORT void GC_LaxoriPrototype_UnregisterFrozen(segment_handle segment)
{
    if (Heap != nullptr)
    {
        Heap->UnregisterFrozenSegment(segment);
    }
}
