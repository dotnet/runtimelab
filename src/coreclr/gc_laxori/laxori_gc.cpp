// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"
#include "gcenv.h"
#include "laxori_gc.h"
#include "laxori_default_limits.h"
#include "laxori_write_barrier.h"
#include "laxori_platform.h"
#include "laxori_worker_policy.h"
#include "gcscan.h"
#include "gcref.h"
#include "gceventstatus.h"
#include "objecthandle.h"

#include <algorithm>
#include <new>
#ifdef TARGET_WINDOWS
#include <psapi.h>
#else
#include <time.h>
#include <unistd.h>
#endif // TARGET_WINDOWS

// Entry of the assembly write barrier slow path (defined below).
extern "C" void LaxoriBarrierSlowPath(Object** slot);
extern "C" void LaxoriEscapeSlowPath(Object** slot, Object* reference);
void LaxoriEscapeObject(Object* object);

namespace
{
    bool ReadSetting(
        const char* name, size_t* value, unsigned base = 10, size_t maximum = SIZE_MAX,
        bool* specified = nullptr, bool boolean = false)
    {
        char publicKey[128];
        int length = snprintf(publicKey, sizeof(publicKey), "System.GC.%s",
            strncmp(name, "GCHeap", 6) == 0 ? name + 2 : name);
        if (length < 0 || static_cast<size_t>(length) >= sizeof(publicKey))
        {
            GCToEEInterface::LogErrorToHost("LAXORI configuration key exceeds its bounded storage.");
            return false;
        }
        for (const char* prefix : {"DOTNET_", "COMPlus_"})
        {
            char environmentKey[128];
            length = snprintf(environmentKey, sizeof(environmentKey), "%s%s", prefix, name);
            if (length < 0 || static_cast<size_t>(length) >= sizeof(environmentKey))
            {
                GCToEEInterface::LogErrorToHost("LAXORI environment key exceeds its bounded storage.");
                return false;
            }
#ifdef TARGET_WINDOWS
            SetLastError(ERROR_SUCCESS);
            DWORD environmentLength = GetEnvironmentVariableA(environmentKey, nullptr, 0);
            bool empty = environmentLength == 1 ||
                (environmentLength == 0 && GetLastError() != ERROR_ENVVAR_NOT_FOUND);
#else
            const char* environmentValue = getenv(environmentKey);
            bool empty = environmentValue != nullptr && *environmentValue == '\0';
#endif // TARGET_WINDOWS
            if (empty)
            {
                GCToEEInterface::LogErrorToHost(name);
                GCToEEInterface::LogErrorToHost("LAXORI configuration is present but empty.");
                return false;
            }
        }
        const char* text = nullptr;
        bool privateValue = GCToEEInterface::GetStringConfigValue(name, nullptr, &text);
        if (!privateValue && !GCToEEInterface::GetStringConfigValue(name, publicKey, &text))
        {
            int64_t integer;
            if (!GCToEEInterface::GetIntConfigValue(name, publicKey, &integer))
            {
                return true;
            }
            if (specified != nullptr) { *specified = true; }
            if (integer >= 0 && static_cast<uint64_t>(integer) <= maximum)
            {
                *value = static_cast<size_t>(integer);
                return true;
            }
            GCToEEInterface::LogErrorToHost(name);
            GCToEEInterface::LogErrorToHost("LAXORI configuration value is out of range.");
            return false;
        }
        if (specified != nullptr) { *specified = true; }
        if (!privateValue)
        {
            base = 10;
        }
        if (boolean && (strcmp(text, "true") == 0 || strcmp(text, "false") == 0))
        {
            size_t parsed = strcmp(text, "true") == 0 ? 1 : 0;
            GCToEEInterface::FreeStringConfigValue(text);
            if (parsed > maximum)
            {
                GCToEEInterface::LogErrorToHost(name);
                GCToEEInterface::LogErrorToHost("LAXORI configuration requests an unsupported feature.");
                return false;
            }
            *value = parsed;
            return true;
        }
        const char* cursor = text;
        if (base == 16 && cursor[0] == '0' && (cursor[1] == 'x' || cursor[1] == 'X'))
        {
            cursor += 2;
        }
        size_t parsed = 0;
        bool valid = *cursor != '\0';
        while (valid && *cursor != '\0')
        {
            unsigned digit = *cursor >= '0' && *cursor <= '9' ? *cursor - '0' :
                *cursor >= 'a' && *cursor <= 'f' ? *cursor - 'a' + 10 :
                *cursor >= 'A' && *cursor <= 'F' ? *cursor - 'A' + 10 : base;
            if (digit >= base || digit > maximum || parsed > (maximum - digit) / base)
            {
                valid = false;
                break;
            }
            parsed = parsed * base + digit;
            cursor++;
        }
        GCToEEInterface::FreeStringConfigValue(text);
        if (!valid)
        {
            GCToEEInterface::LogErrorToHost(name);
            GCToEEInterface::LogErrorToHost("LAXORI configuration must be an unsigned integer in the documented range.");
            return false;
        }
        *value = parsed;
        return true;
    }

    bool ReadBooleanSetting(const char* name, bool* value)
    {
        size_t parsed = *value ? 1 : 0;
        if (!ReadSetting(name, &parsed, 10, 1, nullptr, true))
        {
            return false;
        }
        *value = parsed != 0;
        return true;
    }
}

LaxoriGcHeap* LaxoriGcHeap::s_heap = nullptr;

struct LaxoriGcHeap::ScanCostScope
{
#ifdef LAXORI_HAS_DIAGNOSTIC_PROBES
    LaxoriGcHeap* heap;
    size_t metric;
    int64_t start = 0;
    uint64_t cpu = 0;

    static uint64_t CpuNanoseconds(LaxoriGcHeap* heap)
    {
#ifdef TARGET_WINDOWS
        FILETIME created, exited, kernel, user;
        if (!GetThreadTimes(GetCurrentThread(), &created, &exited, &kernel, &user))
        {
            heap->Fatal("LAXORI scan diagnostics could not read current-thread CPU time.");
        }
        return ((static_cast<uint64_t>(kernel.dwHighDateTime) << 32 | kernel.dwLowDateTime) +
            (static_cast<uint64_t>(user.dwHighDateTime) << 32 | user.dwLowDateTime)) * 100;
#else
        timespec time{};
        if (clock_gettime(CLOCK_THREAD_CPUTIME_ID, &time) != 0)
        {
            heap->Fatal("LAXORI scan diagnostics could not read current-thread CPU time.");
        }
        return static_cast<uint64_t>(time.tv_sec) * 1000000000 + time.tv_nsec;
#endif // TARGET_WINDOWS
    }

    ScanCostScope(LaxoriGcHeap* owner, size_t index) : heap(owner), metric(index)
    {
        if (heap->m_storage_diagnostics_enabled.load(std::memory_order_relaxed))
        {
            cpu = CpuNanoseconds(heap);
            start = minipal_hires_ticks();
        }
    }

    ~ScanCostScope()
    {
        if (start != 0)
        {
            heap->m_scan_metrics[metric].fetch_add(
                minipal_hires_ticks() - start, std::memory_order_relaxed);
            heap->m_scan_cpu_nanoseconds[metric].fetch_add(
                CpuNanoseconds(heap) - cpu, std::memory_order_relaxed);
        }
    }
#else
    ScanCostScope(LaxoriGcHeap* owner, size_t index)
    {
        (void)owner;
        (void)index;
    }

    ~ScanCostScope() = default;
#endif // LAXORI_HAS_DIAGNOSTIC_PROBES
};

void LaxoriGcHeap::CountScanWork(size_t metric)
{
#ifdef LAXORI_HAS_DIAGNOSTIC_PROBES
    if (m_storage_diagnostics_enabled.load(std::memory_order_relaxed))
    {
        m_scan_metrics[metric].fetch_add(1, std::memory_order_relaxed);
    }
#else
    (void)metric;
#endif // LAXORI_HAS_DIAGNOSTIC_PROBES
}

struct LaxoriGcHeap::WalkContext
{
    LaxoriGcHeap* heap;
    walk_fn callback;
    void* context;
};

LaxoriGcHeap::LaxoriGcHeap()
    : m_engine(),
      m_enumeration{ObjectReferenceEnumerationRequestStatus::NotProcessed,
          ObjectReferenceEnumerationRequest::Enabled, nullptr},
      m_frozen{}, m_frozen_readers(0), m_cards(nullptr), m_bundles(nullptr),
      m_collection_lock(), m_gc_done(), m_trace_signal(), m_coordinator_done(), m_concurrent_done(),
      m_initialized(false), m_in_gc(false), m_shutdown(false), m_concurrent_enabled(true),
      m_collections(0), m_memory_info{}, m_memory_sequence{},
      m_current_pause_kind(LaxoriPauseKind::RefCount), m_heap_before(0), m_fragmentation_before(0),
      m_trace_heap_before(0), m_trace_fragmentation_before(0), m_initial_pause_ticks(0),
      m_scan_failure(LaxoriRcResult::Success), m_latency(1), m_loh_mode(1),
      m_total_pause_ticks(0), m_last_pause_ticks(0), m_pause_start(0), m_start_time(0),
      m_last_gc_start(0), m_last_gc_duration(0), m_last_heap_size(0), m_last_committed(0),
      m_last_fragmentation(0), m_last_memory_load(0), m_reserved_limit(0),
      m_barrier_bootstraps(0)
#ifdef TARGET_WINDOWS
      , m_process_exiting(nullptr)
#endif // TARGET_WINDOWS
{
    s_heap = this;
    m_header_bits.request = ObjectHeaderBitsRequest::Enabled;
    m_header_bits.version = GC_OBJECT_HEADER_BITS_PARAMETERS_VERSION;
    m_header_bits.size = sizeof(ObjectHeaderBitsParameters);
    m_header_bits.requested_bit_count = 2;
    m_header_bits.requested_state_count = 3;
    m_header_bits.requested_protocol = ObjectHeaderBitsProtocol::ClaimAndPublish;
    m_header_bits.required_atomic_operations =
        static_cast<uint32_t>(ObjectHeaderBitsAtomicOperation::Load) |
        static_cast<uint32_t>(ObjectHeaderBitsAtomicOperation::CompareExchange) |
        static_cast<uint32_t>(ObjectHeaderBitsAtomicOperation::Store) |
        static_cast<uint32_t>(ObjectHeaderBitsAtomicOperation::Wait);
    m_header_bits.required_memory_order = ObjectHeaderBitsMemoryOrder::SequentiallyConsistent;
}

void LaxoriGcHeap::Fatal(const char* message) const
{
    GCToEEInterface::LogErrorToHost(message);
    fprintf(stderr, "LAXORI fatal: %s (engine failure %u)\n", message, static_cast<unsigned>(m_engine.GetFailure()));
    GCToEEInterface::HandleFatalError(static_cast<unsigned>(E_FAIL));
    abort();
}

void LaxoriGcHeap::Unsupported(const char* operation) const
{
    GCToEEInterface::LogErrorToHost(operation);
}

HRESULT LaxoriGcHeap::Initialize()
{
#ifdef TARGET_WINDOWS
    m_process_exiting = reinterpret_cast<BOOLEAN (WINAPI*)()>(
        GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "RtlDllShutdownInProgress"));
    if (m_process_exiting == nullptr)
    {
        Unsupported("LAXORI cannot establish Windows process-exit reader lifetime.");
        return E_NOTIMPL;
    }
#endif // TARGET_WINDOWS
    if (m_enumeration.request_status != ObjectReferenceEnumerationRequestStatus::Accepted ||
        m_enumeration.get_loader_allocator_object_slot == nullptr ||
        m_header_bits.request_status != ObjectHeaderBitsRequestStatus::Accepted ||
        m_header_bits.object_byte_offset != -static_cast<int32_t>(sizeof(ObjHeader)) ||
        m_header_bits.storage_word_size != sizeof(uint32_t) ||
        m_header_bits.clear_state != 0 || m_header_bits.transition_state != 2 ||
        m_header_bits.published_state != 3)
    {
        Unsupported("LAXORI requires accepted allocation/reference-enumeration contracts.");
        return E_NOTIMPL;
    }
    if (!m_collection_lock.Initialize() || !m_gc_done.CreateManualEventNoThrow(true) ||
        !m_trace_signal.CreateAutoEventNoThrow(false) || !m_coordinator_done.CreateManualEventNoThrow(false) ||
        !m_concurrent_done.CreateManualEventNoThrow(true) ||
        !m_scan_signal.CreateAutoEventNoThrow(false) || !m_scan_ready.CreateManualEventNoThrow(false) ||
        !m_scan_done.CreateAutoEventNoThrow(false) || !m_scan_exited.CreateManualEventNoThrow(false))
    {
        return E_OUTOFMEMORY;
    }
    // P29. The scan hand-over spins for ScanSpinMicroseconds before a thread sleeps, if there is a second processor to
    // run the thread that is waited for.
    m_scan_spin_ticks = GCToEEInterface::GetCurrentProcessCpuCount() < 2 ? 0 : static_cast<int64_t>(
        ScanSpinMicroseconds * static_cast<uint64_t>(minipal_hires_tick_frequency()) / 1000000);
    // P28 (F10). The default is 2 bits, as in the Java LAXORI (util/rc.rs: LOG_REF_COUNT_BITS = 1). The setting takes 2, 4 or 8.
    size_t bits = 2;
    if (!ReadSetting("LAXORIRCBits", &bits, 10, 8))
    {
        return E_FAIL;
    }
    if (bits != 2 && bits != 4 && bits != 8)
    {
        Unsupported("LAXORIRCBits must be 2, 4 or 8.");
        return E_FAIL;
    }
    LaxoriRcConfiguration configuration{
        ImmixBytes, LargeBytes, MaximumMutators, MaximumRoots, 4, static_cast<uint8_t>(bits),
    };
    configuration.evacuateNursery = true;
    configuration.evacuateMature = true;
    configuration.forwardingMask = m_header_bits.bit_mask;
    configuration.forwardingShift = m_header_bits.bit_shift;
    size_t immixInitial = 8;
    size_t largeInitial = 16;
    size_t immixReserve = LaxoriDefaultLimits::LegacyImmixReserveMB;
    size_t largeReserve = LaxoriDefaultLimits::LegacyLargeReserveMB;
    bool legacy = false;
    size_t youngMB = SIZE_MAX >> 20;
    size_t nurseryRatio = SIZE_MAX;
    size_t managedLimitMB = 0;
    size_t nativePlaneLimitMB = LaxoriDefaultLimits::MinimumNativePlaneLimitMB;
    bool nativePlaneLimitSpecified = false;
    bool noRecycling = false;
    bool noMutatorRecycling = false;
    bool overshoot = true;
    bool phaseTimers = true;
    size_t nurseryCapMB = 0;
    size_t selectParallelBlocks = configuration.selectParallelBlocks;
    bool immixReserveSpecified = false;
    bool largeReserveSpecified = false;
    // A block that was never used is zero already (committed memory is zero), so an allocator does not clear it. The
    // background clearer (LAXORIPrezeroThread) is off by default: it moves the clear of reused blocks to another thread, but the
    // program thread then pays the cache misses of the first write to every line (see laxori_immix_zeroing.cpp), and the total
    // CPU time rises.
    configuration.prezero = true;
    configuration.prezeroThread = false;
    bool privateEnabled = true;
    size_t regionKB = 256;
    bool escapedInPlace = true;
    size_t escapedCollectDiv = 16;
    if (!ReadBooleanSetting("LAXORILegacyCapacity", &legacy) ||
        !ReadSetting("LAXORIImmixInitialMB", &immixInitial, 10, LaxoriSideMetadataLayout::AddressLimit >> 20) ||
        !ReadSetting("LAXORILOSInitialMB", &largeInitial, 10, LaxoriSideMetadataLayout::AddressLimit >> 20) ||
        !ReadSetting("LAXORIImmixReserveMB", &immixReserve, 10,
            LaxoriSideMetadataLayout::AddressLimit >> 20, &immixReserveSpecified) ||
        !ReadSetting("LAXORILOSReserveMB", &largeReserve, 10,
            LaxoriSideMetadataLayout::AddressLimit >> 20, &largeReserveSpecified) ||
        !ReadSetting("LAXORIManagedHeapHardLimitMB", &managedLimitMB, 10, LaxoriSideMetadataLayout::AddressLimit >> 20) ||
        !ReadSetting("LAXORINativePlaneLimitMB", &nativePlaneLimitMB, 10, SIZE_MAX >> 20, &nativePlaneLimitSpecified) ||
        !ReadSetting("LAXORITraceThreshold", &m_policy_configuration.traceThreshold, 10, 100) ||
        !ReadSetting("LAXORIRCStopPercent", &m_policy_configuration.rcStopPercent, 10, 100) ||
        !ReadSetting("LAXORIMaxSurvivalMB", &m_policy_configuration.maxSurvivalMB, 10, SIZE_MAX >> 20) ||
        !ReadSetting("LAXORIIncrementsLimit", &m_policy_configuration.incrementsLimit) ||
        !ReadSetting("LAXORINurseryBlocks", &m_policy_configuration.nurseryBlocks) ||
        !ReadSetting("LAXORIYoungLimitMB", &youngMB, 10, SIZE_MAX >> 20) ||
        !ReadSetting("LAXORINurseryRatio", &nurseryRatio, 10, SIZE_MAX - 1) ||
        !ReadSetting("LAXORIMinReuseLines", &configuration.minimumReuseLines, 10, ImmixBlockGeometry::LinesPerBlock) ||
        !ReadSetting("LAXORIBufferSize", &configuration.bufferSize, 10, LaxoriReferenceCounting::BufferCapacity) ||
        !ReadSetting("LAXORIBufferBytes", &configuration.allocationBufferBytes, 10, ImmixBlockGeometry::BlockBytes) ||
        !ReadBooleanSetting("LAXORINoLineRecycling", &noRecycling) ||
        !ReadBooleanSetting("LAXORINoMutatorLineRecycling", &noMutatorRecycling) ||
        !ReadBooleanSetting("LAXORIMediumOverflow", &configuration.mediumOverflow) ||
        !ReadSetting("LAXORIMediumOverflowMode", &configuration.mediumOverflowMode, 10, 3) ||
        !ReadBooleanSetting("LAXORIOvershootToSpace", &overshoot) ||
        !ReadBooleanSetting("LAXORILazyDecrements", &configuration.lazyDecrements) ||
        !ReadBooleanSetting("LAXORIParallelSweep", &configuration.parallelSweep) ||
        !ReadBooleanSetting("LAXORIParallelPauseTrace", &configuration.parallelPauseTrace) ||
        !ReadSetting("LAXORIPauseTraceBatch", &configuration.pauseTraceBatch, 10, 8192) ||
        !ReadBooleanSetting("LAXORIEagerMarkClear", &configuration.eagerMarkClear) ||
        !ReadBooleanSetting("LAXORIWaitForLazy", &m_wait_for_lazy) ||
        !ReadBooleanSetting("LAXORIFastAllocation", &m_fast_enabled) ||
        !ReadBooleanSetting("LAXORIEvacuateNursery", &configuration.evacuateNursery) ||
        !ReadBooleanSetting("LAXORIEvacuateMature", &configuration.evacuateMature) ||
        !ReadSetting("LAXORIDefragHeadroomPercent", &configuration.defragHeadroomPercent, 10, 50) ||
        !ReadSetting("LAXORIChunkDefragPercent", &configuration.chunkDefragPercent, 10, 100) ||
        !ReadBooleanSetting("LAXORISelectValidate", &configuration.selectValidate) ||
        !ReadSetting("LAXORISelectParallelBlocks", &selectParallelBlocks, 10, UINT32_MAX) ||
        !ReadSetting("LAXORIMaxYoungEvacSize", &configuration.maximumYoungCopyBytes) ||
        !ReadBooleanSetting("LAXORISurvivalPredictorWeighted", &m_policy_configuration.weighted) ||
        !ReadBooleanSetting("LAXORISurvivalPredictorHarmonicMean", &m_policy_configuration.harmonic) ||
        !ReadBooleanSetting("LAXORIPhaseTimers", &phaseTimers) ||
        !ReadBooleanSetting("LAXORIHeapSizing", &m_sizing_configuration.enabled) ||
        !ReadSetting("LAXORINurseryCapMB", &nurseryCapMB, 10, SIZE_MAX >> 20) ||
        !ReadBooleanSetting("LAXORIPolicyTrace", &m_policy_trace) ||
        !ReadBooleanSetting("LAXORILookupStats", &m_lookup_stats) ||
        !ReadBooleanSetting("LAXORIWindowStats", &m_window_stats) ||
        !ReadBooleanSetting("LAXORIPrezero", &configuration.prezero) ||
        !ReadBooleanSetting("LAXORIPrezeroThread", &configuration.prezeroThread) ||
        !ReadSetting("LAXORIPrezeroMarginBlocks", &configuration.prezeroMarginBlocks, 10, 4096) ||
        !ReadSetting("LAXORIPrezeroWindowBlocks", &configuration.prezeroWindowBlocks, 10, 65536) ||
        !ReadBooleanSetting("LAXORIPrivate", &privateEnabled) ||
        !ReadSetting("LAXORIRegionKB", &regionKB, 10, 4096) ||
        !ReadBooleanSetting("LAXORIEscapedInPlace", &escapedInPlace) ||
        !ReadSetting("LAXORIEscapedCollectDiv", &escapedCollectDiv, 10, 64))
    {
        return E_FAIL;
    }
    if (escapedCollectDiv == 0)
    {
        escapedCollectDiv = 16;
    }
    size_t gcThreads = 0;
    size_t concurrentThreads = 0;
    if (!ReadSetting("LAXORIGCThreads", &gcThreads, 10, LaxoriWorkerPolicy::MaximumWorkers) ||
        !ReadSetting("LAXORIConcGCThreads", &concurrentThreads, 10, LaxoriWorkerPolicy::MaximumWorkers))
    {
        return E_FAIL;
    }
    // P25 (F4). The worker count of the Java LAXORI (ParallelGCThreads) from the number of processors. A setting of 0 or no
    // setting selects the rule. P26 (F2, F4b). The concurrent trace runs on ConcGCThreads of these workers, as in the Java LAXORI
    // (LaxoriWorkerPolicy::ConcurrentThreads, 3 of 13), with a floor of 2 on small machines. This became the default when a pause no longer walks the trace backlog:
    // Measured, list suffix nursery with 3 trace workers needs the same time as with all workers. LAXORIConcGCThreads overrides it.
    configuration.workers = gcThreads != 0
        ? gcThreads : LaxoriWorkerPolicy::ParallelThreads(GCToEEInterface::GetCurrentProcessCpuCount());
    size_t defaultConcurrent = LaxoriWorkerPolicy::DefaultConcurrentTraceThreads(configuration.workers);
    configuration.concurrentTraceWorkers = concurrentThreads != 0 ? concurrentThreads : defaultConcurrent;
    if (configuration.concurrentTraceWorkers > configuration.workers)
    {
        configuration.concurrentTraceWorkers = configuration.workers;
    }
    m_engine.GetPauseTimers().Configure(phaseTimers);
    configuration.selectParallelBlocks = static_cast<uint32_t>(selectParallelBlocks);
    for (const char* unsupported : {
            "LAXORIMaxPauseMillis", "LAXORITransparentHugepage",
            "GCHeapHardLimit", "GCHeapHardLimitPercent",
            "GCHeapHardLimitSOH", "GCHeapHardLimitLOH", "GCHeapHardLimitPOH",
            "GCHeapHardLimitSOHPercent", "GCHeapHardLimitLOHPercent", "GCHeapHardLimitPOHPercent"})
    {
        size_t value = 0;
        if (!ReadSetting(unsupported, &value, 10, 0, nullptr, true))
        {
            Unsupported("LAXORI does not support this setting. Standard GC hard limits include bookkeeping; use LAXORIManagedHeapHardLimitMB for the explicitly managed-capacity ceiling.");
            return E_NOTIMPL;
        }
    }
    if (legacy)
    {
        if ((immixReserveSpecified && immixReserve != immixInitial) ||
            (largeReserveSpecified && largeReserve != largeInitial))
        {
            Unsupported("LAXORILegacyCapacity conflicts with the explicit reservation.");
            return E_FAIL;
        }
        immixReserve = immixInitial;
        largeReserve = largeInitial;
    }
    m_immix_reserve_defaulted = !legacy && !immixReserveSpecified;
    m_large_reserve_defaulted = !legacy && !largeReserveSpecified;
    m_hard_limit_explicit = managedLimitMB != 0;
    if (m_immix_reserve_defaulted || m_large_reserve_defaulted)
    {
        size_t addressLimitMB = LaxoriSideMetadataLayout::AddressLimit >> 20;
        if (m_reserved_limit != 0 && (m_reserved_limit >> 20) < addressLimitMB)
        {
            addressLimitMB = m_reserved_limit >> 20;
        }
        LaxoriDefaultLimits::ReservesMB(
            GCToOSInterface::GetPhysicalMemoryLimit(), addressLimitMB, managedLimitMB,
            m_immix_reserve_defaulted, &immixReserve, m_large_reserve_defaulted, &largeReserve);
        if (m_immix_reserve_defaulted && immixReserve < immixInitial)
        {
            immixReserve = immixInitial;
        }
        if (m_large_reserve_defaulted && largeReserve < largeInitial)
        {
            largeReserve = largeInitial;
        }
    }
    m_native_limit_defaulted = !nativePlaneLimitSpecified;
    if (m_native_limit_defaulted)
    {
        nativePlaneLimitMB = LaxoriDefaultLimits::NativePlaneLimitMB(immixReserve, largeReserve);
    }
    if (immixInitial == 0 || largeInitial == 0 || immixReserve < immixInitial ||
        largeReserve < largeInitial ||
        immixReserve > (LaxoriSideMetadataLayout::AddressLimit >> 20) - largeReserve ||
        configuration.minimumReuseLines == 0 ||
        configuration.allocationBufferBytes % ImmixBlockGeometry::LineBytes != 0 ||
        (configuration.bufferSize != 256 && configuration.bufferSize != 512 && configuration.bufferSize != 1024) ||
        m_policy_configuration.maxSurvivalMB == 0 || m_policy_configuration.incrementsLimit == 0 ||
        m_policy_configuration.nurseryBlocks == 0 || youngMB == 0 || nativePlaneLimitMB == 0)
    {
        Unsupported("LAXORI capacity and trigger limits must be positive, aligned and bounded.");
        return E_FAIL;
    }
    configuration.immixBytes = immixInitial << 20;
    configuration.largeObjectBytes = largeInitial << 20;
    configuration.immixReservationBytes = immixReserve << 20;
    configuration.largeObjectReservationBytes = largeReserve << 20;
    size_t reservation = configuration.immixReservationBytes + configuration.largeObjectReservationBytes;
    m_hard_limit = managedLimitMB == 0 ? reservation : managedLimitMB << 20;
    if (m_hard_limit < configuration.immixBytes + configuration.largeObjectBytes ||
        m_hard_limit > reservation ||
        (m_reserved_limit != 0 && reservation > m_reserved_limit))
    {
        Unsupported("LAXORI initial capacity must fit the hard limit, and the hard limit must fit the reservation.");
        return E_OUTOFMEMORY;
    }
    configuration.hardLimitBytes = m_hard_limit;
    m_native_plane_limit = nativePlaneLimitMB << 20;
    size_t commitHeadroomMB = 0;
    bool commitHeadroomSpecified = false;
    if (!ReadSetting("LAXORICommitHeadroomMB", &commitHeadroomMB, 10, SIZE_MAX >> 20, &commitHeadroomSpecified))
    {
        return E_FAIL;
    }
#ifdef TARGET_WINDOWS
    // Only the process's immediate job is read, not parent jobs. An explicit zero disables the check.
    JOBOBJECT_EXTENDED_LIMIT_INFORMATION job{};
    if (!(commitHeadroomSpecified && commitHeadroomMB == 0) &&
        QueryInformationJobObject(nullptr, JobObjectExtendedLimitInformation, &job, sizeof(job), nullptr))
    {
        uint64_t specified = static_cast<uint64_t>(commitHeadroomMB) << 20;
        if ((job.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_PROCESS_MEMORY) != 0 && job.ProcessMemoryLimit != 0)
        {
            m_process_commit_limit = job.ProcessMemoryLimit;
            m_process_commit_headroom = commitHeadroomSpecified
                ? specified : LaxoriDefaultLimits::CommitHeadroomBytes(m_process_commit_limit);
        }
        if ((job.BasicLimitInformation.LimitFlags & JOB_OBJECT_LIMIT_JOB_MEMORY) != 0 && job.JobMemoryLimit != 0)
        {
            m_job_commit_limit = job.JobMemoryLimit;
            m_job_commit_headroom = commitHeadroomSpecified
                ? specified : LaxoriDefaultLimits::CommitHeadroomBytes(m_job_commit_limit);
        }
    }
#endif // TARGET_WINDOWS
    configuration.lineRecycling = !noRecycling && !noMutatorRecycling;
    configuration.copyLineRecycling = !noRecycling;
    // The engine enables overshoot only when there is no hard limit and the reservation is larger than the initial
    // capacity; this setting only switches it off. A hard limit below the reservation always disables it.
    configuration.overshootToSpace = overshoot;
    m_policy_configuration.youngLimitBytes = youngMB == (SIZE_MAX >> 20) ? SIZE_MAX : youngMB << 20;
    if (nurseryRatio != SIZE_MAX)
    {
        size_t ratioBase = m_hard_limit;
        if (managedLimitMB == 0 && (m_immix_reserve_defaulted || m_large_reserve_defaulted))
        {
            ratioBase = (m_immix_reserve_defaulted ? LaxoriDefaultLimits::LegacyImmixReserveMB : immixReserve) +
                (m_large_reserve_defaulted ? LaxoriDefaultLimits::LegacyLargeReserveMB : largeReserve);
            ratioBase <<= 20;
        }
        m_policy_configuration.nurseryBlocks =
            ratioBase / ImmixBlockGeometry::BlockBytes / (nurseryRatio + 1);
        if (m_policy_configuration.nurseryBlocks == 0)
        {
            Unsupported("LAXORINurseryRatio leaves no nursery block.");
            return E_FAIL;
        }
    }
    m_policy.Configure(m_policy_configuration);
    m_sizing_configuration.nurseryCapBytes = nurseryCapMB != 0
        ? nurseryCapMB << 20 : LaxoriHeapSizer::DefaultNurseryCap(GCToOSInterface::GetPhysicalMemoryLimit());
    m_sizer.Configure(m_sizing_configuration, configuration.immixBytes);
    m_heap_configuration = configuration;
    LaxoriPauseRuntimeAdapter runtime{Suspend, Resume, this, true};
    LaxoriRcObjectModel model{
        m_enumeration.get_loader_allocator_object_slot, true, FinishTrace, this, IsImmortal, FinishEpoch,
        nullptr, FinishLazy, CanAdmitRange,
    };
    model.admitRange = AdmitHeapTables;
    model.finishRelocation = FinishRelocation;
    if (m_engine.Initialize(configuration, runtime, &model) != LaxoriRcResult::Success)
    {
        Unsupported("LAXORI could not reserve its owned heap, metadata or worker resources.");
        return E_OUTOFMEMORY;
    }
    m_engine.SetBorrowCeiling(PolicyReserveBytes());
    if (m_engine.GetNativePlaneCommittedBytes() > m_native_plane_limit)
    {
        m_engine.Shutdown();
        Unsupported("LAXORI initial native planes exceed LAXORINativePlaneLimitMB.");
        return E_OUTOFMEMORY;
    }
    if (!m_card_storage.Reserve(m_engine.GetReservationBytes() / 2048 + 1) ||
        !m_bundle_storage.Reserve(m_engine.GetReservationBytes() / (2 * 1024 * 1024) + 1))
    {
        m_engine.Shutdown();
        Unsupported("LAXORI could not reserve its card tables.");
        return E_OUTOFMEMORY;
    }
    m_cards = m_card_storage.Get();
    m_bundles = m_bundle_storage.Get();
    if (!InitializePrivateTier(regionKB, privateEnabled, escapedInPlace, escapedCollectDiv))
    {
        m_engine.Shutdown();
        Unsupported("Laxori could not create its private tier (LAXORIRegionKB must be a power of two from 32 to 4096).");
        return E_OUTOFMEMORY;
    }
    g_gc_lowest_address = reinterpret_cast<uint8_t*>(m_engine.GetReservationStart());
    g_gc_highest_address = g_gc_lowest_address + m_engine.GetReservationBytes();
    g_gc_card_table = reinterpret_cast<uint32_t*>(
        reinterpret_cast<uintptr_t>(m_cards) - (m_engine.GetReservationStart() >> 11));
#ifdef FEATURE_MANUALLY_MANAGED_CARD_BUNDLES
    g_gc_card_bundle_table = reinterpret_cast<uint32_t*>(
        reinterpret_cast<uintptr_t>(m_bundles) - (m_engine.GetReservationStart() >> 21));
#endif
    g_gc_pFreeObjectMethodTable = GCToEEInterface::GetFreeObjectMethodTable();
    g_num_processors = 1;
    WriteBarrierParameters parameters{};
    parameters.operation = WriteBarrierOp::Initialize;
    parameters.is_runtime_suspended = true;
    parameters.card_table = g_gc_card_table;
#ifdef FEATURE_MANUALLY_MANAGED_CARD_BUNDLES
    parameters.card_bundle_table = g_gc_card_bundle_table;
#endif
    parameters.lowest_address = g_gc_lowest_address;
    parameters.highest_address = g_gc_highest_address;
    parameters.ephemeral_low = g_gc_lowest_address;
    parameters.ephemeral_high = g_gc_highest_address;
    // The runtime builds the barrier from GetAssignRefFunctions and keeps its patchable slots in sync with these
    // values; they must be set before the runtime installs the helpers.
    LaxoriWriteBarrierSetGlobals(
        m_engine.GetFieldMetadataBase(),
        m_engine.GetReservationStart(),
        m_engine.GetReservationBytes(),
        reinterpret_cast<void*>(&LaxoriBarrierSlowPath),
        LaxoriWriteBarrierComputeXsaveMask(),
        m_region_owner,
        m_private_bits_base,
        m_region_shift,
        reinterpret_cast<void*>(&LaxoriEscapeSlowPath), &m_private_heap_bytes);
    GCToEEInterface::StompWriteBarrier(&parameters);
    gc_alloc_context* context = GCToEEInterface::GetAllocContext();
    if (context != nullptr && FindOrAttach(context, false) == nullptr)
    {
        m_engine.Shutdown();
        return E_OUTOFMEMORY;
    }
    m_start_time = minipal_lowres_ticks();
    GCScan::GcRuntimeStructuresValid(TRUE);
    if (!GCToEEInterface::CreateThread(ScanOnGcThread, this, false, "LAXORI root/lifetime scan"))
    {
        m_engine.Shutdown();
        Unsupported("LAXORI could not create its registered scan executor.");
        return E_OUTOFMEMORY;
    }
    if (m_scan_ready.Wait(Timeout, false) != WAIT_OBJECT_0)
    {
        Fatal("LAXORI scan executor did not become ready.");
    }
    if (!GCToEEInterface::CreateThread(CoordinateTrace, this, false, "LAXORI trace coordinator"))
    {
        m_scan_stopping.store(true, std::memory_order_release);
        m_scan_signal.Set();
        if (m_scan_exited.Wait(Timeout, false) != WAIT_OBJECT_0)
        {
            Fatal("LAXORI scan executor did not exit after coordinator creation failed.");
        }
        m_engine.Shutdown();
        return E_OUTOFMEMORY;
    }
    m_initialized.store(true, std::memory_order_release);
    return S_OK;
}

LaxoriRcMutator* LaxoriGcHeap::FindOrAttach(gc_alloc_context* context, bool barrier)
{
    if (context == nullptr)
    {
        Fatal("LAXORI managed operation has no runtime allocation context.");
    }
    LaxoriRcMutator* mutator = m_engine.FindCachedMutator(context->gc_reserved_1, context);
    if (mutator == nullptr)
    {
        mutator = m_engine.FindMutator(context);
    }
    if (mutator == nullptr)
    {
        LaxoriRcResult result = m_engine.AttachMutator(context, &mutator);
        if (result == LaxoriRcResult::CapacityExceeded && !barrier)
        {
            return nullptr;
        }
        if (result != LaxoriRcResult::Success)
        {
            Fatal("LAXORI mutator admission failed; the prototype supports at most 1024 simultaneous contexts.");
        }
        if (barrier)
        {
            m_barrier_bootstraps.fetch_add(1, std::memory_order_relaxed);
        }
    }
    if (mutator->Tag() == 0)
    {
        mutator->SetTag(LaxoriCurrentThreadTag());
    }
    context->gc_reserved_1 = mutator;
    return mutator;
}

Object* LaxoriGcHeap::Alloc(gc_alloc_context* context, size_t size, uint32_t flags)
{
    if (size < MIN_OBJECT_SIZE || size > SIZE_MAX - (sizeof(uintptr_t) - 1))
    {
        return nullptr;
    }
    size = (size + sizeof(uintptr_t) - 1) & ~(sizeof(uintptr_t) - 1);
    if (m_private_enabled)
    {
        // The buffer of the thread is its private region (or a gap of it). An ordinary small object goes there. Everything
        // else (finalizable, pinned-object-heap, user-old-heap, and larger objects) is allocated by the shared engine and
        // does not touch the buffer.
        if ((flags & (GC_ALLOC_FINALIZE | GC_ALLOC_USER_OLD_HEAP | GC_ALLOC_PINNED_OBJECT_HEAP)) == 0 &&
            size <= m_max_private_object)
        {
            if (context->alloc_ptr != nullptr && static_cast<size_t>(context->alloc_limit - context->alloc_ptr) >= size)
            {
                Object* inPlace = reinterpret_cast<Object*>(context->alloc_ptr);
                context->alloc_ptr += size;
                return inPlace;
            }
            LaxoriRcMutator* mutator = m_shutdown.load(std::memory_order_acquire) ? nullptr : FindOrAttach(context, false);
            if (mutator != nullptr)
            {
                Object* object = AllocPrivateSlow(context, mutator, size);
                if (object != nullptr)
                {
                    return object;
                }
            }
        }
        return AllocShared(context, size, flags);
    }
    // The private tier is off: the allocation of the LXR engine. The stock helpers bump alloc_ptr inside the buffer that this
    // method gave to the thread, and call it only when the buffer cannot hold the object, or for an object that needs the
    // slow path. A small object that fits is taken in place.
    if ((flags & GC_ALLOC_USER_OLD_HEAP) == 0 && context->alloc_ptr != nullptr && size < LaxoriLargeObjectSpace::MinimumObjectBytes &&
        static_cast<size_t>(context->alloc_limit - context->alloc_ptr) >= size)
    {
        Object* inPlace = reinterpret_cast<Object*>(context->alloc_ptr);
        context->alloc_ptr += size;
        RegisterFinalizable(inPlace, flags);
        return inPlace;
    }
    return AllocShared(context, size, flags);
}

// The allocation of the shared heap (the LXR engine).
Object* LaxoriGcHeap::AllocShared(gc_alloc_context* context, size_t size, uint32_t flags)
{
    // A thread that has a private region keeps its buffer for the region. A thread without one uses the buffer of the engine
    // like LXR does (closed before a slow allocation, published again after it).
    bool noRegion = true;
    if (m_private_enabled)
    {
        LaxoriRcMutator* current = context->gc_reserved_1 == nullptr ? nullptr : m_engine.FindCachedMutator(context->gc_reserved_1, context);
        noRegion = current == nullptr || current->Region().start == 0;
    }
    if (noRegion)
    {
        RetireAllocationSpan(context, true);
    }
    if (!m_shutdown.load(std::memory_order_acquire))
    {
        if (GetCollectionCause(false) != LaxoriCollectionCause::None)
        {
            if (CollectForPressure(false, false) != LaxoriRcResult::Success)
            {
                Fatal("LAXORI allocation-pressure collection failed.");
            }
        }
    }
    LaxoriRcMutator* mutator = FindOrAttach(context, false);
    if (mutator == nullptr && !m_shutdown.load())
    {
        if (CollectForPressure(true, false) != LaxoriRcResult::Success)
        {
            Fatal("LAXORI admission-retry collection failed.");
        }
        mutator = FindOrAttach(context, false);
    }
    if (mutator == nullptr)
    {
        if (!m_shutdown.load())
        {
            ReportOutOfMemory(size, LaxoriRcResult::CapacityExceeded);
        }
        return nullptr;
    }
    Object* object = nullptr;
    // A pinned-object-heap object of a small size goes into a pinned block. The engine keeps those blocks apart, so
    // the pause needs no per-object flag to know that the object stays.
    bool pinned = (flags & GC_ALLOC_PINNED_OBJECT_HEAP) != 0;
    LaxoriRcResult result = m_engine.Allocate(mutator, size, &object, pinned);
    LaxoriRcResult growth = LaxoriRcResult::AllocationFailure;
    if (result == LaxoriRcResult::AllocationFailure && !m_shutdown.load())
    {
        // The lazy sweep after the last pause makes promoted blocks Reusable. A collection is needed only if the
        // allocation still fails after it has finished.
        bool cooperative = EnterCollectionLock();
        LaxoriRcResult drained = UpdatePolicyHints();
        LeaveCollectionLock(cooperative);
        if (drained != LaxoriRcResult::Success)
        {
            Fatal("LAXORI lazy sweep did not finish before an allocation retry.");
        }
        m_engine.ResetAllocationCursors();
        result = m_engine.Allocate(mutator, size, &object, pinned);
    }
    if (result == LaxoriRcResult::AllocationFailure && !m_shutdown.load())
    {
        if (CollectForPressure(false, false, true) != LaxoriRcResult::Success)
        {
            Fatal("LAXORI space-pressure collection failed before growth admission.");
        }
        result = m_engine.Allocate(mutator, size, &object, pinned);
    }
    if (result == LaxoriRcResult::AllocationFailure && !m_shutdown.load())
    {
        growth = TryGrow(size >= LaxoriLargeObjectSpace::MinimumObjectBytes, size);
        LaxoriRcResult grown = growth;
        if (grown == LaxoriRcResult::Success)
        {
            m_engine.ResetAllocationCursors();
            result = m_engine.Allocate(mutator, size, &object, pinned);
        }
        else if (grown != LaxoriRcResult::AllocationFailure && grown != LaxoriRcResult::OutOfMemory)
        {
            Fatal("LAXORI growth failed a storage admission invariant.");
        }
    }
    if (result == LaxoriRcResult::AllocationFailure && !m_shutdown.load())
    {
        // An emergency collection that runs during a concurrent trace only ends the trace (a final mark), and the
        // garbage that died after the trace began stays. The next one is a full collection. The program runs again as
        // soon as a pause is over, and the other threads can use the space that it freed before this thread allocates.
        // So the allocation is given a few collections before it fails.
        for (int attempt = 0; attempt < MaximumEmergencyCollections && result == LaxoriRcResult::AllocationFailure &&
            !m_shutdown.load(); attempt++)
        {
            if (CollectForPressure(true, false) != LaxoriRcResult::Success)
            {
                Fatal("LAXORI emergency full collection failed.");
            }
            // The allocation cursors move forward only. The pause sets them back, and then the other threads move them
            // past the blocks that the lazy sweep of the pause has not freed yet. The collection has waited for that
            // sweep, so those blocks are free now but lie behind the cursors.
            m_engine.ResetAllocationCursors();
            result = m_engine.Allocate(mutator, size, &object, pinned);
        }
    }
    if (result == LaxoriRcResult::AllocationFailure)
    {
        if (!m_shutdown.load())
        {
            ReportOutOfMemory(size, growth);
        }
        return nullptr;
    }
    if (result != LaxoriRcResult::Success)
    {
        Fatal("LAXORI owned allocator failed an allocation invariant.");
    }
    if ((flags & GC_ALLOC_USER_OLD_HEAP) != 0)
    {
        context->alloc_bytes_uoh += static_cast<int64_t>(size);
    }
    else
    {
        context->alloc_bytes += static_cast<int64_t>(size);
    }
    m_context_allocated_bytes.fetch_add(size, std::memory_order_relaxed);
    if (m_fast_enabled && size < LaxoriLargeObjectSpace::MinimumObjectBytes &&
        (flags & GC_ALLOC_USER_OLD_HEAP) == 0 && noRegion)
    {
        uintptr_t cursor;
        uintptr_t limit;
        if (m_engine.GetAllocationSpan(mutator, &cursor, &limit) != LaxoriRcResult::Success)
        {
            Fatal("LAXORI could not publish its initialized Immix allocation span.");
        }
        if (cursor != 0)
        {
            context->alloc_ptr = reinterpret_cast<uint8_t*>(cursor + sizeof(ObjHeader));
            context->alloc_limit = reinterpret_cast<uint8_t*>(limit + sizeof(ObjHeader));
            context->alloc_bytes += static_cast<int64_t>(limit - cursor);
            m_context_allocated_bytes.fetch_add(limit - cursor, std::memory_order_relaxed);
            m_fast_refills.fetch_add(1, std::memory_order_relaxed);
        }
    }
    RegisterFinalizable(object, flags);
    return object;
}

// A finalizable object is registered when it is allocated, as the standard collector does. The registry never reads the
// object before a pause, and a thread cannot be suspended between the allocation and the point where the runtime has
// written the MethodTable, so a pause sees a complete object. The critical bit of the finalizer is taken from the
// MethodTable when the pause captures the entry (see CaptureFinalizers). Objects of a collectible type, large objects
// and pinned-object-heap objects reach this method for each object too, so they need no further hook: the pause
// reads the type of an object when it first counts it.
void LaxoriGcHeap::RegisterFinalizable(Object* object, uint32_t flags)
{
    if ((flags & GC_ALLOC_FINALIZE) != 0 && !m_registry.Register(object, false))
    {
        Fatal("LAXORI could not register an object for finalization.");
    }
}

bool LaxoriGcHeap::CanAdmitRange(size_t, size_t slotCount, size_t nativeBytes, void* context)
{
    // Keep the existing native-plane limit: allocation tracking and the four RC log streams.
    LaxoriGcHeap* heap = static_cast<LaxoriGcHeap*>(context);
    heap->m_native_refused_bytes.store(nativeBytes > heap->m_native_plane_limit ? nativeBytes : 0,
        std::memory_order_relaxed);
    if (nativeBytes > heap->m_native_plane_limit)
    {
        return false;
    }
#ifdef TARGET_WINDOWS
    if (heap->m_process_commit_limit != 0 || heap->m_job_commit_limit != 0)
    {
        // Include the range, metadata/cards (one sixteenth of the range), and new native planes.
        size_t rangeBytes = slotCount * sizeof(uintptr_t);
        size_t nativeNow = heap->m_engine.GetNativePlaneCommittedBytes();
        uint64_t added = static_cast<uint64_t>(rangeBytes) + rangeBytes / 16 +
            (nativeBytes > nativeNow ? nativeBytes - nativeNow : 0);
        if (heap->m_process_commit_limit != 0)
        {
            PROCESS_MEMORY_COUNTERS_EX counters{};
            counters.cb = sizeof(counters);
            if (GetProcessMemoryInfo(GetCurrentProcess(), reinterpret_cast<PROCESS_MEMORY_COUNTERS*>(&counters),
                    sizeof(counters)) &&
                !LaxoriDefaultLimits::FitsCommitLimit(heap->m_process_commit_limit, heap->m_process_commit_headroom,
                    counters.PrivateUsage, added))
            {
                heap->RecordCommitRefusal(added, counters.PrivateUsage, false);
                return false;
            }
        }
        uint64_t jobCommitted = 0;
        if (heap->m_job_commit_limit != 0 && GetJobCommittedBytes(&jobCommitted) &&
            !LaxoriDefaultLimits::FitsCommitLimit(heap->m_job_commit_limit, heap->m_job_commit_headroom, jobCommitted, added))
        {
            heap->RecordCommitRefusal(added, jobCommitted, true);
            return false;
        }
    }
#else
    (void)slotCount;
#endif // TARGET_WINDOWS
    return true;
}

#ifdef TARGET_WINDOWS
bool LaxoriGcHeap::GetJobCommittedBytes(uint64_t* committed)
{
    JOBOBJECT_LIMIT_VIOLATION_INFORMATION violation{};
    if (QueryInformationJobObject(nullptr, JobObjectLimitViolationInformation, &violation, sizeof(violation), nullptr))
    {
        *committed = violation.JobMemory;
        return true;
    }
    // The peak is conservative if the current commit cannot be read.
    JOBOBJECT_EXTENDED_LIMIT_INFORMATION job{};
    if (QueryInformationJobObject(nullptr, JobObjectExtendedLimitInformation, &job, sizeof(job), nullptr))
    {
        *committed = job.PeakJobMemoryUsed;
        return true;
    }
    return false;
}

void LaxoriGcHeap::RecordCommitRefusal(uint64_t addedBytes, uint64_t committedBytes, bool jobLimit)
{
    m_commit_refused_job.store(jobLimit, std::memory_order_relaxed);
    m_commit_refused_committed.store(static_cast<size_t>(committedBytes), std::memory_order_relaxed);
    m_commit_refused_bytes.store(static_cast<size_t>(addedBytes), std::memory_order_relaxed);
}
#endif // TARGET_WINDOWS

void LaxoriGcHeap::ReportOutOfMemory(size_t size, LaxoriRcResult growth)
{
    if (m_out_of_memory_reported.exchange(true, std::memory_order_acq_rel))
    {
        return;
    }
    constexpr size_t MB = size_t{1} << 20;
    size_t immixReserve = m_heap_configuration.immixReservationBytes;
    size_t largeReserve = m_heap_configuration.largeObjectReservationBytes;
    size_t admitted = m_engine.GetAdmittedBytes();
    size_t immix = m_engine.GetImmixCapacityBytes();
    size_t large = admitted > immix ? admitted - immix : 0;
    bool largeRequest = size >= LaxoriLargeObjectSpace::MinimumObjectBytes;
    size_t block = ImmixBlockGeometry::BlockBytes;
    char text[640];
    int length;
    if (growth == LaxoriRcResult::CapacityExceeded)
    {
        length = snprintf(text, sizeof(text),
            "LAXORI: out of memory. More than %zu threads allocate at the same time. This limit is fixed.\n",
            MaximumMutators);
    }
    else if (growth == LaxoriRcResult::OutOfMemory && m_commit_refused_bytes.load(std::memory_order_relaxed) != 0)
    {
        bool jobLimit = m_commit_refused_job.load(std::memory_order_relaxed);
        length = snprintf(text, sizeof(text),
            "LAXORI: out of memory. %s %zu MB committed, and the %s commit limit of the job object is %zu MB. "
            "A heap growth would commit %zu KB more and leave less than %zu MB for the collector and the runtime "
            "(Immix capacity %zu MB, LOS %zu MB, native planes %zu MB). Raise the memory limit of the job, or set "
            "DOTNET_LAXORICommitHeadroomMB (decimal MB; 0 switches the check off).\n",
            jobLimit ? "All processes in the job have" : "The process has",
            m_commit_refused_committed.load(std::memory_order_relaxed) / MB, jobLimit ? "job" : "per-process",
            static_cast<size_t>((jobLimit ? m_job_commit_limit : m_process_commit_limit) / MB),
            m_commit_refused_bytes.load(std::memory_order_relaxed) / 1024,
            static_cast<size_t>((jobLimit ? m_job_commit_headroom : m_process_commit_headroom) / MB),
            immix / MB, large / MB, m_engine.GetNativePlaneCommittedBytes() / MB);
    }
    else if (growth == LaxoriRcResult::OutOfMemory && m_native_refused_bytes.load(std::memory_order_relaxed) != 0)
    {
        length = snprintf(text, sizeof(text),
            "LAXORI: out of memory. Native planes would use %zu MB; LAXORINativePlaneLimitMB is %zu MB%s "
            "(%zu MB committed). Raise DOTNET_LAXORINativePlaneLimitMB (decimal MB).\n",
            m_native_refused_bytes.load(std::memory_order_relaxed) / MB, m_native_plane_limit / MB,
            m_native_limit_defaulted ? " (default, derived from the reservation)" : "",
            m_engine.GetNativePlaneCommittedBytes() / MB);
    }
    else if (growth == LaxoriRcResult::OutOfMemory)
    {
        length = snprintf(text, sizeof(text),
            "LAXORI: out of memory. The operating system did not commit heap or metadata memory "
            "(Immix %zu MB of %zu MB, LOS %zu MB of %zu MB). Check the process, job or container memory limit.\n",
            immix / MB, immixReserve / MB, large / MB, largeReserve / MB);
    }
    else if (m_hard_limit < m_engine.GetReservationBytes() &&
        m_hard_limit - (admitted < m_hard_limit ? admitted : m_hard_limit) < (largeRequest ? size : block))
    {
        length = snprintf(text, sizeof(text),
            "LAXORI: out of memory. The heap reached LAXORIManagedHeapHardLimitMB=%zu MB (%zu MB admitted). "
            "Raise DOTNET_LAXORIManagedHeapHardLimitMB (decimal MB).\n", m_hard_limit / MB, admitted / MB);
    }
    else if (largeRequest && largeReserve - (large < largeReserve ? large : largeReserve) < size)
    {
        length = snprintf(text, sizeof(text),
            "LAXORI: out of memory. LOS is full at LAXORILOSReserveMB=%zu MB%s (%zu MB admitted; request %zu bytes). "
            "Raise DOTNET_LAXORILOSReserveMB (decimal MB; also DOTNET_LAXORINativePlaneLimitMB if set).\n",
            largeReserve / MB, m_large_reserve_defaulted
                ? (m_hard_limit_explicit ? " (default with managed hard limit)" : " (default: process memory limit)") : "",
            large / MB, size);
    }
    else if (!largeRequest && immixReserve - (immix < immixReserve ? immix : immixReserve) < block)
    {
        length = snprintf(text, sizeof(text),
            "LAXORI: out of memory. Immix is full at LAXORIImmixReserveMB=%zu MB%s (%zu MB admitted). "
            "Raise DOTNET_LAXORIImmixReserveMB (decimal MB; also DOTNET_LAXORINativePlaneLimitMB if set).\n",
            immixReserve / MB, m_immix_reserve_defaulted
                ? (m_hard_limit_explicit ? " (default with managed hard limit)" : " (default: process memory limit)") : "", immix / MB);
    }
    else
    {
        length = snprintf(text, sizeof(text),
            "LAXORI: out of memory. No free space for %zu bytes (Immix %zu MB of %zu MB, LOS %zu MB of %zu MB). "
            "The heap can be fragmented; raise DOTNET_LAXORIImmixReserveMB or DOTNET_LAXORILOSReserveMB (decimal MB).\n",
            size, immix / MB, immixReserve / MB, large / MB, largeReserve / MB);
    }
    if (length <= 0)
    {
        return;
    }
    size_t count = static_cast<size_t>(length) < sizeof(text) ? static_cast<size_t>(length) : sizeof(text) - 1;
#ifdef TARGET_WINDOWS
    HANDLE error = GetStdHandle(STD_ERROR_HANDLE);
    DWORD written;
    if (error != nullptr && error != INVALID_HANDLE_VALUE)
    {
        WriteFile(error, text, static_cast<DWORD>(count), &written, nullptr);
    }
#else
    ssize_t written = write(STDERR_FILENO, text, count);
    (void)written;
#endif // TARGET_WINDOWS
}

size_t LaxoriGcHeap::PolicyReserveBytes() const
{
    size_t reservation = m_engine.GetImmixReservationBytes();
    return m_immix_reserve_defaulted
        ? LaxoriHeapSizer::PolicyReserveBytes(reservation, m_mature_live_bytes.load(std::memory_order_relaxed))
        : reservation;
}

LaxoriRcResult LaxoriGcHeap::TryGrow(bool large, size_t size)
{
    bool cooperative = EnterCollectionLock();
    LaxoriRcResult result = UpdatePolicyHints();
    if (result == LaxoriRcResult::Success)
    {
        result = m_engine.RequestGrowth(large, size);
    }
    if (result == LaxoriRcResult::Success)
    {
        result = CollectLocked(m_policy.Select(
            m_engine.GetTracePhase() == LaxoriTracePhase::Concurrent, m_engine.IsTraceExhausted(),
            false, false, m_concurrent_enabled.load()), m_policy.EmergencyHint(), true);
        if (result == LaxoriRcResult::Success)
        {
            result = m_engine.GetGrowthResult();
        }
    }
    LeaveCollectionLock(cooperative);
    if (result == LaxoriRcResult::OutOfMemory)
    {
        Unsupported("LAXORI growth could not admit the minimum native planes or commit requested storage.");
    }
    return result;
}

void LaxoriGcHeap::RetireAllocationSpan(gc_alloc_context* context, bool subtractUnused)
{
    if (context == nullptr || context->alloc_ptr == nullptr)
    {
        return;
    }
    LaxoriRcMutator* mutator = m_engine.FindCachedMutator(context->gc_reserved_1, context);
    bool privateBuffer = mutator != nullptr && mutator->Region().buffer;
    if (mutator == nullptr || context->alloc_limit < context->alloc_ptr ||
        m_engine.RetireAllocationSpan(mutator,
            reinterpret_cast<uintptr_t>(context->alloc_ptr) - sizeof(ObjHeader)) != LaxoriRcResult::Success)
    {
        Fatal("LAXORI allocation span contains a foreign or inconsistent used point.");
    }
    if (privateBuffer && m_verify_private)
    {
        VerifyRegion(mutator->Region(), "retire");
    }
    size_t unused = static_cast<size_t>(context->alloc_limit - context->alloc_ptr);
    m_unused_span_bytes.fetch_add(unused, std::memory_order_relaxed);
    if (subtractUnused)
    {
        context->alloc_bytes -= static_cast<int64_t>(unused);
        m_context_allocated_bytes.fetch_sub(unused, std::memory_order_relaxed);
    }
    context->alloc_ptr = nullptr;
    context->alloc_limit = nullptr;
    SetCombinedLimit(context, nullptr);
}

void LaxoriGcHeap::RetireAllocationContext(gc_alloc_context* context, void* heap)
{
    static_cast<LaxoriGcHeap*>(heap)->RetireAllocationSpan(context, true);
}

// Logs the old value of a field that has not been logged in this epoch. The old value is read BEFORE the claim in
// LogBeforeWrite: a racing writer that loses the claim stores only after the winner claimed, and the winner has
// read the field by then.
void LaxoriGcHeap::LogOldValue(Object** slot)
{
    LaxoriGcHeap* heap = s_heap;
    Object* oldTarget = VolatileLoad(slot);
    LaxoriRcMutator* mutator = heap->FindOrAttach(GCToEEInterface::GetAllocContext(), true);
    if (heap->m_engine.LogBeforeWrite(mutator, slot, oldTarget) != LaxoriRcResult::Success)
    {
        heap->Fatal("LAXORI pre-store logging failed before the managed field was overwritten.");
    }
}

void LaxoriGcHeap::BarrierSlowPath(Object** slot)
{
    LogOldValue(slot);
}

// Logs every field of the range whose unlog bit is set. The side metadata has one bit for every 8-byte slot, so a
// metadata byte covers eight fields; a range of fields that are all logged costs one byte load for eight slots, and
// eight metadata bytes (64 fields) are tested with one load. The mutator is resolved once for the whole range.
void LaxoriGcHeap::LogOldValues(Object** destination, size_t count)
{
    LaxoriGcHeap* heap = s_heap;
    LaxoriRcMutator* mutator = nullptr;
    const uint8_t* metadata = reinterpret_cast<const uint8_t*>(g_laxori_write_barrier_globals.metadata_base);
    uintptr_t first = reinterpret_cast<uintptr_t>(destination) >> 3;
    uintptr_t end = first + count;
    for (uintptr_t index = first; index < end;)
    {
        uintptr_t byteIndex = index >> 3;
        unsigned startBit = static_cast<unsigned>(index & 7);
        if (startBit == 0 && (byteIndex & 7) == 0 && end - index >= 64 &&
            VolatileLoad(reinterpret_cast<const uint64_t*>(metadata + byteIndex)) == 0)
        {
            index += 64;
            continue;
        }
        unsigned endBit = (byteIndex == ((end - 1) >> 3)) ? static_cast<unsigned>(((end - 1) & 7) + 1) : 8;
        unsigned bits = VolatileLoad(metadata + byteIndex);
        bits &= (0xFFu << startBit) & ((1u << endBit) - 1);
        for (unsigned bit = startBit; bits != 0 && bit < endBit; bit++)
        {
            if ((bits & (1u << bit)) != 0)
            {
                Object** slot = reinterpret_cast<Object**>(((byteIndex << 3) + bit) << 3);
                Object* oldTarget = VolatileLoad(slot);
                if (mutator == nullptr)
                {
                    mutator = heap->FindOrAttach(GCToEEInterface::GetAllocContext(), true);
                }
                if (heap->m_engine.LogBeforeWrite(mutator, slot, oldTarget) != LaxoriRcResult::Success)
                {
                    heap->Fatal("LAXORI pre-store logging failed before the managed field was overwritten.");
                }
            }
        }
        index = (byteIndex + 1) << 3;
    }
}

bool LaxoriGcHeap::IsInHeap(void*, void* address)
{
    return reinterpret_cast<uintptr_t>(address) - g_laxori_write_barrier_globals.heap_low <
        g_laxori_write_barrier_globals.heap_size;
}

void LaxoriGcHeap::PreStore(Object** destination, Object* newReference)
{
    // A store of a private reference to a place outside the heap (the stack slot of the sync-block table, a handle, native memory)
    // makes the object reachable for the runtime, like a store into the shared heap: the object escapes (Satori's
    // CheckEscapeSatori does the same). A sync block is created through this call, so an object that has one is always escaped
    // and the thread-local collector needs no walk of the region to find it.
    if (s_heap->m_private_enabled && s_heap->m_test_control != 7)
    {
        s_heap->EscapeCheck(destination, newReference);
    }
    if (!IsInHeap(nullptr, destination))
    {
        return;
    }
    const uint8_t* metadata = reinterpret_cast<const uint8_t*>(g_laxori_write_barrier_globals.metadata_base);
    uintptr_t address = reinterpret_cast<uintptr_t>(destination);
    if ((VolatileLoad(metadata + (address >> 6)) >> ((address >> 3) & 7) & 1) != 0)
    {
        LogOldValue(destination);
    }
}

// LAXORI logs before the store, so the post-store hook of the PR has nothing to do.
void LaxoriGcHeap::NoPostStoreBarrier(void*, Object**, Object*)
{
}

void LaxoriGcHeap::LogOldReferences(void* destination, size_t length)
{
    uintptr_t address = reinterpret_cast<uintptr_t>(destination);
    if (length == 0 || !IsInHeap(nullptr, destination))
    {
        return;
    }
    if (length > g_laxori_write_barrier_globals.heap_size - (address - g_laxori_write_barrier_globals.heap_low))
    {
        Fatal("LAXORI bulk write exceeds its heap reservation.");
    }
    uintptr_t end = address + length;
    const uint8_t* metadata = reinterpret_cast<const uint8_t*>(g_laxori_write_barrier_globals.metadata_base);
    uintptr_t firstSlot = address >> 3;
    uintptr_t endSlot = ((end - 1) >> 3) + 1;
    bool unlogged = false;
    for (uintptr_t index = firstSlot; index < endSlot;)
    {
        uintptr_t byteIndex = index >> 3;
        unsigned startBit = static_cast<unsigned>(index & 7);
        if (startBit == 0 && (byteIndex & 7) == 0 && endSlot - index >= 64)
        {
            // Eight metadata bytes (64 fields) in one load, as the Java LAXORI tests the range (barrier.rs).
            if (VolatileLoad(reinterpret_cast<const uint64_t*>(metadata + byteIndex)) != 0)
            {
                unlogged = true;
                break;
            }
            index += 64;
            continue;
        }
        unsigned endBit = byteIndex == ((endSlot - 1) >> 3)
            ? static_cast<unsigned>(((endSlot - 1) & 7) + 1) : 8;
        unsigned mask = (0xFFu << startBit) & ((1u << endBit) - 1);
        if ((VolatileLoad(metadata + byteIndex) & mask) != 0)
        {
            unlogged = true;
            break;
        }
        index = (byteIndex + 1) << 3;
    }
#ifdef LAXORI_HAS_DIAGNOSTIC_PROBES
    bool diagnostics = m_storage_diagnostics_enabled.load(std::memory_order_relaxed);
#endif // LAXORI_HAS_DIAGNOSTIC_PROBES
    if (!unlogged)
    {
        // Fresh allocation can have no valid MethodTable/array length yet. Do not inspect its layout.
#ifdef LAXORI_HAS_DIAGNOSTIC_PROBES
        if (diagnostics)
        {
            m_bulk_metadata_early_outs.fetch_add(1, std::memory_order_relaxed);
        }
#endif // LAXORI_HAS_DIAGNOSTIC_PROBES
        return;
    }
#ifdef LAXORI_HAS_DIAGNOSTIC_PROBES
    if (diagnostics)
    {
        m_bulk_layout_lookups.fetch_add(1, std::memory_order_relaxed);
    }
#endif // LAXORI_HAS_DIAGNOSTIC_PROBES
    Object* object = nullptr;
    if (m_engine.FindContainingObject(address, &object, false) != LaxoriRcResult::Success)
    {
        Fatal("LAXORI cannot find the old bulk destination object.");
    }
    uintptr_t objectAddress = reinterpret_cast<uintptr_t>(object);
    GCReferenceObjectLayout layout = GetGCReferenceObjectLayout(object, object->GetGCSafeMethodTable());
    if (layout.objectSize < sizeof(ObjHeader) + sizeof(Object*) ||
        address < objectAddress + sizeof(Object*) ||
        address - objectAddress > layout.objectSize - sizeof(ObjHeader) ||
        length > layout.objectSize - sizeof(ObjHeader) - (address - objectAddress))
    {
        Fatal("LAXORI bulk write exceeds its destination object.");
    }
    GCReferenceRangeIterator iterator(object);
    uintptr_t alignedStart = address & ~(sizeof(Object*) - 1);
    uintptr_t alignedEnd = (end + sizeof(Object*) - 1) & ~(sizeof(Object*) - 1);
    iterator.RestrictToRange(alignedStart - objectAddress, alignedEnd - alignedStart);
    GCReferenceRange range;
    while (iterator.Next(&range))
    {
        uintptr_t start = reinterpret_cast<uintptr_t>(range.start);
        if (start < address || range.count > (end - start) / sizeof(Object*))
        {
            Fatal("LAXORI bulk write would partially overwrite an old reference.");
        }
        LogOldValues(range.start, range.count);
    }
}

void LaxoriGcHeap::BulkMoveWithWriteBarrier(void* context, void* destination, const void* source, size_t length)
{
    if (destination == source || length == 0)
    {
        return;
    }
    LaxoriGcHeap* heap = static_cast<LaxoriGcHeap*>(context);
    if (heap->m_private_enabled)
    {
        heap->EscapeBulk(destination, source, length);
    }
#ifdef LAXORI_HAS_DIAGNOSTIC_PROBES
    if (heap->m_storage_diagnostics_enabled.load(std::memory_order_relaxed))
    {
        heap->m_bulk_move_calls.fetch_add(1, std::memory_order_relaxed);
    }
#endif // LAXORI_HAS_DIAGNOSTIC_PROBES
    heap->LogOldReferences(destination, length);
    uint8_t* to = static_cast<uint8_t*>(destination);
    const uint8_t* from = static_cast<const uint8_t*>(source);
    if (reinterpret_cast<uintptr_t>(destination) - reinterpret_cast<uintptr_t>(source) >= length)
    {
        while (length != 0 && (reinterpret_cast<uintptr_t>(to) & (sizeof(uintptr_t) - 1)) != 0)
        {
            *to++ = *from++;
            length--;
        }
        while (length >= sizeof(uintptr_t))
        {
            uintptr_t value;
            memcpy(&value, from, sizeof(value));
            VolatileStore(reinterpret_cast<uintptr_t*>(to), value);
            to += sizeof(uintptr_t);
            from += sizeof(uintptr_t);
            length -= sizeof(uintptr_t);
        }
        while (length-- != 0)
        {
            *to++ = *from++;
        }
    }
    else
    {
        to += length;
        from += length;
        while (length != 0 && (reinterpret_cast<uintptr_t>(to) & (sizeof(uintptr_t) - 1)) != 0)
        {
            *--to = *--from;
            length--;
        }
        while (length >= sizeof(uintptr_t))
        {
            to -= sizeof(uintptr_t);
            from -= sizeof(uintptr_t);
            uintptr_t value;
            memcpy(&value, from, sizeof(value));
            VolatileStore(reinterpret_cast<uintptr_t*>(to), value);
            length -= sizeof(uintptr_t);
        }
        while (length-- != 0)
        {
            *--to = *--from;
        }
    }
}

void LaxoriGcHeap::BulkClearWithWriteBarrier(void* context, void* destination, size_t length)
{
    if (length == 0)
    {
        return;
    }
    LaxoriGcHeap* heap = static_cast<LaxoriGcHeap*>(context);
#ifdef LAXORI_HAS_DIAGNOSTIC_PROBES
    if (heap->m_storage_diagnostics_enabled.load(std::memory_order_relaxed))
    {
        heap->m_bulk_clear_calls.fetch_add(1, std::memory_order_relaxed);
    }
#endif // LAXORI_HAS_DIAGNOSTIC_PROBES
    heap->LogOldReferences(destination, length);
    uint8_t* cursor = static_cast<uint8_t*>(destination);
    while (length != 0 && (reinterpret_cast<uintptr_t>(cursor) & (sizeof(uintptr_t) - 1)) != 0)
    {
        *cursor++ = 0;
        length--;
    }
    while (length >= sizeof(uintptr_t))
    {
        VolatileStore(reinterpret_cast<uintptr_t*>(cursor), static_cast<uintptr_t>(0));
        cursor += sizeof(uintptr_t);
        length -= sizeof(uintptr_t);
    }
    while (length-- != 0)
    {
        *cursor++ = 0;
    }
}

// Entry of the assembly write barrier slow path. The assembly saves the whole vector state around this call.
extern "C" void LaxoriBarrierSlowPath(Object** slot)
{
    LaxoriGcHeap::BarrierSlowPath(slot);
}

void LaxoriGcHeap::FixAllocContext(gc_alloc_context* context, void*, void*)
{
    // The EE already charged the departing tail to dead_threads_non_alloc_bytes.
    RetireAllocationSpan(context, false);
    LaxoriRcMutator* mutator = m_engine.FindMutator(context);
    if (mutator != nullptr && m_engine.DetachMutator(mutator) != LaxoriRcResult::Success)
    {
        Fatal("LAXORI thread-detach buffer publication failed.");
    }
    context->gc_reserved_1 = nullptr;
    if (!m_shutdown.load() && m_engine.HasPendingDetaches() &&
        CollectPrototype(LaxoriPauseKind::RefCount) != LaxoriRcResult::Success)
    {
        Fatal("LAXORI detached allocator retirement failed before TLS reset.");
    }
}

bool LaxoriGcHeap::EnterCollectionLock()
{
    LaxoriPauseTimers& timers = m_engine.GetPauseTimers();
    int64_t waitStart = timers.Enabled() ? timers.Stamp() : 0;
    bool cooperative = GCToEEInterface::EnablePreemptiveGC();
    m_collection_lock.Enter();
    GCToEEInterface::DisablePreemptiveGC();
    timers.Begin(waitStart != 0 ? timers.Stamp() - waitStart : 0);
    return cooperative;
}

void LaxoriGcHeap::LeaveCollectionLock(bool cooperative)
{
    m_engine.GetPauseTimers().End();
    GCToEEInterface::EnablePreemptiveGC();
    m_collection_lock.Leave();
    if (cooperative)
    {
        GCToEEInterface::DisablePreemptiveGC();
    }
}

LaxoriRcResult LaxoriGcHeap::CollectLocked(LaxoriPauseKind kind, bool forcedBlocking, bool waitForLazy)
{
    if (m_shutdown.load(std::memory_order_acquire))
    {
        return LaxoriRcResult::InvalidPhase;
    }
    LaxoriRcResult hint = UpdatePolicyHints();
    if (hint != LaxoriRcResult::Success)
    {
        return hint;
    }
    if (kind == LaxoriPauseKind::InitialMark && m_engine.GetTracePhase() != LaxoriTracePhase::Inactive)
    {
        kind = LaxoriPauseKind::RefCount;
    }
    if (kind == LaxoriPauseKind::FinalMark && m_engine.GetTracePhase() != LaxoriTracePhase::Concurrent)
    {
        return m_engine.Drain(Timeout);
    }
    m_last_gc_start.store(GetNow());
    m_current_pause_kind = kind;
    m_forced_blocking = forcedBlocking;
    m_heap_before = m_last_heap_size.load();
    m_fragmentation_before = m_last_fragmentation.load();
    if (kind == LaxoriPauseKind::InitialMark)
    {
        m_trace_heap_before = m_heap_before;
        m_trace_fragmentation_before = m_fragmentation_before;
        m_concurrent_done.Reset();
    }
    LaxoriRcResult result;
    m_engine.GetPauseTimers().MarkPause(static_cast<uint8_t>(kind));
    switch (kind)
    {
        case LaxoriPauseKind::InitialMark: result = m_engine.BeginTrace(CaptureRoots, this, Timeout); break;
        case LaxoriPauseKind::FinalMark: result = m_engine.FinishTrace(CaptureRoots, this, Timeout); break;
        case LaxoriPauseKind::Full: result = m_engine.CollectFull(CaptureRoots, this, Timeout); break;
        default: result = m_engine.Collect(CaptureRoots, this, Timeout); break;
    }
    if (result == LaxoriRcResult::Success && kind != LaxoriPauseKind::InitialMark)
    {
        // P26 (F8). The Java LAXORI runs the lazy decrements and the lazy sweep on its workers while the program runs, and
        // nothing waits for them (plan/laxori/global.rs: schedule_collection only logs when the previous lazy jobs are not
        // finished). So the thread that made the pause goes back to the program at once. It waits for the lazy work only
        // when a decision needs its result: a Full pause or a forced or emergency collection (the Java LAXORI does their
        // decrements inside the pause), a caller that asks for it (a wait for the concurrent collection, a growth), the
        // next collection (UpdatePolicyHints, because the log banks are reused) and an allocation that found no memory
        // (Alloc waits for the lazy sweep before it collects). LAXORIWaitForLazy restores the old wait after every pause.
        bool wait = waitForLazy || forcedBlocking || kind == LaxoriPauseKind::Full || m_wait_for_lazy;
#if defined(LAXORI_SIDE_METADATA_TEST_PLATFORM) && defined(LAXORI_F8_WAIT_CONTROL)
        wait = true;
#endif // LAXORI_SIDE_METADATA_TEST_PLATFORM && LAXORI_F8_WAIT_CONTROL
        if (wait)
        {
            LaxoriPhaseScope postDrain(m_engine.GetPauseTimers(), LaxoriPauseTimers::PostDrain);
            result = m_engine.GetTracePhase() == LaxoriTracePhase::Concurrent
                ? LaxoriRcResult::Success : m_engine.Drain(Timeout);
        }
    }
    m_last_gc_duration.store(GetNow() - m_last_gc_start.load());
    if (result == LaxoriRcResult::Success && m_engine.GetTracePhase() == LaxoriTracePhase::Inactive)
    {
        m_concurrent_done.Set();
    }
    if (kind == LaxoriPauseKind::InitialMark)
    {
        m_trace_signal.Set();
    }
    return result;
}

LaxoriRcResult LaxoriGcHeap::CollectPrototype(LaxoriPauseKind kind, bool waitForLazy)
{
    bool cooperative = EnterCollectionLock();
    LaxoriRcResult result = CollectLocked(kind, false, waitForLazy);
    LeaveCollectionLock(cooperative);
    return result;
}

HRESULT LaxoriGcHeap::GarbageCollect(int, bool, int)
{
    if (CollectForPressure(false, true) != LaxoriRcResult::Success)
    {
        Fatal("LAXORI explicit full collection failed.");
    }
    return S_OK;
}

LaxoriCollectionCause LaxoriGcHeap::GetCollectionCause(bool heapFull) const
{
    size_t used = 0;
    size_t headroom = 0;
    if (m_engine.GetUsedPages(&used) != LaxoriRcResult::Success ||
        m_engine.GetDefragHeadroomBytes(&headroom) != LaxoriRcResult::Success)
    {
        Fatal("LAXORI could not read actual capacity for collection pressure.");
    }
    LaxoriPolicyPressure pressure{
        heapFull, m_engine.GetTracePhase() == LaxoriTracePhase::Concurrent, m_engine.IsTraceExhausted(),
        m_engine.GetYoungAllocationBytes(), m_engine.GetPendingIncrementEntries(),
        m_engine.GetCleanNurseryBlocks(), true, 0,
        used, m_engine.GetAdmittedBytes() / LaxoriLargeObjectSpace::PageBytes,
        m_heap_configuration.evacuateNursery ? 0 : GetCollectionReservedPages(headroom),
    };
    if (m_heap_configuration.evacuateNursery)
    {
        pressure.copyReservePages = GetCollectionReservedPages(headroom);
        pressure.immixFreePages = m_engine.GetImmixFreePages();
        // With overshoot the pause takes the to-space it needs from the reservation. The heap is full when no block is
        // free, and no reserve is kept. A hard limit disables overshoot in the engine, and then this stays as before.
        pressure.overshoot = m_engine.IsOvershootEnabled();
    }
    return m_policy.Trigger(pressure);
}

size_t LaxoriGcHeap::GetCollectionReservedPages(size_t headroomBytes) const
{
    if (m_heap_configuration.evacuateNursery)
    {
        // The young pause copies the survivors of clean nursery blocks into admitted free blocks, so they
        // must exist when it starts. Young objects in recycled blocks are promoted in place and need none.
        return m_policy.CollectionReservedPagesForCopy(
            m_engine.GetCleanNurseryBlocks() * ImmixBlockGeometry::BlockBytes, LaxoriLargeObjectSpace::PageBytes);
    }
    return m_policy.CollectionReservedPages(
        m_engine.GetCleanNurseryBlocks() * ImmixBlockGeometry::BlockBytes,
        headroomBytes / LaxoriLargeObjectSpace::PageBytes);
}

LaxoriRcResult LaxoriGcHeap::UpdatePolicyHints()
{
    LaxoriPhaseScope scope(m_engine.GetPauseTimers(), LaxoriPauseTimers::PolicyDrain);
    return m_engine.DrainLazySweep(Timeout);
}

LaxoriRcResult LaxoriGcHeap::FinishLazy(LaxoriReferenceCounting* engine, void* context)
{
    LaxoriGcHeap* heap = static_cast<LaxoriGcHeap*>(context);
    // The engine cleared its cycle-sweep flag before this callback, so the finalizers may run now.
    if (heap->m_finalization_deferred.exchange(false, std::memory_order_seq_cst))
    {
        GCToEEInterface::EnableFinalization(heap->m_registry.GetPendingCount() != 0);
    }
    // The lazy sweep of a FinalMark pause is no longer awaited by the pause (F8), so the end of the concurrent collection
    // is announced here, when the sweep has cleared the trace phase.
    if (engine->GetTracePhase() == LaxoriTracePhase::Inactive)
    {
        heap->m_concurrent_done.Set();
    }
    size_t mature;
    LaxoriRcResult result = engine->GetOccupiedPages(&mature);
    if (result != LaxoriRcResult::Success)
    {
        return result;
    }
    {
        // The mature live size for the heap sizer: what the sweep left occupied, without the young data that the program
        // has allocated since the pause.
        size_t occupied = engine->GetImmixOccupiedBytes();
        size_t young = engine->GetYoungAllocationBytes();
        heap->m_mature_live_bytes.store(occupied > young ? occupied - young : 0, std::memory_order_relaxed);
        engine->SetBorrowCeiling(heap->PolicyReserveBytes());
    }
    if (!heap->m_policy.FinishLazySweep(
            heap->m_current_pause_kind, engine->GetAdmittedBytes() / LaxoriLargeObjectSpace::PageBytes, mature,
            engine->GetTracePhase() == LaxoriTracePhase::Concurrent, heap->m_concurrent_enabled.load(), true))
    {
        return LaxoriRcResult::InvariantViolation;
    }
    if (heap->m_policy.CycleHint())
    {
        // The next collection starts a trace and clears the ObjectMark plane. Let the workers clear it now (Java LAXORI:
        // decide_next_gc_may_perform_cycle_collection).
        engine->ScheduleMarkClear();
    }
    // The lazy sweep has counted the free lines of the blocks that the pause promoted, so the figures are complete now.
    size_t heapBytes;
    size_t fragmentation;
    result = engine->GetHeapStatistics(&heapBytes, &fragmentation);
    if (result == LaxoriRcResult::Success)
    {
        heap->m_last_heap_size.store(heapBytes);
        heap->m_last_fragmentation.store(fragmentation);
    }
    return result;
}

LaxoriRcResult LaxoriGcHeap::CollectForPressure(bool emergency, bool user, bool heapFull)
{
    bool cooperative = EnterCollectionLock();
    LaxoriRcResult result = UpdatePolicyHints();
    LaxoriCollectionCause cause = GetCollectionCause(heapFull || emergency);
    if (result == LaxoriRcResult::Success && (emergency || user || cause != LaxoriCollectionCause::None))
    {
        size_t capacity = m_engine.GetImmixCapacityBytes();
        size_t target = capacity;
        if (!emergency && !user && cause == LaxoriCollectionCause::HeapFull)
        {
            // The nursery is as large as the free capacity, so a small capacity means many collections. Let the heap
            // sizer raise the capacity in this pause (laxori_heap_sizing.h).
            target = m_sizer.TargetCapacity(
                capacity, m_mature_live_bytes.load(std::memory_order_relaxed), PolicyReserveBytes());
            if (target > capacity && m_engine.RequestImmixCapacity(target) == LaxoriRcResult::Success)
            {
                m_sizing_growths.fetch_add(1, std::memory_order_relaxed);
            }
        }
        m_cause_counts[static_cast<size_t>(cause)].fetch_add(1, std::memory_order_relaxed);
        if (m_policy_trace)
        {
            size_t used = 0;
            m_engine.GetUsedPages(&used);
            fprintf(stderr,
                "LAXORIPOLICY gc=%u cause=%u emergency=%d user=%d heapFull=%d capK=%zu admittedK=%zu usedK=%zu "
                "freeK=%zu reserveK=%zu occupiedK=%zu nonFreeBlocks=%zu youngK=%zu cleanBlocks=%zu liveK=%zu "
                "budgetK=%zu targetK=%zu\n",
                m_collections.load() + 1, static_cast<unsigned>(cause), emergency, user, heapFull, capacity >> 10,
                m_engine.GetAdmittedBytes() >> 10, used << 2, m_engine.GetImmixFreePages() << 2,
                GetCollectionReservedPages(0) << 2, m_engine.GetImmixOccupiedBytes() >> 10, m_engine.GetNonFreeBlocks(),
                m_engine.GetYoungAllocationBytes() >> 10, m_engine.GetCleanNurseryBlocks(),
                m_mature_live_bytes.load() >> 10, m_sizer.BudgetBytes() >> 10, target >> 10);
        }
        m_collection_cause.store(cause, std::memory_order_release);
        result = CollectLocked(m_policy.Select(
            m_engine.GetTracePhase() == LaxoriTracePhase::Concurrent,
            m_engine.IsTraceExhausted(), emergency, user, m_concurrent_enabled.load()),
            emergency || user || m_policy.EmergencyHint());
    }
    LeaveCollectionLock(cooperative);
    return result;
}

void LaxoriGcHeap::Suspend(void* context)
{
    LaxoriGcHeap* heap = static_cast<LaxoriGcHeap*>(context);
    LaxoriPhaseScope suspend(heap->m_engine.GetPauseTimers(), LaxoriPauseTimers::SuspendRuntime);
    heap->PrepareScanExecutor();
    heap->m_in_gc.store(true, std::memory_order_release);
    heap->m_gc_done.Reset();
    heap->m_pause_start = minipal_hires_ticks();
    {
        LaxoriPhaseScope suspendEE(heap->m_engine.GetPauseTimers(), LaxoriPauseTimers::SuspendEE);
        GCToEEInterface::SuspendEE(SUSPEND_FOR_GC);
    }
    GCToEEInterface::GcEnumAllocContexts(RetireAllocationContext, heap);
    heap->m_pause_young_bytes.store(heap->m_engine.GetYoungAllocationBytes(), std::memory_order_release);
    {
        size_t headroom = 0;
        if (heap->m_engine.GetDefragHeadroomBytes(&headroom) == LaxoriRcResult::Success)
        {
            heap->m_pause_free_pages.store(heap->m_engine.GetImmixFreePages(), std::memory_order_release);
            heap->m_pause_reserve_pages.store(
                heap->GetCollectionReservedPages(headroom), std::memory_order_release);
            if (heap->m_heap_configuration.evacuateNursery && heap->m_engine.IsOvershootEnabled())
            {
                // The engine admits the missing blocks right after it has flushed the mutators.
                heap->m_engine.RequestBorrow(heap->m_pause_reserve_pages.load(std::memory_order_acquire));
            }
        }
    }
    if (!heap->m_policy.BeginPause(
            heap->m_pause_young_bytes.load(std::memory_order_acquire),
            heap->m_engine.GetCleanNurseryBlocks() * ImmixBlockGeometry::BlockBytes))
    {
        heap->Fatal("LAXORI survival predictor allocation snapshot was not retired.");
    }
    heap->m_collections.fetch_add(1, std::memory_order_relaxed);
    GCToEEInterface::GcStartWork(2, 2);
}

void LaxoriGcHeap::Resume(bool finished, void* context)
{
    LaxoriGcHeap* heap = static_cast<LaxoriGcHeap*>(context);
    if (heap->m_lookup_stats)
    {
        fprintf(stderr, "LAXORILOOKUP pause=%u calls=%llu ticks=%llu spans=%llu checked=%llu freq=%llu\n",
            heap->m_collections.load(), static_cast<unsigned long long>(heap->m_engine.GetLookupMetric(0)),
            static_cast<unsigned long long>(heap->m_engine.GetLookupMetric(1)),
            static_cast<unsigned long long>(heap->m_engine.GetLookupMetric(2)),
            static_cast<unsigned long long>(heap->m_engine.GetLookupMetric(3)),
            static_cast<unsigned long long>(minipal_hires_tick_frequency()));
    }
    LaxoriPauseTimers& timers = heap->m_engine.GetPauseTimers();
#ifdef LAXORI_HAS_PHASE_TIMERS
    if (heap->m_window_stats &&
        (heap->m_current_pause_kind == LaxoriPauseKind::FinalMark || heap->m_current_pause_kind == LaxoriPauseKind::Full))
    {
        LaxoriTraceMetrics trace = heap->m_engine.GetTraceMetrics();
        LaxoriRcMetrics rc = heap->m_engine.GetMetrics();
        fprintf(stderr,
            "LAXORIWINDOW pause=%u kind=%u initial=%llu final=%llu full=%llu tracePauses=%llu deaths=%llu "
            "first_deaths=%llu first_marked=%llu first_children=%llu later_deaths=%llu later_marked=%llu "
            "later_children=%llu deletion_marks=%llu satb=%llu\n",
            heap->m_collections.load(), static_cast<unsigned>(heap->m_current_pause_kind),
            static_cast<unsigned long long>(trace.initialMarks), static_cast<unsigned long long>(trace.finalMarks),
            static_cast<unsigned long long>(trace.fullMarks), static_cast<unsigned long long>(trace.tracePauses),
            static_cast<unsigned long long>(rc.deaths), static_cast<unsigned long long>(trace.firstEpochDeaths),
            static_cast<unsigned long long>(trace.firstEpochMarked),
            static_cast<unsigned long long>(trace.firstEpochChildren),
            static_cast<unsigned long long>(trace.laterEpochDeaths),
            static_cast<unsigned long long>(trace.laterEpochMarked),
            static_cast<unsigned long long>(trace.laterEpochChildren),
            static_cast<unsigned long long>(trace.deletionMarks), static_cast<unsigned long long>(trace.satbEntries));
    }
#endif // LAXORI_HAS_PHASE_TIMERS
    {
        LaxoriPhaseScope resume(timers, LaxoriPauseTimers::ResumeRuntime);
        // A tracing pause leaves a lazy sweep of dead cycles. It reads the type data of dead objects, which the runtime
        // frees only from a finalizer, so the finalizer thread is signalled when the sweep ends (FinishLazy). The flag
        // is set before the check, so exactly one of Resume and FinishLazy sends the signal.
        heap->m_finalization_deferred.store(true, std::memory_order_seq_cst);
        if (!heap->m_engine.IsCycleSweepPending() &&
            heap->m_finalization_deferred.exchange(false, std::memory_order_seq_cst))
        {
            GCToEEInterface::EnableFinalization(heap->m_registry.GetPendingCount() != 0);
        }
        GCToEEInterface::GcDone(2);
        GCToEEInterface::RestartEE(finished);
    }
    heap->m_in_gc.store(false, std::memory_order_release);
    int64_t elapsed = minipal_hires_ticks() - heap->m_pause_start;
    timers.Add(LaxoriPauseTimers::StwPause, elapsed);
    int64_t frequency = minipal_hires_tick_frequency();
    int64_t ticks = elapsed / frequency * 10000000 + elapsed % frequency * 10000000 / frequency;
    heap->m_last_pause_ticks.store(ticks);
    heap->m_total_pause_ticks.fetch_add(ticks);
    if (heap->m_current_pause_kind == LaxoriPauseKind::InitialMark)
    {
        heap->m_initial_pause_ticks = static_cast<uint64_t>(ticks);
    }
    else
    {
        heap->PublishMemoryInfo();
    }
    heap->m_gc_done.Set();
}

void LaxoriGcHeap::CoordinateTrace(void* context)
{
    LaxoriGcHeap* heap = static_cast<LaxoriGcHeap*>(context);
    while (!heap->m_shutdown.load(std::memory_order_acquire))
    {
        heap->m_trace_signal.Wait(10, false);
        if (!heap->m_shutdown.load() && heap->m_initialized.load() && heap->m_engine.IsTraceExhausted())
        {
            LaxoriRcResult result = heap->CollectPrototype(LaxoriPauseKind::FinalMark);
            if (result != LaxoriRcResult::Success &&
                !(result == LaxoriRcResult::InvalidPhase && heap->m_shutdown.load()))
            {
                heap->Fatal("LAXORI automatic FinalMark transition failed.");
            }
        }
    }
    heap->m_coordinator_done.Set();
}

void LaxoriGcHeap::Shutdown()
{
    if (!m_initialized.load() || m_shutdown.exchange(true))
    {
        return;
    }
#ifdef TARGET_WINDOWS
    if (m_process_exiting())
    {
        // ExitProcess has terminated peer threads before DLL_PROCESS_DETACH. Their events need not have been signaled.
        return;
    }
#endif // TARGET_WINDOWS
    m_trace_signal.Set();
    bool cooperative = GCToEEInterface::EnablePreemptiveGC();
    if (m_coordinator_done.Wait(Timeout, false) != WAIT_OBJECT_0)
    {
        Fatal("LAXORI shutdown could not stop its trace coordinator.");
    }
    // An admitted collection may still need the scan executor until it releases this lock.
    m_collection_lock.Enter();
    if (m_engine.StopWorkers() != LaxoriRcResult::Success)
    {
        Fatal("LAXORI shutdown could not drain its native readers.");
    }
    m_scan_stopping.store(true, std::memory_order_release);
    m_scan_signal.Set();
    if (m_scan_exited.Wait(Timeout, false) != WAIT_OBJECT_0)
    {
        Fatal("LAXORI shutdown could not stop its scan executor.");
    }
    m_collection_lock.Leave();
    if (cooperative)
    {
        GCToEEInterface::DisablePreemptiveGC();
    }
    // The EE may still have live background-thread references. Reservations outlive Shutdown, as in the GC event contract.
}

LaxoriRcResult LaxoriGcHeap::RunScan(uint8_t kind)
{
    ScanCostScope dispatch(this, 4 + kind);
    CountScanWork(kind);
    bool expected = false;
    if (kind > 3 || m_scan_stopping.load(std::memory_order_acquire) ||
        !m_scan_busy.compare_exchange_strong(expected, true, std::memory_order_acq_rel))
    {
        Fatal("LAXORI received an invalid, overlapping or reentrant scan operation.");
    }
    m_scan_failure.store(LaxoriRcResult::Success);
    m_scan_kind.store(kind, std::memory_order_release);
    m_scan_complete.store(0, std::memory_order_relaxed);
    m_scan_request.store(1, std::memory_order_seq_cst);
    // The executor sleeps only after it has raised this flag and has looked at the request once more, so one of the two
    // threads sees the other.
    if (m_scan_executor_sleeping.load(std::memory_order_seq_cst))
    {
        m_scan_signal.Set();
    }
    {
        ScanCostScope wait(this, 9);
        if (!WaitForScanComplete())
        {
            // Returning would let the caller resume/reclaim while the scan might still access its epoch.
            Fatal("LAXORI scan executor did not complete its operation.");
        }
    }
    LaxoriRcResult result = m_scan_failure.load(std::memory_order_acquire);
    m_scan_busy.store(false, std::memory_order_release);
    return result;
}

bool LaxoriGcHeap::WaitForScanComplete()
{
    int64_t end = m_scan_spin_ticks != 0 ? minipal_hires_ticks() + m_scan_spin_ticks : 0;
    while (end != 0)
    {
        if (m_scan_complete.load(std::memory_order_acquire) != 0)
        {
            return true;
        }
        YieldProcessor();
        if (minipal_hires_ticks() >= end)
        {
            break;
        }
    }
    for (;;)
    {
        m_scan_requester_sleeping.store(true, std::memory_order_seq_cst);
        if (m_scan_complete.load(std::memory_order_seq_cst) != 0)
        {
            m_scan_requester_sleeping.store(false, std::memory_order_relaxed);
            return true;
        }
        uint32_t waited = m_scan_done.Wait(Timeout, false);
        m_scan_requester_sleeping.store(false, std::memory_order_relaxed);
        if (m_scan_complete.load(std::memory_order_acquire) != 0)
        {
            return true;
        }
        if (waited != WAIT_OBJECT_0)
        {
            return false;
        }
        // A signal that an earlier request left behind: the executor had seen the flag, but the request had seen the answer.
    }
}

void LaxoriGcHeap::WaitForScanRequest(bool spin)
{
    for (;;)
    {
        int64_t end = spin && m_scan_spin_ticks != 0 ? minipal_hires_ticks() + m_scan_spin_ticks : 0;
        while (end != 0)
        {
            if (m_scan_request.load(std::memory_order_acquire) != 0 || m_scan_stopping.load(std::memory_order_acquire))
            {
                return;
            }
            YieldProcessor();
            if (minipal_hires_ticks() >= end)
            {
                break;
            }
        }
        m_scan_executor_sleeping.store(true, std::memory_order_seq_cst);
        if (m_scan_request.load(std::memory_order_seq_cst) != 0 || m_scan_stopping.load(std::memory_order_seq_cst))
        {
            m_scan_executor_sleeping.store(false, std::memory_order_relaxed);
            return;
        }
        if (m_scan_signal.Wait(INFINITE, false) != WAIT_OBJECT_0)
        {
            Fatal("LAXORI scan executor could not wait for work.");
        }
        m_scan_executor_sleeping.store(false, std::memory_order_relaxed);
        if (m_scan_request.load(std::memory_order_acquire) != 0 || m_scan_stopping.load(std::memory_order_acquire))
        {
            return;
        }
        // Woken without a request: a pause has started (PrepareScanExecutor), and its first scan comes soon.
        spin = true;
    }
}

void LaxoriGcHeap::PrepareScanExecutor()
{
    if (m_scan_executor_sleeping.load(std::memory_order_seq_cst))
    {
        m_scan_signal.Set();
    }
}

LaxoriRcResult LaxoriGcHeap::CaptureRoots(LaxoriReferenceCounting*, void* context)
{
    return static_cast<LaxoriGcHeap*>(context)->RunScan(0);
}

LaxoriRcResult LaxoriGcHeap::FinishTrace(LaxoriReferenceCounting*, void* context)
{
    return static_cast<LaxoriGcHeap*>(context)->RunScan(1);
}

LaxoriRcResult LaxoriGcHeap::FinishEpoch(LaxoriReferenceCounting*, void* context)
{
    return static_cast<LaxoriGcHeap*>(context)->RunScan(2);
}

LaxoriRcResult LaxoriGcHeap::FinishRelocation(LaxoriReferenceCounting*, void* context)
{
    return static_cast<LaxoriGcHeap*>(context)->RunScan(3);
}

void LaxoriGcHeap::ScanOnGcThread(void* context)
{
    LaxoriGcHeap* heap = static_cast<LaxoriGcHeap*>(context);
    // The executor remains alive for the EE's post-start utility-thread naming and readiness handoff.
    heap->m_scan_ready.Set();
    bool spin = false;
    for (;;)
    {
        heap->WaitForScanRequest(spin);
        if (heap->m_scan_stopping.load(std::memory_order_acquire))
        {
            break;
        }
        uint8_t kind = heap->m_scan_kind.load(std::memory_order_acquire);
        heap->m_scan_request.store(0, std::memory_order_relaxed);
        heap->ExecuteScan(kind);
        // The last scan of a pause (the epoch closure) is not followed by another one soon.
        spin = kind != 2;
        heap->m_scan_complete.store(1, std::memory_order_seq_cst);
        if (heap->m_scan_requester_sleeping.load(std::memory_order_seq_cst))
        {
            heap->m_scan_done.Set();
        }
    }
    heap->m_scan_exited.Set();
}

void LaxoriGcHeap::ExecuteScan(uint8_t kind)
{
    LaxoriGcHeap* heap = this;
    ScanCostScope execution(heap, 10 + kind);
    LaxoriPhaseScope scanTotal(heap->m_engine.GetPauseTimers(), LaxoriPauseTimers::Scan0 + kind);
    auto measure = [heap](size_t metric, size_t phase, const auto& callback)
    {
        ScanCostScope cost(heap, metric);
        LaxoriPhaseScope timer(heap->m_engine.GetPauseTimers(), phase);
        callback();
    };
    ScanContext scan{};
    scan.promotion = true;
    scan.thread_number = 0;
    scan.thread_count = 1;
    if (kind == 0)
    {
        bool timed = heap->m_storage_diagnostics_enabled.load(std::memory_order_relaxed);
        int64_t start = timed ? minipal_hires_ticks() : 0;
        measure(31, LaxoriPauseTimers::ScanBeforeRoots, [&]() { GCToEEInterface::BeforeGcScanRoots(2, false, false); });
        LaxoriRcResult result = LaxoriRcResult::Success;
        measure(14, LaxoriPauseTimers::ScanLeaseFinalizers, [&]() { heap->CaptureFinalizers(); });
        measure(15, LaxoriPauseTimers::ScanRoots, [&]() { GCScan::GcScanRoots(CopyRoot, 2, 2, &scan); });
        measure(16, LaxoriPauseTimers::ScanHandles, [&]() { GCScan::GcScanHandles(CopyRoot, 2, 2, &scan); });
        measure(17, LaxoriPauseTimers::ScanProfilerHandles, [&]() { GCScan::GcScanHandlesForProfilerAndETW(2, &scan, LeaseWeakHandle); });
        measure(18, LaxoriPauseTimers::ScanDependent, [&]()
        {
            // Profiler enumeration omits null-secondary primaries; the owning-slot visitor does not.
            scan.promotion = false;
            Ref_ScanDependentHandlesForRelocation(2, 2, &scan, LeaseDependentRoot);
            scan.promotion = true;
        });
        measure(19, LaxoriPauseTimers::ScanSyncBlock, [&]()
        {
            GCToEEInterface::SyncBlockCacheWeakPtrScan(LeaseSyncBlock,
                reinterpret_cast<uintptr_t>(&scan), reinterpret_cast<uintptr_t>(heap));
        });
        measure(20, LaxoriPauseTimers::ScanFrozen, [&]() { heap->ScanFrozenRoots(); });
        if (timed)
        {
            heap->m_root_capture_ticks.fetch_add(
                static_cast<uint64_t>(minipal_hires_ticks() - start),
                std::memory_order_relaxed);
        }
    }
    else if (kind == 1)
    {
        measure(21, LaxoriPauseTimers::ScanDhInitial, [&]()
        {
            GCScan::GcDhInitialScan(TraceRoot, 2, 2, &scan);
            heap->CountScanWork(32);
            while (GCScan::GcDhReScan(&scan)) { heap->CountScanWork(32); }
        });
        measure(22, LaxoriPauseTimers::ScanShortWeak, [&]() { GCScan::GcShortWeakPtrScan(2, 2, &scan); });
        measure(23, LaxoriPauseTimers::ScanQueueFinalizers, [&]() { heap->DecideFinalization(); });
        measure(25, LaxoriPauseTimers::ScanDhRescan, [&]()
        {
            GCScan::GcDhInitialScan(TraceRoot, 2, 2, &scan);
            heap->CountScanWork(32);
            while (GCScan::GcDhReScan(&scan)) { heap->CountScanWork(32); }
        });
        measure(26, LaxoriPauseTimers::ScanWeakPtr, [&]() { GCScan::GcWeakPtrScan(2, 2, &scan); });
        measure(27, LaxoriPauseTimers::ScanWeakPtrSingle, [&]() { GCScan::GcWeakPtrScanBySingleThread(2, 2, &scan); });
    }
    else if (kind == 3)
    {
        if (heap->m_heap_configuration.evacuateNursery || heap->m_heap_configuration.evacuateMature)
        {
            scan.promotion = false;
            measure(28, LaxoriPauseTimers::ScanRelocateRoots, [&]() { GCScan::GcScanRoots(RelocateRoot, 2, 2, &scan); });
            measure(29, LaxoriPauseTimers::ScanRelocateHandles, [&]() { GCScan::GcScanHandles(RelocateRoot, 2, 2, &scan); });
            heap->RelocateFinalizers();
            scan.promotion = true;
            GCToEEInterface::DiagWalkSurvivors(nullptr,
                heap->m_engine.GetNurseryMetrics().pauseCopiedBytes != 0 ||
                heap->m_engine.GetMatureMetrics().pauseCopiedBytes != 0);
        }
    }
    else
    {
        if (!heap->m_policy.RecordCopiedPromotion(heap->m_engine.GetNurseryMetrics().pauseCopiedBytes) ||
            !heap->m_policy.RecordSurvivors(heap->m_engine.GetPauseSurvivorBytes()) ||
            !heap->m_policy.ReleasePause())
        {
            heap->m_scan_failure.store(LaxoriRcResult::InvariantViolation);
        }
        measure(31, LaxoriPauseTimers::ScanPromotionsGranted, [&]()
        {
            GCScan::GcPromotionsGranted(2, 2, &scan);
            GCToEEInterface::AfterGcScanRoots(2, 2, &scan);
        });
        size_t heapBytes = 0;
        size_t fragmentation = 0;
        LaxoriRcResult result = LaxoriRcResult::Success;
        measure(30, LaxoriPauseTimers::ScanHeapStatistics, [&]() { result = heap->m_engine.GetHeapStatistics(&heapBytes, &fragmentation); });
        if (result == LaxoriRcResult::Success)
        {
            heap->m_last_heap_size.store(heapBytes);
            heap->m_last_fragmentation.store(fragmentation);
            heap->m_last_committed.store(heap->m_engine.GetCommittedBytes());
            heap->m_last_memory_load.store(heap->GetMemoryLoad());
        }
        else
        {
            heap->m_scan_failure.store(result);
        }
    }
}

void LaxoriGcHeap::AddRoot(Object* object, uint32_t flags, bool trace)
{
    if (object == nullptr)
    {
        return;
    }
    if (!m_engine.OwnsAddress(reinterpret_cast<uintptr_t>(object)))
    {
        if ((flags & GC_CALL_INTERIOR) != 0 || FrozenContains(reinterpret_cast<uintptr_t>(object), true))
        {
            return;
        }
        m_scan_failure.store(LaxoriRcResult::UnsupportedLifetime);
        return;
    }
    if ((flags & GC_CALL_INTERIOR) != 0)
    {
        Object* containing = nullptr;
        LaxoriRcResult result = m_engine.FindContainingObject(reinterpret_cast<uintptr_t>(object), &containing);
        if (result == LaxoriRcResult::InvalidArgument)
        {
            // No live object contains the address. The runtime lets a byref point one byte past the end of an object
            // (the end pointer of an array loop), and such a byref can outlive the object. The standard collector
            // ignores an interior pointer for which find_object finds nothing, and so does this one.
            return;
        }
        if (result != LaxoriRcResult::Success)
        {
            m_scan_failure.store(result);
            return;
        }
        object = containing;
    }
    if ((flags & GC_CALL_PINNED) != 0)
    {
        // The pin list keeps the object where it is for this pause. The list is not a table by address: its size
        // follows the number of pinned roots.
        LaxoriRcResult pinned = m_engine.AddPinnedRoot(object, false, 0);
        if (pinned != LaxoriRcResult::Success)
        {
            m_scan_failure.store(pinned);
            return;
        }
    }
    LaxoriRcResult result = m_engine.AddRoot(object, false, trace);
    if (result != LaxoriRcResult::Success)
    {
        m_scan_failure.store(result);
    }
}

void LaxoriGcHeap::CopyRoot(Object** slot, ScanContext*, uint32_t flags)
{
    s_heap->CountScanWork(36);
    s_heap->AddRoot(*slot, flags, true);
}

void LaxoriGcHeap::RelocateRoot(Object** slot, ScanContext*, uint32_t flags)
{
    s_heap->CountScanWork(37);
    LaxoriRcResult result = s_heap->m_engine.RelocateRoot(
        slot, (flags & GC_CALL_INTERIOR) != 0, (flags & GC_CALL_PINNED) != 0);
    if (result != LaxoriRcResult::Success)
    {
        s_heap->m_scan_failure.store(result);
    }
}

void LaxoriGcHeap::TraceRoot(Object** slot, ScanContext*, uint32_t flags)
{
    s_heap->CountScanWork(39);
    Object* object = *slot;
    if (object == nullptr || s_heap->FrozenContains(reinterpret_cast<uintptr_t>(object), true))
    {
        return;
    }
    if ((flags & GC_CALL_INTERIOR) != 0)
    {
        object = s_heap->GetContainingObject(object, false);
    }
    LaxoriRcResult result = s_heap->m_engine.TraceRoot(object);
    if (result != LaxoriRcResult::Success)
    {
        s_heap->m_scan_failure.store(result);
    }
}

void LaxoriGcHeap::LeaseWeakHandle(
    Object** slot, Object*, uint32_t flags, ScanContext*, bool)
{
    s_heap->CountScanWork(38);
    if ((flags & kEtwGCRootFlagsRefCounted) != 0 && (flags & kEtwGCRootFlagsWeakRef) == 0)
    {
        s_heap->AddRoot(*slot, 0, true);
        return;
    }
    if ((flags & kEtwGCRootFlagsWeakRef) != 0)
    {
        s_heap->AddRoot(*slot, 0, false);
    }
}

void LaxoriGcHeap::LeaseDependentRoot(Object** slot, ScanContext*, uint32_t)
{
    s_heap->CountScanWork(38);
    s_heap->AddRoot(*slot, 0, false);
}

void CALLBACK LaxoriGcHeap::LeaseSyncBlock(
    PTR_UNCHECKED_OBJECTREF slot, uintptr_t*, uintptr_t, uintptr_t context)
{
    LaxoriGcHeap* heap = reinterpret_cast<LaxoriGcHeap*>(context);
    Object* object = *reinterpret_cast<Object**>(slot);
    heap->AddRoot(object, 0, false);
}

bool LaxoriGcHeap::IsFinalizationSuppressed(Object* object)
{
    return (object->GetHeader()->GetBits() & BIT_SBLK_FINALIZER_RUN) != 0;
}

void LaxoriGcHeap::CaptureFinalizers()
{
    CountScanWork(33);
    LaxoriRcResult result = LaxoriFinalizerCapture(
        m_engine, m_registry, m_current_pause_kind, IsFinalizationSuppressed, &m_registry_counters);
    if (result != LaxoriRcResult::Success)
    {
        m_scan_failure.store(result);
    }
}

void LaxoriGcHeap::DecideFinalization()
{
    ScanCostScope cost(this, 24);
    LaxoriPhaseScope timer(m_engine.GetPauseTimers(), LaxoriPauseTimers::ScanTraceFinalizers);
    LaxoriRcResult result = LaxoriFinalizerDecide(m_engine, m_registry, IsFinalizationSuppressed,
        [](Object* object) { return GCToEEInterface::EagerFinalized(object); }, &m_registry_counters);
    if (result != LaxoriRcResult::Success)
    {
        m_scan_failure.store(result);
    }
}

void LaxoriGcHeap::RelocateFinalizers()
{
    LaxoriFinalizerRelocate(m_engine, m_registry, m_heap_configuration.evacuateMature, m_current_pause_kind);
}

bool LaxoriGcHeap::FrozenContains(uintptr_t address, bool retired) const
{
    for (const FrozenSegment& segment : m_frozen)
    {
        uint8_t state = segment.state.load(std::memory_order_seq_cst);
        if ((state == 2 || (retired && state == 4)) && address >= segment.start &&
            address < segment.allocated.load(std::memory_order_acquire))
        {
            return true;
        }
    }
    return false;
}

bool LaxoriGcHeap::IsImmortal(Object* object, void* context)
{
    return static_cast<LaxoriGcHeap*>(context)->FrozenContains(reinterpret_cast<uintptr_t>(object), true);
}

void LaxoriGcHeap::ScanFrozenRoots()
{
    m_frozen_readers.fetch_add(1, std::memory_order_seq_cst);
    for (FrozenSegment& segment : m_frozen)
    {
        if (segment.state.load(std::memory_order_seq_cst) != 2)
        {
            continue;
        }
        uintptr_t end = segment.allocated.load(std::memory_order_acquire);
        for (uintptr_t cursor = segment.start; cursor < end;)
        {
            Object* object = reinterpret_cast<Object*>(cursor);
            size_t size = object->GetSize();
            if (size > SIZE_MAX - (sizeof(uintptr_t) - 1))
            {
                Fatal("LAXORI frozen-object extent is invalid.");
            }
            size = (size + sizeof(uintptr_t) - 1) & ~(sizeof(uintptr_t) - 1);
            if (size < MIN_OBJECT_SIZE || size > end - cursor)
            {
                Fatal("LAXORI frozen-object extent exceeds its published segment.");
            }
            GCReferenceRanges::Enumerate(object, [&](Object** first, size_t count)
            {
                for (size_t index = 0; index < count; index++)
                {
                    // RegisterFrozenSegment grants read-only storage, not a writable relocation slot.
                    AddRoot(first[index], GC_CALL_PINNED, true);
                }
                return true;
            });
            MethodTable* type = object->GetGCSafeMethodTable();
            if (type->Collectible())
            {
                Object** loader = m_enumeration.get_loader_allocator_object_slot(type);
                if (loader != nullptr)
                {
                    AddRoot(*loader, 0, true);
                }
            }
            cursor += size;
        }
    }
    m_frozen_readers.fetch_sub(1, std::memory_order_seq_cst);
}

int LaxoriGcHeap::GetNumberOfHeaps() { return 1; }
int LaxoriGcHeap::GetHomeHeapNumber() { return 0; }
size_t LaxoriGcHeap::GetPromotedBytes(int) { return m_engine.GetMarkedBytes(); }
bool LaxoriGcHeap::IsPromoted2(Object* object, bool) { return IsPromoted(object); }
bool LaxoriGcHeap::IsEphemeral(Object*) { return false; }
unsigned LaxoriGcHeap::GetCondemnedGeneration() { return 2; }
bool LaxoriGcHeap::IsThreadUsingAllocationContextHeap(gc_alloc_context* context, int thread)
{
    return context != nullptr && thread == 0;
}

bool LaxoriGcHeap::IsPromoted(Object* object)
{
    object = m_engine.ResolveForwardedChecked(object);
    if (object == nullptr)
    {
        return true;
    }
    if (IsInFrozenSegment(object))
    {
        return true;
    }
    LaxoriTracePhase phase = m_engine.GetTracePhase();
    if (phase == LaxoriTracePhase::Full || phase == LaxoriTracePhase::FinalMark)
    {
        return m_engine.IsMarked(object);
    }
    uintptr_t count;
    return m_engine.IsObjectStart(object) &&
        m_engine.LoadCount(object, &count) == LaxoriRcResult::Success && count != 0;
}

bool LaxoriGcHeap::IsHeapPointer(void* object, bool smallOnly)
{
    uintptr_t address = reinterpret_cast<uintptr_t>(object);
    return FrozenContains(address, false) ||
        (m_engine.OwnsAddress(address) &&
            (!smallOnly || address - m_engine.GetReservationStart() < m_engine.GetImmixCapacityBytes()));
}

unsigned LaxoriGcHeap::WhichGeneration(Object* object)
{
    return m_engine.OwnsAddress(reinterpret_cast<uintptr_t>(object)) ? 2 : static_cast<unsigned>(INT32_MAX);
}

Object* LaxoriGcHeap::GetContainingObject(void* interior, bool)
{
    Object* object = nullptr;
    if (!m_engine.OwnsAddress(reinterpret_cast<uintptr_t>(interior)))
    {
        return nullptr;
    }
    LaxoriRcResult result = m_engine.FindContainingObject(reinterpret_cast<uintptr_t>(interior), &object);
    return result == LaxoriRcResult::Success ? object : nullptr;
}

bool LaxoriGcHeap::IsLargeObject(Object* object)
{
    return m_engine.IsLargeObject(object);
}

bool LaxoriGcHeap::IsGCInProgressHelper(bool considerStart)
{
    return m_in_gc.load(std::memory_order_acquire) || (considerStart && g_fSuspensionPending != 0);
}

void LaxoriGcHeap::SetGCInProgress(bool value) { m_in_gc.store(value, std::memory_order_release); }
void LaxoriGcHeap::SetSuspensionPending(bool value) { g_fSuspensionPending = value ? 1 : 0; }
bool LaxoriGcHeap::RuntimeStructuresValid() { return m_initialized.load(std::memory_order_acquire); }
void LaxoriGcHeap::SetYieldProcessorScalingFactor(float) {}
void LaxoriGcHeap::SetWaitForGCEvent() { m_gc_done.Set(); }
void LaxoriGcHeap::ResetWaitForGCEvent() { m_gc_done.Reset(); }

uint32_t LaxoriGcHeap::WaitUntilGCComplete(bool)
{
    bool cooperative = GCToEEInterface::EnablePreemptiveGC();
    uint32_t result = m_gc_done.Wait(INFINITE, false);
    if (cooperative)
    {
        GCToEEInterface::DisablePreemptiveGC();
    }
    return result;
}

bool LaxoriGcHeap::IsConcurrentGCInProgress()
{
    return !m_shutdown.load() && m_engine.GetTracePhase() != LaxoriTracePhase::Inactive;
}

bool LaxoriGcHeap::IsConcurrentGCEnabled() { return m_concurrent_enabled.load(); }
void LaxoriGcHeap::TemporaryEnableConcurrentGC() { m_concurrent_enabled.store(true); }
void LaxoriGcHeap::TemporaryDisableConcurrentGC()
{
    m_concurrent_enabled.store(false);
    WaitUntilConcurrentGCComplete();
}

void LaxoriGcHeap::WaitUntilConcurrentGCComplete()
{
    if (!m_shutdown.load() && IsConcurrentGCInProgress() &&
        CollectPrototype(LaxoriPauseKind::FinalMark, true) != LaxoriRcResult::Success)
    {
        Fatal("LAXORI concurrent-completion wait failed.");
    }
}

HRESULT LaxoriGcHeap::WaitUntilConcurrentGCCompleteAsync(int timeout)
{
    if (timeout < -1)
    {
        Unsupported("LAXORI concurrent-completion timeout must be -1 or nonnegative.");
        return E_FAIL;
    }
    uint32_t result = m_concurrent_done.Wait(timeout == -1 ? INFINITE : static_cast<uint32_t>(timeout), false);
    return result == WAIT_OBJECT_0 ? S_OK :
        result == WAIT_TIMEOUT ? HRESULT_FROM_WIN32(ERROR_TIMEOUT) : E_FAIL;
}

size_t LaxoriGcHeap::GetNumberOfFinalizable() { return m_registry.GetPendingCount(); }
FinalizerWorkItem* LaxoriGcHeap::GetExtraWorkForFinalization() { return nullptr; }

Object* LaxoriGcHeap::GetNextFinalizable()
{
    if (m_registry.GetPendingCount() == 0)
    {
        return nullptr;
    }
    // The runtime frees a loader allocator (and so the type data of its collectible types) only from a finalizer.
    // The lazy cycle sweep of a tracing pause reads the size of each dead cycle object from the object itself, so
    // no finalizer may run until that sweep has finished. Resume does not signal the finalizer thread while the
    // sweep is pending, and FinishLazy signals it once the sweep is done.
    if (m_engine.IsCycleSweepPending())
    {
        return nullptr;
    }
    Object* object = m_registry.TakeNext(IsFinalizationSuppressed);
    if (m_registry.GetFailures() != 0)
    {
        Fatal("LAXORI finalizer registry could not record a released count.");
    }
    if (object != nullptr)
    {
        if (!m_engine.IsObjectStart(object))
        {
            Fatal("LAXORI finalizer queue lost its managed allocation lease.");
        }
        CountScanWork(35);
    }
    return object;
}

bool LaxoriGcHeap::RegisterForFinalization(int, Object* object)
{
    EscapeObject(object);
    if (!m_engine.IsOwnedObject(object))
    {
        Unsupported("LAXORI cannot register finalization for a nonowned or incomplete object.");
        return false;
    }
    object->GetHeader()->ClrBit(BIT_SBLK_FINALIZER_RUN);
    return m_registry.Register(object, object->GetGCSafeMethodTable()->HasCriticalFinalizer(), true);
}

void LaxoriGcHeap::SetFinalizationRun(Object* object)
{
    if (!m_engine.IsOwnedObject(object))
    {
        Fatal("LAXORI finalization suppression received a nonowned object.");
    }
    // The registry sees the header bit when it next looks at the entry (the next pause for an entry that is not
    // counted yet, the next tracing pause for a counted one, or the finalizer thread for a ready one).
    object->GetHeader()->SetBit(BIT_SBLK_FINALIZER_RUN);
}

size_t LaxoriGcHeap::GetValidSegmentSize(bool) { return m_engine.GetReservationBytes(); }
void LaxoriGcHeap::SetReservedVMLimit(size_t limit) { m_reserved_limit = limit; }
size_t LaxoriGcHeap::GetTotalBytesInUse() { return m_engine.GetUsedBytesEstimate(); }
uint64_t LaxoriGcHeap::GetTotalAllocatedBytes() { return m_context_allocated_bytes.load(); }
size_t LaxoriGcHeap::GetCurrentObjSize() { return m_engine.GetUsedBytesEstimate(); }
int LaxoriGcHeap::CollectionCount(int, int) { return static_cast<int>(m_collections.load()); }
unsigned LaxoriGcHeap::GetGcCount() { return m_collections.load(); }
int LaxoriGcHeap::GetGcLatencyMode() { return m_latency.load(); }
int LaxoriGcHeap::SetGcLatencyMode(int mode)
{
    m_latency.store(mode);
    m_concurrent_enabled.store(mode != 0);
    return 0;
}

int LaxoriGcHeap::GetLOHCompactionMode() { return m_loh_mode.load(); }
void LaxoriGcHeap::SetLOHCompactionMode(int mode)
{
    if (mode != 1)
    {
        Fatal("LAXORI is nonmoving and does not implement LOH compaction.");
    }
    m_loh_mode.store(mode);
}

int LaxoriGcHeap::GetLastGCPercentTimeInGC()
{
    uint64_t elapsed = GetNow() - m_start_time;
    return elapsed == 0 ? 0 : static_cast<int>(m_total_pause_ticks.load() / 10000 * 100 / elapsed);
}

size_t LaxoriGcHeap::GetLastGCGenerationSize(int generation)
{
    return generation == 2 ? m_last_heap_size.load() : 0;
}

size_t LaxoriGcHeap::GetLastGCStartTime(int) { return m_last_gc_start.load(); }
size_t LaxoriGcHeap::GetLastGCDuration(int) { return m_last_gc_duration.load(); }
size_t LaxoriGcHeap::GetNow() { return static_cast<size_t>(minipal_lowres_ticks()); }
int64_t LaxoriGcHeap::GetTotalPauseDuration() { return m_total_pause_ticks.load(); }
uint64_t LaxoriGcHeap::GetGenerationBudget(int) { return m_policy_configuration.youngLimitBytes; }
size_t LaxoriGcHeap::GetLOHThreshold() { return LaxoriLargeObjectSpace::MinimumObjectBytes; }

uint32_t LaxoriGcHeap::GetMemoryLoad()
{
    uint32_t load;
    uint64_t physical;
    uint64_t pageFile;
    GCToOSInterface::GetMemoryStatus(GCToOSInterface::GetPhysicalMemoryLimit(), &load, &physical, &pageFile);
    return load;
}

void LaxoriGcHeap::PublishMemoryInfo()
{
    uint64_t physical = GCToOSInterface::GetPhysicalMemoryLimit();
    uint64_t available = physical != 0 && physical < m_hard_limit ? physical : m_hard_limit;
    bool concurrent = m_current_pause_kind == LaxoriPauseKind::FinalMark && !m_forced_blocking;
    uint64_t values[MemoryFields]{
        available, physical / 10 * 9, physical / 100 * m_last_memory_load.load(),
        m_last_heap_size.load(), m_last_fragmentation.load(), m_last_committed.load(),
        m_engine.GetUsedBytesEstimate(), m_engine.GetPinnedRootCount(), m_registry.GetPendingCount(),
        m_collections.load(), 2, static_cast<uint64_t>(GetLastGCPercentTimeInGC()),
        concurrent ? 1u : 0u,
        m_engine.GetNurseryMetrics().pauseCopiedBytes != 0 ||
            m_engine.GetMatureMetrics().pauseCopiedBytes != 0 ? 1u : 0u,
        concurrent ? m_initial_pause_ticks : static_cast<uint64_t>(m_last_pause_ticks.load()),
        concurrent ? static_cast<uint64_t>(m_last_pause_ticks.load()) : 0,
        concurrent ? m_trace_heap_before : m_heap_before,
        concurrent ? m_trace_fragmentation_before : m_fragmentation_before,
    };
    for (size_t kind : {size_t{0}, concurrent ? size_t{3} : size_t{2}})
    {
        m_memory_sequence[kind].fetch_add(1);
        for (size_t field = 0; field < MemoryFields; field++)
        {
            m_memory_info[kind][field].store(values[field]);
        }
        m_memory_sequence[kind].fetch_add(1);
    }
}

void LaxoriGcHeap::GetMemoryInfo(
    uint64_t* highThreshold, uint64_t* available, uint64_t* load, uint64_t* heap,
    uint64_t* fragmentation, uint64_t* committed, uint64_t* promoted, uint64_t* pinned,
    uint64_t* pending, uint64_t* index, uint32_t* generation, uint32_t* pausePercent,
    bool* compacting, bool* concurrent, uint64_t* generations, uint64_t* pauses, int kind)
{
    if (kind < 0 || kind > 3)
    {
        Fatal("LAXORI memory-info collection kind is invalid.");
    }
    uint64_t values[MemoryFields];
    uint64_t sequence;
    do
    {
        sequence = m_memory_sequence[kind].load();
        for (size_t field = 0; field < MemoryFields; field++)
        {
            values[field] = m_memory_info[kind][field].load();
        }
    } while ((sequence & 1) != 0 || sequence != m_memory_sequence[kind].load());
    *available = values[Available];
    *highThreshold = values[HighThreshold];
    *load = values[Load];
    *heap = values[HeapSize];
    *fragmentation = values[Fragmentation];
    *committed = values[Committed];
    *promoted = values[Promoted];
    *pinned = values[Pins];
    *pending = values[Finalizers];
    *index = values[CollectionIndex];
    *generation = static_cast<uint32_t>(values[Generation]);
    *pausePercent = static_cast<uint32_t>(values[PausePercent]);
    *compacting = values[Compacted] != 0;
    *concurrent = values[Concurrent] != 0;
    memset(generations, 0, 20 * sizeof(uint64_t));
    generations[8] = values[HeapBefore];
    generations[9] = values[FragmentationBefore];
    generations[10] = *heap;
    generations[11] = *fragmentation;
    pauses[0] = values[PauseTicks];
    pauses[1] = values[SecondPauseTicks];
}

void LaxoriGcHeap::EnumerateConfigurationValues(void* context, ConfigurationValueFunc callback)
{
    callback(context, "LAXORIHeapBytes", "System.GC.LAXORIHeapBytes", GCConfigurationType::Int64,
        static_cast<int64_t>(m_engine.GetReservationBytes()));
    callback(context, "LAXORIManagedHeapHardLimitMB", "System.GC.LAXORIManagedHeapHardLimitMB", GCConfigurationType::Int64,
        static_cast<int64_t>(m_hard_limit >> 20));
    callback(context, "LAXORINativePlaneLimitMB", "System.GC.LAXORINativePlaneLimitMB", GCConfigurationType::Int64,
        static_cast<int64_t>(m_native_plane_limit >> 20));
    callback(context, "LAXORITraceThreshold", "System.GC.LAXORITraceThreshold", GCConfigurationType::Int64,
        static_cast<int64_t>(m_policy_configuration.traceThreshold));
    callback(context, "LAXORIRCStopPercent", "System.GC.LAXORIRCStopPercent", GCConfigurationType::Int64,
        static_cast<int64_t>(m_policy_configuration.rcStopPercent));
    callback(context, "LAXORIMaxSurvivalMB", "System.GC.LAXORIMaxSurvivalMB", GCConfigurationType::Int64,
        static_cast<int64_t>(m_policy_configuration.maxSurvivalMB));
    callback(context, "LAXORIImmixInitialMB", "System.GC.LAXORIImmixInitialMB", GCConfigurationType::Int64,
        static_cast<int64_t>(m_heap_configuration.immixBytes >> 20));
    callback(context, "LAXORILOSInitialMB", "System.GC.LAXORILOSInitialMB", GCConfigurationType::Int64,
        static_cast<int64_t>(m_heap_configuration.largeObjectBytes >> 20));
    callback(context, "LAXORIImmixReserveMB", "System.GC.LAXORIImmixReserveMB", GCConfigurationType::Int64,
        static_cast<int64_t>(m_heap_configuration.immixReservationBytes >> 20));
    callback(context, "LAXORILOSReserveMB", "System.GC.LAXORILOSReserveMB", GCConfigurationType::Int64,
        static_cast<int64_t>(m_heap_configuration.largeObjectReservationBytes >> 20));
    callback(context, "LAXORIMinReuseLines", "System.GC.LAXORIMinReuseLines", GCConfigurationType::Int64,
        static_cast<int64_t>(m_heap_configuration.minimumReuseLines));
    callback(context, "LAXORIEvacuateNursery", "System.GC.LAXORIEvacuateNursery", GCConfigurationType::Boolean,
        m_heap_configuration.evacuateNursery);
    callback(context, "LAXORIEvacuateMature", "System.GC.LAXORIEvacuateMature", GCConfigurationType::Boolean,
        m_heap_configuration.evacuateMature);
    callback(context, "LAXORIDefragHeadroomPercent", "System.GC.LAXORIDefragHeadroomPercent", GCConfigurationType::Int64,
        static_cast<int64_t>(m_heap_configuration.defragHeadroomPercent));
    callback(context, "LAXORIChunkDefragPercent", "System.GC.LAXORIChunkDefragPercent", GCConfigurationType::Int64,
        static_cast<int64_t>(m_heap_configuration.chunkDefragPercent));
    callback(context, "LAXORIBufferSize", "System.GC.LAXORIBufferSize", GCConfigurationType::Int64,
        static_cast<int64_t>(m_heap_configuration.bufferSize));
    callback(context, "LAXORIIncrementsLimit", "System.GC.LAXORIIncrementsLimit", GCConfigurationType::Int64,
        static_cast<int64_t>(m_policy_configuration.incrementsLimit));
    callback(context, "LAXORINurseryBlocks", "System.GC.LAXORINurseryBlocks", GCConfigurationType::Int64,
        static_cast<int64_t>(m_policy_configuration.nurseryBlocks));
    callback(context, "LAXORIYoungLimitBytes", "System.GC.LAXORIYoungLimitBytes", GCConfigurationType::Int64,
        static_cast<int64_t>(m_policy_configuration.youngLimitBytes));
    callback(context, "LAXORISurvivalPredictorWeighted", "System.GC.LAXORISurvivalPredictorWeighted",
        GCConfigurationType::Boolean, m_policy_configuration.weighted);
    callback(context, "LAXORISurvivalPredictorHarmonicMean", "System.GC.LAXORISurvivalPredictorHarmonicMean",
        GCConfigurationType::Boolean, m_policy_configuration.harmonic);
    callback(context, "LAXORILazyDecrements", "System.GC.LAXORILazyDecrements",
        GCConfigurationType::Boolean, m_heap_configuration.lazyDecrements);
    callback(context, "LAXORIParallelSweep", "System.GC.LAXORIParallelSweep",
        GCConfigurationType::Boolean, m_heap_configuration.parallelSweep);
    callback(context, "LAXORIParallelPauseTrace", "System.GC.LAXORIParallelPauseTrace",
        GCConfigurationType::Boolean, m_heap_configuration.parallelPauseTrace);
    callback(context, "LAXORIEagerMarkClear", "System.GC.LAXORIEagerMarkClear",
        GCConfigurationType::Boolean, m_heap_configuration.eagerMarkClear);
    callback(context, "LAXORILineRecycling", "System.GC.LAXORILineRecycling",
        GCConfigurationType::Boolean, m_heap_configuration.lineRecycling);
    callback(context, "LAXORIMediumOverflow", "System.GC.LAXORIMediumOverflow",
        GCConfigurationType::Boolean, m_heap_configuration.mediumOverflow);
    callback(context, "LAXORIFastAllocation", "System.GC.LAXORIFastAllocation", GCConfigurationType::Boolean, m_fast_enabled);
    callback(context, "LAXORIOvershootToSpace", "System.GC.LAXORIOvershootToSpace", GCConfigurationType::Boolean,
        m_engine.IsOvershootEnabled());
    callback(context, "LAXORIHeapSizing", "System.GC.LAXORIHeapSizing", GCConfigurationType::Boolean,
        m_sizing_configuration.enabled);
    callback(context, "LAXORINurseryCapMB", "System.GC.LAXORINurseryCapMB", GCConfigurationType::Int64,
        static_cast<int64_t>(m_sizing_configuration.nurseryCapBytes >> 20));
    callback(context, "LAXORIMaxMutators", "System.GC.LAXORIMaxMutators", GCConfigurationType::Int64, MaximumMutators);
    callback(context, "LAXORIGCThreads", "System.GC.LAXORIGCThreads", GCConfigurationType::Int64,
        static_cast<int64_t>(m_heap_configuration.workers));
    callback(context, "LAXORIConcGCThreads", "System.GC.LAXORIConcGCThreads", GCConfigurationType::Int64,
        static_cast<int64_t>(m_heap_configuration.concurrentTraceWorkers));
    callback(context, "LAXORIPrezero", "System.GC.LAXORIPrezero", GCConfigurationType::Boolean,
        m_heap_configuration.prezero);
    callback(context, "LAXORIPrezeroThread", "System.GC.LAXORIPrezeroThread", GCConfigurationType::Boolean,
        m_heap_configuration.prezeroThread);
    callback(context, "GCConcurrent", "System.GC.Concurrent", GCConfigurationType::Boolean, IsConcurrentGCEnabled());
}

int LaxoriGcHeap::RefreshMemoryLimit()
{
    Unsupported("LAXORI capacity can grow within startup limits; changing those limits at runtime is unsupported.");
    return refresh_hard_limit_invalid;
}

bool LaxoriGcHeap::RegisterForFullGCNotification(uint32_t, uint32_t)
{
    Unsupported("LAXORI prototype does not support full-GC notification subscriptions.");
    return false;
}

bool LaxoriGcHeap::CancelFullGCNotification()
{
    Unsupported("LAXORI prototype has no full-GC notification subscription to cancel.");
    return false;
}

int LaxoriGcHeap::WaitForFullGCApproach(int)
{
    Unsupported("LAXORI prototype does not support full-GC notification waits.");
    return 4;
}

int LaxoriGcHeap::WaitForFullGCComplete(int)
{
    Unsupported("LAXORI prototype does not support full-GC notification waits.");
    return 4;
}

int LaxoriGcHeap::StartNoGCRegion(uint64_t, bool, uint64_t, bool)
{
    Unsupported("LAXORI prototype cannot guarantee a no-GC reservation.");
    return start_no_gc_no_memory;
}

int LaxoriGcHeap::EndNoGCRegion() { return end_no_gc_not_in_progress; }
enable_no_gc_region_callback_status LaxoriGcHeap::EnableNoGCRegionCallback(
    NoGCRegionCallbackFinalizerWorkItem*, uint64_t)
{
    return not_started;
}

bool LaxoriGcHeap::StressHeap(gc_alloc_context*)
{
    Fatal("LAXORI prototype does not support GCStress.");
    return false;
}

segment_handle LaxoriGcHeap::RegisterFrozenSegment(segment_info* info)
{
    if (info == nullptr || info->ibFirstObject > info->ibAllocated ||
        info->ibAllocated > info->ibCommit || info->ibCommit > info->ibReserved ||
        info->ibReserved > UINTPTR_MAX - reinterpret_cast<uintptr_t>(info->pvMem))
    {
        Unsupported("LAXORI frozen-segment registration has invalid bounds.");
        return nullptr;
    }
    for (FrozenSegment& segment : m_frozen)
    {
        uint8_t expected = 0;
        if (!segment.state.compare_exchange_strong(expected, 1))
        {
            continue;
        }
        uintptr_t base = reinterpret_cast<uintptr_t>(info->pvMem);
        segment.start = base + info->ibFirstObject;
        segment.reserved = base + info->ibReserved;
        segment.allocated.store(base + info->ibAllocated);
        segment.committed.store(base + info->ibCommit);
        segment.state.store(2, std::memory_order_seq_cst);
        return reinterpret_cast<segment_handle>(&segment);
    }
    Unsupported("LAXORI prototype exhausted its 256 frozen-segment lifetime records.");
    return nullptr;
}

void LaxoriGcHeap::UnregisterFrozenSegment(segment_handle handle)
{
    FrozenSegment* segment = reinterpret_cast<FrozenSegment*>(handle);
    if (segment < m_frozen || segment >= m_frozen + MaximumFrozenSegments)
    {
        Fatal("LAXORI frozen-segment handle is invalid.");
    }
    uint8_t expected = 2;
    if (!segment->state.compare_exchange_strong(expected, 3, std::memory_order_seq_cst))
    {
        Fatal("LAXORI frozen segment was already retired.");
    }
    while (m_frozen_readers.load(std::memory_order_seq_cst) != 0)
    {
        GCToOSInterface::YieldThread(0);
    }
    // Historical address classification outlives queued SATB references, not the EE-owned payload.
    segment->state.store(4, std::memory_order_seq_cst);
}

bool LaxoriGcHeap::IsInFrozenSegment(Object* object)
{
    return FrozenContains(reinterpret_cast<uintptr_t>(object), false);
}

void LaxoriGcHeap::UpdateFrozenSegment(segment_handle handle, uint8_t* allocated, uint8_t* committed)
{
    FrozenSegment* segment = reinterpret_cast<FrozenSegment*>(handle);
    if (segment < m_frozen || segment >= m_frozen + MaximumFrozenSegments ||
        segment->state.load() != 2 || reinterpret_cast<uintptr_t>(allocated) < segment->start ||
        allocated > committed || reinterpret_cast<uintptr_t>(committed) > segment->reserved)
    {
        Fatal("LAXORI frozen-segment update has invalid bounds.");
    }
    segment->committed.store(reinterpret_cast<uintptr_t>(committed), std::memory_order_release);
    segment->allocated.store(reinterpret_cast<uintptr_t>(allocated), std::memory_order_release);
}

void LaxoriGcHeap::PublishObject(uint8_t* object)
{
    if (!m_engine.OwnsAddress(reinterpret_cast<uintptr_t>(object)))
    {
        Fatal("LAXORI object publication escaped its owned reservation.");
    }
}

void LaxoriGcHeap::ValidateObjectMember(Object* object)
{
    object = m_engine.ResolveForwarded(object);
    if (!m_engine.IsOwnedObject(object) && !IsInFrozenSegment(object))
    {
        Fatal("LAXORI object verification received a reclaimed or nonowned object.");
    }
    GCReferenceRanges::Enumerate(object, [&](Object** first, size_t count)
    {
        for (size_t index = 0; index < count; index++)
        {
            Object* child = first[index];
            if (child != nullptr && !m_engine.IsOwnedObject(child) && !IsInFrozenSegment(child))
            {
                Fatal("LAXORI object verification found an invalid managed child.");
            }
        }
        return true;
    });
}

void LaxoriGcHeap::ValidateWalk(const char* operation) const
{
    if (m_engine.GetPhase() != LaxoriRcPhase::Suspended)
    {
        Unsupported(operation);
        Fatal("LAXORI heap walking requires a suspended managed runtime.");
    }
}

bool LaxoriGcHeap::WalkHeapObject(Object* object, size_t, void* context)
{
    WalkContext* walk = static_cast<WalkContext*>(context);
    return !walk->heap->IsPromoted(object) || walk->callback(object, walk->context);
}

Object* LaxoriGcHeap::NextObj(Object* object)
{
    return m_engine.FindNextObject(object);
}

void LaxoriGcHeap::DiagWalkObject(Object* object, walk_fn callback, void* context)
{
    ValidateWalk("LAXORI DiagWalkObject requires suspension.");
    object = m_engine.ResolveForwarded(object);
    GCReferenceRanges::Enumerate(object, [&](Object** first, size_t count)
    {
        for (size_t index = 0; index < count; index++)
        {
            if (first[index] != nullptr && !callback(first[index], context))
            {
                return false;
            }
        }
        return true;
    });
}

void LaxoriGcHeap::DiagWalkObject2(Object* object, walk_fn2 callback, void* context)
{
    ValidateWalk("LAXORI DiagWalkObject2 requires suspension.");
    object = m_engine.ResolveForwarded(object);
    GCReferenceRanges::Enumerate(object, [&](Object** first, size_t count)
    {
        for (size_t index = 0; index < count; index++)
        {
            if (first[index] != nullptr &&
                !callback(first[index], reinterpret_cast<uint8_t**>(first + index), context))
            {
                return false;
            }
        }
        return true;
    });
}

void LaxoriGcHeap::DiagWalkHeap(walk_fn callback, void* context, int, bool)
{
    ValidateWalk("LAXORI DiagWalkHeap requires suspension.");
    WalkContext walk{this, callback, context};
    if (m_engine.WalkObjects(WalkHeapObject, &walk) != LaxoriRcResult::Success)
    {
        Fatal("LAXORI heap walk failed.");
    }
}

void LaxoriGcHeap::DiagWalkHeapWithACHandling(walk_fn callback, void* context, int generation, bool large)
{
    DiagWalkHeap(callback, context, generation, large);
}

void LaxoriGcHeap::DiagWalkSurvivorsWithType(
    void*, record_surv_fn callback, void* context, walk_surv_type, int)
{
    ValidateWalk("LAXORI DiagWalkSurvivorsWithType requires suspension.");
    struct SurvivorContext
    {
        LaxoriGcHeap* heap;
        record_surv_fn callback;
        void* context;
        LaxoriWordArray copies;
        bool failed;
    } walk{this, callback, context, {}, false};
    LaxoriRcResult forwarded = m_engine.WalkForwarded([](
        Object* source, Object* destination, size_t size, void* state)
    {
        SurvivorContext* walk = static_cast<SurvivorContext*>(state);
        uint8_t* start = reinterpret_cast<uint8_t*>(source);
        uintptr_t before = reinterpret_cast<uintptr_t>(source);
        uintptr_t after = reinterpret_cast<uintptr_t>(destination);
        ptrdiff_t displacement = after >= before
            ? static_cast<ptrdiff_t>(after - before) : -static_cast<ptrdiff_t>(before - after);
        walk->callback(start, start + size, displacement, walk->context, true, false);
        // The copies are known from the forwarding records. They are reported as moved, so the walk below skips them.
        walk->failed |= !walk->copies.Push(after);
        return true;
    }, &walk);
    if (forwarded != LaxoriRcResult::Success || walk.failed)
    {
        Fatal("LAXORI moved survivor walk failed.");
    }
    std::sort(walk.copies.Data(), walk.copies.Data() + walk.copies.Size());
    LaxoriRcResult result = m_engine.WalkObjects([](Object* object, size_t size, void* state)
    {
        SurvivorContext* walk = static_cast<SurvivorContext*>(state);
        const uintptr_t* copies = walk->copies.Data();
        if (walk->heap->IsPromoted(object) &&
            !std::binary_search(copies, copies + walk->copies.Size(), reinterpret_cast<uintptr_t>(object)))
        {
            uint8_t* start = reinterpret_cast<uint8_t*>(object);
            walk->callback(start, start + size, 0, walk->context, false, false);
        }
        return true;
    }, &walk);
    if (result != LaxoriRcResult::Success)
    {
        Fatal("LAXORI survivor walk failed.");
    }
}

void LaxoriGcHeap::DiagWalkFinalizeQueue(void*, fq_walk_fn callback)
{
    ValidateWalk("LAXORI DiagWalkFinalizeQueue requires suspension.");
    m_registry.ForEachReady([&](Object* object, bool critical) { callback(critical, object); });
}

void LaxoriGcHeap::DiagScanFinalizeQueue(fq_scan_fn callback, ScanContext* scan)
{
    ValidateWalk("LAXORI DiagScanFinalizeQueue requires suspension.");
    m_registry.ForEachReady([&](Object* object, bool)
    {
        Object* local = object;
        callback(&local, scan, 0);
    });
}

void LaxoriGcHeap::DiagScanHandles(handle_scan_fn callback, int, ScanContext* context)
{
    GCScan::GcScanHandlesForProfilerAndETW(2, context, callback);
}

void LaxoriGcHeap::DiagScanDependentHandles(handle_scan_fn callback, int, ScanContext* context)
{
    GCScan::GcScanDependentHandlesForProfilerAndETW(2, context, callback);
}

void LaxoriGcHeap::DiagDescrGenerations(gen_walk_fn callback, void* context)
{
    uint8_t* start = reinterpret_cast<uint8_t*>(m_engine.GetReservationStart()) + sizeof(ObjHeader);
    uint8_t* end = reinterpret_cast<uint8_t*>(m_engine.GetReservationStart() + m_engine.GetReservationBytes());
    callback(context, 2, start, end, end);
}

void LaxoriGcHeap::DiagTraceGCSegments()
{
    GCToEEInterface::EventSink()->FireGCCreateSegment_V1(
        reinterpret_cast<void*>(m_engine.GetReservationStart()), m_engine.GetReservationBytes(), 0);
}

void LaxoriGcHeap::DiagGetGCSettings(EtwGCSettingsInfo* settings)
{
    *settings = {};
    settings->loh_threshold = GetLOHThreshold();
    settings->concurrent_gc_p = IsConcurrentGCEnabled();
    settings->use_frozen_segments_p = true;
}

unsigned LaxoriGcHeap::GetGenerationWithRange(
    Object* object, uint8_t** start, uint8_t** allocated, uint8_t** reserved)
{
    uintptr_t address = reinterpret_cast<uintptr_t>(object);
    if (!m_engine.OwnsAddress(address))
    {
        *start = nullptr;
        *allocated = nullptr;
        *reserved = nullptr;
        for (const FrozenSegment& segment : m_frozen)
        {
            if (segment.state.load(std::memory_order_seq_cst) == 2 && address >= segment.start &&
                address < segment.allocated.load(std::memory_order_acquire))
            {
                *start = reinterpret_cast<uint8_t*>(segment.start);
                *allocated = reinterpret_cast<uint8_t*>(segment.allocated.load());
                *reserved = reinterpret_cast<uint8_t*>(segment.reserved);
                break;
            }
        }
        return static_cast<unsigned>(INT32_MAX);
    }
    *start = reinterpret_cast<uint8_t*>(m_engine.GetReservationStart()) + sizeof(ObjHeader);
    *allocated = reinterpret_cast<uint8_t*>(m_engine.GetReservationStart() + m_engine.GetReservationBytes());
    *reserved = *allocated;
    return 2;
}

void LaxoriGcHeap::ControlEvents(GCEventKeyword keyword, GCEventLevel level)
{
    GCEventStatus::Set(GCEventProvider_Default, keyword, level);
}

void LaxoriGcHeap::ControlPrivateEvents(GCEventKeyword keyword, GCEventLevel level)
{
    GCEventStatus::Set(GCEventProvider_Private, keyword, level);
}

void LaxoriGcHeap::NullBridgeObjectsWeakRefs(size_t, void*)
{
    Fatal("LAXORI prototype does not implement cross-runtime Java bridge reclamation.");
}

// LAXORI uses the barrier mechanism of PR #3316: it is a custom collector. The runtime asks for the barrier helpers in
// GetAssignRefFunctions and calls the shared scalar and bulk hooks below.
void LaxoriGcHeap::GetFastGCFunctions(FastGCFunctions* functions)
{
    *functions = {};
    functions->context = this;
    functions->write_barrier = &LaxoriGcHeap::NoPostStoreBarrier;
    functions->is_in_gc_heap = &LaxoriGcHeap::IsInHeap;
    functions->pre_store = &LaxoriGcHeap::PreStore;
    functions->bulk_move_with_write_barrier = &LaxoriGcHeap::BulkMoveWithWriteBarrier;
    functions->flags = FASTGC_REQUIRES_OLD_VALUE;
    functions->bulk_clear_with_write_barrier = &LaxoriGcHeap::BulkClearWithWriteBarrier;
}

GCHeapType LaxoriGcHeap::GetGCHeapType() { return GC_HEAP_CUSTOM; }

uint64_t LaxoriGcHeap::GetTotalAllocatedBytesPrecise() { return GetTotalAllocatedBytes(); }

static_assert(sizeof(LaxoriWriteBarrierPatchable) == sizeof(GCWriteBarrierPatchableValue) &&
                  offsetof(LaxoriWriteBarrierPatchable, slot) == offsetof(GCWriteBarrierPatchableValue, Slot) &&
                  offsetof(LaxoriWriteBarrierPatchable, source) == offsetof(GCWriteBarrierPatchableValue, Source),
              "LaxoriWriteBarrierPatchable must match GCWriteBarrierPatchableValue");

void LaxoriGcHeap::GetAssignRefFunctions(void** assignRef, size_t* assignRefSize, void** assignRefChecked, size_t* assignRefCheckedSize, uintptr_t** avLocationsList, GCWriteBarrierPatchableValue** patchableValues)
{
    LaxoriWriteBarrierBlockInfo block;
    LaxoriWriteBarrierGetBlockInfo(&block);
    *assignRef = block.assign_ref;
    *assignRefSize = block.assign_ref_size;
    *assignRefChecked = block.checked_assign_ref;
    *assignRefCheckedSize = block.checked_assign_ref_size;
    *avLocationsList = block.av_locations;
    *patchableValues = reinterpret_cast<GCWriteBarrierPatchableValue*>(block.patchable);
}

ObjectReferenceEnumerationParameters* LaxoriGcHeap::GetObjectReferenceEnumerationParameters() { return &m_enumeration; }
ObjectHeaderBitsParameters* LaxoriGcHeap::GetObjectHeaderBitsParameters() { return &m_header_bits; }

LaxoriRcResult LaxoriGcHeap::HoldPrototypeTrace(bool hold)
{
    bool cooperative = EnterCollectionLock();
    LaxoriRcResult result = m_engine.HoldTrace(hold);
    LeaveCollectionLock(cooperative);
    m_trace_signal.Set();
    return result;
}

LaxoriRcResult LaxoriGcHeap::ArmPrototypeScan(OBJECTHANDLE handle)
{
    bool cooperative = EnterCollectionLock();
    LaxoriRcResult result = m_engine.ArmScanCheckpoint(reinterpret_cast<Object*>(GetHandleAddress(handle)));
    LeaveCollectionLock(cooperative);
    return result;
}

bool LaxoriGcHeap::IsOwnedHandle(OBJECTHANDLE handle) const
{
    Object** slot = reinterpret_cast<Object**>(reinterpret_cast<uintptr_t>(handle) & ~static_cast<uintptr_t>(1));
    return slot != nullptr && m_engine.IsOwnedObject(*slot);
}

uintptr_t LaxoriGcHeap::GetOwnedStart() const { return m_engine.GetReservationStart(); }
size_t LaxoriGcHeap::GetOwnedBytes() const { return m_engine.GetReservationBytes(); }
uintptr_t LaxoriGcHeap::GetHandleAddress(OBJECTHANDLE handle) const
{
    Object** slot = reinterpret_cast<Object**>(reinterpret_cast<uintptr_t>(handle) & ~static_cast<uintptr_t>(1));
    return slot == nullptr ? 0 : reinterpret_cast<uintptr_t>(*slot);
}

size_t LaxoriGcHeap::GetAddressExtent(uintptr_t address) const
{
    return m_engine.GetLiveObjectSize(reinterpret_cast<Object*>(address));
}

segment_handle LaxoriGcHeap::RegisterPrototypeFrozenArray(
    void* memory, size_t capacity, OBJECTHANDLE array, OBJECTHANDLE target)
{
    bool cooperative = EnterCollectionLock();
    Object* templateObject = reinterpret_cast<Object*>(GetHandleAddress(array));
    Object* referent = reinterpret_cast<Object*>(GetHandleAddress(target));
    segment_handle result = nullptr;
    if (memory != nullptr && templateObject != nullptr && referent != nullptr &&
        templateObject->GetGCSafeMethodTable()->HasComponentSize() &&
        templateObject->GetGCSafeMethodTable()->RawGetComponentSize() == sizeof(Object*) &&
        templateObject->GetGCSafeMethodTable()->ContainsGCPointers() &&
        reinterpret_cast<ArrayBase*>(templateObject)->GetNumComponents() == 1 &&
        m_engine.IsOwnedObject(referent))
    {
        size_t size = templateObject->GetSize();
        if (size <= capacity && sizeof(ObjHeader) <= capacity - size)
        {
            memcpy(memory, templateObject->GetHeader(), size);
            Object* frozen = reinterpret_cast<Object*>(static_cast<uint8_t*>(memory) + sizeof(ObjHeader));
            size_t fields = 0;
            GCReferenceRanges::Enumerate(frozen, [&](Object** first, size_t count)
            {
                for (size_t index = 0; index < count; index++)
                {
                    first[index] = referent;
                    fields++;
                }
                return true;
            });
            if (fields == 1)
            {
                segment_info info{};
                info.pvMem = memory;
                info.ibFirstObject = sizeof(ObjHeader);
                info.ibAllocated = sizeof(ObjHeader) + size;
                info.ibCommit = capacity;
                info.ibReserved = capacity;
                result = RegisterFrozenSegment(&info);
            }
        }
    }
    if (result == nullptr)
    {
        Unsupported("LAXORI frozen validation requires writable owned fixture storage, a one-element reference array and an owned target.");
    }
    LeaveCollectionLock(cooperative);
    return result;
}

LaxoriRcResult LaxoriGcHeap::ArmPrototypeLifetime(OBJECTHANDLE handle)
{
    if (handle == nullptr)
    {
        return LaxoriRcResult::InvalidArgument;
    }
    bool cooperative = EnterCollectionLock();
    Object** slot = reinterpret_cast<Object**>(
        reinterpret_cast<uintptr_t>(handle) & ~static_cast<uintptr_t>(1));
    LaxoriRcResult result = m_engine.ArmLifetimeProbe(*slot);
    LeaveCollectionLock(cooperative);
    return result;
}

LaxoriRcResult LaxoriGcHeap::EnablePrototypeStorageDiagnostics()
{
    bool cooperative = EnterCollectionLock();
    LaxoriRcResult result = m_engine.EnableStorageDiagnostics(Timeout);
    if (result == LaxoriRcResult::Success)
    {
        m_storage_diagnostics_enabled.store(true, std::memory_order_release);
    }
    LeaveCollectionLock(cooperative);
    return result;
}

LaxoriRcResult LaxoriGcHeap::GetPrototypeStorageRange(
    uint32_t component, uintptr_t* start, size_t* bytes) const
{
    if (start == nullptr || bytes == nullptr)
    {
        return LaxoriRcResult::InvalidArgument;
    }
    if (component == 16)
    {
        // The per-object flag table was removed in P17 S7.
        *start = 0;
        *bytes = 0;
        return LaxoriRcResult::Success;
    }
    return m_engine.GetStorageRange(component, start, bytes);
}

uint64_t LaxoriGcHeap::GetPrototypeMetric(uint32_t metric) const
{
    if (metric >= 700 && metric < 700 + LaxoriReferenceCounting::RegionMetricCount)
    {
        if (metric - 700 == LaxoriReferenceCounting::TlTscKHz)
        {
            return TscKHz();
        }
        return m_engine.GetRegionMetric(metric - 700);
    }
    LaxoriRcMetrics rc = m_engine.GetMetrics();
    LaxoriTraceMetrics trace = m_engine.GetTraceMetrics();
    LaxoriNurseryMetrics nursery = m_engine.GetNurseryMetrics();
    LaxoriMatureMetrics mature = m_engine.GetMatureMetrics();
    switch (metric)
    {
        case 0: return m_initialized.load();
        case 1: return rc.allocations;
        case 2: return rc.allocatedBytes;
        case 3: return m_engine.GetUsedBytesEstimate();
        case 4: return UINT64_MAX; // Per-object reclaimed bytes exist only in the test platform (P17 S3).
        case 5: return m_collections.load();
        case 6: return rc.largeObjectBytesReleased;
        case 7: return rc.blocksReleased;
        case 8: return rc.rootIncrements;
        case 9: return rc.deaths;
        case 10: return trace.initialMarks;
        case 11: return trace.finalMarks;
        case 12: return trace.fullMarks;
        case 13: return trace.objects;
        case 14: return trace.references;
        case 15: return trace.satbEntries;
        case 16: return trace.deletionMarks;
        case 17: return trace.forcedScans;
        case 18: return trace.splitPackets;
        case 19: return trace.cycleObjects;
        case 20: return trace.cycleBytes;
        case 21: return trace.interruptions;
        case 22: return rc.threadFlushes;
        case 23: return rc.incrementEntries;
        case 24: return rc.decrementEntries;
        case 25: return m_registry.GetPendingCount();
        case 26: return m_engine.GetCommittedBytes();
        case 27: return m_barrier_bootstraps.load();
        case 28: return static_cast<uint64_t>(m_engine.GetTracePhase());
        case 29: return trace.concurrentSlices;
        case 30: return m_engine.IsScanCheckpointActive();
        case 31: return trace.checkpointInterruptions;
        case 32: return m_engine.GetSpanMetric(0);
        case 33: return m_fast_refills.load();
        case 34: return m_unused_span_bytes.load();
        case 35: return m_engine.GetReservationBytes();
        case 36: return m_engine.GetAdmittedBytes();
        case 37: return m_engine.GetGrowthCount();
        case 38: return m_engine.GetNativePlaneCommittedBytes();
        case 39: return static_cast<uint64_t>(m_policy.SurvivalRatio() * 1000000000.0);
        case 40: return static_cast<uint64_t>(m_policy.MatureLivePages() * 1000.0);
        case 41: return m_policy.CycleHint();
        case 42: return m_policy.EmergencyHint();
        case 43: return static_cast<uint64_t>(m_collection_cause.load());
        case 44: return m_hard_limit;
        case 45: return m_native_plane_limit;
        case 46: return m_engine.GetLifetimeProbeBirths();
        case 47: return m_engine.GetLifetimeProbeReclaims();
        case 48: return rc.promotions;
        case 462: return rc.loggedSlots;
        case 463: return rc.oldTargetChecks;
        case 464: return m_bulk_move_calls.load(std::memory_order_relaxed);
        case 465: return m_bulk_clear_calls.load(std::memory_order_relaxed);
        case 466: return m_bulk_layout_lookups.load(std::memory_order_relaxed);
        case 467: return m_bulk_metadata_early_outs.load(std::memory_order_relaxed);
        case 71: return 0; // The flag table of the collector (P17 S7 removed it).
        case 72: return m_engine.GetStorageMetric(23);
        case 73: return m_engine.GetStorageMetric(24);
        case 74: return m_engine.GetStorageMetric(25);
        case 75: return m_engine.GetStorageMetric(26);
        case 76: return 18 + LaxoriSideMetadataLayout::SpecCount;
        case 77: return m_engine.GetStorageMetric(27);
        case 78: return m_heap_configuration.evacuateNursery;
        case 79: return nursery.copiedObjects;
        case 80: return nursery.copiedBytes;
        case 81: return nursery.inPlacePinnedBytes;
        case 82: return nursery.copyFailures;
        case 83: return nursery.forwardingWaits;
        case 84: return nursery.retiredSourceBytes;
        case 85: return nursery.pauseCopiedBytes;
        case 86: return m_pause_young_bytes.load(std::memory_order_acquire);
        case 87: return m_heap_configuration.lineRecycling;
        case 88: return m_heap_configuration.copyLineRecycling;
        case 90: return m_heap_configuration.evacuateMature;
        case 91: return mature.eligibleBlocks;
        case 92: return mature.selectedBlocks;
        case 93: return mature.pinnedBlocks;
        case 94: return mature.estimatedCopyBytes;
        case 95: return mature.copiedObjects;
        case 96: return mature.copiedBytes;
        case 97: return mature.copyFailures;
        case 98: return mature.rememberedEntries;
        case 99: return mature.staleEntries;
        case 100: return mature.replayedEntries;
        case 101: return mature.retiredSourceBytes;
        case 102: return mature.releasedBlocks;
        case 103: return mature.invalidSlots;
        case 114: return minipal_hires_tick_frequency();
        case 115: return m_storage_diagnostics_enabled.load(std::memory_order_acquire)
            ? m_root_capture_ticks.load(std::memory_order_acquire) : UINT64_MAX;
        case 116: return mature.pauseCopiedBytes;
        default:
            if (metric >= 240 && metric < 240 + 4)
            {
                return m_engine.GetPauseTimers().GetMeta(metric - 240);
            }
            if (metric == 244)
            {
                return m_engine.GetPauseTimers().Enabled();
            }
            switch (metric)
            {
                case 245: return static_cast<uint64_t>(m_policy.MeasuredSurvivalRatio() * 1000000000.0);
                case 246: return m_engine.GetPauseSurvivorBytes();
                case 247: return m_pause_reserve_pages.load(std::memory_order_acquire);
                case 248: return m_pause_free_pages.load(std::memory_order_acquire);
                case 249: return ImmixBlockManager::GetFlipOverlaps();
                case 250: return ImmixBlockManager::GetFlipBlockedOperations();
                default: break;
            }
            switch (metric)
            {
                case 450: return m_registry.GetCountedEntries();
                case 451: return m_registry.GetRegistered();
                case 452: return m_registry_counters.retained.load(std::memory_order_relaxed);
                case 453: return m_registry_counters.released.load(std::memory_order_relaxed);
                case 454: return m_registry_counters.decisions.load(std::memory_order_relaxed);
                case 455: return m_registry.GetDropped();
                case 456: return m_registry.GetTaken();
                case 461: return m_registry.GetDuplicates();
                case 457: return m_registry_counters.walked.load(std::memory_order_relaxed);
                case 458: return m_engine.GetPinnedRootCount();
                case 459: return m_registry.GetCountedCapacity() * sizeof(uintptr_t);
                case 460: return m_registry.GetPendingCount();
                case 500: return m_engine.GetSchedulerMetrics().parks;
                case 501: return m_engine.GetSchedulerMetrics().wakes;
                case 502: return m_engine.GetSchedulerMetrics().signals;
                case 503: return m_engine.GetSchedulerMetrics().externalWaits;
                case 504: return m_engine.GetSchedulerMetrics().externalNotifies;
                case 505: return m_engine.GetSchedulerMetrics().executed;
                case 506: return m_engine.GetSchedulerMetrics().stolen;
                default: break;
            }
            if (metric >= 600 && metric < 600 + LaxoriReferenceCounting::LazyMetricCount)
            {
                return m_engine.GetLazyMetric(metric - 600);
            }
            if (metric >= 520 && metric < 528)
            {
                return m_cause_counts[metric - 520].load(std::memory_order_relaxed);
            }
            switch (metric)
            {
                case 528: return m_sizer.BudgetBytes();
                case 529: return m_sizing_growths.load(std::memory_order_relaxed);
                case 530: return m_mature_live_bytes.load(std::memory_order_relaxed);
                case 531: return m_sizing_configuration.nurseryCapBytes;
                case 532: return m_engine.GetImmixFloorBytes();
                case 533: return m_engine.GetImmixCapacityBytes();
                default: break;
            }
            if (metric >= 410 && metric < 410 + LaxoriReferenceCounting::OvershootMetricCount)
            {
                return m_engine.GetOvershootMetric(metric - 410);
            }
            if (metric == 410 + LaxoriReferenceCounting::OvershootMetricCount)
            {
                return m_engine.IsOvershootEnabled();
            }
            if (metric >= 256 && metric < 256 + LaxoriPauseTimers::PublishedCount)
            {
                return m_engine.GetPauseTimers().GetLast(metric - 256);
            }
            if (metric >= 340 && metric < 340 + LaxoriPauseTimers::PublishedCount)
            {
                return m_engine.GetPauseTimers().GetTotal(metric - 340);
            }
            if (metric >= 232 && metric < 237)
            {
                return m_engine.GetStorageMetric(metric - 180);
            }
            if (metric >= 208 && metric < 232)
            {
                return m_engine.GetStorageMetric(metric - 180);
            }
            if (metric >= 118 && metric < 118 + ScanMetricCount)
            {
                return m_storage_diagnostics_enabled.load(std::memory_order_acquire)
                    ? m_scan_metrics[metric - 118].load(std::memory_order_acquire) : UINT64_MAX;
            }
            if (metric >= 162 && metric < 162 + ScanMetricCount)
            {
                return m_storage_diagnostics_enabled.load(std::memory_order_acquire)
                    ? m_scan_cpu_nanoseconds[metric - 162].load(std::memory_order_acquire) : UINT64_MAX;
            }
            if (metric >= 104 && metric <= 113)
            {
                return m_engine.GetCostMetric(metric - 104);
            }
            if (metric >= 48 && metric <= 70)
            {
                return m_engine.GetStorageMetric(metric - 48);
            }
            if (metric >= 1000 && metric < 1000 + LaxoriPauseTimers::KindCount * 128)
            {
                return m_engine.GetPauseTimers().GetKindTotal((metric - 1000) / 128, (metric - 1000) % 128);
            }
            if (metric >= 660 && metric < 664)
            {
                return m_engine.GetPauseTraceMetric(metric - 660);
            }
            if (metric >= 640 && metric < 648)
            {
                return m_engine.GetMediumOverflowMetric(metric - 640);
            }
            if (metric >= 670 && metric < 680)
            {
                return m_engine.GetZeroingMetric(metric - 670);
            }
            Unsupported("LAXORI prototype metric index is invalid.");
            return UINT64_MAX;
    }
}
