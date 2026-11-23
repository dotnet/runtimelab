// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef LAXORI_GC_H
#define LAXORI_GC_H

#include "gc.h"
#include "laxori_reference_counting.h"
#include "laxori_finalizer_pause.h"
#include "laxori_collection_policy.h"
#include "laxori_heap_sizing.h"

class LaxoriGcHeap final : public IGCHeapInternal
{
public:
    LaxoriGcHeap();

    HRESULT Initialize() override;
    Object* Alloc(gc_alloc_context* context, size_t size, uint32_t flags) override;
    void FixAllocContext(gc_alloc_context* context, void* arg, void* heap) override;
    void PublishObject(uint8_t* object) override;
    HRESULT GarbageCollect(int generation, bool lowMemory, int mode) override;
    void Shutdown() override;

    int GetNumberOfHeaps() override;
    int GetHomeHeapNumber() override;
    size_t GetPromotedBytes(int heap) override;
    bool IsPromoted2(Object* object, bool verifyHeader) override;
    bool IsPromoted(Object* object) override;
    bool IsHeapPointer(void* object, bool smallOnly) override;
    bool IsEphemeral(Object* object) override;
    unsigned WhichGeneration(Object* object) override;
    unsigned GetCondemnedGeneration() override;
    bool IsThreadUsingAllocationContextHeap(gc_alloc_context* context, int thread) override;
    Object* GetContainingObject(void* interior, bool collectedOnly) override;
    Object* NextObj(Object* object) override;
    bool IsLargeObject(Object* object) override;
    void ValidateObjectMember(Object* object) override;

    bool IsGCInProgressHelper(bool considerStart) override;
    void SetGCInProgress(bool value) override;
    void SetSuspensionPending(bool value) override;
    bool RuntimeStructuresValid() override;
    void SetYieldProcessorScalingFactor(float value) override;
    uint32_t WaitUntilGCComplete(bool considerStart) override;
    void SetWaitForGCEvent() override;
    void ResetWaitForGCEvent() override;
    bool IsConcurrentGCInProgress() override;
    bool IsConcurrentGCEnabled() override;
    void TemporaryEnableConcurrentGC() override;
    void TemporaryDisableConcurrentGC() override;
    void WaitUntilConcurrentGCComplete() override;
    HRESULT WaitUntilConcurrentGCCompleteAsync(int timeout) override;

    size_t GetNumberOfFinalizable() override;
    Object* GetNextFinalizable() override;
    bool RegisterForFinalization(int generation, Object* object) override;
    void SetFinalizationRun(Object* object) override;
    FinalizerWorkItem* GetExtraWorkForFinalization() override;

    size_t GetValidSegmentSize(bool large) override;
    void SetReservedVMLimit(size_t limit) override;
    size_t GetTotalBytesInUse() override;
    uint64_t GetTotalAllocatedBytes() override;
    size_t GetCurrentObjSize() override;
    int CollectionCount(int generation, int kind) override;
    unsigned GetGcCount() override;
    uint32_t GetMemoryLoad() override;
    int GetGcLatencyMode() override;
    int SetGcLatencyMode(int mode) override;
    int GetLOHCompactionMode() override;
    void SetLOHCompactionMode(int mode) override;
    int GetLastGCPercentTimeInGC() override;
    size_t GetLastGCGenerationSize(int generation) override;
    size_t GetLastGCStartTime(int generation) override;
    size_t GetLastGCDuration(int generation) override;
    size_t GetNow() override;
    int64_t GetTotalPauseDuration() override;
    uint64_t GetGenerationBudget(int generation) override;
    size_t GetLOHThreshold() override;
    void EnumerateConfigurationValues(void* context, ConfigurationValueFunc callback) override;
    int RefreshMemoryLimit() override;
    void GetMemoryInfo(
        uint64_t* highThreshold, uint64_t* available, uint64_t* load, uint64_t* heap,
        uint64_t* fragmentation, uint64_t* committed, uint64_t* promoted, uint64_t* pinned,
        uint64_t* pending, uint64_t* index, uint32_t* generation, uint32_t* pausePercent,
        bool* compacting, bool* concurrent, uint64_t* generations, uint64_t* pauses, int kind) override;

    bool RegisterForFullGCNotification(uint32_t generation, uint32_t large) override;
    bool CancelFullGCNotification() override;
    int WaitForFullGCApproach(int timeout) override;
    int WaitForFullGCComplete(int timeout) override;
    int StartNoGCRegion(uint64_t total, bool hasLarge, uint64_t large, bool disallow) override;
    int EndNoGCRegion() override;
    enable_no_gc_region_callback_status EnableNoGCRegionCallback(
        NoGCRegionCallbackFinalizerWorkItem* callback, uint64_t threshold) override;
    bool StressHeap(gc_alloc_context* context) override;

    segment_handle RegisterFrozenSegment(segment_info* info) override;
    void UnregisterFrozenSegment(segment_handle segment) override;
    bool IsInFrozenSegment(Object* object) override;
    void UpdateFrozenSegment(segment_handle segment, uint8_t* allocated, uint8_t* committed) override;

    void DiagWalkObject(Object* object, walk_fn callback, void* context) override;
    void DiagWalkObject2(Object* object, walk_fn2 callback, void* context) override;
    void DiagWalkHeap(walk_fn callback, void* context, int generation, bool large) override;
    void DiagWalkHeapWithACHandling(walk_fn callback, void* context, int generation, bool large) override;
    void DiagWalkSurvivorsWithType(
        void* gcContext, record_surv_fn callback, void* context, walk_surv_type type, int generation) override;
    void DiagWalkFinalizeQueue(void* gcContext, fq_walk_fn callback) override;
    void DiagScanFinalizeQueue(fq_scan_fn callback, ScanContext* context) override;
    void DiagScanHandles(handle_scan_fn callback, int generation, ScanContext* context) override;
    void DiagScanDependentHandles(handle_scan_fn callback, int generation, ScanContext* context) override;
    void DiagDescrGenerations(gen_walk_fn callback, void* context) override;
    void DiagTraceGCSegments() override;
    void DiagGetGCSettings(EtwGCSettingsInfo* settings) override;
    unsigned GetGenerationWithRange(Object* object, uint8_t** start, uint8_t** allocated, uint8_t** reserved) override;
    void ControlEvents(GCEventKeyword keyword, GCEventLevel level) override;
    void ControlPrivateEvents(GCEventKeyword keyword, GCEventLevel level) override;
    void NullBridgeObjectsWeakRefs(size_t count, void* handles) override;
    void GetFastGCFunctions(FastGCFunctions* functions) override;
    GCHeapType GetGCHeapType() override;
    uint64_t GetTotalAllocatedBytesPrecise() override;
    void GetAssignRefFunctions(void** assignRef, size_t* assignRefSize, void** assignRefChecked, size_t* assignRefCheckedSize, uintptr_t** avLocationsList, GCWriteBarrierPatchableValue** patchableValues) override;

    // Called by the assembly write barrier (through LaxoriBarrierSlowPath) when the unlog bit of a field is set.
    static void BarrierSlowPath(Object** slot);

    // Laxori: the private tier.
    // Called by the assembly write barrier when a reference to an object of a private region of the current thread is stored
    // into a place that is not private (or into an exposed slot of the region).
    static void EscapeSlowPath(Object** slot, Object* reference);
    // Escapes the object (and what it reaches in the region) when it is in the private region of the current thread.
    static void EscapeObject(Object* object);
    bool PrivateEnabled() const { return m_private_enabled; }

    ObjectReferenceEnumerationParameters* GetObjectReferenceEnumerationParameters() override;
    ObjectHeaderBitsParameters* GetObjectHeaderBitsParameters() override;

    LaxoriRcResult CollectPrototype(LaxoriPauseKind kind, bool waitForLazy = false);
    LaxoriRcResult HoldPrototypeTrace(bool hold);
    LaxoriRcResult ArmPrototypeScan(OBJECTHANDLE handle);
    LaxoriRcResult ArmPrototypeLifetime(OBJECTHANDLE handle);
    LaxoriRcResult EnablePrototypeStorageDiagnostics();
    LaxoriRcResult GetPrototypeStorageRange(uint32_t component, uintptr_t* start, size_t* bytes) const;
    uint64_t GetPrototypeMetric(uint32_t metric) const;
    bool IsOwnedHandle(OBJECTHANDLE handle) const;
    uintptr_t GetOwnedStart() const;
    size_t GetOwnedBytes() const;
    uintptr_t GetHandleAddress(OBJECTHANDLE handle) const;
    size_t GetAddressExtent(uintptr_t address) const;
    segment_handle RegisterPrototypeFrozenArray(
        void* memory, size_t capacity, OBJECTHANDLE array, OBJECTHANDLE target);

private:
    static constexpr size_t ImmixBytes = 8 * 1024 * 1024;
    static constexpr size_t LargeBytes = 16 * 1024 * 1024;
    static constexpr size_t MaximumMutators = 1024;
    static constexpr size_t MaximumRoots = 131072;
    // How many emergency collections an allocation that finds no memory may run before it fails.
    static constexpr int MaximumEmergencyCollections = 3;
    static constexpr size_t MaximumFrozenSegments = 256;
    static constexpr uint32_t Timeout = 30000;
    // P29. How long the pause thread and the scan executor spin for each other before they sleep, in microseconds.
    static constexpr uint64_t ScanSpinMicroseconds = 300;

    struct FrozenSegment
    {
        std::atomic<uint8_t> state{0};
        uintptr_t start = 0;
        uintptr_t reserved = 0;
        std::atomic<uintptr_t> allocated{0};
        std::atomic<uintptr_t> committed{0};
    };

    struct ScanCostScope;
    struct WalkContext;

    static LaxoriGcHeap* s_heap;
    static void Suspend(void* context);
    static void Resume(bool finished, void* context);
    static void RetireAllocationContext(gc_alloc_context* context, void* heap);
    static bool CanAdmitRange(size_t firstSlot, size_t slotCount, size_t nativeBytes, void* heap);
#ifdef TARGET_WINDOWS
    static bool GetJobCommittedBytes(uint64_t* committed);
    void RecordCommitRefusal(uint64_t addedBytes, uint64_t committedBytes, bool jobLimit);
#endif // TARGET_WINDOWS
    static bool AdmitHeapTables(size_t firstSlot, size_t slotCount, bool initialize, void* heap);
    bool CommitHeapTables(size_t firstSlot, size_t slotCount);
    static LaxoriRcResult CaptureRoots(LaxoriReferenceCounting* engine, void* context);
    static LaxoriRcResult FinishTrace(LaxoriReferenceCounting* engine, void* context);
    static LaxoriRcResult FinishEpoch(LaxoriReferenceCounting* engine, void* context);
    static LaxoriRcResult FinishRelocation(LaxoriReferenceCounting* engine, void* context);
    static LaxoriRcResult FinishLazy(LaxoriReferenceCounting* engine, void* context);
    static void ScanOnGcThread(void* context);
    static void CoordinateTrace(void* context);
    static void CopyRoot(Object** slot, ScanContext* context, uint32_t flags);
    static void TraceRoot(Object** slot, ScanContext* context, uint32_t flags);
    static void RelocateRoot(Object** slot, ScanContext* context, uint32_t flags);
    static void LeaseWeakHandle(Object** slot, Object* secondary, uint32_t flags, ScanContext* context, bool dependent);
    static void LeaseDependentRoot(Object** slot, ScanContext* context, uint32_t flags);
    static void CALLBACK LeaseSyncBlock(
        PTR_UNCHECKED_OBJECTREF slot, uintptr_t* extra, uintptr_t scan, uintptr_t heap);
    static bool IsFinalizationSuppressed(Object* object);
    static bool WalkHeapObject(Object* object, size_t size, void* context);
    static bool IsImmortal(Object* object, void* context);

    // Shared hooks and the assembly slow path. Bulk callbacks log before copying/clearing every chunk byte.
    static void LogOldValue(Object** slot);
    static void LogOldValues(Object** destination, size_t count);
    static void PreStore(Object** destination, Object* newReference);
    static bool IsInHeap(void* context, void* address);
    static void NoPostStoreBarrier(void* context, Object** destination, Object* newReference);
    void LogOldReferences(void* destination, size_t length);
    static void BulkMoveWithWriteBarrier(void* context, void* destination, const void* source, size_t length);
    static void BulkClearWithWriteBarrier(void* context, void* destination, size_t length);

    void Fatal(const char* message) const;
    void Unsupported(const char* operation) const;
    // ---- Laxori: private tier (laxori_private.cpp) ----
    struct LocalGcContext;
    bool InitializePrivateTier(size_t regionKB, bool enabled, bool collectEscaped, size_t escapedCollectDiv);
    Object* AllocPrivateSlow(gc_alloc_context* context, LaxoriRcMutator* mutator, size_t size);
    Object* AllocShared(gc_alloc_context* context, size_t size, uint32_t flags);
    void OpenGap(gc_alloc_context* context, LaxoriRcMutator* mutator, uint32_t gapIndex);
    void SetBufferLimits(gc_alloc_context* context, uintptr_t cursor, uintptr_t limit);
    void DissolveCurrentRegion(gc_alloc_context* context, LaxoriRcMutator* mutator, size_t reason);
    bool BitTest(uintptr_t address) const;
    void BitSet(uintptr_t address);
    void BitClear(uintptr_t address);
    bool AnyExposed(uintptr_t first, size_t length) const;
    Object* NextMarked(uintptr_t from, uintptr_t limit) const;
    bool InRegion(const LaxoriPrivateRegion& region, uintptr_t pointer) const;
    bool InRegionInterior(const LaxoriPrivateRegion& region, uintptr_t pointer) const;
    Object* FindObject(const LaxoriPrivateRegion& region, uintptr_t location, uintptr_t liveCursor);
    void PushMarkStack(LaxoriPrivateRegion& region, Object* object);
    Object* PopMarkStack(LaxoriPrivateRegion& region);
    void EscapeRecursively(LaxoriPrivateRegion& region, Object* object);
    void EscapeInCurrentRegion(Object** destination, Object* reference);
    void EscapeCheck(Object** destination, Object* reference);
    void EscapeBulk(void* destination, const void* source, size_t length);
    bool LocalCollect(LaxoriRcMutator* mutator);
    bool LocalCollectCore(LaxoriRcMutator* mutator);
    bool m_tl_timers = false;                // DOTNET_LAXORITlTimers=1: cycle and count diagnostics of the thread-local collector
    bool m_sync_walk = false;                // DOTNET_LAXORISyncWalk=1: the thread-local mark also walks the region for sync blocks (the old way)
    uint64_t m_tsc_start = 0;
    int64_t m_tsc_ns_start = 0;
public:
    uint64_t TscKHz() const;
private:
    void VerifyRegion(LaxoriPrivateRegion& region, const char* where);
    bool m_verify_private = false;
    // Test-only control (DOTNET_LAXORITestControl): 1 skips the update of the stack roots, 2 skips the update of the references
    // of the objects, 3 lets an escape skip the children of the object, 4 skips the clearing of a gap. The managed tests must fail.
    int m_test_control = 0;
    bool LocalMark(LaxoriPrivateRegion& region);
    bool LocalPlan(LaxoriPrivateRegion& region, size_t* freeBytes);
    void LocalUpdate(LaxoriPrivateRegion& region);
    void LocalCompact(LaxoriPrivateRegion& region);
    void ClearPads(LaxoriPrivateRegion& region);
    static void MarkFn(Object** slot, ScanContext* scan, uint32_t flags);
    static void UpdateFn(Object** slot, ScanContext* scan, uint32_t flags);
    bool m_private_enabled = false;
    bool m_collect_escaped = false;
    size_t m_escaped_collect_limit = 0;      // a region is collected when its escaped bytes are at most this (0: only without escapes)
    uintptr_t m_arena = 0;
    size_t m_arena_bytes = 0;
    size_t m_region_bytes = 0;
    size_t m_region_blocks = 0;
    size_t m_region_shift = 0;
    size_t m_max_private_object = 0;
    uint64_t* m_region_owner = nullptr;
    LaxoriVirtualArray<uint64_t> m_region_owner_storage;
    LaxoriVirtualArray<uint64_t> m_private_bits_storage;
    LaxoriVirtualArray<uint32_t> m_private_index_storage;
    // Committed Immix high-water mark, read by the copied barrier through a fixed pointer.
    uint64_t m_private_heap_bytes = 0;
    uint64_t* m_private_bits = nullptr;
    uintptr_t m_private_bits_base = 0;
    uint32_t* m_private_index = nullptr;
    LaxoriRcMutator* FindOrAttach(gc_alloc_context* context, bool barrier);
    LaxoriRcResult RunScan(uint8_t kind);
    bool WaitForScanComplete();
    void WaitForScanRequest(bool spin);
    // Wakes the scan executor at the start of a pause, so that it spins when the first scan is requested.
    void PrepareScanExecutor();
    void ExecuteScan(uint8_t kind);
    void AddRoot(Object* object, uint32_t flags, bool trace);
    // P17 S6: the finalizer registry takes part in a pause at three places. CaptureFinalizers runs in the root
    // capture of every pause. DecideFinalization runs after marking in a tracing pause only. RelocateFinalizers
    // follows the relocation of roots and handles when objects were copied.
    void CaptureFinalizers();
    void DecideFinalization();
    void RelocateFinalizers();
    void CountScanWork(size_t metric);
    bool FrozenContains(uintptr_t address, bool retired) const;
    void ScanFrozenRoots();
    LaxoriRcResult CollectLocked(LaxoriPauseKind kind, bool forcedBlocking = false, bool waitForLazy = false);
    bool EnterCollectionLock();
    void LeaveCollectionLock(bool cooperative);
    void ValidateWalk(const char* operation) const;
    void PublishMemoryInfo();
    void RetireAllocationSpan(gc_alloc_context* context, bool subtractUnused);
    void RegisterFinalizable(Object* object, uint32_t flags);
    LaxoriRcResult TryGrow(bool large, size_t size);
    void ReportOutOfMemory(size_t size, LaxoriRcResult growth);
    size_t PolicyReserveBytes() const;
    LaxoriRcResult CollectForPressure(bool emergency, bool user, bool heapFull = false);
    LaxoriRcResult UpdatePolicyHints();
    LaxoriCollectionCause GetCollectionCause(bool heapFull) const;
    size_t GetCollectionReservedPages(size_t headroomBytes) const;

    enum MemoryField : size_t
    {
        Available, HighThreshold, Load, HeapSize, Fragmentation, Committed, Promoted,
        Pins, Finalizers, CollectionIndex, Generation, PausePercent, Concurrent, Compacted,
        PauseTicks, SecondPauseTicks, HeapBefore, FragmentationBefore, MemoryFields,
    };

    LaxoriReferenceCounting m_engine;
    LaxoriCollectionPolicy m_policy;
    LaxoriPolicyConfiguration m_policy_configuration{};
    LaxoriRcConfiguration m_heap_configuration{};
    bool m_fast_enabled = true;
    // P26 (F8). Wait for the lazy decrements and the lazy sweep after every pause (the behaviour before F8).
    bool m_wait_for_lazy = false;
    size_t m_native_plane_limit = 0;
    size_t m_hard_limit = 0;
    bool m_immix_reserve_defaulted = false;
    bool m_large_reserve_defaulted = false;
    bool m_hard_limit_explicit = false;
    bool m_native_limit_defaulted = false;
    std::atomic<bool> m_out_of_memory_reported{false};
    std::atomic<size_t> m_native_refused_bytes{0};
    // Compare the per-process limit with process commit, and the job limit with total job commit.
    uint64_t m_process_commit_limit = 0;
    uint64_t m_process_commit_headroom = 0;
    uint64_t m_job_commit_limit = 0;
    uint64_t m_job_commit_headroom = 0;
    std::atomic<size_t> m_commit_refused_bytes{0};
    std::atomic<size_t> m_commit_refused_committed{0};
    std::atomic<bool> m_commit_refused_job{false};
    std::atomic<LaxoriCollectionCause> m_collection_cause{LaxoriCollectionCause::None};
    // Heap sizing (laxori_heap_sizing.h). The sizer is used with the collection lock held. The live estimate is written by
    // FinishLazy, on a worker thread.
    LaxoriHeapSizer m_sizer;
    LaxoriHeapSizingConfiguration m_sizing_configuration{};
    std::atomic<size_t> m_mature_live_bytes{0};
    std::atomic<uint64_t> m_sizing_growths{0};
    std::atomic<uint64_t> m_cause_counts[8]{};
    bool m_policy_trace = false;
    // Prints the interior-lookup counters of the engine at the end of every pause (LAXORILookupStats).
    bool m_lookup_stats = false;
    // Prints the work of the lazy decrements in the first epoch of a trace and in later epochs when a trace ends
    // (LAXORIWindowStats). Only a library built with phase timers or diagnostic probes counts it.
    bool m_window_stats = false;
    ObjectReferenceEnumerationParameters m_enumeration;
    ObjectHeaderBitsParameters m_header_bits{};
    FrozenSegment m_frozen[MaximumFrozenSegments];
    std::atomic<uint32_t> m_frozen_readers;
    LaxoriVirtualArray<uint8_t> m_card_storage;
    LaxoriVirtualArray<uint8_t> m_bundle_storage;
    uint8_t* m_cards;
    uint8_t* m_bundles;
    CLRCriticalSection m_collection_lock;
    GCEvent m_gc_done;
    GCEvent m_trace_signal;
    GCEvent m_coordinator_done;
    GCEvent m_concurrent_done;
    GCEvent m_scan_signal;
    GCEvent m_scan_ready;
    GCEvent m_scan_done;
    GCEvent m_scan_exited;
    std::atomic<uint8_t> m_scan_kind{0};
    std::atomic<bool> m_scan_busy{false};
    std::atomic<bool> m_scan_stopping{false};
    // Hand-over between the pause thread and the scan executor. Both threads spin for a short time before they sleep on
    // the events, so that a request and its answer do not wait for the wake-up of a sleeping thread (LAXORI_SCAN_SPIN_US).
    std::atomic<uint32_t> m_scan_request{0};
    std::atomic<uint32_t> m_scan_complete{0};
    std::atomic<bool> m_scan_executor_sleeping{false};
    std::atomic<bool> m_scan_requester_sleeping{false};
    int64_t m_scan_spin_ticks = 0;
    std::atomic<bool> m_initialized;
    std::atomic<bool> m_in_gc;
    std::atomic<bool> m_shutdown;
    std::atomic<bool> m_concurrent_enabled;
    std::atomic<unsigned> m_collections;
    LaxoriFinalizerRegistry m_registry;
    LaxoriFinalizerPauseCounters m_registry_counters;
    // Set by Resume when it did not signal the finalizer thread because a cycle sweep was pending (see
    // GetNextFinalizable). FinishLazy sends the signal once the sweep has finished.
    std::atomic<bool> m_finalization_deferred{false};
    std::atomic<uint64_t> m_memory_info[4][MemoryFields];
    std::atomic<uint64_t> m_memory_sequence[4];
    LaxoriPauseKind m_current_pause_kind;
    bool m_forced_blocking = false;
    uint64_t m_heap_before;
    uint64_t m_fragmentation_before;
    uint64_t m_trace_heap_before;
    uint64_t m_trace_fragmentation_before;
    uint64_t m_initial_pause_ticks;
    std::atomic<LaxoriRcResult> m_scan_failure;
    std::atomic<int> m_latency;
    std::atomic<int> m_loh_mode;
    std::atomic<int64_t> m_total_pause_ticks;
    std::atomic<int64_t> m_last_pause_ticks;
    int64_t m_pause_start;
    uint64_t m_start_time;
    std::atomic<size_t> m_last_gc_start;
    std::atomic<size_t> m_last_gc_duration;
    std::atomic<size_t> m_last_heap_size;
    std::atomic<size_t> m_last_committed;
    std::atomic<size_t> m_last_fragmentation;
    std::atomic<uint32_t> m_last_memory_load;
    size_t m_reserved_limit;
    std::atomic<uint64_t> m_barrier_bootstraps;
    std::atomic<uint64_t> m_fast_refills{0};
    std::atomic<uint64_t> m_unused_span_bytes{0};
    std::atomic<uint64_t> m_context_allocated_bytes{0};
    std::atomic<uint64_t> m_pause_young_bytes{0};
    std::atomic<uint64_t> m_pause_reserve_pages{0};
    std::atomic<uint64_t> m_pause_free_pages{0};
    std::atomic<bool> m_storage_diagnostics_enabled{false};
    std::atomic<uint64_t> m_bulk_move_calls{0};
    std::atomic<uint64_t> m_bulk_clear_calls{0};
    std::atomic<uint64_t> m_bulk_layout_lookups{0};
    std::atomic<uint64_t> m_bulk_metadata_early_outs{0};
    std::atomic<uint64_t> m_root_capture_ticks{0};
    static constexpr size_t ScanMetricCount = 44;
    std::atomic<uint64_t> m_scan_metrics[ScanMetricCount]{};
    std::atomic<uint64_t> m_scan_cpu_nanoseconds[ScanMetricCount]{};
#ifdef TARGET_WINDOWS
    BOOLEAN (WINAPI *m_process_exiting)();
#endif // TARGET_WINDOWS
};

// The inline allocation helpers of the runtime test the limit that the runtime keeps in ee_alloc_context::m_CombinedLimit,
// the pointer-sized field just before the gc_alloc_context (vm/gcheaputilities.h). The runtime refreshes it after every call of
// Alloc. The private tier changes the buffer outside Alloc (opening a gap, retiring), so it writes the field too.
inline void SetCombinedLimit(gc_alloc_context* context, uint8_t* limit)
{
    reinterpret_cast<uint8_t**>(context)[-1] = limit;
}

#endif // LAXORI_GC_H
