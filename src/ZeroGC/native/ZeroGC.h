// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// ZeroGC.h
//
// ZeroGC is an experimental "epsilon"-style standalone CoreCLR GC. It only ever
// allocates memory: it never scans, never compacts and never reclaims a single
// byte. It exists to:
//
//   * provide a minimal, well documented reference for people who want to
//     write their own standalone GC using the CoreCLR IGCHeap ABI, and
//   * be a useful baseline for measuring the overhead the "real" GC adds to a
//     process, for very-short-lived or allocation-light workloads.
//
// It is directly inspired by, and named after, the "Zero GC" concept described
// in https://github.com/kkokosa/UpsilonGC and the JVM's Epsilon GC (JEP 318).
//
#ifndef __ZEROGC_H__
#define __ZEROGC_H__

#ifndef BUILD_AS_STANDALONE
#define BUILD_AS_STANDALONE
#endif
#ifndef FEATURE_STANDALONE_GC
#define FEATURE_STANDALONE_GC
#endif

#include <stdint.h>
#include <stddef.h>
#include <windows.h>

#include "gcenv.structs.h"
#include "gcenv.base.h"
#include "gcenv.os.h"
#include "gcenv.interlocked.h"
#include "gcenv.interlocked.inl"
#include "gcenv.object.h"
#include "gcenv.sync.h"
#include "gcenv.ee.h"
#include "gcinterface.h"

// Forwarded from the runtime; set once in GC_Initialize.
extern IGCToCLR* g_theGCToCLR;
extern VersionInfo g_runtimeSupportedVersion;
extern bool g_oldMethodTableFlags;

// ---------------------------------------------------------------------------
// Global counters, exposed so that the managed GC.* APIs (GC.CollectionCount,
// GC.GetTotalAllocatedBytes, GC.GetGCMemoryInfo, ...) return meaningful,
// comparable numbers when ZeroGC is active.
// ---------------------------------------------------------------------------
struct ZeroGCCounters
{
    // Total bytes ever handed out to the managed heap (never reclaimed).
    volatile int64_t TotalAllocatedBytes;

    // Number of times IGCHeap::Alloc refilled a thread allocation context
    // (i.e. the number of times the bump-pointer arena was touched under the lock).
    volatile int64_t AllocContextRefills;

    // Number of live GC handles currently outstanding.
    volatile int64_t LiveHandleCount;

    // Peak number of live GC handles.
    volatile int64_t PeakHandleCount;

    // GarbageCollect() call count - always 0 unless an app explicitly forces one,
    // in which case we still just no-op, but we count the call for observability.
    volatile int64_t InducedCollectRequests;
};

extern ZeroGCCounters g_zeroGCCounters;

// ---------------------------------------------------------------------------
// ZeroGCHeap: the only heap. Bump-pointer allocator over a single very large
// reserved (but lazily committed) virtual address range. Never frees, never
// scans, never moves an object once allocated.
// ---------------------------------------------------------------------------
class ZeroGCHeap : public IGCHeap
{
public:
    static ZeroGCHeap* CreateAndInitialize();

    // Hosting
    bool IsValidSegmentSize(size_t size) override;
    bool IsValidGen0MaxSize(size_t size) override;
    size_t GetValidSegmentSize(bool large_seg = false) override;
    void SetReservedVMLimit(size_t vmlimit) override;

    // Concurrent GC (ZeroGC has none)
    void WaitUntilConcurrentGCComplete() override;
    bool IsConcurrentGCInProgress() override;
    void TemporaryEnableConcurrentGC() override;
    void TemporaryDisableConcurrentGC() override;
    bool IsConcurrentGCEnabled() override;
    HRESULT WaitUntilConcurrentGCCompleteAsync(int millisecondsTimeout) override;

    // Finalization - ZeroGC never determines unreachability so nothing is
    // ever queued for finalization (mirrors Epsilon GC semantics).
    size_t GetNumberOfFinalizable() override;
    Object* GetNextFinalizable() override;

    // BCL routines
    void GetMemoryInfo(uint64_t* highMemLoadThresholdBytes,
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
                        int kind) override;
    uint32_t GetMemoryLoad() override;
    int GetGcLatencyMode() override;
    int SetGcLatencyMode(int newLatencyMode) override;
    int GetLOHCompactionMode() override;
    void SetLOHCompactionMode(int newLOHCompactionMode) override;
    bool RegisterForFullGCNotification(uint32_t gen2Percentage, uint32_t lohPercentage) override;
    bool CancelFullGCNotification() override;
    int WaitForFullGCApproach(int millisecondsTimeout) override;
    int WaitForFullGCComplete(int millisecondsTimeout) override;
    unsigned WhichGeneration(Object* obj) override;
    int CollectionCount(int generation, int get_bgc_fgc_coutn = 0) override;
    int StartNoGCRegion(uint64_t totalSize, bool lohSizeKnown, uint64_t lohSize, bool disallowFullBlockingGC) override;
    int EndNoGCRegion() override;
    size_t GetTotalBytesInUse() override;
    uint64_t GetTotalAllocatedBytes() override;
    HRESULT GarbageCollect(int generation = -1, bool low_memory_p = false, int mode = collection_blocking) override;
    unsigned GetMaxGeneration() override;
    void SetFinalizationRun(Object* obj) override;
    bool RegisterForFinalization(int gen, Object* obj) override;
    int GetLastGCPercentTimeInGC() override;
    size_t GetLastGCGenerationSize(int gen) override;

    // Misc
    HRESULT Initialize() override;
    bool IsPromoted(Object* object) override;
    bool IsHeapPointer(void* object, bool small_heap_only = false) override;
    unsigned GetCondemnedGeneration() override;
    bool IsGCInProgressHelper(bool bConsiderGCStart = false) override;
    unsigned GetGcCount() override;
    bool IsThreadUsingAllocationContextHeap(gc_alloc_context* acontext, int thread_number) override;
    bool IsEphemeral(Object* object) override;
    uint32_t WaitUntilGCComplete(bool bConsiderGCStart = false) override;
    void FixAllocContext(gc_alloc_context* acontext, void* arg, void* heap) override;
    size_t GetCurrentObjSize() override;
    void SetGCInProgress(bool fInProgress) override;
    bool RuntimeStructuresValid() override;
    void SetSuspensionPending(bool fSuspensionPending) override;
    void SetYieldProcessorScalingFactor(float yieldProcessorScalingFactor) override;
    void Shutdown() override;

    size_t GetLastGCStartTime(int generation) override;
    size_t GetLastGCDuration(int generation) override;
    size_t GetNow() override;

    // Allocation
    Object* Alloc(gc_alloc_context* acontext, size_t size, uint32_t flags) override;
    void PublishObject(uint8_t* obj) override;
    void SetWaitForGCEvent() override;
    void ResetWaitForGCEvent() override;

    // Heap verification
    bool IsLargeObject(Object* pObj) override;
    void ValidateObjectMember(Object* obj) override;
    Object* NextObj(Object* object) override;
    Object* GetContainingObject(void* pInteriorPtr, bool fCollectedGenOnly) override;

    // Diagnostics - no-ops, ZeroGC does not support heap walking/profiling APIs.
    void DiagWalkObject(Object* obj, walk_fn fn, void* context) override;
    void DiagWalkObject2(Object* obj, walk_fn2 fn, void* context) override;
    void DiagWalkHeap(walk_fn fn, void* context, int gen_number, bool walk_large_object_heap_p) override;
    void DiagWalkSurvivorsWithType(void* gc_context, record_surv_fn fn, void* diag_context, walk_surv_type type, int gen_number = -1) override;
    void DiagWalkFinalizeQueue(void* gc_context, fq_walk_fn fn) override;
    void DiagScanFinalizeQueue(fq_scan_fn fn, ScanContext* context) override;
    void DiagScanHandles(handle_scan_fn fn, int gen_number, ScanContext* context) override;
    void DiagScanDependentHandles(handle_scan_fn fn, int gen_number, ScanContext* context) override;
    void DiagDescrGenerations(gen_walk_fn fn, void* context) override;
    void DiagTraceGCSegments() override;
    void DiagGetGCSettings(EtwGCSettingsInfo* settings) override;

    bool StressHeap(gc_alloc_context* acontext) override;

    segment_handle RegisterFrozenSegment(segment_info* pseginfo) override;
    void UnregisterFrozenSegment(segment_handle seg) override;
    bool IsInFrozenSegment(Object* object) override;

    void ControlEvents(GCEventKeyword keyword, GCEventLevel level) override;
    void ControlPrivateEvents(GCEventKeyword keyword, GCEventLevel level) override;

    unsigned int GetGenerationWithRange(Object* object, uint8_t** ppStart, uint8_t** ppAllocated, uint8_t** ppReserved) override;

    int64_t GetTotalPauseDuration() override;
    void EnumerateConfigurationValues(void* context, ConfigurationValueFunc configurationValueFunc) override;
    void UpdateFrozenSegment(segment_handle seg, uint8_t* allocated, uint8_t* committed) override;
    int RefreshMemoryLimit() override;
    enable_no_gc_region_callback_status EnableNoGCRegionCallback(NoGCRegionCallbackFinalizerWorkItem* callback, uint64_t callback_threshold) override;
    FinalizerWorkItem* GetExtraWorkForFinalization() override;
    uint64_t GetGenerationBudget(int generation) override;
    size_t GetLOHThreshold() override;
    void DiagWalkHeapWithACHandling(walk_fn fn, void* context, int gen_number, bool walk_large_object_heap_p) override;
    void NullBridgeObjectsWeakRefs(size_t length, void* unreachableObjectHandles) override;

private:
    ZeroGCHeap() = default;

    // Refills a thread's allocation context with a fresh chunk cut from a
    // per-thread arena slice, then satisfies the current allocation from it.
    Object* AllocateFromArena(gc_alloc_context* acontext, size_t size, uint32_t flags);

    // Atomically claims (via a single lock-free interlocked add - no
    // critical section) a private, contiguous slice of at least `size`
    // bytes from the shared master reservation for the calling thread's
    // exclusive use. See the per-thread-arena design notes above
    // AllocateFromArena's definition in ZeroGCHeap.cpp. Returns nullptr if
    // the reservation is exhausted.
    uint8_t* ClaimArenaSlice(size_t size);

    // m_arenaNextFree is advanced via a racy InterlockedExchangeAdd64 and so
    // can transiently overshoot m_arenaReservedEnd by a few claims' worth
    // right at the point the arena is exhausted; clamp it for any
    // informational/reporting read (this is never on the hot alloc path).
    uint8_t* ArenaHighWaterMark() const { return min(m_arenaNextFree, m_arenaReservedEnd); }

    uint8_t* m_arenaBase = nullptr;
    // Shared "claimed so far" watermark: the only field the hot allocation
    // path touches, and only via InterlockedExchangeAdd64 (ClaimArenaSlice),
    // never a lock. Everywhere else it is read as a plain (racy but
    // never-torn on x64) informational high-water mark, same as before.
    uint8_t* m_arenaNextFree = nullptr;
    uint8_t* m_arenaReservedEnd = nullptr; // end of reserved address space
    LARGE_INTEGER m_qpcFrequency{};
    LARGE_INTEGER m_startTime{};
};

extern ZeroGCHeap* g_zeroGCHeap;

// ---------------------------------------------------------------------------
// A trivial handle table: a growable array of Object* slots plus a free list.
// ZeroGC never collects, so:
//   * strong/pinned/weak handles never need to be "scanned" (nothing moves,
//     nothing dies), they just remember whatever pointer was stored in them.
//   * dependent handles behave the same way (secondary is never cleared).
// This mirrors the same simplification UpsilonGC's Zero GC used.
// ---------------------------------------------------------------------------
class ZeroGCHandleStore : public IGCHandleStore
{
public:
    ZeroGCHandleStore();
    ~ZeroGCHandleStore() override;

    void Uproot() override;
    bool ContainsHandle(OBJECTHANDLE handle) override;
    OBJECTHANDLE CreateHandleOfType(Object* object, HandleType type) override;
    OBJECTHANDLE CreateHandleOfType(Object* object, HandleType type, int heapToAffinitizeTo) override;
    OBJECTHANDLE CreateHandleWithExtraInfo(Object* object, HandleType type, void* pExtraInfo) override;
    OBJECTHANDLE CreateDependentHandle(Object* primary, Object* secondary) override;

    struct Slot
    {
        Object* Value;
        Object* Secondary;    // used for dependent handles
        void* ExtraInfo;
        HandleType Type;
        Slot* NextFree;
        bool InUse;
    };

    OBJECTHANDLE AllocSlot(Object* value, HandleType type);
    void FreeSlot(OBJECTHANDLE handle);
    static Slot* SlotFromHandle(OBJECTHANDLE handle) { return reinterpret_cast<Slot*>(handle); }

private:
    // Per-thread free list (plain, non-atomic pointer - no CAS, no lock).
    // A single global CRITICAL_SECTION here would serialize every
    // GCHandle Alloc/Free (and anything that goes through one under the
    // hood - WeakReference, interop pinning, ConditionalWeakTable, etc.)
    // across ALL threads process-wide - exactly the same contention shape
    // the original arena bump allocator had before it was switched to
    // per-thread arenas.
    //
    // An earlier version of this fix used a single shared lock-free
    // Treiber stack (CAS push/pop) instead. That has a genuine ABA
    // correctness bug, not just a benign "leak an orphaned slot" edge
    // case as first assumed: thread T1 can read (head=X, next=Y), stall,
    // and have some other thread pop-then-repush X in the meantime such
    // that when T1 resumes its CAS(&head, X, Y) still succeeds - but by
    // then Y may have *itself* already been legitimately popped and be in
    // active use by a third thread T2. T1's stale CAS makes Y the new
    // head anyway, so a later AllocSlot on a fourth thread T3 can pop Y
    // and hand out the *same* Slot that T2 is still actively using -
    // real, silent memory corruption (observed in practice: 3+ concurrent
    // threads doing sustained Alloc/Free churn reliably corrupted an
    // unrelated ConditionalWeakTable-backed dependent handle used by
    // System.Text.Json's reflection metadata cache).
    //
    // The fix here sidesteps the ABA hazard entirely rather than papering
    // over it with a 128-bit tagged/versioned CAS: each OS thread only
    // ever pushes to and pops from *its own* free list, so no two threads
    // ever contend on the same list and no cross-thread pointer race is
    // possible. A slot freed on a different thread than it was allocated
    // on simply gets added to the *freeing* thread's own list instead of
    // the allocating thread's - still safe, still recycled, just not
    // necessarily by the original owner. Brand-new slots (thread-local
    // free list empty) are allocated via plain `new`, relying on the CRT
    // heap's own (much finer-grained) internal synchronization instead of
    // a coarse lock of our own.
    static thread_local Slot* t_freeList;
};

class ZeroGCHandleManager : public IGCHandleManager
{
public:
    bool Initialize() override;
    void Shutdown() override;
    IGCHandleStore* GetGlobalHandleStore() override;
    IGCHandleStore* CreateHandleStore() override;
    void DestroyHandleStore(IGCHandleStore* store) override;
    OBJECTHANDLE CreateGlobalHandleOfType(Object* object, HandleType type) override;
    OBJECTHANDLE CreateDuplicateHandle(OBJECTHANDLE handle) override;
    void DestroyHandleOfType(OBJECTHANDLE handle, HandleType type) override;
    void DestroyHandleOfUnknownType(OBJECTHANDLE handle) override;
    void SetExtraInfoForHandle(OBJECTHANDLE handle, HandleType type, void* pExtraInfo) override;
    void* GetExtraInfoFromHandle(OBJECTHANDLE handle) override;
    void StoreObjectInHandle(OBJECTHANDLE handle, Object* object) override;
    bool StoreObjectInHandleIfNull(OBJECTHANDLE handle, Object* object) override;
    void SetDependentHandleSecondary(OBJECTHANDLE handle, Object* object) override;
    Object* GetDependentHandleSecondary(OBJECTHANDLE handle) override;
    Object* InterlockedCompareExchangeObjectInHandle(OBJECTHANDLE handle, Object* object, Object* comparandObject) override;
    HandleType HandleFetchType(OBJECTHANDLE handle) override;
    void TraceRefCountedHandles(HANDLESCANPROC callback, uintptr_t param1, uintptr_t param2) override;

private:
    ZeroGCHandleStore* m_globalStore = nullptr;
};

#endif // __ZEROGC_H__
