// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// ZeroGCHeap.cpp - the IGCHeap implementation. See ZeroGC.h for the design overview.
//
#include "ZeroGC.h"
#include <cstdio>

#ifdef ZEROGC_TRACE
static void ZeroGCTrace(const char* msg)
{
    HANDLE h = CreateFileA("C:\\temp\\zerogc_trace.log", FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE,
        nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h != INVALID_HANDLE_VALUE)
    {
        DWORD written;
        WriteFile(h, msg, (DWORD)strlen(msg), &written, nullptr);
        WriteFile(h, "\r\n", 2, &written, nullptr);
        CloseHandle(h);
    }
}
#define ZGC_TRACE(msg) ZeroGCTrace(msg)
#else
#define ZGC_TRACE(msg)
#endif

ZeroGCCounters g_zeroGCCounters = {};
ZeroGCHeap* g_zeroGCHeap = nullptr;

// Reserve a huge chunk of address space up front (cheap - reservation does not
// commit physical memory) so that the bump allocator never has to relocate.
// 1 TiB is comfortably more than any of our test workloads allocate in a few
// minutes, while staying far below what x64 user-mode address space offers.
static const size_t ARENA_RESERVE_SIZE = (size_t)1 << 36; // 64 GiB - plenty for a multi-minute test run
static const size_t ARENA_COMMIT_CHUNK = 16 * 1024 * 1024; // 16 MiB at a time
static const size_t CONTEXT_ALLOC_QUANTUM = 128 * 1024;    // handed to each thread's alloc_context

// The frozen segment table is only used for a handful of segments (CoreLib's
// frozen string / frozen object heap), so a small fixed table is enough.
struct FrozenSegment
{
    uint8_t* Base;
    uint8_t* Allocated;
    uint8_t* Committed;
    uint8_t* Reserved;
    bool InUse;
};
static const int MAX_FROZEN_SEGMENTS = 64;
static FrozenSegment g_frozenSegments[MAX_FROZEN_SEGMENTS];
static CRITICAL_SECTION g_frozenSegmentsLock;

ZeroGCHeap* ZeroGCHeap::CreateAndInitialize()
{
    // NOTE: do NOT call Initialize() here. IGCHeap::Initialize() is a real
    // interface method that the EE calls itself, once, after GC_Initialize
    // returns this object - calling it eagerly here would run all of the
    // setup logic (including StompWriteBarrier) twice.
    ZeroGCHeap* heap = new (nothrow) ZeroGCHeap();
    return heap;
}

HRESULT ZeroGCHeap::Initialize()
{
    ZGC_TRACE("ZeroGCHeap::Initialize - enter");
    InitializeCriticalSection(&m_lock);
    InitializeCriticalSection(&g_frozenSegmentsLock);
    QueryPerformanceFrequency(&m_qpcFrequency);
    QueryPerformanceCounter(&m_startTime);

    m_arenaBase = (uint8_t*)VirtualAlloc(nullptr, ARENA_RESERVE_SIZE, MEM_RESERVE, PAGE_READWRITE);
    if (m_arenaBase == nullptr)
        return E_OUTOFMEMORY;

    m_arenaReservedEnd = m_arenaBase + ARENA_RESERVE_SIZE;
    m_arenaNextFree = m_arenaBase;
    m_arenaCommitEnd = m_arenaBase;
    ZGC_TRACE("ZeroGCHeap::Initialize - arena reserved");

    // Tell the EE about our (single, ever-growing) heap bounds so that the
    // JIT-generated write barrier never writes outside of our arena. Because
    // we never move or reclaim objects, the card table's *contents* are
    // irrelevant to us - but the table still has to exist, be sized
    // correctly, and (crucially) be *biased* by g_lowest_address: the
    // JIT-generated write barrier indexes the card table with the
    // destination's raw (absolute) address shifted by card_byte_shift - it
    // does NOT subtract lowest_address first. So the pointer we hand back
    // must itself be pre-shifted downward by lowest_address's card index,
    // the same trick gc.cpp's translate_card_table() uses, or every card
    // write lands far outside our small allocation.
    const int card_byte_shift = 11; // 2048 bytes/card; matches JIT_WriteBarrier on x64 (shr rcx, 0Bh)
    uintptr_t lowCardIndex = (uintptr_t)m_arenaBase >> card_byte_shift;
    uintptr_t highCardIndex = (uintptr_t)m_arenaReservedEnd >> card_byte_shift;
    size_t cardTableSize = (highCardIndex - lowCardIndex) + 1;
    uint8_t* cardTableRaw = (uint8_t*)calloc(cardTableSize, 1);
    if (cardTableRaw == nullptr)
    {
        ZGC_TRACE("ZeroGCHeap::Initialize - FAILED to allocate card table");
        return E_OUTOFMEMORY;
    }
    uint8_t* cardTableBiased = cardTableRaw - lowCardIndex;

    // FEATURE_MANUALLY_MANAGED_CARD_BUNDLES is defined for x64/arm64 (see
    // clrdefinitions.cmake), which means the EE's write barrier (gchelpers.cpp
    // ErectWriteBarrier/SetCardBundleByte) unconditionally maintains a *second*,
    // coarser-grained "card bundle" byte table (1 byte covers 2^21 = 2MB of
    // address space) alongside the card table itself, and dereferences it
    // unconditionally whenever a card is newly dirtied. We must allocate and
    // bias this table exactly like the card table (just with a different
    // shift), or the very first write barrier crashes writing through a
    // dangling/nullptr-based pointer.
    const int card_bundle_byte_shift = 21;
    uint8_t* cardBundleBiased = nullptr;
#ifdef FEATURE_MANUALLY_MANAGED_CARD_BUNDLES
    {
        uintptr_t lowBundleIndex = (uintptr_t)m_arenaBase >> card_bundle_byte_shift;
        uintptr_t highBundleIndex = (uintptr_t)m_arenaReservedEnd >> card_bundle_byte_shift;
        size_t cardBundleSize = (highBundleIndex - lowBundleIndex) + 1;
        uint8_t* cardBundleRaw = (uint8_t*)calloc(cardBundleSize, 1);
        if (cardBundleRaw == nullptr)
        {
            ZGC_TRACE("ZeroGCHeap::Initialize - FAILED to allocate card bundle table");
            return E_OUTOFMEMORY;
        }
        cardBundleBiased = cardBundleRaw - lowBundleIndex;
    }
#endif

    WriteBarrierParameters wbParams = {};
    wbParams.operation = WriteBarrierOp::Initialize;
    wbParams.is_runtime_suspended = true; // nothing is running yet
    wbParams.requires_upper_bounds_check = false;
    wbParams.card_table = (uint32_t*)cardTableBiased;
    wbParams.card_bundle_table = (uint32_t*)cardBundleBiased;
    wbParams.lowest_address = m_arenaBase;
    wbParams.highest_address = m_arenaReservedEnd;
    wbParams.ephemeral_low = (uint8_t*)1;   // "everything is ephemeral" -> avoids upper-bound checks
    wbParams.ephemeral_high = (uint8_t*)~(uintptr_t)0;
    wbParams.region_to_generation_table = nullptr;
    wbParams.region_shr = 0;
    wbParams.region_use_bitwise_write_barrier = false;

    if (g_theGCToCLR != nullptr)
    {
        ZGC_TRACE("ZeroGCHeap::Initialize - calling StompWriteBarrier");
        g_theGCToCLR->StompWriteBarrier(&wbParams);
        ZGC_TRACE("ZeroGCHeap::Initialize - StompWriteBarrier returned");
    }

    for (int i = 0; i < MAX_FROZEN_SEGMENTS; i++)
        g_frozenSegments[i].InUse = false;

    ZGC_TRACE("ZeroGCHeap::Initialize - exit OK");
    return S_OK;
}

Object* ZeroGCHeap::AllocateFromArena(gc_alloc_context* acontext, size_t size, uint32_t flags)
{
    ZGC_TRACE("AllocateFromArena - enter");
    size_t alignedSize = (size + 7) & ~(size_t)7;

    // Every real CoreCLR object is preceded by an 8-byte (sizeof(ObjHeader))
    // "sync block" header - Object::GetHeader() computes `this - 1` on that
    // type, and the EE writes to it directly (e.g. for thin locks / hash
    // codes), so it must be inside memory we own. MethodTable::GetBaseSize()
    // (and thus `size` here) already accounts for one object's own header,
    // which is enough to keep *consecutive* objects within a chunk safely
    // spaced - but the very first object placed in a brand new chunk has
    // nothing "before" it that belongs to us. Reserve one extra header's
    // worth of padding up front for every new chunk to guarantee that.
    const size_t headerPad = sizeof(void*); // == sizeof(ObjHeader), see gcenv.object.h

    EnterCriticalSection(&m_lock);

    // Large objects (and pinned/POH objects) get their own dedicated chunk so
    // that we don't waste the tail of a context's quantum on them.
    bool isLarge = (flags & GC_ALLOC_LARGE_OBJECT_HEAP) != 0 || (flags & GC_ALLOC_PINNED_OBJECT_HEAP) != 0
        || alignedSize >= LARGE_OBJECT_SIZE;
    size_t chunkSize = isLarge ? alignedSize : max(alignedSize, CONTEXT_ALLOC_QUANTUM);
    size_t reserveSize = chunkSize + headerPad;

    if ((size_t)(m_arenaReservedEnd - m_arenaNextFree) < reserveSize)
    {
        // Out of reserved address space - a real GC would fail the allocation.
        LeaveCriticalSection(&m_lock);
        return nullptr;
    }

    if ((size_t)(m_arenaCommitEnd - m_arenaNextFree) < reserveSize)
    {
        size_t needed = reserveSize - (m_arenaCommitEnd - m_arenaNextFree);
        size_t commitSize = max(needed, ARENA_COMMIT_CHUNK);
        commitSize = (commitSize + 0xFFFF) & ~(size_t)0xFFFF; // round to 64K
        if (m_arenaCommitEnd + commitSize > m_arenaReservedEnd)
            commitSize = m_arenaReservedEnd - m_arenaCommitEnd;

        if (VirtualAlloc(m_arenaCommitEnd, commitSize, MEM_COMMIT, PAGE_READWRITE) == nullptr)
        {
            LeaveCriticalSection(&m_lock);
            return nullptr;
        }
        m_arenaCommitEnd += commitSize;
    }

    uint8_t* rawStart = m_arenaNextFree;
    uint8_t* chunkStart = rawStart + headerPad;
    m_arenaNextFree = rawStart + reserveSize;

    if (!isLarge)
    {
        acontext->alloc_ptr = chunkStart + alignedSize;
        acontext->alloc_limit = chunkStart + chunkSize;
    }
    else
    {
        // Don't let a large/pinned allocation get merged into the context's
        // fast-path bump pointer.
        acontext->alloc_ptr = chunkStart + alignedSize;
        acontext->alloc_limit = chunkStart + alignedSize;
    }

    acontext->alloc_bytes += (int64_t)alignedSize;
    acontext->alloc_count++;

    InterlockedExchangeAdd64(&g_zeroGCCounters.TotalAllocatedBytes, (int64_t)alignedSize);
    InterlockedIncrement64(&g_zeroGCCounters.AllocContextRefills);

    LeaveCriticalSection(&m_lock);

    Object* obj = (Object*)chunkStart;
    ZGC_TRACE("AllocateFromArena - exit OK");
    return obj;
}

Object* ZeroGCHeap::Alloc(gc_alloc_context* acontext, size_t size, uint32_t flags)
{
    return AllocateFromArena(acontext, size, flags);
}

void ZeroGCHeap::PublishObject(uint8_t* obj) { ZGC_TRACE("ZeroGCHeap::PublishObject"); }
void ZeroGCHeap::SetWaitForGCEvent() { ZGC_TRACE("ZeroGCHeap::SetWaitForGCEvent"); }
void ZeroGCHeap::ResetWaitForGCEvent() { ZGC_TRACE("ZeroGCHeap::ResetWaitForGCEvent"); }

bool ZeroGCHeap::IsValidSegmentSize(size_t size) { ZGC_TRACE("ZeroGCHeap::IsValidSegmentSize"); return (size & (size - 1)) == 0; }
bool ZeroGCHeap::IsValidGen0MaxSize(size_t size) { ZGC_TRACE("ZeroGCHeap::IsValidGen0MaxSize"); return true; }
size_t ZeroGCHeap::GetValidSegmentSize(bool large_seg) { ZGC_TRACE("ZeroGCHeap::GetValidSegmentSize"); return ARENA_COMMIT_CHUNK; }
void ZeroGCHeap::SetReservedVMLimit(size_t vmlimit) { ZGC_TRACE("ZeroGCHeap::SetReservedVMLimit"); }

void ZeroGCHeap::WaitUntilConcurrentGCComplete() { ZGC_TRACE("ZeroGCHeap::WaitUntilConcurrentGCComplete"); }
bool ZeroGCHeap::IsConcurrentGCInProgress() { ZGC_TRACE("ZeroGCHeap::IsConcurrentGCInProgress"); return false; }
void ZeroGCHeap::TemporaryEnableConcurrentGC() { ZGC_TRACE("ZeroGCHeap::TemporaryEnableConcurrentGC"); }
void ZeroGCHeap::TemporaryDisableConcurrentGC() { ZGC_TRACE("ZeroGCHeap::TemporaryDisableConcurrentGC"); }
bool ZeroGCHeap::IsConcurrentGCEnabled() { ZGC_TRACE("ZeroGCHeap::IsConcurrentGCEnabled"); return false; }
HRESULT ZeroGCHeap::WaitUntilConcurrentGCCompleteAsync(int millisecondsTimeout) { ZGC_TRACE("ZeroGCHeap::WaitUntilConcurrentGCCompleteAsync"); return S_OK; }

size_t ZeroGCHeap::GetNumberOfFinalizable() { ZGC_TRACE("ZeroGCHeap::GetNumberOfFinalizable"); return 0; }
Object* ZeroGCHeap::GetNextFinalizable() { ZGC_TRACE("ZeroGCHeap::GetNextFinalizable"); return nullptr; }

void ZeroGCHeap::GetMemoryInfo(uint64_t* highMemLoadThresholdBytes,
                                uint64_t* totalAvailableMemoryBytes,
                                uint64_t* lastRecordedMemLoadBytes,
                                uint64_t* lastRecordedHeapSizeBytes,
                                uint64_t* lastRecordedFragmentationBytes,
                                uint64_t* totalCommittedBytes,
                                uint64_t* promotedBytes,
                                uint64_t* pinnedObjectCount,
                                uint64_t* finalizationPendingCount,
                                uint64_t* index,
                                uint32_t* generation,
                                uint32_t* pauseTimePct,
                                bool* isCompaction,
                                bool* isConcurrent,
                                uint64_t* genInfoRaw,
                                uint64_t* pauseInfoRaw,
                                int kind)
{
    MEMORYSTATUSEX memStatus = {};
    memStatus.dwLength = sizeof(memStatus);
    GlobalMemoryStatusEx(&memStatus);

    size_t committed = (size_t)(m_arenaNextFree - m_arenaBase);

    if (highMemLoadThresholdBytes) *highMemLoadThresholdBytes = (uint64_t)((memStatus.ullTotalPhys * 90) / 100);
    if (totalAvailableMemoryBytes) *totalAvailableMemoryBytes = memStatus.ullTotalPhys;
    if (lastRecordedMemLoadBytes) *lastRecordedMemLoadBytes = memStatus.ullTotalPhys - memStatus.ullAvailPhys;
    if (lastRecordedHeapSizeBytes) *lastRecordedHeapSizeBytes = committed;
    if (lastRecordedFragmentationBytes) *lastRecordedFragmentationBytes = 0;
    if (totalCommittedBytes) *totalCommittedBytes = committed;
    if (promotedBytes) *promotedBytes = 0;
    if (pinnedObjectCount) *pinnedObjectCount = 0;
    if (finalizationPendingCount) *finalizationPendingCount = 0;
    if (index) *index = 0;
    if (generation) *generation = 0;
    if (pauseTimePct) *pauseTimePct = 0;
    if (isCompaction) *isCompaction = false;
    if (isConcurrent) *isConcurrent = false;
    if (genInfoRaw) memset(genInfoRaw, 0, sizeof(uint64_t) * 8);
    if (pauseInfoRaw) memset(pauseInfoRaw, 0, sizeof(uint64_t) * 2);
}

uint32_t ZeroGCHeap::GetMemoryLoad()
{
    MEMORYSTATUSEX memStatus = {};
    memStatus.dwLength = sizeof(memStatus);
    GlobalMemoryStatusEx(&memStatus);
    return memStatus.dwMemoryLoad;
}

int ZeroGCHeap::GetGcLatencyMode() { ZGC_TRACE("ZeroGCHeap::GetGcLatencyMode"); return 2; /* SustainedLowLatency-ish: we're always low latency */ }
int ZeroGCHeap::SetGcLatencyMode(int newLatencyMode) { ZGC_TRACE("ZeroGCHeap::SetGcLatencyMode"); return 0; }
int ZeroGCHeap::GetLOHCompactionMode() { ZGC_TRACE("ZeroGCHeap::GetLOHCompactionMode"); return 0; }
void ZeroGCHeap::SetLOHCompactionMode(int newLOHCompactionMode) { ZGC_TRACE("ZeroGCHeap::SetLOHCompactionMode"); }
bool ZeroGCHeap::RegisterForFullGCNotification(uint32_t gen2Percentage, uint32_t lohPercentage) { ZGC_TRACE("ZeroGCHeap::RegisterForFullGCNotification"); return false; }
bool ZeroGCHeap::CancelFullGCNotification() { ZGC_TRACE("ZeroGCHeap::CancelFullGCNotification"); return false; }
int ZeroGCHeap::WaitForFullGCApproach(int millisecondsTimeout) { ZGC_TRACE("ZeroGCHeap::WaitForFullGCApproach"); return wait_full_gc_na; }
int ZeroGCHeap::WaitForFullGCComplete(int millisecondsTimeout) { ZGC_TRACE("ZeroGCHeap::WaitForFullGCComplete"); return wait_full_gc_na; }

unsigned ZeroGCHeap::WhichGeneration(Object* obj) { ZGC_TRACE("ZeroGCHeap::WhichGeneration"); return 0; }

int ZeroGCHeap::CollectionCount(int generation, int get_bgc_fgc_coutn)
{
    // ZeroGC never runs a collection of any kind, in any generation.
    return 0;
}

int ZeroGCHeap::StartNoGCRegion(uint64_t totalSize, bool lohSizeKnown, uint64_t lohSize, bool disallowFullBlockingGC)
{
    // We are permanently in a "no GC region" - trivially succeed.
    return start_no_gc_success;
}

int ZeroGCHeap::EndNoGCRegion()
{
    return end_no_gc_success;
}

size_t ZeroGCHeap::GetTotalBytesInUse()
{
    return (size_t)(m_arenaNextFree - m_arenaBase);
}

uint64_t ZeroGCHeap::GetTotalAllocatedBytes()
{
    // NOTE: g_zeroGCCounters.TotalAllocatedBytes only accounts for bytes handed
    // out via AllocateFromArena (i.e. allocation-context *refills*). Most
    // individual object allocations are actually satisfied inline by the JIT's
    // fast-path bump allocator using the alloc_ptr/alloc_limit window we set up
    // on each refill, without ever calling back into GC code - so that counter
    // drastically undercounts real allocation volume. Since we never reclaim
    // memory, the arena's bump-pointer position is itself an exact total of
    // every byte ever allocated (refill overhead aside), so use that instead.
    return (uint64_t)GetTotalBytesInUse();
}

HRESULT ZeroGCHeap::GarbageCollect(int generation, bool low_memory_p, int mode)
{
    InterlockedIncrement64(&g_zeroGCCounters.InducedCollectRequests);
    // Intentionally does nothing else: this is the whole point of ZeroGC.
    return S_OK;
}

unsigned ZeroGCHeap::GetMaxGeneration() { ZGC_TRACE("ZeroGCHeap::GetMaxGeneration"); return 2; }
void ZeroGCHeap::SetFinalizationRun(Object* obj) { ZGC_TRACE("ZeroGCHeap::SetFinalizationRun"); }
bool ZeroGCHeap::RegisterForFinalization(int gen, Object* obj) { ZGC_TRACE("ZeroGCHeap::RegisterForFinalization"); return true; }
int ZeroGCHeap::GetLastGCPercentTimeInGC() { ZGC_TRACE("ZeroGCHeap::GetLastGCPercentTimeInGC"); return 0; }
size_t ZeroGCHeap::GetLastGCGenerationSize(int gen) { ZGC_TRACE("ZeroGCHeap::GetLastGCGenerationSize"); return 0; }

bool ZeroGCHeap::IsPromoted(Object* object) { ZGC_TRACE("ZeroGCHeap::IsPromoted"); return true; /* everything survives forever */ }

bool ZeroGCHeap::IsHeapPointer(void* object, bool small_heap_only)
{
    uint8_t* p = (uint8_t*)object;
    return p >= m_arenaBase && p < m_arenaNextFree;
}

unsigned ZeroGCHeap::GetCondemnedGeneration() { ZGC_TRACE("ZeroGCHeap::GetCondemnedGeneration"); return 0; }
bool ZeroGCHeap::IsGCInProgressHelper(bool bConsiderGCStart) { ZGC_TRACE("ZeroGCHeap::IsGCInProgressHelper"); return false; }
unsigned ZeroGCHeap::GetGcCount() { ZGC_TRACE("ZeroGCHeap::GetGcCount"); return 0; }
bool ZeroGCHeap::IsThreadUsingAllocationContextHeap(gc_alloc_context* acontext, int thread_number) { ZGC_TRACE("ZeroGCHeap::IsThreadUsingAllocationContextHeap"); return true; }
bool ZeroGCHeap::IsEphemeral(Object* object) { ZGC_TRACE("ZeroGCHeap::IsEphemeral"); return true; }
uint32_t ZeroGCHeap::WaitUntilGCComplete(bool bConsiderGCStart) { ZGC_TRACE("ZeroGCHeap::WaitUntilGCComplete"); return 0; }
void ZeroGCHeap::FixAllocContext(gc_alloc_context* acontext, void* arg, void* heap) { ZGC_TRACE("ZeroGCHeap::FixAllocContext"); }
size_t ZeroGCHeap::GetCurrentObjSize() { ZGC_TRACE("ZeroGCHeap::GetCurrentObjSize"); return (size_t)g_zeroGCCounters.TotalAllocatedBytes; }
void ZeroGCHeap::SetGCInProgress(bool fInProgress) { ZGC_TRACE("ZeroGCHeap::SetGCInProgress"); }
bool ZeroGCHeap::RuntimeStructuresValid() { ZGC_TRACE("ZeroGCHeap::RuntimeStructuresValid"); return true; }
void ZeroGCHeap::SetSuspensionPending(bool fSuspensionPending) { ZGC_TRACE("ZeroGCHeap::SetSuspensionPending"); }
void ZeroGCHeap::SetYieldProcessorScalingFactor(float yieldProcessorScalingFactor) { ZGC_TRACE("ZeroGCHeap::SetYieldProcessorScalingFactor"); }
void ZeroGCHeap::Shutdown() { ZGC_TRACE("ZeroGCHeap::Shutdown"); }

size_t ZeroGCHeap::GetLastGCStartTime(int generation) { ZGC_TRACE("ZeroGCHeap::GetLastGCStartTime"); return 0; }
size_t ZeroGCHeap::GetLastGCDuration(int generation) { ZGC_TRACE("ZeroGCHeap::GetLastGCDuration"); return 0; }

size_t ZeroGCHeap::GetNow()
{
    LARGE_INTEGER now;
    QueryPerformanceCounter(&now);
    return (size_t)((now.QuadPart - m_startTime.QuadPart) * 1000 / m_qpcFrequency.QuadPart);
}

bool ZeroGCHeap::IsLargeObject(Object* pObj)
{
    MethodTable* mt = pObj->GetGCSafeMethodTable();
    return mt->GetBaseSize() >= LARGE_OBJECT_SIZE;
}

void ZeroGCHeap::ValidateObjectMember(Object* obj) { ZGC_TRACE("ZeroGCHeap::ValidateObjectMember"); }

Object* ZeroGCHeap::NextObj(Object* object)
{
    // We don't maintain a walkable segment layout (chunks may not be
    // contiguous with live objects packed with no gaps), so heap-walking is
    // not supported. Diagnostics tools should treat this as "unsupported".
    return nullptr;
}

Object* ZeroGCHeap::GetContainingObject(void* pInteriorPtr, bool fCollectedGenOnly)
{
    return nullptr;
}

void ZeroGCHeap::DiagWalkObject(Object* obj, walk_fn fn, void* context) { ZGC_TRACE("ZeroGCHeap::DiagWalkObject"); }
void ZeroGCHeap::DiagWalkObject2(Object* obj, walk_fn2 fn, void* context) { ZGC_TRACE("ZeroGCHeap::DiagWalkObject2"); }
void ZeroGCHeap::DiagWalkHeap(walk_fn fn, void* context, int gen_number, bool walk_large_object_heap_p) { ZGC_TRACE("ZeroGCHeap::DiagWalkHeap"); }
void ZeroGCHeap::DiagWalkSurvivorsWithType(void* gc_context, record_surv_fn fn, void* diag_context, walk_surv_type type, int gen_number) { ZGC_TRACE("ZeroGCHeap::DiagWalkSurvivorsWithType"); }
void ZeroGCHeap::DiagWalkFinalizeQueue(void* gc_context, fq_walk_fn fn) { ZGC_TRACE("ZeroGCHeap::DiagWalkFinalizeQueue"); }
void ZeroGCHeap::DiagScanFinalizeQueue(fq_scan_fn fn, ScanContext* context) { ZGC_TRACE("ZeroGCHeap::DiagScanFinalizeQueue"); }
void ZeroGCHeap::DiagScanHandles(handle_scan_fn fn, int gen_number, ScanContext* context) { ZGC_TRACE("ZeroGCHeap::DiagScanHandles"); }
void ZeroGCHeap::DiagScanDependentHandles(handle_scan_fn fn, int gen_number, ScanContext* context) { ZGC_TRACE("ZeroGCHeap::DiagScanDependentHandles"); }
void ZeroGCHeap::DiagDescrGenerations(gen_walk_fn fn, void* context)
{
    if (fn != nullptr)
        fn(context, 0, m_arenaBase, m_arenaNextFree, m_arenaReservedEnd);
}
void ZeroGCHeap::DiagTraceGCSegments() { ZGC_TRACE("ZeroGCHeap::DiagTraceGCSegments"); }
void ZeroGCHeap::DiagGetGCSettings(EtwGCSettingsInfo* settings)
{
    if (settings == nullptr)
        return;
    memset(settings, 0, sizeof(*settings));
}

bool ZeroGCHeap::StressHeap(gc_alloc_context* acontext) { ZGC_TRACE("ZeroGCHeap::StressHeap"); return false; }

segment_handle ZeroGCHeap::RegisterFrozenSegment(segment_info* pseginfo)
{
    ZGC_TRACE("RegisterFrozenSegment - enter");
    EnterCriticalSection(&g_frozenSegmentsLock);
    for (int i = 0; i < MAX_FROZEN_SEGMENTS; i++)
    {
        if (!g_frozenSegments[i].InUse)
        {
            g_frozenSegments[i].InUse = true;
            g_frozenSegments[i].Base = (uint8_t*)pseginfo->pvMem;
            g_frozenSegments[i].Allocated = (uint8_t*)pseginfo->pvMem + pseginfo->ibAllocated;
            g_frozenSegments[i].Committed = (uint8_t*)pseginfo->pvMem + pseginfo->ibCommit;
            g_frozenSegments[i].Reserved = (uint8_t*)pseginfo->pvMem + pseginfo->ibReserved;
            LeaveCriticalSection(&g_frozenSegmentsLock);
            return (segment_handle)(intptr_t)(i + 1);
        }
    }
    LeaveCriticalSection(&g_frozenSegmentsLock);
    return (segment_handle)nullptr;
}

void ZeroGCHeap::UnregisterFrozenSegment(segment_handle seg)
{
    intptr_t idx = (intptr_t)seg - 1;
    if (idx < 0 || idx >= MAX_FROZEN_SEGMENTS)
        return;
    EnterCriticalSection(&g_frozenSegmentsLock);
    g_frozenSegments[idx].InUse = false;
    LeaveCriticalSection(&g_frozenSegmentsLock);
}

bool ZeroGCHeap::IsInFrozenSegment(Object* object)
{
    uint8_t* p = (uint8_t*)object;
    bool found = false;
    EnterCriticalSection(&g_frozenSegmentsLock);
    for (int i = 0; i < MAX_FROZEN_SEGMENTS; i++)
    {
        if (g_frozenSegments[i].InUse && p >= g_frozenSegments[i].Base && p < g_frozenSegments[i].Committed)
        {
            found = true;
            break;
        }
    }
    LeaveCriticalSection(&g_frozenSegmentsLock);
    return found;
}

void ZeroGCHeap::ControlEvents(GCEventKeyword keyword, GCEventLevel level) { ZGC_TRACE("ZeroGCHeap::ControlEvents"); }
void ZeroGCHeap::ControlPrivateEvents(GCEventKeyword keyword, GCEventLevel level) { ZGC_TRACE("ZeroGCHeap::ControlPrivateEvents"); }

unsigned int ZeroGCHeap::GetGenerationWithRange(Object* object, uint8_t** ppStart, uint8_t** ppAllocated, uint8_t** ppReserved)
{
    if (ppStart) *ppStart = m_arenaBase;
    if (ppAllocated) *ppAllocated = m_arenaNextFree;
    if (ppReserved) *ppReserved = m_arenaReservedEnd;
    return 0;
}

int64_t ZeroGCHeap::GetTotalPauseDuration() { ZGC_TRACE("ZeroGCHeap::GetTotalPauseDuration"); return 0; }

void ZeroGCHeap::EnumerateConfigurationValues(void* context, ConfigurationValueFunc configurationValueFunc)
{
    if (configurationValueFunc == nullptr)
        return;
    configurationValueFunc(context, "ZeroGC", "System.GC.Name", GCConfigurationType::StringUtf8, (int64_t)(intptr_t)"ZeroGC");
}

void ZeroGCHeap::UpdateFrozenSegment(segment_handle seg, uint8_t* allocated, uint8_t* committed)
{
    intptr_t idx = (intptr_t)seg - 1;
    if (idx < 0 || idx >= MAX_FROZEN_SEGMENTS)
        return;
    EnterCriticalSection(&g_frozenSegmentsLock);
    g_frozenSegments[idx].Allocated = allocated;
    g_frozenSegments[idx].Committed = committed;
    LeaveCriticalSection(&g_frozenSegmentsLock);
}

int ZeroGCHeap::RefreshMemoryLimit() { ZGC_TRACE("ZeroGCHeap::RefreshMemoryLimit"); return refresh_success; }

enable_no_gc_region_callback_status ZeroGCHeap::EnableNoGCRegionCallback(NoGCRegionCallbackFinalizerWorkItem* callback, uint64_t callback_threshold)
{
    return not_started;
}

FinalizerWorkItem* ZeroGCHeap::GetExtraWorkForFinalization() { ZGC_TRACE("ZeroGCHeap::GetExtraWorkForFinalization"); return nullptr; }
uint64_t ZeroGCHeap::GetGenerationBudget(int generation) { ZGC_TRACE("ZeroGCHeap::GetGenerationBudget"); return ARENA_COMMIT_CHUNK; }
size_t ZeroGCHeap::GetLOHThreshold() { ZGC_TRACE("ZeroGCHeap::GetLOHThreshold"); return LARGE_OBJECT_SIZE; }
void ZeroGCHeap::DiagWalkHeapWithACHandling(walk_fn fn, void* context, int gen_number, bool walk_large_object_heap_p) { ZGC_TRACE("ZeroGCHeap::DiagWalkHeapWithACHandling"); }
void ZeroGCHeap::NullBridgeObjectsWeakRefs(size_t length, void* unreachableObjectHandles) { ZGC_TRACE("ZeroGCHeap::NullBridgeObjectsWeakRefs"); }
