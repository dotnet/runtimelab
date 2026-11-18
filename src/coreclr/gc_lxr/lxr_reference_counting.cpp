// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"
#include "gcenv.h"
#include "gcref.h"
#include "lxr_reference_counting.h"
#include "lxr_prefetch.h"

#include <new>
#include <algorithm>
#include <minipal/utils.h>
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
#include <chrono>
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
#include <thread>
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
#endif // LXR_SIDE_METADATA_TEST_PLATFORM

namespace
{
    enum Metric : size_t
    {
        Allocations, AllocatedBytes, LoggedSlots, IncrementEntries, DecrementEntries,
        BufferFlushes, ThreadFlushes, Promotions, Deaths, RootIncrements, RootDecrements,
        ObjectScans, ObjectSizeQueries, OldTargetChecks, ChildRegistrations,
        BlocksReleased, LargeObjectBytesReleased, Epochs,
    };

    enum TraceMetric : size_t
    {
        InitialMarks, FinalMarks, FullMarks, TracedObjects, TracedReferences, SatbEntries,
        DeletionMarks, ForcedScans, SplitPackets, CycleObjects, CycleBytes, Interruptions,
        ConcurrentSlices, CheckpointInterruptions,
    };

    bool TryMultiply(size_t left, size_t right, size_t* value)
    {
        if (right != 0 && left > SIZE_MAX / right)
        {
            return false;
        }

        *value = left * right;
        return true;
    }

    constexpr uint8_t RetainedSource = 2;
    constexpr uint8_t CopiedSource = 4;
    constexpr uintptr_t LogReferenceMask = LxrSideMetadataLayout::AddressLimit - 1;
    constexpr uint8_t LogCountShift = LxrSideMetadataLayout::AddressBits;
    static_assert(sizeof(uintptr_t) == 8);
    static_assert(LxrReferenceCounting::BufferCapacity <= (UINTPTR_MAX >> LogCountShift));
}

enum class LxrReferenceCounting::WorkKind : uint8_t
{
    Slots,
    NurserySlots,
    Roots,
    Targets,
    Decrements,
    PreviousRoots,
    NurserySweep,
    MatureSweep,
    Satb,
    PrepareTrace,
    WeakClosure,
    TraceObjects,
    TraceScan,
    EpochClosure,
    MarkClear,
    ReplayRemembered,
    SelectMature,
};

struct LxrReferenceCounting::LogChunk
{
    uintptr_t references[BufferCapacity]{};
    size_t count = 0;
    bool increment = false;
};

struct LxrReferenceCounting::LogStream
{
    static constexpr size_t CommitChunkSlots = 64 * 1024 / sizeof(uintptr_t);
    static constexpr size_t MaximumCommitChunkSlots = 1024 * 1024 / sizeof(uintptr_t);

    uintptr_t* references = nullptr;
    std::atomic<uint16_t>* published = nullptr;
    std::atomic<size_t> reserved{0};
    std::atomic<size_t> committed{0};
    size_t capacity = 0;
    LxrVirtualArray<uintptr_t> referenceStorage;
    LxrVirtualArray<std::atomic<uint16_t>> publicationStorage;
    std::atomic<size_t> highWater{0};
    std::atomic<uint64_t> flushes{0};
    std::atomic<uint64_t> copiedBytes{0};
    std::atomic<uint64_t> resetBytes{0};

    bool EnsureCommitted(LxrReferenceCounting* engine, size_t required)
    {
        if (required <= committed.load(std::memory_order_acquire))
        {
            return true;
        }
        // All streams share the admission lock so concurrent growth cannot spend the same native-memory allowance.
        while (engine->m_log_commit_lock.test_and_set(std::memory_order_acquire))
        {
            YieldProcessor();
        }
        size_t current = committed.load(std::memory_order_relaxed);
        bool success = true;
        if (required > current)
        {
            size_t increment = current < CommitChunkSlots ? CommitChunkSlots : current;
            increment = increment > MaximumCommitChunkSlots ? MaximumCommitChunkSlots : increment;
            size_t next = capacity - current < increment ? capacity : current + increment;
            next = next < required ? required : next;
            auto fits = [&](size_t target)
            {
                size_t nativeBytes = engine->GetNativePlaneCommittedBytes() +
                    referenceStorage.ProjectedCommittedBytes(current, target - current) -
                    referenceStorage.GetCommittedBytes();
                if (published != nullptr)
                {
                    nativeBytes += publicationStorage.ProjectedCommittedBytes(current, target - current) -
                        publicationStorage.GetCommittedBytes();
                }
                return engine->m_object_model.canAdmitRange == nullptr ||
                    engine->m_object_model.canAdmitRange(0, 0, nativeBytes, engine->m_object_model.context);
            };
            success = fits(next);
            if (!success)
            {
                size_t pageSlots = GCToOSInterface::GetPageSize() / sizeof(uintptr_t);
                size_t minimum = (required + pageSlots - 1) & ~(pageSlots - 1);
                minimum = minimum > capacity ? capacity : minimum;
                success = minimum < next && fits(minimum);
                next = minimum;
            }
            if (success)
            {
                success = referenceStorage.Commit(current, next - current, false) &&
                    (published == nullptr || publicationStorage.Commit(current, next - current));
                if (success)
                {
                    committed.store(next, std::memory_order_release);
                }
            }
        }
        engine->m_log_commit_lock.clear(std::memory_order_release);
        return success;
    }

    // ResetBank owns the stream exclusively, after all readers of the old bank have finished.
    bool Trim(size_t used)
    {
        size_t keep = (used + CommitChunkSlots - 1) & ~(CommitChunkSlots - 1);
        keep = keep < CommitChunkSlots ? CommitChunkSlots : keep;
        size_t current = committed.load(std::memory_order_relaxed);
        if (keep >= current || current - keep < keep)
        {
            return true;
        }
        if (!referenceStorage.Decommit(keep, current - keep, true) ||
            (published != nullptr && !publicationStorage.Decommit(keep, current - keep, true)))
        {
            return false;
        }
        committed.store(keep, std::memory_order_release);
        return true;
    }
};

struct LxrReferenceCounting::LogBank
{
    LogStream increments;
    LogStream decrements;
    size_t capacity = 0;
};

struct LxrReferenceCounting::RememberedChunk
{
    struct Entry
    {
        Object** slot;
        uint8_t reuse;
    };

    Entry entries[BufferCapacity]{};
    std::atomic<uint8_t> published[BufferCapacity]{};
    std::atomic<size_t> reserved{0};
    RememberedChunk* next = nullptr;
};

class LxrReferenceCounting::CostScope final
{
public:
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
    CostScope(LxrReferenceCounting* engine, size_t metric)
        : m_engine(engine), m_metric(metric), m_start(-1)
    {
        if (engine->m_storage_diagnostics.load(std::memory_order_relaxed))
        {
            m_start = Clock();
        }
    }

    ~CostScope()
    {
        if (m_start >= 0)
        {
            m_engine->m_cost_ticks[m_metric].fetch_add(
                static_cast<uint64_t>(Clock() - m_start), std::memory_order_relaxed);
        }
    }

private:
    static int64_t Clock()
    {
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        return std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::steady_clock::now().time_since_epoch()).count();
#else
        return minipal_hires_ticks();
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    }

    LxrReferenceCounting* m_engine;
    size_t m_metric;
    int64_t m_start;
#else
    CostScope(LxrReferenceCounting* engine, size_t metric)
    {
        (void)engine;
        (void)metric;
    }

    ~CostScope() = default;
#endif // LXR_HAS_DIAGNOSTIC_PROBES
};

int64_t LxrPauseTimers::Now()
{
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    return std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::steady_clock::now().time_since_epoch()).count();
#else
    return minipal_hires_ticks();
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
}

int64_t LxrPauseTimers::Frequency()
{
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    return 1000000000;
#else
    return minipal_hires_tick_frequency();
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
}

uint64_t LxrPauseTimers::ToNanoseconds(int64_t ticks)
{
    if (ticks <= 0)
    {
        return 0;
    }
    uint64_t frequency = static_cast<uint64_t>(Frequency());
    uint64_t value = static_cast<uint64_t>(ticks);
    return value / frequency * 1000000000 + value % frequency * 1000000000 / frequency;
}

LxrPauseTimers::LxrPauseTimers()
    : m_scalar{}, m_last{}, m_total{}, m_kind_total{}
{
}

void LxrPauseTimers::Begin(int64_t waited)
{
    if (!Enabled())
    {
        return;
    }
    for (std::atomic<int64_t>& value : m_scalar)
    {
        value.store(0, std::memory_order_relaxed);
    }
    for (GroupState& group : m_groups)
    {
        group.busy.store(0, std::memory_order_relaxed);
        group.first.store(INT64_MAX, std::memory_order_relaxed);
        group.last.store(0, std::memory_order_relaxed);
        group.packets.store(0, std::memory_order_relaxed);
    }
    m_scalar[LockWait].store(waited, std::memory_order_relaxed);
    m_paused.store(false, std::memory_order_relaxed);
    m_begin_timestamps = m_timestamps.load(std::memory_order_relaxed);
    m_begin = Stamp();
}

void LxrPauseTimers::AddPacket(Group group, int64_t start, int64_t end)
{
    GroupState& state = m_groups[group];
    state.busy.fetch_add(end - start, std::memory_order_relaxed);
    state.packets.fetch_add(1, std::memory_order_relaxed);
    int64_t seen = state.first.load(std::memory_order_relaxed);
    while (start < seen && !state.first.compare_exchange_weak(seen, start, std::memory_order_relaxed))
    {
    }
    seen = state.last.load(std::memory_order_relaxed);
    while (end > seen && !state.last.compare_exchange_weak(seen, end, std::memory_order_relaxed))
    {
    }
}

void LxrPauseTimers::MarkPause(uint8_t kind)
{
    m_kind.store(kind, std::memory_order_relaxed);
    m_paused.store(true, std::memory_order_relaxed);
}

void LxrPauseTimers::End()
{
    if (!Enabled() || !m_paused.load(std::memory_order_relaxed))
    {
        return;
    }
    m_scalar[Total].store(Stamp() - m_begin, std::memory_order_relaxed);
    uint64_t values[PublishedCount];
    for (size_t index = 0; index < ScalarCount; index++)
    {
        values[index] = ToNanoseconds(m_scalar[index].load(std::memory_order_relaxed));
    }
    for (size_t group = 0; group < GroupCount; group++)
    {
        uint64_t packets = m_groups[group].packets.load(std::memory_order_relaxed);
        int64_t first = m_groups[group].first.load(std::memory_order_relaxed);
        int64_t last = m_groups[group].last.load(std::memory_order_relaxed);
        size_t slot = ScalarCount + 3 * group;
        values[slot] = packets != 0 ? ToNanoseconds(last - first) : 0;
        values[slot + 1] = ToNanoseconds(m_groups[group].busy.load(std::memory_order_relaxed));
        values[slot + 2] = packets;
    }
    m_sequence.fetch_add(1, std::memory_order_acq_rel);
    size_t kind = static_cast<size_t>(m_kind.load(std::memory_order_relaxed));
    for (size_t index = 0; index < PublishedCount; index++)
    {
        m_last[index].store(values[index], std::memory_order_relaxed);
        m_total[index].fetch_add(values[index], std::memory_order_relaxed);
        if (kind < KindCount)
        {
            m_kind_total[kind][index].fetch_add(values[index], std::memory_order_relaxed);
        }
    }
    if (kind < KindCount)
    {
        m_kind_total[kind][PublishedCount].fetch_add(1, std::memory_order_relaxed);
    }
    m_published_timestamps.store(
        m_timestamps.load(std::memory_order_relaxed) - m_begin_timestamps, std::memory_order_relaxed);
    m_sequence.fetch_add(1, std::memory_order_release);
}

uint64_t LxrPauseTimers::GetMeta(size_t index) const
{
    switch (index)
    {
        case MetaSequence: return m_sequence.load(std::memory_order_acquire);
        case MetaKind: return m_kind.load(std::memory_order_relaxed);
        case MetaTimestamps: return m_published_timestamps.load(std::memory_order_relaxed);
        case MetaCost:
        {
            // Mean cost of one timestamp, measured on demand and never inside a pause.
            constexpr int Samples = 2000;
            int64_t start = Now();
            int64_t sink = 0;
            for (int sample = 0; sample < Samples; sample++)
            {
                sink += Now();
            }
            int64_t end = Now();
            return ToNanoseconds(end - start) / Samples + (sink == 0 ? 1 : 0);
        }
        default: return UINT64_MAX;
    }
}

uint64_t LxrPauseTimers::GetLast(size_t index) const
{
    return index < PublishedCount ? m_last[index].load(std::memory_order_relaxed) : UINT64_MAX;
}

uint64_t LxrPauseTimers::GetTotal(size_t index) const
{
    return index < PublishedCount ? m_total[index].load(std::memory_order_relaxed) : UINT64_MAX;
}

uint64_t LxrPauseTimers::GetKindTotal(size_t kind, size_t index) const
{
    return kind < KindCount && index <= PublishedCount
        ? m_kind_total[kind][index].load(std::memory_order_relaxed) : UINT64_MAX;
}

namespace
{
    uint64_t TicksToNanoseconds(int64_t ticks);

    class PacketScope final
    {
    public:
#ifdef LXR_HAS_PHASE_TIMERS
        PacketScope(LxrPauseTimers& timers, LxrPauseTimers::Group group, bool timed)
            : m_timers(timers), m_group(group), m_start(timed && timers.Enabled() ? timers.Stamp() : 0)
        {
        }

        ~PacketScope()
        {
            if (m_start != 0)
            {
                m_timers.AddPacket(m_group, m_start, m_timers.Stamp());
            }
        }
#else
        PacketScope(LxrPauseTimers& timers, LxrPauseTimers::Group group, bool timed)
        {
            (void)timers;
            (void)group;
            (void)timed;
        }

        ~PacketScope() = default;
#endif // LXR_HAS_PHASE_TIMERS

        PacketScope(const PacketScope&) = delete;
        PacketScope& operator=(const PacketScope&) = delete;

#ifdef LXR_HAS_PHASE_TIMERS
    private:
        LxrPauseTimers& m_timers;
        LxrPauseTimers::Group m_group;
        int64_t m_start;
#endif // LXR_HAS_PHASE_TIMERS
    };
}

struct LxrReferenceCounting::Work
{
    LxrReferenceCounting* engine;
    WorkKind kind;
    Work* next;
    Work* traceNext;
    uintptr_t* ownedReferences;
    const uintptr_t* references;
    LxrWorkPacket packet;
    bool trace;
    TraceScan* scan;
    // The remembered chunk that a replay packet works through (WorkKind::ReplayRemembered).
    RememberedChunk* chunk = nullptr;
    std::atomic<bool> traceProcessed;
    std::atomic<bool> doneQueued;
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    bool watchedReader = false;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM

    Work(LxrReferenceCounting* owner, uint64_t id, WorkKind workKind,
         const uintptr_t* values, size_t count, LxrWorkBucketStage stage,
         bool traceObjects, TraceScan* traceScan, bool borrow, bool logged)
        : engine(owner), kind(workKind), next(nullptr), traceNext(nullptr),
          ownedReferences(count == 0 || borrow ? nullptr : new (std::nothrow) uintptr_t[count]),
          references(borrow ? values : ownedReferences),
          packet(id, stage, references, count, Execute, this), trace(traceObjects), scan(traceScan),
          traceProcessed(false), doneQueued(false)
    {
        if (count != 0 && ownedReferences != nullptr)
        {
            memcpy(ownedReferences, values, count * sizeof(uintptr_t));
            if (logged)
            {
                ownedReferences[0] &= LogReferenceMask;
            }
        }
    }

    uintptr_t GetReference(size_t index) const
    {
        uintptr_t value = references[index];
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_LOG_DECODE_CONTROL)
        return value;
#else
        return index == 0 && ownedReferences == nullptr ? value & LogReferenceMask : value;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_LOG_DECODE_CONTROL
    }

    ~Work()
    {
        delete[] ownedReferences;
    }
};

struct LxrReferenceCounting::TraceScan
{
    Object* object;
    GCReferenceRangeIterator iterator;
    GCReferenceRange range;
    size_t cursor;
    Object* loaderAllocator;
    std::atomic<bool> complete;
    TraceScan* next;
    std::atomic<size_t> users{0};
    // Key and chain of the index of split scans (see FindTraceScan). Changed only under the lock of the bucket.
    size_t indexSlot = SIZE_MAX;
    TraceScan* indexNext = nullptr;

    TraceScan(Object* target, Object* loader)
        : object(target), iterator(target), range{}, cursor(0),
          loaderAllocator(loader), complete(false), next(nullptr)
    {
    }
};

struct LxrReferenceCounting::ReferenceBatch
{
    LxrReferenceCounting* engine;
    WorkKind kind;
    const LxrWorkPacket* parent;
    uintptr_t references[BufferCapacity];
    size_t count = 0;
    bool trace = false;

    LxrRcResult Flush()
    {
        if (count == 0)
        {
            return LxrRcResult::Success;
        }
        LxrRcResult result = LxrRcResult::Success;
        LxrWorkBucketStage stage = LxrRCProcessIncs;
        if (kind == WorkKind::Decrements)
        {
            stage = engine->m_pause_kind == LxrPauseKind::Full || !engine->m_configuration.lazyDecrements
                ? LxrRCProcessDecs : LxrWorkBucketStage::Unconstrained;
            if (engine->MarkDeadObjects())
            {
#ifdef LXR_HAS_PHASE_TIMERS
                engine->CountWindow(2, count);
#endif // LXR_HAS_PHASE_TIMERS
                result = engine->QueueTrace(references, count);
            }
        }
        if (result == LxrRcResult::Success)
        {
            result = engine->SubmitWork(
                engine->NewWork(kind, references, count, stage, trace), parent);
        }
        if (result == LxrRcResult::Success)
        {
            count = 0;
        }
        return result;
    }

    LxrRcResult Append(uintptr_t reference, bool traceChildren)
    {
        if (count != 0 && trace != traceChildren)
        {
            LxrRcResult result = Flush();
            if (result != LxrRcResult::Success)
            {
                return result;
            }
        }
        trace = traceChildren;
        references[count++] = reference;
        return count == BufferCapacity ? Flush() : LxrRcResult::Success;
    }
};

struct LxrReferenceCounting::TraceBatch
{
    LxrReferenceCounting* engine;
    size_t count = 0;
    bool registered = false;
    bool accounted = false;
    uintptr_t references[FullTraceBatchCapacity];

    explicit TraceBatch(LxrReferenceCounting* owner) : engine(owner)
    {
        // Unpublished targets retain completion ownership until copied work takes over.
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_TRACE_BATCH_EARLY_CONTROL)
        uint64_t pending = engine->m_trace_pending.load(std::memory_order_relaxed);
        while (pending != UINT64_MAX &&
            !engine->m_trace_pending.compare_exchange_weak(
                pending, pending + 1, std::memory_order_acq_rel))
        {
        }
        if (pending == UINT64_MAX)
        {
            engine->Fail(LxrRcResult::CapacityExceeded);
            return;
        }
        registered = true;
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_TRACE_BATCH_EARLY_CONTROL
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
        if (engine->m_storage_diagnostics.load(std::memory_order_relaxed))
        {
            accounted = true;
            engine->m_trace_batch_metrics[0].fetch_add(1, std::memory_order_relaxed);
            uint64_t current = engine->m_trace_batch_metrics[3].fetch_add(
                sizeof(TraceBatch), std::memory_order_relaxed) + sizeof(TraceBatch);
            uint64_t high = engine->m_trace_batch_metrics[4].load(std::memory_order_relaxed);
            while (high < current && !engine->m_trace_batch_metrics[4].compare_exchange_weak(
                high, current, std::memory_order_relaxed))
            {
            }
            current = engine->m_storage_metrics[1].fetch_add(
                sizeof(TraceBatch), std::memory_order_relaxed) + sizeof(TraceBatch);
            high = engine->m_storage_metrics[2].load(std::memory_order_relaxed);
            while (high < current && !engine->m_storage_metrics[2].compare_exchange_weak(
                high, current, std::memory_order_relaxed))
            {
            }
        }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
    }

    TraceBatch(const TraceBatch&) = delete;
    TraceBatch& operator=(const TraceBatch&) = delete;

    ~TraceBatch()
    {
        if (count != 0 && engine->GetFailure() == LxrRcResult::Success)
        {
            engine->Fail(LxrRcResult::InvariantViolation);
        }
        if (registered && engine->m_trace_pending.fetch_sub(1, std::memory_order_acq_rel) == 0)
        {
            engine->Fail(LxrRcResult::InvariantViolation);
        }
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
        if (accounted)
        {
            engine->m_trace_batch_metrics[3].fetch_sub(sizeof(TraceBatch), std::memory_order_relaxed);
            engine->m_storage_metrics[1].fetch_sub(sizeof(TraceBatch), std::memory_order_relaxed);
        }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
    }

    LxrRcResult Append(uintptr_t target)
    {
        if (count == FullTraceBatchCapacity)
        {
            return engine->Fail(LxrRcResult::InvariantViolation);
        }
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
        if (engine->m_storage_diagnostics.load(std::memory_order_relaxed))
        {
            engine->m_trace_batch_metrics[1].fetch_add(1, std::memory_order_relaxed);
        }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_TRACE_BATCH_UNBATCHED_CONTROL)
        return engine->QueueTrace(&target, 1);
#else
        references[count++] = target;
        return count >= engine->m_trace_batch_limit.load(std::memory_order_relaxed) ? Flush() : LxrRcResult::Success;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_TRACE_BATCH_UNBATCHED_CONTROL
    }

    LxrRcResult Flush()
    {
        if (count == 0)
        {
            return LxrRcResult::Success;
        }
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_TRACE_BATCH_DROP_CONTROL)
        return LxrRcResult::Success;
#else
        LxrRcResult result = engine->QueueTrace(references, count);
        if (result == LxrRcResult::Success)
        {
            count = 0;
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
            if (engine->m_storage_diagnostics.load(std::memory_order_relaxed))
            {
                engine->m_trace_batch_metrics[2].fetch_add(1, std::memory_order_relaxed);
            }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
        }
        return result;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_TRACE_BATCH_DROP_CONTROL
    }
};

struct LxrReferenceCounting::DeathContext
{
    LxrReferenceCounting* engine;
    ReferenceBatch* children;
    size_t worker;
};

// One cache line group per worker, written only by that worker with plain relaxed stores, and summed when
// read. The last entry is shared by callers that are not workers and is updated with atomic additions.
// The hot pause counters (IncrementEntries, DecrementEntries, Promotions, ObjectScans, ObjectSizeQueries,
// Deaths, RootIncrements, RootDecrements) use the slot of their Metric index.
struct alignas(64) LxrReferenceCounting::WorkerCounters
{
    std::atomic<uint64_t> values[32]{};
};

// A copied source keeps its first field only until the copy is published, so the size of a forwarded source
// cannot be read from the source afterwards (an array or string length is that field). The copy path stores the
// size here, and RetireSourceChunk consumes the records. Only the worker that owns a list appends to it;
// the shared slot (mature copy) takes a spin lock.
struct alignas(64) LxrReferenceCounting::CopyLog
{
    struct Chunk
    {
        // The chunk is allocated with new and freed with delete by every worker, one chunk for each 1000 copied objects. On
        // Windows the process heap serves only blocks up to 16368 bytes from its scalable low-fragmentation front end. A larger
        // block goes to the back end, which takes a global lock and walks its free lists: with 13 workers and tens of
        // thousands of chunks in one pause the delete loop of RetireSourceChunks took 38 s. 1020 entries keep the chunk
        // inside the front end. The assert keeps it so.
        static constexpr size_t Capacity = 1020;
        struct Entry
        {
            uintptr_t start;
            size_t size;
        };

        Entry entries[Capacity];
        size_t count = 0;
        Chunk* next = nullptr;
    };
    static_assert(sizeof(Chunk) <= 16368, "a copy log chunk must stay in the low-fragmentation heap on Windows");

    Chunk* head = nullptr;
    std::atomic_flag lock = ATOMIC_FLAG_INIT;
};

namespace
{
    // P28 (F10). The copy counters are per worker, as the survival counters of the Java LXR (SurvivalRatioPredictorLocal: a local
    // copy that is summed when it is read), not three shared atomics that every worker adds to for every copied object.
    constexpr size_t CopiedObjects = Epochs + 1;
    constexpr size_t CopiedBytes = Epochs + 2;
    constexpr size_t PauseCopiedBytes = Epochs + 3;
    constexpr size_t PromotedBytes = Epochs + 4;
    // The trace counts its objects and references per worker: a counter that every worker adds to for every object and
    // every edge is a contended cache line.
    constexpr size_t TraceObjectsCounter = Epochs + 5;
    constexpr size_t TraceReferencesCounter = Epochs + 6;
    // The mature copy counts per worker for the same reason.
    constexpr size_t MatureCopiedObjects = Epochs + 7;
    constexpr size_t MatureCopiedBytes = Epochs + 8;
    constexpr size_t PauseMatureCopiedBytes = Epochs + 9;
    static_assert(PauseMatureCopiedBytes < 32);
}

LxrRcMutator::LxrRcMutator()
    : engine(nullptr), state(0), identity(0), owner(0), allocator(),
      increments(nullptr), decrements(nullptr), publishedCursor(0), publishedLimit(0),
      spanStart(0), overflowAllocator(), overflowSpanStart(0), overflowObjects(0), overflowBytes(0), spans(),
      loggedSlots(0), oldTargetChecks(0)
{
}

LxrReferenceCounting::LxrReferenceCounting()
    : m_configuration{}, m_object_model{}, m_observer(false), m_runtime{},
      m_capture(nullptr), m_capture_context(nullptr),
      m_layout{}, m_metadata(), m_blocks(), m_counts(), m_allocation(), m_large(),
      m_scheduler(), m_coordinator(), m_arena(nullptr),
      m_trace_locks(nullptr), m_scan_buckets(nullptr),
      m_scan_work(nullptr), m_trace_phase(LxrTracePhase::Inactive), m_marking(false),
      m_hold_trace(false), m_first_window(false), m_scan_checkpoint(0), m_scan_checkpoint_active(false),
      m_trace_pending(0), m_trace_metrics{},
      m_pause_kind(LxrPauseKind::RefCount), m_cycle_sweep(false),
      m_mutators(nullptr), m_mutator_chunks(nullptr),
      m_banks(nullptr), m_roots{nullptr, nullptr}, m_root_counts{0, 0}, m_root_capacity{0, 0},
      m_current_roots(0), m_dead_large(nullptr), m_mature_candidates(nullptr), m_dead_large_count(0),
      m_producer_bank(0), m_work(nullptr), m_trace_unsubmitted(nullptr), m_trace_done(nullptr), m_next_id(1),
      m_phase(LxrRcPhase::Uninitialized), m_failure(LxrRcResult::Success), m_metrics{}
{
}

LxrReferenceCounting::~LxrReferenceCounting()
{
    Shutdown();
}

LxrRcResult LxrReferenceCounting::Initialize(
    const LxrRcConfiguration& configuration, const LxrPauseRuntimeAdapter& runtime,
    const LxrRcObjectModel* objectModel)
{
    if (GetPhase() != LxrRcPhase::Uninitialized ||
        configuration.immixBytes == 0 ||
        configuration.immixBytes % ImmixBlockGeometry::BlockBytes != 0 ||
        configuration.largeObjectBytes < LxrLargeObjectSpace::MinimumObjectBytes ||
        configuration.largeObjectBytes % LxrLargeObjectSpace::PageBytes != 0 ||
        configuration.maximumMutators == 0 || configuration.maximumRoots == 0 ||
        configuration.workers == 0 || runtime.suspend == nullptr || runtime.resume == nullptr ||
        configuration.minimumReuseLines == 0 ||
        configuration.minimumReuseLines > ImmixBlockGeometry::LinesPerBlock ||
        configuration.bufferSize == 0 || configuration.bufferSize > BufferCapacity ||
        configuration.defragHeadroomPercent > 50 || configuration.chunkDefragPercent > 100 ||
        configuration.immixBytes > SIZE_MAX - configuration.largeObjectBytes)
    {
        return LxrRcResult::InvalidArgument;
    }
    uint8_t logBits;
    switch (configuration.referenceCountBits)
    {
        case 2: logBits = 1; break;
        case 4: logBits = 2; break;
        case 8: logBits = 3; break;
        default: return LxrRcResult::InvalidArgument;
    }
    m_configuration = configuration;
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    // P50. A run of any test can use the parallel selection (and the comparison with the serial one) in every pause.
    if (const char* blocks = getenv("LXR_TEST_SELECT_PARALLEL_BLOCKS"))
    {
        m_configuration.selectParallelBlocks = static_cast<uint32_t>(strtoul(blocks, nullptr, 10));
    }
    if (const char* validate = getenv("LXR_TEST_SELECT_VALIDATE"))
    {
        m_configuration.selectValidate = *validate == '1';
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    m_initial_immix = configuration.immixBytes;
    m_initial_large = configuration.largeObjectBytes;
    m_configuration.immixBytes = configuration.immixReservationBytes == 0
        ? configuration.immixBytes : configuration.immixReservationBytes;
    m_configuration.largeObjectBytes = configuration.largeObjectReservationBytes == 0
        ? configuration.largeObjectBytes : configuration.largeObjectReservationBytes;
    if (m_configuration.immixBytes < m_initial_immix ||
        m_configuration.largeObjectBytes < m_initial_large ||
        m_configuration.immixBytes % ImmixBlockGeometry::BlockBytes != 0 ||
        m_configuration.largeObjectBytes % ImmixBlockGeometry::BlockBytes != 0 ||
        m_configuration.immixBytes > SIZE_MAX - m_configuration.largeObjectBytes)
    {
        return LxrRcResult::InvalidArgument;
    }
    if (m_configuration.hardLimitBytes == 0)
    {
        m_configuration.hardLimitBytes = GetReservationBytes();
    }
    if (m_initial_immix + m_initial_large > m_configuration.hardLimitBytes ||
        m_configuration.hardLimitBytes > GetReservationBytes())
    {
        return LxrRcResult::InvalidArgument;
    }
    size_t slots = GetReservationBytes() / sizeof(uintptr_t);
    m_immix_capacity.store(m_initial_immix, std::memory_order_relaxed);
    m_immix_floor = m_initial_immix;
    // Overshoot needs room in the reservation and no hard limit below it. With a hard limit the engine never admits
    // more than the configuration allows, so this mode keeps the reserve of the trigger and nothing else.
    m_overshoot = configuration.overshootToSpace && (configuration.evacuateNursery || configuration.evacuateMature) &&
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_S5B_IGNORE_HARD_LIMIT_CONTROL)
        m_configuration.hardLimitBytes == GetReservationBytes() &&
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_S5B_IGNORE_HARD_LIMIT_CONTROL
        m_configuration.immixBytes > m_initial_immix;
    m_overshoot_metrics[PeakCapacityBytes].store(m_initial_immix, std::memory_order_relaxed);
    m_object_model = objectModel == nullptr ? LxrRcObjectModel{} : *objectModel;
    m_runtime = runtime;
    LxrRcResult buffers = InitializeBuffers(slots);
    if (buffers != LxrRcResult::Success)
    {
        return buffers;
    }
    if (m_layout.Create(logBits, &m_layout) != SideMetadataResult::Success ||
#ifdef LXR_OBJECT_START_WALK
        // The walk variant never commits, reads or writes the ValidObject plane.
        m_metadata.Initialize(&m_layout, ((UINT64_C(1) << LxrSideMetadataLayout::SpecCount) - 1) &
            ~(UINT64_C(1) << static_cast<size_t>(LxrSideMetadataKind::ValidObject))) != SideMetadataResult::Success)
#else
        m_metadata.Initialize(&m_layout) != SideMetadataResult::Success)
#endif // LXR_OBJECT_START_WALK
    {
        return Fail(LxrRcResult::MetadataFailure);
    }
    m_arena = GCToOSInterface::VirtualReserve(
        GetReservationBytes(), ImmixBlockGeometry::BlockBytes, VirtualReserveFlags::None);
    if (m_arena == nullptr ||
        reinterpret_cast<uintptr_t>(m_arena) >= LxrSideMetadataLayout::AddressLimit ||
        GetReservationBytes() > LxrSideMetadataLayout::AddressLimit -
            reinterpret_cast<uintptr_t>(m_arena) ||
        !GCToOSInterface::VirtualCommit(m_arena, m_initial_immix))
    {
        return Fail(LxrRcResult::AllocationFailure);
    }
    uintptr_t arena = reinterpret_cast<uintptr_t>(m_arena);
    if (m_metadata.CommitDataRange(arena, m_initial_immix) != SideMetadataResult::Success ||
        m_metadata.CommitDataRange(arena + m_configuration.immixBytes, m_initial_large) !=
            SideMetadataResult::Success ||
        m_blocks.Initialize(&m_metadata) != SideMetadataResult::Success ||
        m_blocks.RegisterBlockRange(arena, m_initial_immix) != SideMetadataResult::Success ||
        m_counts.Initialize(&m_metadata, &m_layout) != SideMetadataResult::Success ||
        !(m_rc_metadata = m_metadata.GetAccessor(LxrSideMetadataKind::ReferenceCount)).IsValid() ||
        !(m_mark_metadata = m_metadata.GetAccessor(LxrSideMetadataKind::ObjectMark)).IsValid() ||
        !(m_unlogged_metadata = m_metadata.GetAccessor(LxrSideMetadataKind::FieldUnlogged)).IsValid() ||
#ifdef LXR_OBJECT_START_WALK
        !(m_straddle_metadata = m_metadata.GetAccessor(LxrSideMetadataKind::ReferenceCountStraddle)).IsValid() ||
#else
        !(m_valid_metadata = m_metadata.GetAccessor(LxrSideMetadataKind::ValidObject)).IsValid() ||
#endif // LXR_OBJECT_START_WALK
        !(m_defrag_metadata = m_metadata.GetAccessor(LxrSideMetadataKind::BlockDefrag)).IsValid() ||
        !(m_line_reuse_metadata = m_metadata.GetAccessor(LxrSideMetadataKind::LineReuse)).IsValid() ||
        !(m_page_reuse_metadata = m_metadata.GetAccessor(LxrSideMetadataKind::LargeObjectPageReuse)).IsValid() ||
        m_allocation.Initialize(&m_metadata, &m_blocks, arena, m_initial_immix,
            configuration.minimumReuseLines) !=
            SideMetadataResult::Success ||
        m_large.Initialize(&m_metadata, m_configuration.largeObjectBytes,
            reinterpret_cast<void*>(arena + m_configuration.immixBytes), m_initial_large) !=
                LxrLargeObjectResult::Success)
    {
        return Fail(LxrRcResult::MetadataFailure);
    }
    if (m_configuration.largeObjectBytes != 0 && !GrowDeadLarge())
    {
        return Fail(LxrRcResult::OutOfMemory);
    }
    if (m_overshoot)
    {
        m_allocation.EnablePlacementQuiescent(m_immix_floor / ImmixBlockGeometry::BlockBytes);
    }
    if (m_scheduler.Initialize(configuration.workers, true, true,
            DecrementsDrained, OverallDrained, this) != LxrWorkSchedulerResult::Success)
    {
        return Fail(LxrRcResult::SchedulerFailure);
    }
    if (m_scheduler.SetCancelledPacketCallback(ReleaseCancelledPacket, this) != LxrWorkSchedulerResult::Success)
    {
        return Fail(LxrRcResult::SchedulerFailure);
    }
    if (configuration.concurrentTraceWorkers != 0 &&
        m_scheduler.ConfigureConcurrentTraceWorkers(
            configuration.concurrentTraceWorkers < configuration.workers
                ? configuration.concurrentTraceWorkers : configuration.workers) != LxrWorkSchedulerResult::Success)
    {
        return Fail(LxrRcResult::SchedulerFailure);
    }
    m_allocation.SetAcquireNotes(
        {m_allocated_block_storage.Get(), m_allocated_list, &m_allocated_count, m_mature_candidates, m_block_flags,
            m_zero_state});
    for (size_t index = 0; index < configuration.maximumMutators; index++)
    {
        if (m_mutators[index].allocator.Initialize(&m_allocation, index + 1) != SideMetadataResult::Success ||
            m_mutators[index].overflowAllocator.Initialize(&m_allocation, index + 1) != SideMetadataResult::Success)
        {
            return Fail(LxrRcResult::AllocationFailure);
        }
    }
    // The pinned allocator takes the owner number after every mutator and every copy allocator. The pause thread has copy
    // allocators too: it runs packets itself (LxrWorkScheduler::RunStwCycle) with the execution index workers.
    if (m_pinned_allocator.Initialize(
            &m_allocation, configuration.maximumMutators + 2 * (configuration.workers + 1) + 1) !=
        SideMetadataResult::Success)
    {
        return Fail(LxrRcResult::AllocationFailure);
    }
    if (configuration.evacuateNursery || configuration.evacuateMature)
    {
        size_t allocatorBytes;
        if (configuration.workers > (SIZE_MAX - configuration.maximumMutators) / 2 - 1 ||
            !TryMultiply(2 * (configuration.workers + 1), sizeof(ImmixBumpAllocator), &allocatorBytes) ||
            configuration.forwardingMask == 0 || configuration.forwardingShift >= 32 ||
            (configuration.forwardingMask >> configuration.forwardingShift) != 3)
        {
            return Fail(LxrRcResult::InvalidArgument);
        }
        m_copy_allocators = new (std::nothrow) ImmixBumpAllocator[2 * (configuration.workers + 1)];
        if (m_copy_allocators == nullptr)
        {
            return Fail(LxrRcResult::OutOfMemory);
        }
        for (size_t index = 0; index < 2 * (configuration.workers + 1); index++)
        {
            if (m_copy_allocators[index].Initialize(
                    &m_allocation, configuration.maximumMutators + index + 1) != SideMetadataResult::Success)
            {
                return Fail(LxrRcResult::AllocationFailure);
            }
        }
    }
    LxrPauseRuntimeAdapter adapter{Suspend, Resume, this, runtime.finishedGc};
    if (m_coordinator.Initialize(&m_scheduler, adapter) != LxrPauseCoordinatorResult::Success)
    {
        return Fail(LxrRcResult::SchedulerFailure);
    }
    if (m_zero_state != nullptr && m_configuration.prezeroThread)
    {
        // Without the thread every block is cleared by the allocator that takes it, as before.
        m_zeroer.Start(&m_allocation, m_configuration.prezeroMarginBlocks, m_configuration.prezeroWindowBlocks);
    }
    m_phase.store(LxrRcPhase::Mutator, std::memory_order_release);
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::InitializeBuffers(size_t slots)
{
    size_t bytes;
    if (m_configuration.maximumMutators > SIZE_MAX / 2 ||
        !TryMultiply(2 * m_configuration.maximumMutators, sizeof(LogChunk), &bytes) ||
        !TryMultiply(slots, sizeof(uintptr_t), &bytes) ||
        !TryMultiply(slots, sizeof(std::atomic<uint16_t>), &bytes) ||
        !TryMultiply(slots, sizeof(std::atomic<size_t>), &bytes) ||
        !TryMultiply(slots, sizeof(TraceScan*), &bytes) ||
        !TryMultiply(m_configuration.maximumMutators, sizeof(LxrRcMutator), &bytes) ||
        !TryMultiply(m_configuration.maximumRoots, sizeof(uintptr_t), &bytes))
    {
        return LxrRcResult::CapacityExceeded;
    }
    m_worker_counters = new (std::nothrow) WorkerCounters[m_configuration.workers + 1];
    if (m_worker_counters == nullptr)
    {
        return LxrRcResult::OutOfMemory;
    }
    m_copy_logs = new (std::nothrow) CopyLog[m_configuration.workers + 1];
    if (m_copy_logs == nullptr)
    {
        return LxrRcResult::OutOfMemory;
    }
    m_mutators = new (std::nothrow) LxrRcMutator[m_configuration.maximumMutators];
    m_mutator_chunks = new (std::nothrow) LogChunk[2 * m_configuration.maximumMutators];
    m_banks = new (std::nothrow) LogBank[2];
    if (!m_observer)
    {
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        size_t objectSlots = GetObjectStorageSlots();
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        if (
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
            !m_extent_storage.Reserve(objectSlots) || !m_live_storage.Reserve(slots / 64) ||
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
            !m_allocated_block_storage.Reserve(m_configuration.immixBytes / ImmixBlockGeometry::BlockBytes) ||
            !CommitPlanes(0, m_initial_immix / sizeof(uintptr_t)) ||
            !CommitPlanes(m_configuration.immixBytes / sizeof(uintptr_t),
                m_initial_large / sizeof(uintptr_t)))
        {
            return Fail(LxrRcResult::OutOfMemory);
        }
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        m_extents = m_extent_storage.Get();
        m_live_objects = m_live_storage.Get();
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        m_trace_locks = new (std::nothrow) std::atomic<uint8_t>[TraceLockCount]{};
        m_scan_buckets = new (std::nothrow) TraceScanBucket[TraceScanBucketCount];
    }
    m_roots[0] = new (std::nothrow) uintptr_t[m_configuration.maximumRoots]{};
    m_roots[1] = new (std::nothrow) uintptr_t[m_configuration.maximumRoots]{};
    m_root_capacity[0] = m_configuration.maximumRoots;
    m_root_capacity[1] = m_configuration.maximumRoots;
    if (m_configuration.largeObjectBytes != 0)
    {
        // Address space for one entry for each page of the reservation; GrowDeadLarge commits the entries of the admitted pages.
        if (!m_dead_large_storage.Reserve(m_configuration.largeObjectBytes / LxrLargeObjectSpace::PageBytes))
        {
            return Fail(LxrRcResult::OutOfMemory);
        }
        m_dead_large = m_dead_large_storage.Get();
    }
    if (m_configuration.immixBytes != 0)
    {
        // Address space for one entry for each block of the reservation. Committed memory is zero, and the entries of
        // m_allocated_list are written before they are read. GrowBlockTables commits the admitted blocks.
        size_t blocks = m_configuration.immixBytes / ImmixBlockGeometry::BlockBytes;
        bool reserved = m_mature_candidates_storage.Reserve(blocks) && m_block_flags_storage.Reserve(blocks) &&
            m_allocated_list_storage.Reserve(blocks);
        m_mature_candidates = m_mature_candidates_storage.Get();
        m_block_flags = m_block_flags_storage.Get();
        m_allocated_list = m_allocated_list_storage.Get();
        if (reserved && m_configuration.prezero && !m_observer)
        {
            reserved = m_zero_state_storage.Reserve(blocks);
            m_zero_state = m_zero_state_storage.Get();
        }
        if (reserved && m_configuration.evacuateMature)
        {
            reserved = m_evacuation_sources_storage.Reserve(blocks);
            m_evacuation_sources = m_evacuation_sources_storage.Get();
        }
        if (!reserved || !GrowBlockTables(m_initial_immix))
        {
            return Fail(LxrRcResult::OutOfMemory);
        }
    }
    if (m_mutators == nullptr || m_mutator_chunks == nullptr ||
        m_banks == nullptr || (!m_observer &&
            (m_trace_locks == nullptr || m_scan_buckets == nullptr)) ||
        m_roots[0] == nullptr ||
        m_roots[1] == nullptr || (m_configuration.largeObjectBytes != 0 && m_dead_large == nullptr) ||
        (m_configuration.immixBytes != 0 && m_mature_candidates == nullptr))
    {
        return Fail(LxrRcResult::OutOfMemory);
    }
    for (size_t index = 0; index < 2; index++)
    {
        LogBank& bank = m_banks[index];
        bank.capacity = m_observer ? slots : (m_initial_immix + m_initial_large) / sizeof(uintptr_t);
        for (LogStream* stream : {&bank.increments, &bank.decrements})
        {
            stream->capacity = bank.capacity;
            if (!stream->referenceStorage.Reserve(slots) ||
                (m_observer && !stream->publicationStorage.Reserve(slots)))
            {
                return Fail(LxrRcResult::OutOfMemory);
            }
            stream->references = stream->referenceStorage.Get();
            stream->published = stream->publicationStorage.Get();
            if (stream->references == nullptr || (m_observer && stream->published == nullptr))
            {
                return Fail(LxrRcResult::OutOfMemory);
            }
        }
    }
    for (size_t index = 0; index < m_configuration.maximumMutators; index++)
    {
        m_mutators[index].engine = this;
        m_mutators[index].owner = index + 1;
    }
    return LxrRcResult::Success;
}

bool LxrReferenceCounting::CommitPlanes(size_t first, size_t count, bool initialize)
{
    size_t immixSlots = m_configuration.immixBytes / sizeof(uintptr_t);
    size_t blockSlots = ImmixBlockGeometry::BlockBytes / sizeof(uintptr_t);
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    size_t objectFirst = GetObjectStorageIndex(first);
    size_t objectCount = GetObjectStorageIndex(first + count) - objectFirst;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    return (first >= immixSlots ||
            m_allocated_block_storage.Commit(first / blockSlots, count / blockSlots, initialize))
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        && m_extent_storage.Commit(objectFirst, objectCount, initialize) &&
        m_live_storage.Commit(first / 64, count / 64, initialize)
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        ;
}

bool LxrReferenceCounting::DecommitPlanes(size_t first, size_t count, bool tailIsLast)
{
    size_t immixSlots = m_configuration.immixBytes / sizeof(uintptr_t);
    size_t blockSlots = ImmixBlockGeometry::BlockBytes / sizeof(uintptr_t);
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    size_t objectFirst = GetObjectStorageIndex(first);
    size_t objectCount = GetObjectStorageIndex(first + count) - objectFirst;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    return (first >= immixSlots ||
            m_allocated_block_storage.Decommit(first / blockSlots, count / blockSlots, tailIsLast))
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        && m_extent_storage.Decommit(objectFirst, objectCount, tailIsLast) &&
        m_live_storage.Decommit(first / 64, count / 64, tailIsLast)
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        ;
}

size_t LxrReferenceCounting::GetObjectStorageIndex(size_t heapSlot) const
{
    size_t immixSlots = m_configuration.immixBytes / sizeof(uintptr_t);
    return heapSlot < immixSlots ? heapSlot :
        immixSlots + (heapSlot - immixSlots) / (LxrLargeObjectSpace::PageBytes / sizeof(uintptr_t));
}

size_t LxrReferenceCounting::GetObjectStorageSlots() const
{
    return m_configuration.immixBytes / sizeof(uintptr_t) +
        m_configuration.largeObjectBytes / LxrLargeObjectSpace::PageBytes;
}

#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
std::atomic<size_t>& LxrReferenceCounting::ExtentSlot(size_t heapSlot) const
{
    return m_extents[GetObjectStorageIndex(heapSlot)];
}
#endif // LXR_SIDE_METADATA_TEST_PLATFORM

std::atomic<uint8_t>& LxrReferenceCounting::TraceLockSlot(size_t heapSlot) const
{
    // Fibonacci hashing spreads neighbouring objects over the locks.
    size_t stripe = static_cast<size_t>(
        (static_cast<uint64_t>(heapSlot) * UINT64_C(0x9E3779B97F4A7C15)) >> (64 - 14));
    static_assert(TraceLockCount == (size_t{1} << 14), "The hash above yields 14 bits.");
    return m_trace_locks[stripe];
}

namespace
{
    template <typename Bucket>
    void LockBucket(Bucket& bucket)
    {
        while (bucket.lock.test_and_set(std::memory_order_acquire))
        {
            YieldProcessor();
        }
    }
}

// The index of the scans of split objects. A bucket lock is a leaf lock: nothing is called while it is held.
// scan->indexSlot is the key of an indexed scan and SIZE_MAX for a scan that is not in the index.
LxrReferenceCounting::TraceScan* LxrReferenceCounting::FindTraceScan(size_t heapSlot) const
{
    if (m_scan_index_count.load(std::memory_order_acquire) == 0)
    {
        return nullptr;
    }
    TraceScanBucket& bucket = m_scan_buckets[
        static_cast<size_t>((static_cast<uint64_t>(heapSlot) * UINT64_C(0x9E3779B97F4A7C15)) >> (64 - 12))];
    static_assert(TraceScanBucketCount == (size_t{1} << 12), "The hash above yields 12 bits.");
    LockBucket(bucket);
    TraceScan* found = bucket.head;
    while (found != nullptr && found->indexSlot != heapSlot)
    {
        found = found->indexNext;
    }
    bucket.lock.clear(std::memory_order_release);
    return found;
}

void LxrReferenceCounting::LinkTraceScan(size_t heapSlot, TraceScan* scan)
{
    TraceScanBucket& bucket = m_scan_buckets[
        static_cast<size_t>((static_cast<uint64_t>(heapSlot) * UINT64_C(0x9E3779B97F4A7C15)) >> (64 - 12))];
    LockBucket(bucket);
    // An entry for the same address that is still here belongs to a dead object whose scan has not been retired.
    for (TraceScan** link = &bucket.head; *link != nullptr; link = &(*link)->indexNext)
    {
        if ((*link)->indexSlot == heapSlot)
        {
            TraceScan* stale = *link;
            *link = stale->indexNext;
            stale->indexNext = nullptr;
            stale->indexSlot = SIZE_MAX;
            m_scan_index_count.fetch_sub(1, std::memory_order_acq_rel);
            break;
        }
    }
    scan->indexSlot = heapSlot;
    scan->indexNext = bucket.head;
    bucket.head = scan;
    m_scan_index_count.fetch_add(1, std::memory_order_acq_rel);
    bucket.lock.clear(std::memory_order_release);
}

void LxrReferenceCounting::UnlinkTraceScan(TraceScan* scan)
{
    size_t heapSlot = scan->indexSlot;
    if (heapSlot == SIZE_MAX)
    {
        return;
    }
    TraceScanBucket& bucket = m_scan_buckets[
        static_cast<size_t>((static_cast<uint64_t>(heapSlot) * UINT64_C(0x9E3779B97F4A7C15)) >> (64 - 12))];
    LockBucket(bucket);
    if (scan->indexSlot == heapSlot)
    {
        for (TraceScan** link = &bucket.head; *link != nullptr; link = &(*link)->indexNext)
        {
            if (*link == scan)
            {
                *link = scan->indexNext;
                scan->indexNext = nullptr;
                scan->indexSlot = SIZE_MAX;
                m_scan_index_count.fetch_sub(1, std::memory_order_acq_rel);
                break;
            }
        }
    }
    bucket.lock.clear(std::memory_order_release);
}

void LxrReferenceCounting::MoveTraceScan(size_t fromSlot, size_t toSlot, TraceScan* scan)
{
    // Both buckets are locked in address order, so a reader finds the scan under the old or the new address, never
    // under neither.
    TraceScanBucket* from = &m_scan_buckets[
        static_cast<size_t>((static_cast<uint64_t>(fromSlot) * UINT64_C(0x9E3779B97F4A7C15)) >> (64 - 12))];
    TraceScanBucket* to = &m_scan_buckets[
        static_cast<size_t>((static_cast<uint64_t>(toSlot) * UINT64_C(0x9E3779B97F4A7C15)) >> (64 - 12))];
    TraceScanBucket* first = from < to ? from : to;
    TraceScanBucket* second = from < to ? to : from;
    LockBucket(*first);
    if (second != first)
    {
        LockBucket(*second);
    }
    if (scan->indexSlot == fromSlot)
    {
        for (TraceScan** link = &from->head; *link != nullptr; link = &(*link)->indexNext)
        {
            if (*link == scan)
            {
                *link = scan->indexNext;
                break;
            }
        }
        for (TraceScan** link = &to->head; *link != nullptr; link = &(*link)->indexNext)
        {
            if ((*link)->indexSlot == toSlot)
            {
                TraceScan* stale = *link;
                *link = stale->indexNext;
                stale->indexNext = nullptr;
                stale->indexSlot = SIZE_MAX;
                m_scan_index_count.fetch_sub(1, std::memory_order_acq_rel);
                break;
            }
        }
        scan->indexSlot = toSlot;
        scan->indexNext = to->head;
        to->head = scan;
    }
    if (second != first)
    {
        second->lock.clear(std::memory_order_release);
    }
    first->lock.clear(std::memory_order_release);
}

LxrRcResult LxrReferenceCounting::InitializeObserver(size_t admittedSlots, size_t maximumMutators)
{
    if (GetPhase() != LxrRcPhase::Uninitialized || admittedSlots == 0 || maximumMutators == 0)
    {
        return LxrRcResult::InvalidArgument;
    }
    m_observer = true;
    m_configuration = {0, 0, maximumMutators, 1, 0, 0};
    LxrRcResult result = InitializeBuffers(admittedSlots);
    if (result == LxrRcResult::Success)
    {
        m_phase.store(LxrRcPhase::Mutator, std::memory_order_release);
    }
    return result;
}

// The counter belongs to the one thread that holds the mutator, so a load and a store are enough (no lock xadd).
static inline void CountMutator(std::atomic<uint64_t>& counter)
{
    counter.store(counter.load(std::memory_order_relaxed) + 1, std::memory_order_relaxed);
}

LxrRcResult LxrReferenceCounting::CaptureClaimedWrite(
    LxrRcMutator* mutator, Object** slot, Object* oldTarget)
{
    if (!m_observer || GetPhase() != LxrRcPhase::Mutator ||
        mutator == nullptr || mutator->engine != this ||
        mutator->state.load(std::memory_order_acquire) != 2 || slot == nullptr)
    {
        return Fail(LxrRcResult::InvalidArgument);
    }
    LxrRcResult result = oldTarget == nullptr ? LxrRcResult::Success :
        Append(mutator, false, reinterpret_cast<uintptr_t>(oldTarget));
    if (result == LxrRcResult::Success)
    {
        result = Append(mutator, true, reinterpret_cast<uintptr_t>(slot));
        CountMutator(mutator->loggedSlots);
    }
    return result;
}

LxrRcResult LxrReferenceCounting::DrainObservedLogs(
    LxrRcUnlogObservedSlot unlog, LxrRcObservedEntry observe, void* context)
{
    if (!m_observer || GetPhase() != LxrRcPhase::Mutator ||
        unlog == nullptr || observe == nullptr)
    {
        return LxrRcResult::InvalidArgument;
    }
    LxrRcResult result = FlushMutators();
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    LogBank& bank = m_banks[m_producer_bank.load(std::memory_order_relaxed)];
    for (bool increment : {true, false})
    {
        LogStream& stream = increment ? bank.increments : bank.decrements;
        size_t limit = stream.reserved.load(std::memory_order_acquire);
        for (size_t cursor = 0; cursor < limit;)
        {
            size_t count;
            result = ReadPublishedRange(stream, cursor, limit, &count);
            if (result != LxrRcResult::Success)
            {
                return result;
            }
            for (size_t index = cursor; index < cursor + count; index++)
            {
                Object* current = nullptr;
                if (increment)
                {
                    Object** slot = reinterpret_cast<Object**>(stream.references[index]);
                    current = *slot;
                    unlog(slot, context);
                    CountWorker(SIZE_MAX, IncrementEntries, 1);
                }
                else
                {
                    CountWorker(SIZE_MAX, DecrementEntries, 1);
                }
                observe(increment, stream.references[index], current, context);
            }
            cursor += count;
        }
    }
    result = ResetBank(bank);
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    return GetFailure();
}

void LxrReferenceCounting::Shutdown()
{
    if (GetPhase() == LxrRcPhase::Shutdown)
    {
        return;
    }
    m_phase.store(LxrRcPhase::Shutdown, std::memory_order_release);
    m_zeroer.Stop();
    m_scheduler.Shutdown();
    DeleteRememberedChunks();
    DeleteWork();
    m_large.Shutdown();
    m_allocation.Shutdown();
    m_counts.Shutdown();
    m_blocks.Shutdown();
    m_metadata.Shutdown();
    if (m_arena != nullptr)
    {
        GCToOSInterface::VirtualRelease(m_arena, GetReservationBytes());
        m_arena = nullptr;
    }
    if (m_banks != nullptr)
    {
        for (size_t index = 0; index < 2; index++)
        {
            LogBank& bank = m_banks[index];
            bank.increments.referenceStorage.Shutdown();
            bank.increments.publicationStorage.Shutdown();
            bank.decrements.referenceStorage.Shutdown();
            bank.decrements.publicationStorage.Shutdown();
        }
    }
    delete[] m_banks;
    if (m_mutators != nullptr)
    {
        m_metrics[LoggedSlots].fetch_add(SumMutatorCounter(&LxrRcMutator::loggedSlots), std::memory_order_relaxed);
        m_metrics[OldTargetChecks].fetch_add(
            SumMutatorCounter(&LxrRcMutator::oldTargetChecks), std::memory_order_relaxed);
    }
    delete[] m_mutators;
    delete[] m_copy_allocators;
    DeleteCopyLogs();
    DeleteReleaseChunks();
    delete[] m_copy_logs;
    delete[] m_worker_counters;
    m_evacuation_sources_storage.Shutdown();
    delete[] m_mutator_chunks;
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    m_extent_storage.Shutdown();
    m_live_storage.Shutdown();
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    m_allocated_block_storage.Shutdown();
    delete[] m_roots[0];
    delete[] m_roots[1];
    m_dead_large_storage.Shutdown();
    m_dead_large_capacity.store(0, std::memory_order_relaxed);
    m_mature_candidates_storage.Shutdown();
    m_block_flags_storage.Shutdown();
    m_zero_state_storage.Shutdown();
    m_allocated_list_storage.Shutdown();
    m_block_table_blocks.store(0, std::memory_order_relaxed);
    delete[] m_trace_locks;
    delete[] m_scan_buckets;
    m_banks = nullptr;
    m_mutators = nullptr;
    m_copy_allocators = nullptr;
    m_copy_logs = nullptr;
    m_worker_counters = nullptr;
    m_evacuation_sources = nullptr;
    m_mutator_chunks = nullptr;
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    m_extents = nullptr;
    m_live_objects = nullptr;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    m_trace_locks = nullptr;
    m_scan_buckets = nullptr;
    m_block_flags = nullptr;
    m_zero_state = nullptr;
    m_pins.Clear();
    m_roots[0] = nullptr;
    m_roots[1] = nullptr;
    m_root_capacity[0] = 0;
    m_root_capacity[1] = 0;
    m_retained_roots.Truncate(0);
    m_released_roots.Truncate(0);
    m_dead_large = nullptr;
    m_mature_candidates = nullptr;
    m_allocated_list = nullptr;
}

LxrRcResult LxrReferenceCounting::Fail(LxrRcResult result)
{
    LxrRcResult expected = LxrRcResult::Success;
    m_failure.compare_exchange_strong(expected, result, std::memory_order_seq_cst);
    m_phase.store(LxrRcPhase::Faulted, std::memory_order_release);
    return result;
}

LxrRcResult LxrReferenceCounting::FromMetadata(SideMetadataResult result)
{
    return result == SideMetadataResult::Success
        ? LxrRcResult::Success : Fail(LxrRcResult::MetadataFailure);
}

void LxrReferenceCounting::CountWorker(size_t worker, size_t counter, uint64_t amount)
{
    if (worker < m_configuration.workers)
    {
        std::atomic<uint64_t>& value = m_worker_counters[worker].values[counter];
        value.store(value.load(std::memory_order_relaxed) + amount, std::memory_order_relaxed);
    }
    else
    {
        m_worker_counters[m_configuration.workers].values[counter].fetch_add(amount, std::memory_order_relaxed);
    }
}

uint64_t LxrReferenceCounting::SumWorker(size_t counter) const
{
    uint64_t sum = 0;
    if (m_worker_counters != nullptr)
    {
        for (size_t worker = 0; worker <= m_configuration.workers; worker++)
        {
            sum += m_worker_counters[worker].values[counter].load(std::memory_order_relaxed);
        }
    }
    return sum;
}

uint64_t LxrReferenceCounting::SumMutatorCounter(std::atomic<uint64_t> LxrRcMutator::* counter) const
{
    uint64_t sum = 0;
    if (m_mutators != nullptr)
    {
        for (size_t index = 0; index < m_configuration.maximumMutators; index++)
        {
            sum += (m_mutators[index].*counter).load(std::memory_order_relaxed);
        }
    }
    return sum;
}

size_t LxrReferenceCounting::GetPauseSurvivorBytes() const
{
    return static_cast<size_t>(SumWorker(PromotedBytes));
}

LxrRcResult LxrReferenceCounting::FromScheduler(LxrWorkSchedulerResult result)
{
    return result == LxrWorkSchedulerResult::Success
        ? LxrRcResult::Success : Fail(LxrRcResult::SchedulerFailure);
}

LxrRcMutator* LxrReferenceCounting::FindMutator(void* identity) const
{
    if (m_mutators == nullptr || identity == nullptr)
    {
        return nullptr;
    }
    uintptr_t key = reinterpret_cast<uintptr_t>(identity);
    for (size_t index = 0; index < m_configuration.maximumMutators; index++)
    {
        if (m_mutators[index].state.load(std::memory_order_acquire) == 2 &&
            m_mutators[index].identity.load(std::memory_order_relaxed) == key)
        {
            return &m_mutators[index];
        }
    }
    return nullptr;
}

LxrRcMutator* LxrReferenceCounting::FindCachedMutator(void* cached, void* identity) const
{
    uintptr_t address = reinterpret_cast<uintptr_t>(cached);
    uintptr_t base = reinterpret_cast<uintptr_t>(m_mutators);
    if (m_mutators == nullptr || address < base ||
        address - base >= m_configuration.maximumMutators * sizeof(LxrRcMutator) ||
        (address - base) % sizeof(LxrRcMutator) != 0)
    {
        return nullptr;
    }
    LxrRcMutator* mutator = static_cast<LxrRcMutator*>(cached);
    return mutator->state.load(std::memory_order_acquire) == 2 &&
        mutator->identity.load(std::memory_order_relaxed) == reinterpret_cast<uintptr_t>(identity)
        ? mutator : nullptr;
}

LxrRcResult LxrReferenceCounting::GetAllocationSpan(
    LxrRcMutator* mutator, uintptr_t* cursor, uintptr_t* limit)
{
    if (mutator == nullptr || mutator->engine != this ||
        mutator->state.load(std::memory_order_acquire) != 2 ||
        GetPhase() != LxrRcPhase::Mutator || cursor == nullptr || limit == nullptr ||
        mutator->publishedLimit != 0)
    {
        return LxrRcResult::InvalidArgument;
    }
    *cursor = 0;
    *limit = 0;
    uintptr_t first = mutator->allocator.GetCursor();
    uintptr_t end = mutator->allocator.GetLimit();
    size_t cap = m_configuration.allocationBufferBytes;
    if (cap != 0 && end >= first && end - first > cap)
    {
        end = first + cap;
    }
    if (end >= first && end - first >= MIN_OBJECT_SIZE)
    {
        if (mutator->spanStart == 0)
        {
            mutator->spanStart = first;
        }
        mutator->publishedCursor = first;
        mutator->publishedLimit = end;
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        mutator->lastPublishedLimit = end;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        *cursor = first;
        *limit = end;
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::CloseSpan(LxrRcMutator* mutator, uintptr_t end)
{
    uintptr_t start = mutator->spanStart;
    mutator->spanStart = 0;
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_D2_SPAN_TO_LIMIT_CONTROL)
    // Control: the span covers the unused tail of the buffer too.
    if (mutator->lastPublishedLimit > end)
    {
        end = mutator->lastPublishedLimit;
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_D2_SPAN_TO_LIMIT_CONTROL
    if (start == 0 || end <= start)
    {
        return LxrRcResult::Success;
    }
    if (!mutator->spans.Push(start, end))
    {
        return Fail(LxrRcResult::OutOfMemory);
    }
    m_span_metrics[0].fetch_add(1, std::memory_order_relaxed);
    m_span_metrics[1].fetch_add(end - start, std::memory_order_relaxed);
    m_metrics[AllocatedBytes].fetch_add(end - start, std::memory_order_relaxed);
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::CloseOverflowSpan(LxrRcMutator* mutator, uintptr_t end)
{
    uintptr_t start = mutator->overflowSpanStart;
    mutator->overflowSpanStart = 0;
    if (start == 0 || end <= start)
    {
        return LxrRcResult::Success;
    }
    if (!mutator->spans.Push(start, end))
    {
        return Fail(LxrRcResult::OutOfMemory);
    }
    m_span_metrics[0].fetch_add(1, std::memory_order_relaxed);
    m_span_metrics[1].fetch_add(end - start, std::memory_order_relaxed);
    m_metrics[AllocatedBytes].fetch_add(end - start, std::memory_order_relaxed);
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::AllocateMediumOverflow(
    LxrRcMutator* mutator, const ImmixAllocationRequest& request, uintptr_t* start, bool* allocated)
{
    *allocated = false;
    ImmixAllocationStatus status;
    SideMetadataResult result = mutator->overflowAllocator.TryAllocate(request, start, &status);
    if (result != SideMetadataResult::Success)
    {
        return FromMetadata(result);
    }
    if (status != ImmixAllocationStatus::Allocated)
    {
        // The rest of the block stays unused, as the rest of a hole does: the span of its objects ends at the used point.
        if (CloseOverflowSpan(mutator, mutator->overflowAllocator.GetCursor()) != LxrRcResult::Success)
        {
            return GetFailure();
        }
        ImmixMarkingContext marking = m_marking.load(std::memory_order_acquire)
            ? ImmixMarkingContext::ConcurrentOrFinalMark : ImmixMarkingContext::Inactive;
        // Mode 3 looks for a hole that fits first. An allocator that holds a clean block stays with clean blocks until the
        // next pause: the rest of its block is still free for the next request.
        bool recycled = m_configuration.mediumOverflowMode == 3 && !mutator->overflowAllocator.HoldsFreshBlock();
        result = mutator->overflowAllocator.TryRefill(
            recycled ? ImmixAllocationSource::MutatorRecycled : ImmixAllocationSource::MutatorFresh,
            marking, &status, request.size);
        if (result == SideMetadataResult::Success && status == ImmixAllocationStatus::Exhausted && recycled)
        {
            result = mutator->overflowAllocator.TryRefill(ImmixAllocationSource::MutatorFresh, marking, &status);
        }
        if (result == SideMetadataResult::Success && status == ImmixAllocationStatus::Exhausted)
        {
            return LxrRcResult::Success;
        }
        if (result != SideMetadataResult::Success || status != ImmixAllocationStatus::Ready)
        {
            return LxrRcResult::AllocationFailure;
        }
        if (mutator->overflowAllocator.HoldsFreshBlock())
        {
            m_overflow_blocks.fetch_add(1, std::memory_order_relaxed);
        }
        result = mutator->overflowAllocator.TryAllocate(request, start, &status);
        if (result != SideMetadataResult::Success || status != ImmixAllocationStatus::Allocated)
        {
            return LxrRcResult::AllocationFailure;
        }
    }
    if (mutator->overflowSpanStart == 0)
    {
        mutator->overflowSpanStart = *start;
    }
    mutator->overflowObjects.store(mutator->overflowObjects.load(std::memory_order_relaxed) + 1, std::memory_order_relaxed);
    mutator->overflowBytes.store(
        mutator->overflowBytes.load(std::memory_order_relaxed) + request.size, std::memory_order_relaxed);
    *allocated = true;
    return LxrRcResult::Success;
}

uint64_t LxrReferenceCounting::GetMediumOverflowMetric(size_t metric) const
{
    switch (metric)
    {
        case 0:
        case 1:
        {
            uint64_t total = 0;
            for (size_t index = 0; m_mutators != nullptr && index < m_configuration.maximumMutators; index++)
            {
                const std::atomic<uint64_t>& value =
                    metric == 0 ? m_mutators[index].overflowObjects : m_mutators[index].overflowBytes;
                total += value.load(std::memory_order_relaxed);
            }
            return total;
        }
        case 2: return m_overflow_blocks.load(std::memory_order_relaxed);
        case 3: return m_allocation.GetHolesSkipped();
        case 4: return m_allocation.GetRecycledLinesConsumed();
        case 5: return m_allocation.GetFreshBlocksAcquired();
        case 6: return m_allocation.GetRecycledBlocksAcquired();
        case 7: return m_configuration.mediumOverflow && m_configuration.lineRecycling ? m_configuration.mediumOverflowMode : 0;
        default: return UINT64_MAX;
    }
}

LxrRcResult LxrReferenceCounting::RetireAllocationSpan(LxrRcMutator* mutator, uintptr_t cursor)
{
    // The thread used the buffer up to cursor. Whatever lies between the used point and the end of the buffer is
    // zero memory that no object occupies. The allocator goes on from the used point, so the span stays open and
    // covers the objects of the next buffer of the same hole too.
    if (mutator == nullptr || mutator->engine != this ||
        mutator->publishedLimit == 0 || cursor < mutator->publishedCursor || cursor > mutator->publishedLimit ||
        cursor % sizeof(uintptr_t) != 0 ||
        mutator->allocator.SetCursor(cursor) != SideMetadataResult::Success)
    {
        return LxrRcResult::InvariantViolation;
    }
    mutator->publishedCursor = 0;
    mutator->publishedLimit = 0;
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::AttachMutator(void* identity, LxrRcMutator** mutator)
{
    if (identity == nullptr || mutator == nullptr || GetPhase() != LxrRcPhase::Mutator ||
        FindMutator(identity) != nullptr)
    {
        return LxrRcResult::InvalidArgument;
    }
    *mutator = nullptr;
    for (size_t index = 0; index < m_configuration.maximumMutators; index++)
    {
        LxrRcMutator& candidate = m_mutators[index];
        uint8_t expected = 0;
        if (!candidate.state.compare_exchange_strong(expected, 1, std::memory_order_acq_rel))
        {
            continue;
        }
        candidate.engine = this;
        candidate.owner = index + 1;
        candidate.identity.store(reinterpret_cast<uintptr_t>(identity), std::memory_order_relaxed);
        candidate.increments = nullptr;
        candidate.decrements = nullptr;
        candidate.publishedCursor = 0;
        candidate.publishedLimit = 0;
        candidate.spanStart = 0;
        candidate.overflowSpanStart = 0;
        candidate.spans.Clear();
        candidate.state.store(2, std::memory_order_release);
        *mutator = &candidate;
        return LxrRcResult::Success;
    }
    return LxrRcResult::CapacityExceeded;
}

LxrRcResult LxrReferenceCounting::Publish(void** value)
{
    LogChunk* chunk = static_cast<LogChunk*>(*value);
    if (chunk == nullptr || chunk->count == 0)
    {
        return LxrRcResult::Success;
    }
    LogBank& bank = m_banks[m_producer_bank.load(std::memory_order_relaxed)];
    LogStream& stream = chunk->increment ? bank.increments : bank.decrements;
    LxrRcResult result = PublishReferences(stream, stream.capacity, chunk->references, chunk->count);
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    chunk->count = 0;
    *value = nullptr;
    m_metrics[BufferFlushes].fetch_add(1, std::memory_order_relaxed);
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::PublishReferences(
    LogStream& stream, size_t capacity, const uintptr_t* references, size_t count)
{
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_LOG_CAPACITY_CONTROL)
    capacity--;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_LOG_CAPACITY_CONTROL
    if (count == 0 || count > BufferCapacity)
    {
        return Fail(LxrRcResult::CapacityExceeded);
    }
    if (stream.published == nullptr)
    {
        for (size_t index = 0; index < count; index++)
        {
            if ((references[index] & ~LogReferenceMask) != 0)
            {
                return Fail(LxrRcResult::InvariantViolation);
            }
        }
    }
    size_t start = stream.reserved.load(std::memory_order_relaxed);
    while (true)
    {
        if (count == 0 || count > BufferCapacity ||
            count > capacity || start > capacity - count)
        {
            return Fail(LxrRcResult::CapacityExceeded);
        }
        // A consumer can observe reserved immediately. Back its entire range before publishing the reservation.
        if (!stream.EnsureCommitted(this, start + count))
        {
            return Fail(LxrRcResult::OutOfMemory);
        }
        if (stream.reserved.compare_exchange_weak(
                start, start + count, std::memory_order_relaxed))
        {
            break;
        }
    }
    if (stream.published != nullptr)
    {
        memcpy(stream.references + start, references, count * sizeof(uintptr_t));
        stream.published[start].store(static_cast<uint16_t>(count), std::memory_order_release);
    }
    else
    {
        memcpy(stream.references + start + 1, references + 1, (count - 1) * sizeof(uintptr_t));
        // The aligned first word is published atomically only after the other payload words.
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_LOG_PUBLICATION_CONTROL)
        VolatileStore(stream.references + start, references[0] | (static_cast<uintptr_t>(count) << LogCountShift));
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_LOG_PUBLICATION_CONTROL
    }
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
    if (m_storage_diagnostics.load(std::memory_order_relaxed))
    {
        size_t high = stream.highWater.load(std::memory_order_relaxed);
        while (high < start + count && !stream.highWater.compare_exchange_weak(
            high, start + count, std::memory_order_relaxed))
        {
        }
        stream.flushes.fetch_add(1, std::memory_order_relaxed);
        stream.copiedBytes.fetch_add(count * sizeof(uintptr_t), std::memory_order_relaxed);
    }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::ReadPublishedRange(
    const LogStream& stream, size_t cursor, size_t limit, size_t* count)
{
    if (cursor >= limit || limit > stream.capacity)
    {
        return Fail(LxrRcResult::InvariantViolation);
    }
    size_t value = stream.published != nullptr
        ? stream.published[cursor].load(std::memory_order_acquire)
        : VolatileLoad(stream.references + cursor) >> LogCountShift;
    if (value == 0 || value > BufferCapacity || value > limit - cursor)
    {
        return Fail(LxrRcResult::InvariantViolation);
    }
    *count = value;
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::ResetBank(LogBank& bank)
{
    for (LogStream* stream : {&bank.increments, &bank.decrements})
    {
        size_t limit = stream->reserved.load(std::memory_order_acquire);
        for (size_t cursor = 0; cursor < limit;)
        {
            size_t count;
            LxrRcResult result = ReadPublishedRange(*stream, cursor, limit, &count);
            if (result != LxrRcResult::Success)
            {
                return result;
            }
            if (stream->published != nullptr)
            {
                stream->published[cursor].store(0, std::memory_order_relaxed);
            }
            else
            {
                stream->references[cursor] &= LogReferenceMask;
            }
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
            if (m_storage_diagnostics.load(std::memory_order_relaxed))
            {
                stream->resetBytes.fetch_add(stream->published != nullptr
                    ? sizeof(uint16_t) : sizeof(uintptr_t), std::memory_order_relaxed);
            }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
            cursor += count;
        }
        stream->reserved.store(0, std::memory_order_relaxed);
        if (!stream->Trim(limit))
        {
            return Fail(LxrRcResult::OutOfMemory);
        }
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::DetachMutator(LxrRcMutator* mutator)
{
    if (mutator == nullptr || mutator->engine != this ||
        mutator->state.load(std::memory_order_acquire) != 2)
    {
        return LxrRcResult::InvalidArgument;
    }
    LxrRcResult published = Publish(&mutator->decrements);
    if (published == LxrRcResult::Success)
    {
        published = Publish(&mutator->increments);
    }
    if (published != LxrRcResult::Success)
    {
        return published;
    }
    m_metrics[ThreadFlushes].fetch_add(1, std::memory_order_relaxed);
    if (m_observer)
    {
        mutator->identity.store(0, std::memory_order_relaxed);
        mutator->state.store(0, std::memory_order_release);
        return LxrRcResult::Success;
    }
    // The thread leaves with its spans. They go to the shared list until the next pause, because the slot of the
    // mutator can be taken by another thread before that.
    LxrRcResult closed = CloseSpan(mutator, mutator->allocator.GetCursor());
    if (closed == LxrRcResult::Success)
    {
        closed = CloseOverflowSpan(mutator, mutator->overflowAllocator.GetCursor());
    }
    if (closed != LxrRcResult::Success)
    {
        return closed;
    }
    if (mutator->spans.Size() != 0
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_D2_DETACH_DROP_CONTROL)
        && false
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_D2_DETACH_DROP_CONTROL
        )
    {
        while (m_detached_lock.test_and_set(std::memory_order_acquire))
        {
            YieldProcessor();
        }
        bool taken = m_detached_spans.Take(mutator->spans);
        m_detached_lock.clear(std::memory_order_release);
        if (!taken)
        {
            return Fail(LxrRcResult::OutOfMemory);
        }
    }
    ImmixAllocationStatus status;
    SideMetadataResult result = mutator->allocator.Reset(&status);
    ImmixAllocationStatus overflowStatus;
    SideMetadataResult overflowResult = mutator->overflowAllocator.Reset(&overflowStatus);
    if (result == SideMetadataResult::Success && overflowResult != SideMetadataResult::Success)
    {
        result = overflowResult;
    }
    else if (result == SideMetadataResult::Success && status == ImmixAllocationStatus::Ready)
    {
        status = overflowStatus;
    }
    mutator->identity.store(0, std::memory_order_relaxed);
    mutator->state.store(
        result == SideMetadataResult::Success && status == ImmixAllocationStatus::Ready ? 0 : 3,
        std::memory_order_release);
    if (result != SideMetadataResult::Success)
    {
        return Fail(LxrRcResult::AllocationFailure);
    }
    if (status != ImmixAllocationStatus::Ready && status != ImmixAllocationStatus::Contended)
    {
        return Fail(LxrRcResult::AllocationFailure);
    }
    // A contended allocator remains engine-owned, not in the departing thread's TLS.
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::Append(LxrRcMutator* mutator, bool increment, uintptr_t value)
{
    void** active = increment ? &mutator->increments : &mutator->decrements;
    LogChunk* chunk = static_cast<LogChunk*>(*active);
    if (chunk == nullptr)
    {
        size_t index = 2 * (mutator->owner - 1) + (increment ? 0 : 1);
        chunk = &m_mutator_chunks[index];
        chunk->count = 0;
        chunk->increment = increment;
        *active = chunk;
    }
    chunk->references[chunk->count++] = value;
    if (chunk->count == m_configuration.bufferSize)
    {
        return Publish(active);
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::LogBeforeWrite(
    LxrRcMutator* mutator, Object** slot, Object* oldTarget)
{
    if (GetPhase() != LxrRcPhase::Mutator)
    {
        return GetFailure() == LxrRcResult::Success ? LxrRcResult::InvalidPhase : GetFailure();
    }
    if (mutator == nullptr || mutator->engine != this ||
        mutator->state.load(std::memory_order_acquire) != 2 || slot == nullptr)
    {
        return Fail(LxrRcResult::InvalidArgument);
    }
    uintptr_t observed;
    bool exchanged;
#if !defined(_DEBUG) && !defined(LXR_SIDE_METADATA_TEST_PLATFORM)
    uintptr_t slotAddress = reinterpret_cast<uintptr_t>(slot);
    if (!OwnsAddress(slotAddress) || slotAddress % sizeof(uintptr_t) != 0)
    {
        return Fail(LxrRcResult::MetadataFailure);
    }
    exchanged = m_unlogged_metadata.CompareExchange(
        slotAddress, 0, 1, SideMetadataMemoryOrder::Relaxed, &observed);
#else
    SideMetadataResult result = m_metadata.CompareExchange(
        LxrSideMetadataKind::FieldUnlogged, reinterpret_cast<uintptr_t>(slot),
        0, 1, SideMetadataMemoryOrder::SequentiallyConsistent, &observed, &exchanged);
    if (result != SideMetadataResult::Success)
    {
        return FromMetadata(result);
    }
#endif // !defined(_DEBUG) && !defined(LXR_SIDE_METADATA_TEST_PLATFORM)
    if (!exchanged)
    {
        return LxrRcResult::Success;
    }
    if (oldTarget != nullptr)
    {
        if (!OwnsAddress(Start(oldTarget)))
        {
            if (m_object_model.isImmortal == nullptr ||
                !m_object_model.isImmortal(oldTarget, m_object_model.context))
            {
                return Fail(LxrRcResult::UnsupportedLifetime);
            }
            oldTarget = nullptr;
        }
    }
    if (oldTarget != nullptr)
    {
        uintptr_t count;
#if !defined(_DEBUG) && !defined(LXR_SIDE_METADATA_TEST_PLATFORM)
        uintptr_t oldStart = Start(oldTarget);
        if (oldStart % sizeof(uintptr_t) != 0)
        {
            return Fail(LxrRcResult::MetadataFailure);
        }
        count = m_rc_metadata.Load(oldStart, SideMetadataMemoryOrder::Relaxed);
#else
        result = m_metadata.Load(LxrSideMetadataKind::ReferenceCount, Start(oldTarget),
            SideMetadataMemoryOrder::Relaxed, &count);
        if (result != SideMetadataResult::Success)
        {
            return FromMetadata(result);
        }
#endif // !defined(_DEBUG) && !defined(LXR_SIDE_METADATA_TEST_PLATFORM)
        CountMutator(mutator->oldTargetChecks);
        _ASSERTE(count != 0);
        if (count == 0)
        {
            return Fail(LxrRcResult::InvariantViolation);
        }
        LxrRcResult appended = Append(mutator, false, reinterpret_cast<uintptr_t>(oldTarget));
        if (appended != LxrRcResult::Success)
        {
            return appended;
        }
    }
    LxrRcResult appended = Append(mutator, true, reinterpret_cast<uintptr_t>(slot));
    if (appended == LxrRcResult::Success)
    {
        CountMutator(mutator->loggedSlots);
    }
    return appended;
}

uintptr_t LxrReferenceCounting::Start(Object* object)
{
    return reinterpret_cast<uintptr_t>(object) - sizeof(ObjHeader);
}

Object* LxrReferenceCounting::Reference(uintptr_t start)
{
    return reinterpret_cast<Object*>(start + sizeof(ObjHeader));
}

bool LxrReferenceCounting::IsImmix(uintptr_t start) const
{
    uintptr_t arena = reinterpret_cast<uintptr_t>(m_arena);
    return start >= arena && start - arena < m_immix_capacity.load(std::memory_order_acquire);
}

bool LxrReferenceCounting::IsReturnedImmix(uintptr_t start) const
{
    // Queued work can name an object of a block that the give-back returned. The block was free, so the object
    // is dead. Its metadata reads as zero, as it does for a free block that is still admitted.
#ifdef LXR_S5B_NO_STALE_TOLERANCE_CONTROL
    return false;
#endif // LXR_S5B_NO_STALE_TOLERANCE_CONTROL
    uintptr_t arena = reinterpret_cast<uintptr_t>(m_arena);
    return m_overshoot && start >= arena && start - arena < m_configuration.immixBytes &&
        start - arena >= m_immix_capacity.load(std::memory_order_acquire);
}

bool LxrReferenceCounting::IsLarge(uintptr_t start) const
{
    uintptr_t arena = m_large.GetReservationStart();
    return start >= arena && start - arena < m_large.GetAdmittedBytes();
}

LxrRcResult LxrReferenceCounting::Allocate(
    LxrRcMutator* mutator, size_t completeSize, Object** object, bool pinned)
{
    if (object == nullptr || mutator == nullptr || mutator->engine != this ||
        mutator->state.load(std::memory_order_acquire) != 2 ||
        completeSize < MIN_OBJECT_SIZE || completeSize % sizeof(uintptr_t) != 0 ||
        GetPhase() != LxrRcPhase::Mutator || m_observer)
    {
        return LxrRcResult::InvalidArgument;
    }
    *object = nullptr;
    uintptr_t start = 0;
    bool overflowed = false;
    if (LxrLargeObjectSpace::ShouldUseLargeObjectSpace(completeSize))    {
        LxrLargeObjectAllocation allocation;
        if (m_large.Allocate(completeSize, sizeof(uintptr_t), 0, mutator->owner, &allocation) !=
            LxrLargeObjectResult::Success)
        {
            return LxrRcResult::AllocationFailure;
        }
        start = allocation.object;
    }
    else if (pinned
#if defined(LXR_S7_POH_NORMAL_CONTROL)
        && false
#endif // LXR_S7_POH_NORMAL_CONTROL
        )
    {
        // One allocator serves every thread, so the call takes a lock. Pinned-object-heap allocations are rare.
        while (m_pinned_lock.test_and_set(std::memory_order_acquire))
        {
            YieldProcessor();
        }
        ImmixAllocationRequest request{completeSize, sizeof(uintptr_t), 0};
        ImmixAllocationStatus status;
        SideMetadataResult result = m_pinned_allocator.TryAllocate(request, &start, &status);
        bool recycled = m_configuration.lineRecycling;
        for (size_t attempts = m_configuration.immixBytes / ImmixBlockGeometry::LineBytes +
                 m_configuration.immixBytes / ImmixBlockGeometry::BlockBytes;
             result == SideMetadataResult::Success && status != ImmixAllocationStatus::Allocated && attempts-- != 0;)
        {
            ImmixMarkingContext marking = m_marking.load(std::memory_order_acquire)
                ? ImmixMarkingContext::ConcurrentOrFinalMark : ImmixMarkingContext::Inactive;
#ifdef LXR_OBJECT_START_WALK
            // The hole is left behind: the span of its objects ends at the used point.
            ClosePinnedSpan(m_pinned_allocator.GetCursor());
#endif // LXR_OBJECT_START_WALK
            result = m_pinned_allocator.TryRefill(
                recycled ? ImmixAllocationSource::MutatorPinnedRecycled : ImmixAllocationSource::MutatorPinnedFresh,
                marking, &status);
            if (result == SideMetadataResult::Success && status == ImmixAllocationStatus::Exhausted && recycled)
            {
                recycled = false;
                result = m_pinned_allocator.TryRefill(ImmixAllocationSource::MutatorPinnedFresh, marking, &status);
            }
            if (result != SideMetadataResult::Success || status != ImmixAllocationStatus::Ready)
            {
                break;
            }
            result = m_pinned_allocator.TryAllocate(request, &start, &status);
        }
#ifdef LXR_OBJECT_START_WALK
        if (result == SideMetadataResult::Success && status == ImmixAllocationStatus::Allocated &&
            m_pinned_span_start == 0)
        {
            m_pinned_span_start = start;
        }
#endif // LXR_OBJECT_START_WALK
        m_pinned_lock.clear(std::memory_order_release);
        if (result != SideMetadataResult::Success || status != ImmixAllocationStatus::Allocated)
        {
            return result == SideMetadataResult::Success ? LxrRcResult::AllocationFailure : FromMetadata(result);
        }
        // A pinned object is allocated one by one and is found by its bit (interior pointers into it included). The
        // ranges of the block were prepared when the hole was taken, so the bit is all that is left to set.
        if (
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_D2_NO_PINNED_VALID_CONTROL)
            FromMetadata(m_counts.RegisterObjectInClearedSpan(start, completeSize)) != LxrRcResult::Success
#else
            false
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_D2_NO_PINNED_VALID_CONTROL
            )
        {
            return GetFailure();
        }
    }
    else
    {
        ImmixAllocationRequest request{completeSize, sizeof(uintptr_t), 0};
        ImmixAllocationStatus status;
        SideMetadataResult result = mutator->allocator.TryAllocate(request, &start, &status);
        if (result != SideMetadataResult::Success)
        {
            return FromMetadata(result);
        }
        bool recycled = m_configuration.lineRecycling;
        size_t minimumHoleBytes = 0;
        if (status != ImmixAllocationStatus::Allocated && recycled && m_configuration.mediumOverflow)
        {
            // A request that does not fit the hole and is larger than a line goes to a clean block of its own. The
            // holes are for the objects that fit them, and a refill leaves alone the holes that are too small.
            minimumHoleBytes = completeSize;
            if (completeSize > ImmixBlockGeometry::LineBytes && m_configuration.mediumOverflowMode >= 2)
            {
                LxrRcResult medium = AllocateMediumOverflow(mutator, request, &start, &overflowed);
                if (medium != LxrRcResult::Success)
                {
                    return medium;
                }
                if (overflowed)
                {
                    status = ImmixAllocationStatus::Allocated;
                }
            }
        }
        size_t attempts = m_configuration.immixBytes / ImmixBlockGeometry::LineBytes +
            m_configuration.immixBytes / ImmixBlockGeometry::BlockBytes;
        while (status != ImmixAllocationStatus::Allocated && attempts-- != 0)
        {
            // The hole is left behind: the span of its objects ends at the used point.
            if (CloseSpan(mutator, mutator->allocator.GetCursor()) != LxrRcResult::Success)
            {
                return GetFailure();
            }
            result = mutator->allocator.TryRefill(
                recycled ? ImmixAllocationSource::MutatorRecycled : ImmixAllocationSource::MutatorFresh,
                m_marking.load(std::memory_order_acquire)
                    ? ImmixMarkingContext::ConcurrentOrFinalMark : ImmixMarkingContext::Inactive, &status,
                minimumHoleBytes);
            if (result == SideMetadataResult::Success && status == ImmixAllocationStatus::Exhausted && recycled)
            {
                recycled = false;
                result = mutator->allocator.TryRefill(
                    ImmixAllocationSource::MutatorFresh,
                    m_marking.load(std::memory_order_acquire)
                        ? ImmixMarkingContext::ConcurrentOrFinalMark : ImmixMarkingContext::Inactive, &status);
            }
            if (result != SideMetadataResult::Success || status != ImmixAllocationStatus::Ready)
            {
                return LxrRcResult::AllocationFailure;
            }
            result = mutator->allocator.TryAllocate(request, &start, &status);
            if (result != SideMetadataResult::Success)
            {
                return LxrRcResult::AllocationFailure;
            }
        }
        if (status != ImmixAllocationStatus::Allocated)
        {
            return LxrRcResult::AllocationFailure;
        }
    }
    *object = Reference(start);
    if (!pinned && !overflowed && IsImmix(start) && mutator->spanStart == 0)
    {
        mutator->spanStart = start;
    }
    if (IsLarge(start) && m_marking.load(std::memory_order_acquire) &&
        FromMetadata(m_metadata.Store(LxrSideMetadataKind::ObjectMark, start, 1,
            SideMetadataMemoryOrder::SequentiallyConsistent)) != LxrRcResult::Success)
    {
        return GetFailure();
    }
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    {
        // The oracle of the test platform has the size of every object that went through this call.
        size_t index = (start - GetReservationStart()) / sizeof(uintptr_t);
        if (ExtentSlot(index).load(std::memory_order_acquire) != 0)
        {
            return Fail(LxrRcResult::InvariantViolation);
        }
        OracleBirth(start, completeSize);
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
#ifdef LXR_HAS_LIFETIME_PROBE
    if (m_probe_start.load(std::memory_order_acquire) == start)
    {
        m_probe_retired.store(false, std::memory_order_release);
        m_probe_births.fetch_add(1, std::memory_order_release);
    }
#endif // LXR_HAS_LIFETIME_PROBE
    m_metrics[Allocations].fetch_add(1, std::memory_order_relaxed);
    if (pinned || IsLarge(start))
    {
        // The bytes of a young object are counted when its span closes.
        m_metrics[AllocatedBytes].fetch_add(completeSize, std::memory_order_relaxed);
    }
    return LxrRcResult::Success;
}

LxrReferenceCounting::Work* LxrReferenceCounting::NewWork(
    WorkKind kind, const uintptr_t* references, size_t count, LxrWorkBucketStage stage,
    bool trace, TraceScan* scan, bool borrow, bool logged)
{
    size_t capacity = kind == WorkKind::TraceObjects ? FullTraceBatchCapacity : BufferCapacity;
    if (count > capacity || (count != 0 && references == nullptr))
    {
        Fail(LxrRcResult::InvalidArgument);
        return nullptr;
    }
    uint64_t id = m_next_id.load(std::memory_order_relaxed);
    while (id != UINT64_MAX &&
        !m_next_id.compare_exchange_weak(id, id + 1, std::memory_order_relaxed))
    {
    }
    if (id == UINT64_MAX)
    {
        Fail(LxrRcResult::CapacityExceeded);
        return nullptr;
    }
    Work* work = new (std::nothrow) Work(this, id, kind, references, count, stage, trace, scan, borrow, logged);
    if (work == nullptr || (count != 0 && work->references == nullptr))
    {
        delete work;
        Fail(LxrRcResult::OutOfMemory);
        return nullptr;
    }
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
    uint64_t payloadBytes = borrow ? 0 : count * sizeof(uintptr_t);
    uint64_t bytes = sizeof(Work) + payloadBytes;
    if (m_storage_diagnostics.load(std::memory_order_relaxed))
    {
        m_storage_metrics[0].fetch_add(1, std::memory_order_relaxed);
        uint64_t current = m_storage_metrics[1].fetch_add(bytes, std::memory_order_relaxed) + bytes;
        uint64_t high = m_storage_metrics[2].load(std::memory_order_relaxed);
        while (high < current && !m_storage_metrics[2].compare_exchange_weak(
            high, current, std::memory_order_relaxed))
        {
        }
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_STORAGE_SKIP_COPY_CONTROL)
        m_storage_metrics[3].fetch_add(payloadBytes, std::memory_order_relaxed);
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_STORAGE_SKIP_COPY_CONTROL
    }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
    if (kind == WorkKind::TraceObjects || kind == WorkKind::TraceScan || kind == WorkKind::ReplayRemembered)
    {
        uint64_t pending = m_trace_pending.load(std::memory_order_relaxed);
        while (true)
        {
            if (pending == UINT64_MAX)
            {
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
                if (m_storage_diagnostics.load(std::memory_order_relaxed))
                {
                    m_storage_metrics[1].fetch_sub(bytes, std::memory_order_relaxed);
                }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
                delete work;
                Fail(LxrRcResult::CapacityExceeded);
                return nullptr;
            }
            if (m_trace_pending.compare_exchange_weak(pending, pending + 1, std::memory_order_release))
            {
                break;
            }
        }
    }
    if (scan != nullptr)
    {
        scan->users.fetch_add(1, std::memory_order_relaxed);
    }
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    if (kind == WorkKind::TraceObjects && count == 1 &&
        work->GetReference(0) == m_reader_target.load(std::memory_order_acquire) &&
        m_reader_held.load(std::memory_order_acquire))
    {
        Work* expected = nullptr;
        if (m_reader_work.compare_exchange_strong(expected, work, std::memory_order_acq_rel))
        {
            work->watchedReader = true;
            m_reader_initial.store(work->GetReference(0), std::memory_order_release);
            m_reader_id.store(work->packet.GetId(), std::memory_order_release);
        }
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    if (kind == WorkKind::TraceObjects || kind == WorkKind::TraceScan || kind == WorkKind::ReplayRemembered)
    {
        // P26 (F2). A trace packet is owned by the scheduler or by m_trace_unsubmitted until it is processed. It is in no
        // list that a pause walks.
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_F2_WALK_BACKLOG_CONTROL)
        return work;
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_F2_WALK_BACKLOG_CONTROL
    }
    Work* head = m_work.load(std::memory_order_relaxed);
    do
    {
        work->next = head;
    } while (!m_work.compare_exchange_weak(
        head, work, std::memory_order_release, std::memory_order_relaxed));
    return work;
}

void LxrReferenceCounting::PushTraceUnsubmitted(Work* work)
{
    Work* head = m_trace_unsubmitted.load(std::memory_order_relaxed);
    do
    {
        work->traceNext = head;
    } while (!m_trace_unsubmitted.compare_exchange_weak(
        head, work, std::memory_order_release, std::memory_order_relaxed));
}

void LxrReferenceCounting::RetireTraceWork(Work* work)
{
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F2_WALK_BACKLOG_CONTROL)
    // Control: the behaviour before P26. m_work owns the trace packets, and Retire frees the finished ones by walking it.
    (void)work;
    return;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F2_WALK_BACKLOG_CONTROL
    if (work->doneQueued.exchange(true, std::memory_order_acq_rel))
    {
        Fail(LxrRcResult::InvariantViolation);
        return;
    }
    Work* head = m_trace_done.load(std::memory_order_relaxed);
    do
    {
        work->next = head;
    } while (!m_trace_done.compare_exchange_weak(
        head, work, std::memory_order_release, std::memory_order_relaxed));
}

void LxrReferenceCounting::ReleaseCancelledPacket(LxrWorkPacket* packet, void* context)
{
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F2_WALK_BACKLOG_CONTROL)
    (void)packet;
    (void)context; // Control: m_work owns every packet and frees it in DeleteWork.
    return;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F2_WALK_BACKLOG_CONTROL
    Work* work = static_cast<Work*>(packet->GetContext());
    if (work->kind == WorkKind::TraceObjects || work->kind == WorkKind::TraceScan ||
        work->kind == WorkKind::ReplayRemembered)
    {
        static_cast<LxrReferenceCounting*>(context)->ReleaseWork(work);
    }
}

void LxrReferenceCounting::ReleaseWork(Work* work)
{
    if (work->scan != nullptr)
    {
        size_t previous = work->scan->users.fetch_sub(1, std::memory_order_relaxed);
        if (previous == 0)
        {
            Fail(LxrRcResult::InvariantViolation);
        }
    }
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
    if (m_storage_diagnostics.load(std::memory_order_relaxed))
    {
        m_storage_metrics[1].fetch_sub(sizeof(Work) +
            (work->ownedReferences == nullptr ? 0 : work->packet.GetReferenceCount() * sizeof(uintptr_t)),
            std::memory_order_relaxed);
    }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
    delete work;
}

void LxrReferenceCounting::DeleteWork()
{
    Work* work = m_work.exchange(nullptr, std::memory_order_acquire);
    while (work != nullptr)
    {
        Work* next = work->next;
        ReleaseWork(work);
        work = next;
    }
    work = m_trace_done.exchange(nullptr, std::memory_order_acquire);
    while (work != nullptr)
    {
        Work* next = work->next;
        ReleaseWork(work);
        work = next;
    }
    work = m_trace_unsubmitted.exchange(nullptr, std::memory_order_acquire);
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F2_WALK_BACKLOG_CONTROL)
    work = nullptr; // Control: m_work owns these packets and has freed them above.
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F2_WALK_BACKLOG_CONTROL
    while (work != nullptr)
    {
        Work* next = work->traceNext;
        ReleaseWork(work);
        work = next;
    }
    TraceScan* scan = m_scan_work.exchange(nullptr, std::memory_order_acquire);
    while (scan != nullptr)
    {
        TraceScan* next = scan->next;
        UnlinkTraceScan(scan);
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
        if (m_storage_diagnostics.load(std::memory_order_relaxed))
        {
            m_storage_metrics[5].fetch_sub(sizeof(TraceScan), std::memory_order_relaxed);
        }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
        delete scan;
        scan = next;
    }
}

void LxrReferenceCounting::RetireCompletedWork()
{
    // P26 (F2). The trace packets that were processed since the last pause are in m_trace_done. The pending ones are
    // in the scheduler or in m_trace_unsubmitted, and this function does not touch them. So the cost follows the work
    // of the last epoch, not the trace backlog. m_work holds no trace packet.
    Work* done = m_trace_done.exchange(nullptr, std::memory_order_acquire);
    while (done != nullptr)
    {
        Work* next = done->next;
        m_walk[0].fetch_add(1, std::memory_order_relaxed);
        ReleaseWork(done);
        done = next;
    }
    Work* kept = nullptr;
    Work* work = m_work.load(std::memory_order_acquire);
    while (work != nullptr)
    {
        Work* next = work->next;
        m_walk[0].fetch_add(1, std::memory_order_relaxed);
        LxrWorkPacketState state = work->packet.GetState();
        if (state == LxrWorkPacketState::Completed
#ifdef LXR_RETIRE_PENDING_CONTROL
            || state == LxrWorkPacketState::CallerOwned
#endif // LXR_RETIRE_PENDING_CONTROL
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F2_WALK_BACKLOG_CONTROL)
            || (state == LxrWorkPacketState::CallerOwned && work->traceProcessed.load(std::memory_order_acquire))
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F2_WALK_BACKLOG_CONTROL
            )
        {
            ReleaseWork(work);
        }
        else
        {
            work->next = kept;
            kept = work;
        }
        work = next;
    }
    m_work.store(kept, std::memory_order_release);
    TraceScan* scans = nullptr;
    TraceScan* scan = m_scan_work.load(std::memory_order_acquire);
    while (scan != nullptr)
    {
        TraceScan* next = scan->next;
        if (scan->complete && scan->users.load(std::memory_order_acquire) == 0)
        {
            UnlinkTraceScan(scan);
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
            if (m_storage_diagnostics.load(std::memory_order_relaxed))
            {
                m_storage_metrics[5].fetch_sub(sizeof(TraceScan), std::memory_order_relaxed);
            }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
            delete scan;
        }
        else
        {
            scan->next = scans;
            scans = scan;
        }
        scan = next;
    }
    m_scan_work.store(scans, std::memory_order_release);
}

LxrRcResult LxrReferenceCounting::SubmitWork(Work* work, const LxrWorkPacket* parent)
{
    if (work == nullptr)
    {
        return GetFailure();
    }
    if (parent != nullptr)
    {
        m_metrics[ChildRegistrations].fetch_add(1, std::memory_order_relaxed);
        if (m_pause_kind == LxrPauseKind::Full || !m_configuration.lazyDecrements)
        {
            return FromScheduler(m_scheduler.Submit(&work->packet));
        }
        return FromScheduler(m_scheduler.SubmitTrackedChild(
            parent, &work->packet, LxrCompletionWorkKind::Decrement));
    }
    if (work->kind == WorkKind::Decrements || work->kind == WorkKind::PreviousRoots)
    {
        if (m_pause_kind == LxrPauseKind::Full || !m_configuration.lazyDecrements)
        {
            return FromScheduler(m_scheduler.Submit(&work->packet));
        }
        return FromScheduler(m_scheduler.SubmitDeferredConcurrentTracked(
            &work->packet, LxrCompletionWorkKind::Decrement, LxrDeferredWorkPriority::Prioritized));
    }
    return FromScheduler(m_scheduler.Submit(&work->packet));
}

LxrRcResult LxrReferenceCounting::FlushMutators()
{
    size_t active = 0;
    for (size_t index = 0; index < m_configuration.maximumMutators; index++)
    {
        LxrRcMutator& mutator = m_mutators[index];
        uint8_t state = mutator.state.load(std::memory_order_acquire);
        if (state == 2 || state == 3)
        {
            active++;
            LxrRcResult published = Publish(&mutator.decrements);
            if (published == LxrRcResult::Success)
            {
                published = Publish(&mutator.increments);
            }
            if (published != LxrRcResult::Success)
            {
                return published;
            }
            if (m_observer)
            {
                continue;
            }
            if (mutator.publishedLimit != 0)
            {
                return Fail(LxrRcResult::InvariantViolation);
            }
            // The buffer is retired, so the cursor of the allocator is the used point: the span of the thread ends there.
            LxrRcResult closed = CloseSpan(&mutator, mutator.allocator.GetCursor());
            if (closed == LxrRcResult::Success)
            {
                closed = CloseOverflowSpan(&mutator, mutator.overflowAllocator.GetCursor());
            }
            if (closed != LxrRcResult::Success)
            {
                return closed;
            }
            if (!m_young_spans.Take(mutator.spans))
            {
                return Fail(LxrRcResult::OutOfMemory);
            }
            ImmixAllocationStatus status;
            SideMetadataResult result = mutator.allocator.Reset(&status);
            ImmixAllocationStatus overflowStatus;
            SideMetadataResult overflowResult = mutator.overflowAllocator.Reset(&overflowStatus);
            if (result != SideMetadataResult::Success || status != ImmixAllocationStatus::Ready ||
                overflowResult != SideMetadataResult::Success || overflowStatus != ImmixAllocationStatus::Ready)
            {
                return Fail(LxrRcResult::AllocationFailure);
            }
            if (state == 3)
            {
                mutator.state.store(0, std::memory_order_release);
            }
        }
    }
    if (!m_observer)
    {
#ifdef LXR_OBJECT_START_WALK
        // The pinned allocator records a span like a mutator does. Mutators are stopped, so no pinned allocation runs.
        ClosePinnedSpan(m_pinned_allocator.GetCursor());
#endif // LXR_OBJECT_START_WALK
        while (m_detached_lock.test_and_set(std::memory_order_acquire))
        {
            YieldProcessor();
        }
        bool merged = m_young_spans.Take(m_detached_spans);
        m_detached_lock.clear(std::memory_order_release);
        if (!merged)
        {
            return Fail(LxrRcResult::OutOfMemory);
        }
        m_span_metrics[2].store(m_young_spans.Size(), std::memory_order_relaxed);
#ifdef LXR_HAS_LIFETIME_PROBE
        CheckProbeInSpans();
#endif // LXR_HAS_LIFETIME_PROBE
        // The pinned allocator belongs to no mutator. It gives its block back like a mutator allocator does.
        ImmixAllocationStatus status;
        if (m_pinned_allocator.Reset(&status) != SideMetadataResult::Success ||
            status != ImmixAllocationStatus::Ready)
        {
            return Fail(LxrRcResult::AllocationFailure);
        }
    }
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
    if (m_storage_diagnostics.load(std::memory_order_relaxed))
    {
        m_storage_metrics[8].store(active, std::memory_order_release);
    }
#else
    (void)active;
#endif // LXR_HAS_DIAGNOSTIC_PROBES
    return LxrRcResult::Success;
}

void LxrReferenceCounting::MarkPromotedStart(uintptr_t start)
{
#ifdef LXR_OBJECT_START_WALK
    // The walk variant keeps no start plane: the count that the caller stores next is the record of the start.
    (void)start;
#else
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_D2_NO_PROMOTE_VALID_CONTROL)
    // Control: a survivor gets no ValidObject bit.
    (void)start;
    return;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_D2_NO_PROMOTE_VALID_CONTROL
    // Only the first count of an object needs the bit. The count is in the same metadata line as the one that the
    // increment reads next, so the test is cheap.
    if (m_rc_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) == 0)
    {
        m_valid_metadata.Store(start, 1, SideMetadataMemoryOrder::SequentiallyConsistent);
    }
#endif // LXR_OBJECT_START_WALK
}

LxrRcResult LxrReferenceCounting::FindInYoungSpans(uintptr_t address, Object** object, bool requireCounted) const
{
    const LxrSpan* span = m_young_spans.Find(address);
    if (span == nullptr)
    {
        return LxrRcResult::InvalidArgument;
    }
    // Every object of a span is complete: a thread cannot be suspended inside the bump-pointer helper, and the
    // pause has cut the buffers at their used point. The walk reads the size of each object from the object. A
    // source that the pause has copied has lost its first field to the forwarding pointer, so its size is read from
    // the copy, which has the same size.
    uintptr_t cursor = span->start;
    while (cursor < span->end)
    {
        Object* candidate = Reference(cursor);
        size_t size = ObjectSize(ResolveForwarded(candidate));
        if (size < MIN_OBJECT_SIZE || size > span->end - cursor)
        {
            return LxrRcResult::InvalidArgument;
        }
        if (address < cursor + size)
        {
            if (address < reinterpret_cast<uintptr_t>(candidate) ||
                (requireCounted && m_rc_metadata.Load(cursor, SideMetadataMemoryOrder::Relaxed) == 0))
            {
                return LxrRcResult::InvalidArgument;
            }
            *object = candidate;
            return LxrRcResult::Success;
        }
        cursor += size;
    }
    return LxrRcResult::InvalidArgument;
}

#ifdef LXR_HAS_LIFETIME_PROBE
void LxrReferenceCounting::CheckProbeInSpans()
{
    // The probe is armed on a live object. A span that starts an object at the probed address is a birth only after
    // the probed object has retired: the object that is there before is the probed one.
    uintptr_t probe = m_probe_start.load(std::memory_order_acquire);
    const LxrSpan* span = probe == 0 || !m_probe_retired.load(std::memory_order_acquire)
        ? nullptr : m_young_spans.Find(probe);
    if (span == nullptr)
    {
        return;
    }
    uintptr_t cursor = span->start;
    while (cursor < span->end && cursor < probe)
    {
        size_t size = ObjectSize(Reference(cursor));
        if (size < MIN_OBJECT_SIZE || size > span->end - cursor)
        {
            return;
        }
        cursor += size;
    }
    if (cursor == probe)
    {
        m_probe_retired.store(false, std::memory_order_release);
        m_probe_births.fetch_add(1, std::memory_order_release);
    }
}
#endif // LXR_HAS_LIFETIME_PROBE

bool LxrReferenceCounting::IsOwnedObject(Object* object) const
{
    uintptr_t start = Start(object);
    if (!OwnsAddress(start) || start % sizeof(uintptr_t) != 0)
    {
        return false;
    }
    return !IsLarge(start) || IsObjectStart(object);
}

LxrRcResult LxrReferenceCounting::PrepareWork()
{
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
    if (m_storage_diagnostics.load(std::memory_order_relaxed))
    {
        m_storage_metrics[7].store(m_root_counts[0] + m_root_counts[1], std::memory_order_release);
    }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
    LogBank& bank = m_banks[m_producer_bank.load(std::memory_order_relaxed)];
    for (bool increment : {true, false})
    {
        LogStream& stream = increment ? bank.increments : bank.decrements;
        size_t limit = stream.reserved.load(std::memory_order_acquire);
        WorkKind kind = increment ? WorkKind::Slots : WorkKind::Decrements;
        LxrWorkBucketStage stage = increment ? LxrRCProcessIncs :
            (m_pause_kind == LxrPauseKind::Full || !m_configuration.lazyDecrements
                ? LxrRCProcessDecs : LxrWorkBucketStage::Unconstrained);
        for (size_t cursor = 0; cursor < limit;)
        {
            size_t count;
            LxrRcResult result = ReadPublishedRange(stream, cursor, limit, &count);
            if (result != LxrRcResult::Success)
            {
                return result;
            }
            result = SubmitWork(NewWork(kind, stream.references + cursor, count, stage, true, nullptr, true));
            if (result != LxrRcResult::Success)
            {
                return result;
            }
            if (!increment && m_trace_phase.load(std::memory_order_acquire) != LxrTracePhase::Inactive &&
                m_pause_kind != LxrPauseKind::InitialMark)
            {
                result = SubmitWork(NewWork(WorkKind::Satb, stream.references + cursor, count,
                    LxrWorkBucketStage::FinishConcurrentWork, true, nullptr, true));
                if (result != LxrRcResult::Success)
                {
                    return result;
                }
            }
            cursor += count;
        }
    }
    for (size_t rootBank = 0; rootBank < 2; rootBank++)
    {
        bool current = rootBank == m_current_roots;
        for (size_t offset = 0; offset < m_root_counts[rootBank]; offset += BufferCapacity)
        {
            size_t count = m_root_counts[rootBank] - offset;
            if (count > BufferCapacity)
            {
                count = BufferCapacity;
            }
            LxrRcResult submitted = SubmitWork(NewWork(
                current ? WorkKind::Roots : WorkKind::PreviousRoots,
                m_roots[rootBank] + offset, count,
                current ? LxrRCProcessIncs :
                    (m_pause_kind == LxrPauseKind::Full || !m_configuration.lazyDecrements
                        ? LxrRCProcessDecs : LxrWorkBucketStage::Unconstrained)));
            if (submitted != LxrRcResult::Success)
            {
                return submitted;
            }
#ifdef LXR_RC_EARLY_ROOT_CONTROL
            if (current)
            {
                submitted = SubmitWork(NewWork(WorkKind::PreviousRoots,
                    m_roots[rootBank] + offset, count, LxrWorkBucketStage::Unconstrained));
                if (submitted != LxrRcResult::Success)
                {
                    return submitted;
                }
            }
#endif
        }
    }
    for (bool retain : {true, false})
    {
        LxrWordArray& array = retain ? m_retained_roots : m_released_roots;
        for (size_t offset = 0; offset < array.Size(); offset += BufferCapacity)
        {
            size_t count = array.Size() - offset;
            if (count > BufferCapacity)
            {
                count = BufferCapacity;
            }
            LxrRcResult submitted = SubmitWork(NewWork(
                retain ? WorkKind::Roots : WorkKind::PreviousRoots, &array[offset], count,
                retain ? LxrRCProcessIncs :
                    (m_pause_kind == LxrPauseKind::Full || !m_configuration.lazyDecrements
                        ? LxrRCProcessDecs : LxrWorkBucketStage::Unconstrained)));
            if (submitted != LxrRcResult::Success)
            {
                return submitted;
            }
        }
        array.Truncate(0);
        array.Trim();
    }
    if (m_pause_kind != LxrPauseKind::RefCount || m_configuration.evacuateNursery ||
        m_configuration.evacuateMature)
    {
        LxrRcResult result = SubmitWork(NewWork(
            WorkKind::PrepareTrace, nullptr, 0, LxrWorkBucketStage::Prepare));
        if (result != LxrRcResult::Success)
        {
            return result;
        }
        if (m_pause_kind == LxrPauseKind::Full || m_pause_kind == LxrPauseKind::FinalMark)
        {
            result = SubmitWork(NewWork(WorkKind::WeakClosure, nullptr, 0, LxrWorkBucketStage::Closure));
            if (result != LxrRcResult::Success)
            {
                return result;
            }
        }
    }
    if (m_object_model.finishEpoch != nullptr || m_configuration.evacuateNursery ||
        m_configuration.evacuateMature)
    {
        LxrRcResult result = SubmitWork(NewWork(
            WorkKind::EpochClosure, nullptr, 0, LxrWorkBucketStage::Final));
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    return SubmitReleasePackets();
}

void LxrReferenceCounting::Suspend(void* context)
{
    LxrReferenceCounting* engine = static_cast<LxrReferenceCounting*>(context);
    engine->m_runtime.suspend(engine->m_runtime.context);
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_PREZERO_NO_PAUSE_GATE_CONTROL)
    // Control: the pause does not hold the clearer back.
    engine->m_allocation.SuspendPreZero();
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_PREZERO_NO_PAUSE_GATE_CONTROL
    int64_t prepareStart = engine->m_pause_timers.Enabled() ? engine->m_pause_timers.Stamp() : 0;
    engine->m_phase.store(LxrRcPhase::Suspended, std::memory_order_release);
    engine->m_copy_exhausted.store(false, std::memory_order_relaxed);
    engine->m_mature_copy_exhausted.store(false, std::memory_order_relaxed);
    engine->m_pause_copied_bytes.store(0, std::memory_order_relaxed);
    engine->m_pause_mature_copied_bytes.store(0, std::memory_order_relaxed);
    for (size_t worker = 0; worker <= engine->m_configuration.workers; worker++)
    {
        engine->m_worker_counters[worker].values[PromotedBytes].store(0, std::memory_order_relaxed);
        engine->m_worker_counters[worker].values[PauseCopiedBytes].store(0, std::memory_order_relaxed);
        engine->m_worker_counters[worker].values[PauseMatureCopiedBytes].store(0, std::memory_order_relaxed);
    }
    for (std::atomic<uint64_t>& walk : engine->m_walk)
    {
        walk.store(0, std::memory_order_relaxed);
    }
    engine->RetireCompletedWork();
    if (engine->FlushMutators() != LxrRcResult::Success)
    {
        return;
    }
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_F9_NO_PAUSE_RESET_CONTROL)
    engine->m_allocation.ResetCursors();
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_F9_NO_PAUSE_RESET_CONTROL
    if (engine->m_grow_bytes != 0)
    {
        engine->m_growth_result = engine->GrowSuspended();
    }
    if (engine->m_overshoot)
    {
        engine->m_allocation.ResetCopyCursorQuiescent();
        if (engine->BorrowSuspended() != LxrRcResult::Success)
        {
            return;
        }
    }
    if (engine->m_pause_kind == LxrPauseKind::InitialMark ||
        (engine->m_pause_kind == LxrPauseKind::Full &&
            engine->m_trace_phase.load(std::memory_order_acquire) == LxrTracePhase::Inactive))
    {
        // The marks of this trace are set from here on, so the plane is Dirty again after this pause. The packets of
        // ScheduleMarkClear have drained in the pre-drain of this pause, so Clearing cannot be seen here in practice; if
        // it is, the serial clear below is still correct.
        if (engine->m_mark_plane.exchange(MarkPlane::Dirty, std::memory_order_acq_rel) != MarkPlane::Clean)
        {
            int64_t clearStart = engine->m_pause_timers.Enabled() ? engine->m_pause_timers.Stamp() : 0;
            if (engine->FromMetadata(engine->m_metadata.ResetAllDataRangesQuiescent(
                    LxrSideMetadataKind::ObjectMark, 0)) != LxrRcResult::Success)
            {
                return;
            }
            if (clearStart != 0)
            {
                engine->m_lazy_metrics[MarkClearPause].fetch_add(1, std::memory_order_relaxed);
                engine->m_lazy_metrics[MarkClearPauseNs].fetch_add(
                    TicksToNanoseconds(engine->m_pause_timers.Stamp() - clearStart), std::memory_order_relaxed);
            }
        }
    }
    if (engine->m_pause_kind == LxrPauseKind::FinalMark || engine->m_pause_kind == LxrPauseKind::Full)
    {
        engine->m_marking.store(false, std::memory_order_release);
        engine->m_hold_trace.store(false, std::memory_order_release);
        engine->m_trace_phase.store(engine->m_pause_kind == LxrPauseKind::Full
            ? LxrTracePhase::Full : LxrTracePhase::FinalMark, std::memory_order_release);
        engine->m_cycle_sweep.store(true, std::memory_order_release);
    }
    else if (engine->m_pause_kind == LxrPauseKind::InitialMark)
    {
        engine->m_trace_phase.store(LxrTracePhase::InitialMark, std::memory_order_release);
    }
    ImmixBlockOperationStatus status;
    if (engine->m_blocks.StartGcPause(&status) != SideMetadataResult::Success ||
        status != ImmixBlockOperationStatus::Updated)
    {
        engine->Fail(LxrRcResult::InvalidPhase);
        return;
    }
    LxrLargeObjectResult large = engine->m_large.GetPhase() == LxrLargeObjectPhase::ConcurrentMark
        ? engine->m_large.BeginRelease() : engine->m_large.BeginCollection(false,
            engine->m_pause_kind == LxrPauseKind::InitialMark || engine->m_pause_kind == LxrPauseKind::Full);
    if (large != LxrLargeObjectResult::Success)
    {
        engine->Fail(LxrRcResult::InvalidPhase);
        return;
    }
    if (engine->m_capture != nullptr)
    {
        if (prepareStart != 0)
        {
            engine->m_pause_timers.Add(
                LxrPauseTimers::SuspendPrepare, engine->m_pause_timers.Stamp() - prepareStart);
        }
        LxrPhaseScope capture(engine->m_pause_timers, LxrPauseTimers::RootCapture);
        LxrRcResult result = engine->m_capture(engine, engine->m_capture_context);
        if (result != LxrRcResult::Success)
        {
            engine->Fail(result);
            return;
        }
    }
    // The root capture has found every pinned root. From here to the end of the pause the pin list does not change.
    if (engine->SealPins() != LxrRcResult::Success)
    {
        return;
    }
    LxrPhaseScope prepareWork(engine->m_pause_timers, LxrPauseTimers::PrepareWork);
    if (engine->m_configuration.evacuateMature &&
        (engine->m_pause_kind == LxrPauseKind::InitialMark || engine->m_pause_kind == LxrPauseKind::Full))
    {
        if (engine->SelectMatureCandidates() != LxrRcResult::Success)
        {
            return;
        }
    }
    if (engine->m_configuration.evacuateMature &&
        (engine->m_pause_kind == LxrPauseKind::FinalMark || engine->m_pause_kind == LxrPauseKind::Full) &&
        engine->RetainPinnedMatureBlocks() != LxrRcResult::Success)
    {
        return;
    }
    engine->PrepareWork();
    // The packets of a parallel selection that woke late are done by now (P50).
    engine->FinishSelection(LxrRcResult::Success);
}

void LxrReferenceCounting::Resume(bool finishedGc, void* context)
{
    LxrReferenceCounting* engine = static_cast<LxrReferenceCounting*>(context);
    engine->m_young_spans.Clear();
    if (engine->m_pause_timers.Enabled())
    {
        int64_t released = engine->m_release_end_stamp.exchange(0, std::memory_order_relaxed);
        if (released != 0)
        {
            engine->m_pause_timers.Add(LxrPauseTimers::ResumeGap, engine->m_pause_timers.Stamp() - released);
        }
    }
    engine->m_phase.store(LxrRcPhase::Mutator, std::memory_order_release);
    if (engine->m_pause_timers.Enabled())
    {
        engine->m_lazy_start_stamp.store(engine->m_pause_timers.Stamp(), std::memory_order_relaxed);
    }
    if (engine->GetTracePhase() == LxrTracePhase::Concurrent)
    {
        LxrPhaseScope publish(engine->m_pause_timers, LxrPauseTimers::PublishTrace);
        if (engine->PublishTraceWork() != LxrRcResult::Success)
        {
            engine->Fail(LxrRcResult::SchedulerFailure);
        }
    }
    engine->m_runtime.resume(finishedGc, engine->m_runtime.context);
    engine->m_allocation.ResumePreZero();
    engine->m_zeroer.Wake();
}

LxrRcResult LxrReferenceCounting::Collect(
    LxrRcCaptureRoots capture, void* context, uint32_t timeoutMilliseconds)
{
    return RunPause(LxrPauseKind::RefCount, capture, context, timeoutMilliseconds);
}

LxrRcResult LxrReferenceCounting::BeginTrace(
    LxrRcCaptureRoots capture, void* context, uint32_t timeoutMilliseconds)
{
    if (GetTracePhase() != LxrTracePhase::Inactive || m_observer)
    {
        return LxrRcResult::InvalidPhase;
    }
    return RunPause(LxrPauseKind::InitialMark, capture, context, timeoutMilliseconds);
}

LxrRcResult LxrReferenceCounting::FinishTrace(
    LxrRcCaptureRoots capture, void* context, uint32_t timeoutMilliseconds)
{
    if (GetTracePhase() != LxrTracePhase::Concurrent)
    {
        return LxrRcResult::InvalidPhase;
    }
    return RunPause(LxrPauseKind::FinalMark, capture, context, timeoutMilliseconds);
}

LxrRcResult LxrReferenceCounting::CollectFull(
    LxrRcCaptureRoots capture, void* context, uint32_t timeoutMilliseconds)
{
    if (m_observer)
    {
        return LxrRcResult::InvalidPhase;
    }
    return RunPause(LxrPauseKind::Full, capture, context, timeoutMilliseconds);
}

LxrRcResult LxrReferenceCounting::RunPause(
    LxrPauseKind kind, LxrRcCaptureRoots capture, void* context, uint32_t timeoutMilliseconds)
{
    if (GetFailure() != LxrRcResult::Success)
    {
        return GetFailure();
    }
    if (GetPhase() != LxrRcPhase::Mutator)
    {
        return LxrRcResult::InvalidPhase;
    }
    if (m_scheduler.GetGeneration() >= UINT64_MAX - 1)
    {
        return Fail(LxrRcResult::CapacityExceeded);
    }
    LxrRcResult drained;
    {
        LxrPhaseScope preDrain(m_pause_timers, LxrPauseTimers::PreDrain);
#ifdef LXR_S4_NO_SWEEP_WAIT_CONTROL
        drained = LxrRcResult::Success;
#else
        drained = GetTracePhase() == LxrTracePhase::Inactive
            ? Drain(timeoutMilliseconds) : DrainEpoch(timeoutMilliseconds);
#endif // LXR_S4_NO_SWEEP_WAIT_CONTROL
    }
    if (drained != LxrRcResult::Success)
    {
        return drained;
    }
    if (GetTracePhase() == LxrTracePhase::Inactive)
    {
        DeleteWork();
    }
    else
    {
        m_trace_metrics[Interruptions].fetch_add(1, std::memory_order_relaxed);
        if (m_scan_checkpoint_active.load(std::memory_order_acquire))
        {
            m_trace_metrics[CheckpointInterruptions].fetch_add(1, std::memory_order_relaxed);
        }
        m_scan_checkpoint.store(0, std::memory_order_release);
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        m_gate_armed.store(false, std::memory_order_release);
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_MATURE_READER_COMPLETION_CONTROL)
        // Force the previously permitted completion between checkpoint release and scheduler interruption.
        std::chrono::steady_clock::time_point deadline = std::chrono::steady_clock::now() +
            std::chrono::milliseconds(timeoutMilliseconds);
        for (TraceScan* scan = m_scan_work.load(std::memory_order_acquire); scan != nullptr; scan = scan->next)
        {
            while (!scan->complete.load(std::memory_order_acquire) &&
                std::chrono::steady_clock::now() < deadline)
            {
                GCToOSInterface::YieldThread(0);
            }
            if (!scan->complete.load(std::memory_order_acquire))
            {
                return Fail(LxrRcResult::InvariantViolation);
            }
        }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_MATURE_READER_COMPLETION_CONTROL
    }
    m_dead_large_count.store(0, std::memory_order_relaxed);
    m_capture = capture;
    m_capture_context = context;
    m_pause_kind = kind;
    // The drain above ended every lazy decrement of the previous epoch, so the first epoch after InitialMark is over (P46).
    m_first_window.store(false, std::memory_order_release);
#ifdef LXR_HAS_PHASE_TIMERS
    if (m_marking.load(std::memory_order_acquire))
    {
        m_window_metrics[6].fetch_add(1, std::memory_order_relaxed);
    }
#endif // LXR_HAS_PHASE_TIMERS
    if (m_coordinator.PreparePause(kind, !m_configuration.lazyDecrements,
            m_configuration.evacuateNursery || m_configuration.evacuateMature) !=
                LxrPauseCoordinatorResult::Success ||
        m_coordinator.StartPause(timeoutMilliseconds) != LxrPauseCoordinatorResult::Success)
    {
        return Fail(LxrRcResult::SchedulerFailure);
    }
    if (m_coordinator.CompletePause(timeoutMilliseconds) != LxrPauseCoordinatorResult::Success)
    {
        m_coordinator.AbortPause();
        return Fail(LxrRcResult::SchedulerFailure);
    }
    m_grow_bytes = 0;
    return GetFailure();
}

LxrRcResult LxrReferenceCounting::DrainEpoch(uint32_t timeoutMilliseconds)
{
    if (GetFailure() != LxrRcResult::Success)
    {
        return GetFailure();
    }
    if (m_scheduler.GetGeneration() != 0)
    {
        LxrRcResult result = FromScheduler(m_scheduler.WaitForCompletion(
            m_scheduler.GetGeneration(), timeoutMilliseconds));
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    return FromScheduler(m_scheduler.WaitForBucketIdle(
        LxrWorkBucketStage::Unconstrained, timeoutMilliseconds));
}

LxrRcResult LxrReferenceCounting::Drain(uint32_t timeoutMilliseconds)
{
    LxrRcResult result = DrainEpoch(timeoutMilliseconds);
    if (result == LxrRcResult::Success)
    {
        result = FromScheduler(m_scheduler.WaitForIdle(timeoutMilliseconds));
    }
    if (result == LxrRcResult::Success && GetTracePhase() == LxrTracePhase::Inactive)
    {
        DeleteWork();
    }
    return result;
}

LxrRcResult LxrReferenceCounting::AddPinnedRoot(Object* object, bool interior, size_t shard)
{
    if (GetPhase() != LxrRcPhase::Suspended)
    {
        return LxrRcResult::InvalidPhase;
    }
    if (object == nullptr || m_block_flags == nullptr)
    {
        return LxrRcResult::Success;
    }
    if (interior)
    {
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_S7_PIN_INTERIOR_CONTROL)
        // Control: the address itself goes on the list, not the object that contains it.
#else
        LxrRcResult found = FindContainingObject(reinterpret_cast<uintptr_t>(object), &object);
        if (found != LxrRcResult::Success)
        {
            return found;
        }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_S7_PIN_INTERIOR_CONTROL
    }
    uintptr_t start = Start(object);
    if (!m_pins.Add(shard, reinterpret_cast<uintptr_t>(object)))
    {
        return Fail(LxrRcResult::OutOfMemory);
    }
    if (IsImmix(start))
    {
        // Large objects never move, so only an Immix block needs the pause bit.
#if !defined(LXR_S7_NO_BLOCK_BYTE_CONTROL)
        m_block_flags[(start - GetReservationStart()) / ImmixBlockGeometry::BlockBytes].fetch_or(
            PausePinFlag, std::memory_order_release);
#endif // !LXR_S7_NO_BLOCK_BYTE_CONTROL
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::SealPins()
{
    uint64_t count = m_pins.Seal();
    if (m_pins.HasFailed())
    {
        return Fail(LxrRcResult::OutOfMemory);
    }
    m_pause_pinned_roots.store(count, std::memory_order_release);
    return LxrRcResult::Success;
}

void LxrReferenceCounting::ClearPins()
{
    if (m_block_flags != nullptr)
    {
        for (size_t index = 0; index < m_pins.Size(); index++)
        {
            uintptr_t start = Start(reinterpret_cast<Object*>(m_pins[index]));
            if (IsImmix(start))
            {
#if !defined(LXR_S7_NO_BLOCK_CLEAR_CONTROL)
                m_block_flags[(start - GetReservationStart()) / ImmixBlockGeometry::BlockBytes].fetch_and(
                    static_cast<uint8_t>(~PausePinFlag), std::memory_order_release);
#endif // !LXR_S7_NO_BLOCK_CLEAR_CONTROL
            }
        }
    }
    m_pins.Clear();
}

bool LxrReferenceCounting::IsPinnedObject(Object* object) const
{
#if defined(LXR_S7_IGNORE_PIN_CONTROL)
    // Control: the pause never knows a pin.
    (void)object;
    return false;
#else
    uintptr_t start = Start(object);
    if (m_block_flags == nullptr || !IsImmix(start) ||
        (start - GetReservationStart()) / ImmixBlockGeometry::BlockBytes >= m_block_table_blocks.load(std::memory_order_acquire))
    {
        return false;
    }
    uint8_t flags = m_block_flags[(start - GetReservationStart()) / ImmixBlockGeometry::BlockBytes].load(
        std::memory_order_acquire);
    if (flags == 0)
    {
        return false;
    }
    if ((flags & ImmixAllocationManager::PinnedBlockFlag) != 0)
    {
        return true;
    }
    return (flags & PausePinFlag) != 0 && m_pins.Contains(reinterpret_cast<uintptr_t>(object));
#endif // LXR_S7_IGNORE_PIN_CONTROL
}

bool LxrReferenceCounting::IsPinnedForPause(Object* object) const
{
    return object != nullptr && IsPinnedObject(object);
}

bool LxrReferenceCounting::IsPinnedBlock(uintptr_t address) const
{
    return m_block_flags != nullptr && IsImmix(address) &&
        (address - GetReservationStart()) / ImmixBlockGeometry::BlockBytes < m_block_table_blocks.load(std::memory_order_acquire) &&
        (m_block_flags[(address - GetReservationStart()) / ImmixBlockGeometry::BlockBytes].load(
            std::memory_order_acquire) & ImmixAllocationManager::PinnedBlockFlag) != 0;
}

size_t LxrReferenceCounting::CountPausePinnedBlocks() const
{
    size_t count = 0;
    if (m_block_flags != nullptr)
    {
        for (size_t block = 0; block < GetImmixCapacityBytes() / ImmixBlockGeometry::BlockBytes; block++)
        {
            count += (m_block_flags[block].load(std::memory_order_acquire) & PausePinFlag) != 0 ? 1 : 0;
        }
    }
    return count;
}

LxrRcResult LxrReferenceCounting::AddRoot(Object* object, bool interior, bool trace)
{
    if (GetPhase() != LxrRcPhase::Suspended)
    {
        return LxrRcResult::InvalidPhase;
    }
    if (object == nullptr)
    {
        return LxrRcResult::Success;
    }
    if (interior)
    {
        LxrRcResult result = FindContainingObject(reinterpret_cast<uintptr_t>(object), &object);
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    // A root may point to a young object, which has no ValidObject bit. The runtime reports exact roots, so the value
    // only has to lie in the owned space.
    if (!IsOwnedObject(object))
    {
        return LxrRcResult::InvalidArgument;
    }
    size_t& count = m_root_counts[m_current_roots];
    if (count == m_root_capacity[m_current_roots])
    {
        // The roots of a pause are not limited by the initial size: a registry or a handle table can hold more.
        size_t capacity = m_root_capacity[m_current_roots];
        if (capacity >= RootGrowthLimit)
        {
            return LxrRcResult::CapacityExceeded;
        }
        capacity = capacity == 0 ? BufferCapacity : capacity * 2;
        uintptr_t* grown = new (std::nothrow) uintptr_t[capacity];
        if (grown == nullptr)
        {
            return LxrRcResult::OutOfMemory;
        }
        memcpy(grown, m_roots[m_current_roots], count * sizeof(uintptr_t));
        delete[] m_roots[m_current_roots];
        m_roots[m_current_roots] = grown;
        m_root_capacity[m_current_roots] = capacity;
    }
    m_roots[m_current_roots][count++] = reinterpret_cast<uintptr_t>(object) | (trace ? 0 : 1);
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::RetainRoot(Object* object)
{
    if (GetPhase() != LxrRcPhase::Suspended)
    {
        return LxrRcResult::InvalidPhase;
    }
    if (object == nullptr)
    {
        return LxrRcResult::Success;
    }
    if (!IsOwnedObject(object))
    {
        return LxrRcResult::InvalidArgument;
    }
    // The low bit tells the increment not to trace the object. Tracing is a decision of the caller.
    return m_retained_roots.Push(reinterpret_cast<uintptr_t>(object) | 1)
        ? LxrRcResult::Success : LxrRcResult::OutOfMemory;
}

LxrRcResult LxrReferenceCounting::ReleaseRoot(Object* object)
{
    if (GetPhase() != LxrRcPhase::Suspended)
    {
        return LxrRcResult::InvalidPhase;
    }
    if (object == nullptr)
    {
        return LxrRcResult::Success;
    }
    return m_released_roots.Push(reinterpret_cast<uintptr_t>(object))
        ? LxrRcResult::Success : LxrRcResult::OutOfMemory;
}

LxrRcResult LxrReferenceCounting::GetObjectSize(Object* object, size_t* size, size_t worker)
{
    if (GetPhase() == LxrRcPhase::Sweeping)
    {
        return Fail(LxrRcResult::InvariantViolation);
    }
    CountWorker(worker, ObjectSizeQueries, 1);
    size_t value = object->GetSize();
    if (value > SIZE_MAX - (sizeof(uintptr_t) - 1))
    {
        return Fail(LxrRcResult::InvalidArgument);
    }
    *size = (value + sizeof(uintptr_t) - 1) & ~(sizeof(uintptr_t) - 1);
    if (*size < MIN_OBJECT_SIZE)
    {
        return Fail(LxrRcResult::InvalidArgument);
    }
    return LxrRcResult::Success;
}

Object* LxrReferenceCounting::ResolveForwarded(Object* object) const
{
    if ((!m_configuration.evacuateNursery && !m_configuration.evacuateMature) || object == nullptr)
    {
        return object;
    }
    uintptr_t start = Start(object);
#ifdef LXR_OBJECT_START_WALK
    // The walk variant has no start plane to tell a copy source from an arbitrary address. The value must be the start
    // of an object (young, or counted), as the references of live slots, roots and counted objects are. A value that may
    // be stale goes through ResolveForwardedChecked. A header that has no forwarding state is the common case, and
    // costs no metadata read.
    if (!IsImmix(start) || start % sizeof(uintptr_t) != 0)
    {
        return object;
    }
#else
    // P22 (D2). A young object has no ValidObject bit, but a source that the pause copies gets the bit before the
    // claim (TryCopyNursery) and keeps it until RetireSourceChunk. So the bit tells a source from an arbitrary
    // or stale address (the trace queue and the decrement stream can name a dead object, and the memory may now hold
    // part of a new object) without a per-object size table.
    if (!IsImmix(start) || start % sizeof(uintptr_t) != 0 ||
        m_valid_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) == 0)
    {
        return object;
    }
#endif // LXR_OBJECT_START_WALK
    ObjHeader* header = object->GetHeader();
    uint32_t state = header->GetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift);
    if (state == 2)
    {
        m_nursery_metrics[4].fetch_add(1, std::memory_order_relaxed);
        state = header->WaitWhileGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift, 2);
    }
    if (state == 3)
    {
        // Keep the MethodTable and synchronization word intact for EE diagnostics during relocation.
        Object** forwarding = reinterpret_cast<Object**>(reinterpret_cast<uint8_t*>(object) + sizeof(uintptr_t));
        // The pointer is stored before the state changes to 3 (a full barrier), and the state was read with acquire.
        return VolatileLoad(forwarding);
    }
    _ASSERTE(state == 0);
    return object;
}

Object* LxrReferenceCounting::ResolveForwardedChecked(Object* object) const
{
#if !defined(LXR_OBJECT_START_WALK) || (defined(LXR_WALK_TRUSTED_RESOLVE_CONTROL))
    // (Control for the walk variant: a stale value is trusted like a start.)
    return ResolveForwarded(object);
#else
    if ((!m_configuration.evacuateNursery && !m_configuration.evacuateMature) || object == nullptr)
    {
        return object;
    }
    uintptr_t start = Start(object);
    if (!IsImmix(start) || start % sizeof(uintptr_t) != 0)
    {
        return object;
    }
    size_t slot = (start - GetReservationStart()) / sizeof(uintptr_t);
    if (m_rc_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) != 0)
    {
        // A counted start has a valid header. The first slot of a continuation line also holds a count, but it lies inside
        // another object, and its header is arbitrary data.
        return IsContinuationSlot(slot) ? object : ResolveForwarded(object);
    }
    // Count zero: a young object, a dead object, or an address inside an object. Only the young object that the pause
    // claimed as a copy source has a forwarding state. The sources are in the spans of the pause, which exist until
    // Resume; a value that no span starts an object at is not a source. The header is read first only to skip the walk
    // for the common case of a header without a forwarding state.
    if (m_young_spans.Size() == 0 ||
        object->GetHeader()->GetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift) == 0)
    {
        return object;
    }
    m_lookup_metrics[3].fetch_add(1, std::memory_order_relaxed);
    Object* found = nullptr;
    if (FindInYoungSpans(reinterpret_cast<uintptr_t>(object), &found, false) != LxrRcResult::Success || found != object)
    {
        return object;
    }
    return ResolveForwarded(object);
#endif // !LXR_OBJECT_START_WALK
}

#ifdef LXR_OBJECT_START_WALK
bool LxrReferenceCounting::IsContinuationSlot(size_t slot) const
{
#if defined(LXR_WALK_NO_CONTINUATION_SKIP_CONTROL)
    // Control: the count of a continuation line is taken for the start of an object.
    (void)slot;
    return false;
#else
    uintptr_t start = GetReservationStart() + slot * sizeof(uintptr_t);
    return (start & ImmixBlockGeometry::LineMask) == 0 &&
        m_straddle_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) != 0;
#endif // LXR_WALK_NO_CONTINUATION_SKIP_CONTROL
}

size_t LxrReferenceCounting::FindCountedSlotBelow(size_t lowerSlot, size_t slot) const
{
    // The count plane has 2, 4 or 8 bits per slot. One 64-bit word covers 64 / bits slots, and the arena start is aligned to
    // a block, so a slot index and its position in the word agree.
    uintptr_t arena = GetReservationStart();
    uint32_t bits = static_cast<uint32_t>(1) << m_rc_metadata.GetLogBitsPerValue();
    uint64_t lowest = 0;
    for (uint32_t position = 0; position < 64; position += bits)
    {
        lowest |= UINT64_C(1) << position;
    }
    while (true)
    {
        SideMetadataLocation location = m_rc_metadata.Locate(arena + slot * sizeof(uintptr_t));
        uint64_t word = static_cast<uint64_t>(
            SideMetadataAccessor::LoadWord(location.word, SideMetadataMemoryOrder::Relaxed));
        // One bit per slot (the lowest bit of its field) that says that the count is not zero.
        uint64_t folded = word;
        for (uint32_t shift = 1; shift < bits; shift <<= 1)
        {
            folded |= folded >> shift;
        }
        folded &= lowest;
        // Only the slots at or below the slot (the slots of the word that lie above it are not part of the search).
        uint32_t slotInWord = location.shift / bits;
        uint32_t keep = (slotInWord + 1) * bits;
        if (keep < 64)
        {
            folded &= (UINT64_C(1) << keep) - 1;
        }
        while (folded != 0)
        {
#ifdef TARGET_WINDOWS
            DWORD top;
#else
            uint32_t top;
#endif // TARGET_WINDOWS
            BitScanReverse64(&top, folded);
            size_t candidate = slot - slotInWord + top / bits;
            if (candidate < lowerSlot)
            {
                return SIZE_MAX;
            }
            if (!IsContinuationSlot(candidate))
            {
                std::atomic_thread_fence(std::memory_order_acquire);
                return candidate;
            }
            folded &= ~(UINT64_C(1) << top);
        }
        size_t wordFirst = slot - slotInWord;
        if (wordFirst <= lowerSlot)
        {
            return SIZE_MAX;
        }
#if defined(LXR_WALK_ONE_WORD_CONTROL)
        // Control: the scan looks at one word of the count plane only.
        return SIZE_MAX;
#endif // LXR_WALK_ONE_WORD_CONTROL
        slot = wordFirst - 1;
    }
}

void LxrReferenceCounting::ClosePinnedSpan(uintptr_t end)
{
#if defined(LXR_WALK_NO_PINNED_SPAN_CONTROL)
    // Control: a pinned object has no span.
    (void)end;
    m_pinned_span_start = 0;
    return;
#endif // LXR_WALK_NO_PINNED_SPAN_CONTROL
    uintptr_t start = m_pinned_span_start;
    m_pinned_span_start = 0;
    if (start == 0 || end <= start)
    {
        return;
    }
    while (m_detached_lock.test_and_set(std::memory_order_acquire))
    {
        YieldProcessor();
    }
    bool pushed = m_detached_spans.Push(start, end);
    m_detached_lock.clear(std::memory_order_release);
    if (!pushed)
    {
        Fail(LxrRcResult::OutOfMemory);
        return;
    }
    m_span_metrics[0].fetch_add(1, std::memory_order_relaxed);
    m_span_metrics[1].fetch_add(end - start, std::memory_order_relaxed);
    m_metrics[AllocatedBytes].fetch_add(end - start, std::memory_order_relaxed);
}
#endif // LXR_OBJECT_START_WALK

LxrRcResult LxrReferenceCounting::RelocateRoot(Object** slot, bool interior, bool pinned) const
{
    if (slot == nullptr || GetPhase() != LxrRcPhase::Suspended)
    {
        return LxrRcResult::InvalidPhase;
    }
    Object* object = *slot;
    if (object == nullptr || !OwnsAddress(reinterpret_cast<uintptr_t>(object)))
    {
        return LxrRcResult::Success;
    }
    uintptr_t offset = 0;
    bool pastEnd = false;
    if (interior)
    {
        uintptr_t address = reinterpret_cast<uintptr_t>(object);
        LxrRcResult result = FindContainingObject(address, &object);
        if (result == LxrRcResult::InvalidArgument)
        {
            // No live object contains the address (see LxrGcHeap::AddRoot). A byref that points one past the end of an
            // object moves with that object, so that it keeps its distance from the byrefs inside the object. The root
            // did not keep the object, so it does not pin it either.
            Object* previous = nullptr;
            if (address % sizeof(uintptr_t) != 0 ||
                FindContainingObject(address - sizeof(uintptr_t), &previous) != LxrRcResult::Success ||
                Start(previous) + ObjectSize(ResolveForwarded(previous)) != address)
            {
                return LxrRcResult::Success;
            }
            object = previous;
            pastEnd = true;
        }
        else if (result != LxrRcResult::Success)
        {
            return result;
        }
        offset = address - reinterpret_cast<uintptr_t>(object);
    }
    Object* destination = ResolveForwarded(object);
    if (pinned && !pastEnd && destination != object)
    {
        return LxrRcResult::InvariantViolation;
    }
    *slot = reinterpret_cast<Object*>(reinterpret_cast<uintptr_t>(destination) + offset);
    return LxrRcResult::Success;
}

LxrNurseryMetrics LxrReferenceCounting::GetNurseryMetrics() const
{
    return {
        SumWorker(CopiedObjects), SumWorker(CopiedBytes), m_nursery_metrics[2].load(),
        m_nursery_metrics[3].load(), m_nursery_metrics[4].load(), m_nursery_metrics[5].load(),
        m_pause_copied_bytes.load() + SumWorker(PauseCopiedBytes),
    };
}

LxrMatureMetrics LxrReferenceCounting::GetMatureMetrics() const
{
    return {
        m_mature_metrics[0].load(), m_mature_metrics[1].load(), m_mature_metrics[2].load(),
        m_mature_metrics[3].load(), SumWorker(MatureCopiedObjects) + m_mature_metrics[4].load(),
        SumWorker(MatureCopiedBytes) + m_mature_metrics[5].load(),
        m_mature_metrics[6].load(), m_mature_metrics[7].load(), m_mature_metrics[8].load(),
        m_mature_metrics[9].load(), m_mature_metrics[10].load(), m_mature_metrics[11].load(),
        m_mature_metrics[12].load(),
        static_cast<size_t>(SumWorker(PauseMatureCopiedBytes)) + m_pause_mature_copied_bytes.load(),
    };
}

uint64_t LxrReferenceCounting::GetCostMetric(size_t metric) const
{
    if (metric == 0)
    {
        return m_storage_diagnostics.load(std::memory_order_acquire);
    }
    if (metric > 9 || !m_storage_diagnostics.load(std::memory_order_acquire))
    {
        return UINT64_MAX;
    }
    return m_cost_ticks[metric - 1].load(std::memory_order_acquire);
}

LxrRcResult LxrReferenceCounting::GetDefragHeadroomBytes(size_t* bytes) const
{
    if (bytes == nullptr)
    {
        return LxrRcResult::InvalidArgument;
    }
    *bytes = 0;
    size_t blocks = m_allocation.GetBlocksInUse();
    *bytes = blocks * (ImmixBlockGeometry::BlockBytes / LxrLargeObjectSpace::PageBytes) *
        m_configuration.defragHeadroomPercent / 100 * LxrLargeObjectSpace::PageBytes;
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::GetUsedPages(size_t* pages) const
{
    if (pages == nullptr)
    {
        return LxrRcResult::InvalidArgument;
    }
    size_t bytes = m_large.GetCommittedBytes() +
        m_allocation.GetBlocksInUse() * ImmixBlockGeometry::BlockBytes;
    *pages = bytes / LxrLargeObjectSpace::PageBytes;
    return LxrRcResult::Success;
}

bool LxrReferenceCounting::IsMatureCandidate(uintptr_t address)
{
    if (m_evacuation_sources == nullptr ||
        (GetTracePhase() == LxrTracePhase::Inactive && m_pause_kind == LxrPauseKind::RefCount) ||
        !IsImmix(address) ||
        (address - GetReservationStart()) / ImmixBlockGeometry::BlockBytes >= m_block_table_blocks.load(std::memory_order_acquire) ||
        m_evacuation_sources[(address - GetReservationStart()) / ImmixBlockGeometry::BlockBytes].load(
            std::memory_order_acquire) == 0)
    {
        return false;
    }
    return m_defrag_metadata.Load(address, SideMetadataMemoryOrder::SequentiallyConsistent) == UINT8_MAX;
}

struct LxrReferenceCounting::MatureCandidate
{
    uintptr_t block;
    size_t deadLines;
    size_t chunkScore;
};

LxrRcResult LxrReferenceCounting::SelectMatureCandidates()
{
    if (!m_configuration.evacuateMature)
    {
        return LxrRcResult::Success;
    }
#ifdef LXR_HAS_PHASE_TIMERS
    LxrPhaseScope selectTimer(m_pause_timers, LxrPauseTimers::SelectMature);
#endif // LXR_HAS_PHASE_TIMERS
    if (m_configuration.selectValidate)
    {
        return SelectMatureValidated();
    }
    // The blocks of the allocator in use stand for the occupied blocks (the loop costs 236 ns for each occupied block and 64 ns
    // for each other one, P49): a small heap is faster on the pause thread than the hand-over to the workers.
    if (m_allocation.GetBlocksInUse() >= m_configuration.selectParallelBlocks &&
        GetImmixCapacityBytes() / ImmixBlockGeometry::BlockBytes > 1)
    {
        return SelectMatureParallel(false);
    }
    return SelectMatureSerial();
}

// The serial selection. It is the loop of the first version (P6) and the reference of the parallel selection: the validation
// switch runs both and compares the sets.
LxrRcResult LxrReferenceCounting::SelectMatureSerial()
{
#ifdef LXR_HAS_PHASE_TIMERS
    m_select_metrics[0].fetch_add(1, std::memory_order_relaxed);
#endif // LXR_HAS_PHASE_TIMERS
    using Candidate = MatureCandidate;
    size_t blocks = GetImmixCapacityBytes() / ImmixBlockGeometry::BlockBytes;
    Candidate* candidates = new (std::nothrow) Candidate[blocks];
    if (candidates == nullptr)
    {
        return Fail(LxrRcResult::OutOfMemory);
    }
    size_t count = 0;
    size_t freeBytes = 0;
    LxrRcResult result = LxrRcResult::Success;
    for (size_t index = 0; index < blocks; index++)
    {
        uintptr_t block = GetReservationStart() + index * ImmixBlockGeometry::BlockBytes;
        ImmixBlockState state;
        uint8_t unavailable;
        result = FromMetadata(m_blocks.GetStateFast(block, &state, &unavailable));
        if (result != LxrRcResult::Success)
        {
            break;
        }
        if (state == ImmixBlockState::Unallocated)
        {
            bool nursery;
            result = FromMetadata(m_blocks.IsNurseryFast(block, &nursery));
            if (result != LxrRcResult::Success)
            {
                break;
            }
            if (!nursery)
            {
                freeBytes += ImmixBlockGeometry::BlockBytes;
            }
            continue;
        }
        if (IsPinnedBlock(block))
        {
            // The objects of a pinned block never move, so it is never a source of the evacuation.
            continue;
        }
        size_t deadLines = 0;
        {
            // The count plane of a block is a few hundred bytes: a line is dead when its bytes are all zero. The mutators are
            // stopped and the lazy work has drained, so plain reads see the planes as they are.
            const size_t logBits = m_rc_metadata.GetLogBitsPerValue();
            const size_t logBytes = m_rc_metadata.GetLogBytesPerValue();
            const size_t bitsPerLine = (ImmixBlockGeometry::LineBytes >> logBytes) << logBits;
            uint32_t byteShift = 0;
            const volatile uint8_t* plane = m_rc_metadata.LocateByte(block, &byteShift);
            if (logBits <= 3 && byteShift == 0 && bitsPerLine % 64 == 0)
            {
                const size_t wordsPerLine = bitsPerLine / 64;
                const volatile uint64_t* words = reinterpret_cast<const volatile uint64_t*>(plane);
                for (size_t line = 0; line < ImmixBlockGeometry::LinesPerBlock; line++)
                {
                    uint64_t any = 0;
                    for (size_t word = 0; word < wordsPerLine; word++)
                    {
                        any |= words[line * wordsPerLine + word];
                    }
                    deadLines += any == 0 ? 1 : 0;
                }
            }
            else
            {
                for (size_t line = 0; line < ImmixBlockGeometry::LinesPerBlock; line++)
                {
                    deadLines += m_rc_metadata.IsRangeZero(block + line * ImmixBlockGeometry::LineBytes,
                        ImmixBlockGeometry::LineBytes, SideMetadataMemoryOrder::SequentiallyConsistent) ? 1 : 0;
                }
            }
        }
        candidates[count++] = {block, deadLines, 0};
    }
    if (result == LxrRcResult::Success)
    {
        constexpr size_t ChunkBytes = size_t{1} << 22;
        constexpr size_t BlocksPerChunk = ChunkBytes / ImmixBlockGeometry::BlockBytes;
        size_t percent = m_configuration.chunkDefragPercent;
        if (m_pause_kind == LxrPauseKind::Full)
        {
            percent = percent > 50 ? 100 : percent * 2;
        }
        size_t threshold = BlocksPerChunk * percent / 100;
        threshold = threshold == 0 ? 1 : threshold;
        // The candidates of a chunk are next to each other. A chunk is counted once, not once for each of its candidates.
        uintptr_t lastChunk = UINTPTR_MAX;
        size_t lastOccupied = 0;
        for (size_t index = 0; index < count; index++)
        {
            uintptr_t chunk = candidates[index].block & ~(static_cast<uintptr_t>(ChunkBytes) - 1);
            size_t occupied = 0;
            if (chunk == lastChunk)
            {
                occupied = lastOccupied;
            }
            else
            {
                uintptr_t first = chunk > GetReservationStart() ? chunk : GetReservationStart();
                uintptr_t end = chunk + ChunkBytes;
                end = end < GetReservationStart() + GetImmixCapacityBytes()
                    ? end : GetReservationStart() + GetImmixCapacityBytes();
                for (uintptr_t block = first; block < end; block += ImmixBlockGeometry::BlockBytes)
                {
                    ImmixBlockState state;
                    uint8_t unavailable;
                    result = FromMetadata(m_blocks.GetState(block, &state, &unavailable));
                    if (result != LxrRcResult::Success)
                    {
                        delete[] candidates;
                        return result;
                    }
                    occupied += state == ImmixBlockState::Unallocated ? 0 : 1;
                }
                lastChunk = chunk;
                lastOccupied = occupied;
            }
            if (occupied < threshold)
            {
                candidates[index].chunkScore = BlocksPerChunk - occupied;
            }
        }
        size_t eligible = 0;
        for (size_t index = 0; index < count; index++)
        {
            if (candidates[index].chunkScore != 0 || m_pause_kind == LxrPauseKind::Full ||
                candidates[index].deadLines >= ImmixBlockGeometry::LinesPerBlock / 2)
            {
                candidates[eligible++] = candidates[index];
            }
        }
        count = eligible;
#ifdef LXR_HAS_PHASE_TIMERS
        LxrPhaseScope tailTimer(m_pause_timers, LxrPauseTimers::SelectTail);
#endif // LXR_HAS_PHASE_TIMERS
        result = ApplyMatureSelection(candidates, count, freeBytes);
    }
    delete[] candidates;
    return result;
}

// The part that the serial and the parallel selection share: sorts the eligible candidates, applies the copy budget, and
// stores the evacuation set. The input order decides the order of equal keys (std::sort is not stable), so both selections
// give it the same order: ascending block address.
LxrRcResult LxrReferenceCounting::ApplyMatureSelection(MatureCandidate* candidates, size_t count, size_t freeBytes)
{
    using Candidate = MatureCandidate;
    LxrRcResult result = LxrRcResult::Success;
    {
        m_mature_metrics[0].fetch_add(count, std::memory_order_relaxed);
        std::sort(candidates, candidates + count, [](const Candidate& left, const Candidate& right)
        {
            return left.chunkScore == right.chunkScore
                ? left.deadLines < right.deadLines : left.chunkScore < right.chunkScore;
        });
        size_t headroom;
        result = GetDefragHeadroomBytes(&headroom);
        if (result != LxrRcResult::Success)
        {
            return Fail(result);
        }
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        if (m_select_headroom_override != SIZE_MAX)
        {
            headroom = m_select_headroom_override;
        }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        size_t budget = m_pause_kind == LxrPauseKind::Full ? (freeBytes > headroom ? freeBytes : headroom) : headroom;
        // Unlike the reference's shared page resource, only admitted Immix pages are usable to-space here.
        budget = budget < freeBytes ? budget : freeBytes;
        size_t estimated = 0;
        while (count != 0 && budget != 0)
        {
            Candidate candidate = candidates[--count];
            size_t index = (candidate.block - GetReservationStart()) / ImmixBlockGeometry::BlockBytes;
            m_evacuation_sources[index].store(1, std::memory_order_release);
            result = FromMetadata(m_metadata.Store(LxrSideMetadataKind::BlockDefrag, candidate.block,
                UINT8_MAX, SideMetadataMemoryOrder::SequentiallyConsistent));
            if (result != LxrRcResult::Success)
            {
                break;
            }
            m_mature_metrics[1].fetch_add(1, std::memory_order_relaxed);
            estimated += (ImmixBlockGeometry::LinesPerBlock - candidate.deadLines) *
                ImmixBlockGeometry::LineBytes / 2;
            if (estimated >= budget)
            {
                break;
            }
        }
        m_mature_metrics[3].fetch_add(estimated, std::memory_order_relaxed);
    }
    return result;
}

struct LxrReferenceCounting::SelectionRun
{
    // The longest wait of the pause thread for the packets of the selection. A packet takes milliseconds at most.
    static constexpr uint32_t SelectTimeoutMilliseconds = 60000;
    static constexpr size_t ChunkBytes = size_t{1} << 22;
    static constexpr size_t BlocksPerChunk = ChunkBytes / ImmixBlockGeometry::BlockBytes;
    static_assert(BlocksPerChunk <= UINT8_MAX);

    uintptr_t start = 0;
    // The end of the blocks that the serial loop visits.
    uintptr_t limit = 0;
    // The first chunk of the heap. It starts at or below the reservation start; the serial loop cuts it the same way.
    uintptr_t firstChunk = 0;
    size_t chunks = 0;
    size_t threshold = 0;
    bool full = false;
    // BlocksPerChunk slots for each chunk: a chunk writes its eligible candidates to its own slots, so that no thread writes
    // to the slots of another and the merge keeps the address order of the serial loop.
    MatureCandidate* candidates = nullptr;
    uint8_t* counts = nullptr;
    uint8_t* freeBlocks = nullptr;
    std::atomic<size_t> cursor{0};
    std::atomic<size_t> processed{0};
    std::atomic<size_t> visited{0};
    std::atomic<LxrRcResult> failure{LxrRcResult::Success};
};

// The number of lines of a block whose count bytes are all zero. The count plane of a block is a few hundred bytes. The mutators
// are stopped and the lazy work has drained, so plain reads see the planes as they are. The serial selection has the same loop
// inline (the reference for the validation); the two must stay equal.
size_t LxrReferenceCounting::CountDeadLines(uintptr_t block)
{
    size_t deadLines = 0;
    const size_t logBits = m_rc_metadata.GetLogBitsPerValue();
    const size_t logBytes = m_rc_metadata.GetLogBytesPerValue();
    const size_t bitsPerLine = (ImmixBlockGeometry::LineBytes >> logBytes) << logBits;
    uint32_t byteShift = 0;
    const volatile uint8_t* plane = m_rc_metadata.LocateByte(block, &byteShift);
    if (logBits <= 3 && byteShift == 0 && bitsPerLine % 64 == 0)
    {
        const size_t wordsPerLine = bitsPerLine / 64;
        const volatile uint64_t* words = reinterpret_cast<const volatile uint64_t*>(plane);
#ifdef LXR_SELECT_DEAD_LINES_CONTROL
        // Control: the first line is never counted.
        for (size_t line = 1; line < ImmixBlockGeometry::LinesPerBlock; line++)
#else
        for (size_t line = 0; line < ImmixBlockGeometry::LinesPerBlock; line++)
#endif // LXR_SELECT_DEAD_LINES_CONTROL
        {
            uint64_t any = 0;
            for (size_t word = 0; word < wordsPerLine; word++)
            {
                any |= words[line * wordsPerLine + word];
            }
            deadLines += any == 0 ? 1 : 0;
        }
    }
    else
    {
        for (size_t line = 0; line < ImmixBlockGeometry::LinesPerBlock; line++)
        {
            deadLines += m_rc_metadata.IsRangeZero(block + line * ImmixBlockGeometry::LineBytes,
                ImmixBlockGeometry::LineBytes, SideMetadataMemoryOrder::SequentiallyConsistent) ? 1 : 0;
        }
    }
    return deadLines;
}

// One chunk of the parallel selection: the first loop of the serial selection over the blocks of the chunk, the count of the
// occupied blocks of the chunk (the serial selection counts them in a second pass over the chunk), the chunk score, and the
// filter of the eligible candidates. Everything that it reads or writes belongs to the chunk, except the metadata of the blocks.
void LxrReferenceCounting::SelectChunk(SelectionRun* run, size_t chunk)
{
    uintptr_t base = run->firstChunk + chunk * SelectionRun::ChunkBytes;
    uintptr_t first = base > run->start ? base : run->start;
    uintptr_t end = base + SelectionRun::ChunkBytes;
    end = end < run->limit ? end : run->limit;
    MatureCandidate* out = run->candidates + chunk * SelectionRun::BlocksPerChunk;
    size_t count = 0;
    size_t occupied = 0;
    size_t freeBlocks = 0;
    for (uintptr_t block = first; block < end; block += ImmixBlockGeometry::BlockBytes)
    {
        ImmixBlockState state;
        uint8_t unavailable;
        LxrRcResult result = FromMetadata(m_blocks.GetStateFast(block, &state, &unavailable));
        if (result != LxrRcResult::Success)
        {
            run->failure.store(result, std::memory_order_relaxed);
            return;
        }
        if (state == ImmixBlockState::Unallocated)
        {
            bool nursery;
            result = FromMetadata(m_blocks.IsNurseryFast(block, &nursery));
            if (result != LxrRcResult::Success)
            {
                run->failure.store(result, std::memory_order_relaxed);
                return;
            }
            freeBlocks += nursery ? 0 : 1;
            continue;
        }
#ifdef LXR_SELECT_OCCUPIED_CONTROL
        // Control: a pinned block does not count as occupied.
        if (IsPinnedBlock(block))
        {
            continue;
        }
        occupied++;
#else
        occupied++;
        if (IsPinnedBlock(block))
        {
            // The objects of a pinned block never move, so it is never a source of the evacuation.
            continue;
        }
#endif // LXR_SELECT_OCCUPIED_CONTROL
        out[count++] = {block, CountDeadLines(block), 0};
    }
    size_t score = occupied < run->threshold ? SelectionRun::BlocksPerChunk - occupied : 0;
    size_t kept = 0;
    for (size_t index = 0; index < count; index++)
    {
        if (score != 0 || run->full || out[index].deadLines >= ImmixBlockGeometry::LinesPerBlock / 2)
        {
            out[kept] = out[index];
            out[kept].chunkScore = score;
            kept++;
        }
    }
    run->counts[chunk] = static_cast<uint8_t>(kept);
    run->freeBlocks[chunk] = static_cast<uint8_t>(freeBlocks);
}

// The loop of every thread that takes part in a parallel selection: the pause thread and the packets that it submitted. A thread
// takes the next chunk from the cursor until none is left, so a thread that wakes late finds nothing to do and the pause thread
// alone can finish the whole selection.
void LxrReferenceCounting::RunSelectionLoop(SelectionRun* run)
{
#ifdef LXR_HAS_PHASE_TIMERS
    int64_t start = m_pause_timers.Enabled() ? m_pause_timers.Stamp() : 0;
#endif // LXR_HAS_PHASE_TIMERS
    size_t taken = 0;
    while (run->failure.load(std::memory_order_relaxed) == LxrRcResult::Success &&
        GetFailure() == LxrRcResult::Success)
    {
        size_t chunk = run->cursor.fetch_add(1, std::memory_order_relaxed);
        if (chunk >= run->chunks)
        {
            break;
        }
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_SELECT_LOSE_CHUNK_CONTROL)
        // Control: a chunk is lost (it is not visited, so it is not counted as visited).
        if (chunk % 7 == 3)
        {
            run->processed.fetch_add(1, std::memory_order_relaxed);
            continue;
        }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_SELECT_LOSE_CHUNK_CONTROL
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_SELECT_SKIP_CHUNK_CONTROL)
        // Control: a chunk is counted as visited but not visited.
        if (chunk % 7 == 3)
        {
            run->counts[chunk] = 0;
            run->freeBlocks[chunk] = 0;
            run->visited.fetch_add(1, std::memory_order_relaxed);
            run->processed.fetch_add(1, std::memory_order_relaxed);
            continue;
        }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_SELECT_SKIP_CHUNK_CONTROL
        SelectChunk(run, chunk);
        run->visited.fetch_add(1, std::memory_order_relaxed);
        run->processed.fetch_add(1, std::memory_order_relaxed);
        taken++;
    }
#ifdef LXR_HAS_PHASE_TIMERS
    if (taken != 0)
    {
        m_select_metrics[3].fetch_add(1, std::memory_order_relaxed);
        if (start != 0)
        {
            m_pause_timers.Add(LxrPauseTimers::SelectBusy, m_pause_timers.Stamp() - start);
        }
    }
#else
    (void)taken;
#endif // LXR_HAS_PHASE_TIMERS
}

// The parallel selection (Java LXR: SelectDefragBlocks packets over the chunks of the heap and a serial tail,
// plan/lxr/gc_work/mature_evac.rs). The pause thread submits packets to the Unconstrained stage, takes chunks itself, and
// waits until every chunk is done; only then does it merge the chunks and run the shared tail. So the evacuation set is
// complete before PrepareWork, as with the serial selection, and the scheduler needs no new stage: no lazy work is queued at
// this point (the pre-drain of the pause has ended it), and a lazy packet is published only after the mutators resume. The
// packets that have not started when the chunks are done only read the cursor; Suspend waits for them at its end
// (FinishSelection), because the cycle of the pause cannot start while the stage is busy.
LxrRcResult LxrReferenceCounting::SelectMatureParallel(bool finish)
{
#ifdef LXR_HAS_PHASE_TIMERS
    m_select_metrics[1].fetch_add(1, std::memory_order_relaxed);
#endif // LXR_HAS_PHASE_TIMERS
    size_t blocks = GetImmixCapacityBytes() / ImmixBlockGeometry::BlockBytes;
    SelectionRun* run = new (std::nothrow) SelectionRun;
    if (run == nullptr)
    {
        return Fail(LxrRcResult::OutOfMemory);
    }
    run->start = GetReservationStart();
    run->limit = run->start + blocks * ImmixBlockGeometry::BlockBytes;
    run->firstChunk = run->start & ~(static_cast<uintptr_t>(SelectionRun::ChunkBytes) - 1);
    run->chunks = (run->limit - run->firstChunk + SelectionRun::ChunkBytes - 1) / SelectionRun::ChunkBytes;
    run->full = m_pause_kind == LxrPauseKind::Full;
    size_t percent = m_configuration.chunkDefragPercent;
    if (run->full)
    {
        percent = percent > 50 ? 100 : percent * 2;
    }
    run->threshold = SelectionRun::BlocksPerChunk * percent / 100;
    run->threshold = run->threshold == 0 ? 1 : run->threshold;
    run->candidates = new (std::nothrow) MatureCandidate[run->chunks * SelectionRun::BlocksPerChunk];
    run->counts = new (std::nothrow) uint8_t[run->chunks];
    run->freeBlocks = new (std::nothrow) uint8_t[run->chunks];
    if (run->candidates == nullptr || run->counts == nullptr || run->freeBlocks == nullptr)
    {
        delete[] run->candidates;
        delete[] run->counts;
        delete[] run->freeBlocks;
        delete run;
        return Fail(LxrRcResult::OutOfMemory);
    }
    LxrRcResult result = LxrRcResult::Success;
    {
#ifdef LXR_HAS_PHASE_TIMERS
        LxrPhaseScope scanTimer(m_pause_timers, LxrPauseTimers::SelectScan);
#endif // LXR_HAS_PHASE_TIMERS
        m_select_run.store(run, std::memory_order_release);
        size_t helpers = m_configuration.workers < run->chunks - 1 ? m_configuration.workers : run->chunks - 1;
        size_t submitted = 0;
        for (; submitted < helpers && result == LxrRcResult::Success; submitted++)
        {
            result = SubmitWork(NewWork(WorkKind::SelectMature, nullptr, 0, LxrWorkBucketStage::Unconstrained));
        }
        RunSelectionLoop(run);
        // The threads that took a chunk are done with it when the counter reaches the number of chunks. A packet that has not
        // started yet finds the cursor at the end and returns at once, so the pause does not wait for the wake-up of the workers:
        // FinishSelection waits for the stage at the end of Suspend, after the pause has done other work.
        for (unsigned spins = 0; run->processed.load(std::memory_order_acquire) < run->chunks &&
            run->failure.load(std::memory_order_relaxed) == LxrRcResult::Success && GetFailure() == LxrRcResult::Success;
            spins++)
        {
            if (spins < 64)
            {
                YieldProcessor();
            }
            else
            {
                GCToOSInterface::YieldThread(0);
            }
        }
    }
    size_t visited = run->visited.load(std::memory_order_acquire);
    if (result == LxrRcResult::Success)
    {
        result = run->failure.load(std::memory_order_relaxed);
    }
    if (result == LxrRcResult::Success && GetFailure() == LxrRcResult::Success && visited != run->chunks)
    {
        // Every chunk is visited exactly once; a lost chunk would change the evacuation set.
#ifdef LXR_HAS_PHASE_TIMERS
        m_select_metrics[5].fetch_add(run->chunks - visited, std::memory_order_relaxed);
#endif // LXR_HAS_PHASE_TIMERS
        result = Fail(LxrRcResult::InvariantViolation);
    }
    if (result == LxrRcResult::Success)
    {
        result = GetFailure();
    }
    if (result == LxrRcResult::Success)
    {
#ifdef LXR_HAS_PHASE_TIMERS
        m_select_metrics[2].fetch_add(run->chunks, std::memory_order_relaxed);
#endif // LXR_HAS_PHASE_TIMERS
        // The chunks are in address order, as the candidates of the serial loop are.
        size_t total = 0;
        size_t freeBlocks = 0;
        {
#ifdef LXR_HAS_PHASE_TIMERS
            LxrPhaseScope gatherTimer(m_pause_timers, LxrPauseTimers::SelectGather);
#endif // LXR_HAS_PHASE_TIMERS
            for (size_t index = 0; index < run->chunks; index++)
            {
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_SELECT_GATHER_REVERSED_CONTROL)
                // Control: the chunks are merged in descending address order.
                size_t chunk = run->chunks - 1 - index;
#else
                size_t chunk = index;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_SELECT_GATHER_REVERSED_CONTROL
                size_t chunkCount = run->counts[chunk];
                MatureCandidate* source = run->candidates + chunk * SelectionRun::BlocksPerChunk;
                if (source != run->candidates + total && chunkCount != 0)
                {
                    memmove(run->candidates + total, source, chunkCount * sizeof(MatureCandidate));
                }
                total += chunkCount;
                freeBlocks += run->freeBlocks[chunk];
            }
        }
#ifdef LXR_HAS_PHASE_TIMERS
        LxrPhaseScope tailTimer(m_pause_timers, LxrPauseTimers::SelectTail);
#endif // LXR_HAS_PHASE_TIMERS
        result = ApplyMatureSelection(run->candidates, total, freeBlocks * ImmixBlockGeometry::BlockBytes);
    }
    // A packet that is late may still read the cursor of the run, so the run is freed by FinishSelection, after the wait for
    // the stage. The caller of a test calls it at once; Suspend calls it at the end of the pause preparation.
    return finish ? FinishSelection(result) : result;
}

// Waits until no packet of the selection is queued or running, then frees the run. It runs at the end of Suspend: the stage must
// be idle before the pause starts its cycle (BeginStwCycle), and the late workers have woken by then. A packet that did not
// finish in time may still use the run, so the run stays allocated; the engine has failed then.
LxrRcResult LxrReferenceCounting::FinishSelection(LxrRcResult result)
{
    SelectionRun* run = m_select_run.load(std::memory_order_acquire);
    if (run == nullptr)
    {
        return result;
    }
    LxrRcResult waited = FromScheduler(m_scheduler.WaitForBucketIdle(
        LxrWorkBucketStage::Unconstrained, SelectionRun::SelectTimeoutMilliseconds));
    if (waited != LxrRcResult::Success)
    {
        return result == LxrRcResult::Success ? waited : result;
    }
    m_select_run.store(nullptr, std::memory_order_release);
    delete[] run->candidates;
    delete[] run->counts;
    delete[] run->freeBlocks;
    delete run;
    return result;
}

// Runs the serial and the parallel selection on the same state of the heap and compares the evacuation set, its side
// metadata and the counters of the selection (LXRSelectValidate, and the P4 tests). It leaves the state of the parallel
// selection, which must equal the serial one.
LxrRcResult LxrReferenceCounting::SelectMatureValidated()
{
    size_t blocks = GetImmixCapacityBytes() / ImmixBlockGeometry::BlockBytes;
    uint8_t* evacuation = new (std::nothrow) uint8_t[4 * blocks];
    if (evacuation == nullptr)
    {
        return Fail(LxrRcResult::OutOfMemory);
    }
    uint8_t* before = evacuation;
    uint8_t* beforeDefrag = evacuation + blocks;
    uint8_t* serial = evacuation + 2 * blocks;
    uint8_t* serialDefrag = evacuation + 3 * blocks;
    auto snapshot = [&](uint8_t* flags, uint8_t* defrag)
    {
        for (size_t index = 0; index < blocks; index++)
        {
            flags[index] = m_evacuation_sources[index].load(std::memory_order_relaxed);
            defrag[index] = static_cast<uint8_t>(m_defrag_metadata.Load(
                GetReservationStart() + index * ImmixBlockGeometry::BlockBytes, SideMetadataMemoryOrder::SequentiallyConsistent));
        }
    };
    snapshot(before, beforeDefrag);
    uint64_t counters[4];
    for (size_t index = 0; index < 4; index++)
    {
        counters[index] = m_mature_metrics[index].load(std::memory_order_relaxed);
    }
    LxrRcResult result = SelectMatureSerial();
    uint64_t serialCounters[4];
    for (size_t index = 0; index < 4; index++)
    {
        serialCounters[index] = m_mature_metrics[index].load(std::memory_order_relaxed) - counters[index];
    }
    if (result == LxrRcResult::Success)
    {
        snapshot(serial, serialDefrag);
        for (size_t index = 0; index < blocks && result == LxrRcResult::Success; index++)
        {
            m_evacuation_sources[index].store(before[index], std::memory_order_relaxed);
            result = FromMetadata(m_metadata.Store(LxrSideMetadataKind::BlockDefrag,
                GetReservationStart() + index * ImmixBlockGeometry::BlockBytes, beforeDefrag[index],
                SideMetadataMemoryOrder::SequentiallyConsistent));
        }
        for (size_t index = 0; index < 4; index++)
        {
            m_mature_metrics[index].store(counters[index], std::memory_order_relaxed);
        }
    }
    if (result == LxrRcResult::Success)
    {
        result = SelectMatureParallel();
    }
    if (result == LxrRcResult::Success)
    {
        // The parallel state goes to `before`, which is free now.
        snapshot(before, beforeDefrag);
        uint64_t differences = 0;
        for (size_t index = 0; index < blocks; index++)
        {
            differences += before[index] != serial[index] ? 1 : 0;
            differences += beforeDefrag[index] != serialDefrag[index] ? 1 : 0;
        }
        for (size_t index = 0; index < 4; index++)
        {
            differences += m_mature_metrics[index].load(std::memory_order_relaxed) - counters[index] !=
                serialCounters[index] ? 1 : 0;
        }
        if (differences != 0)
        {
#ifdef LXR_HAS_PHASE_TIMERS
            m_select_metrics[4].fetch_add(differences, std::memory_order_relaxed);
#endif // LXR_HAS_PHASE_TIMERS
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
            fprintf(stderr, "LXR selection validation: %llu differences\n", static_cast<unsigned long long>(differences));
            if (!m_select_report_only)
            {
                result = Fail(LxrRcResult::InvariantViolation);
            }
#else
            GCToEEInterface::LogErrorToHost("LXR selection validation: the parallel evacuation set differs from the serial one.");
            result = Fail(LxrRcResult::InvariantViolation);
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        }
    }
    delete[] evacuation;
    return result;
}

#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
LxrRcResult LxrReferenceCounting::SelectionEquivalenceForTest(
    uint64_t seed, LxrPauseKind kind, uint32_t occupiedPercent, uint32_t partialChunkPercent, uint32_t pinnedPercent,
    uint32_t sourcePercent, uint32_t deadLinePercent, size_t headroomBytes, uint64_t* mismatches, uint64_t* selected,
    uint64_t* eligible)
{
    if (mismatches == nullptr || selected == nullptr || eligible == nullptr || m_evacuation_sources == nullptr)
    {
        return LxrRcResult::InvalidArgument;
    }
    uint64_t state = seed * 0x9E3779B97F4A7C15ull + 1;
    auto next = [&state]()
    {
        state += 0x9E3779B97F4A7C15ull;
        uint64_t value = state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9ull;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBull;
        return value ^ (value >> 31);
    };
    size_t blocks = GetImmixCapacityBytes() / ImmixBlockGeometry::BlockBytes;
    constexpr size_t BlocksPerChunk = SelectionRun::BlocksPerChunk;
    uint8_t epoch = m_blocks.GetGlobalPhaseEpoch();
    uint8_t nurseryEpoch = (epoch & 1) != 0 ? epoch : static_cast<uint8_t>(epoch - 1);
    constexpr size_t SlotsPerLine = ImmixBlockGeometry::LineBytes / sizeof(uintptr_t);
    size_t chunkBlocks = 0;
    uint32_t chunkOccupied = occupiedPercent;
    for (size_t index = 0; index < blocks; index++)
    {
        uintptr_t block = GetReservationStart() + index * ImmixBlockGeometry::BlockBytes;
        if (chunkBlocks == 0 || (block & (SelectionRun::ChunkBytes - 1)) == 0)
        {
            chunkBlocks = BlocksPerChunk;
            chunkOccupied = next() % 100 < partialChunkPercent ? occupiedPercent / 4 : occupiedPercent;
        }
        bool occupied = next() % 100 < chunkOccupied;
        uint8_t raw = 0;
        if (occupied)
        {
            uint64_t pick = next() % 3;
            raw = pick == 0 ? UINT8_MAX : (pick == 1 ? UINT8_MAX - 1 : static_cast<uint8_t>(1 + next() % 200));
        }
        LxrRcResult result = FromMetadata(m_metadata.Store(
            LxrSideMetadataKind::BlockMark, block, raw, SideMetadataMemoryOrder::SequentiallyConsistent));
        if (result == LxrRcResult::Success)
        {
            // A free block is a nursery block half of the time: it does not count as free space.
            uint8_t blockEpoch = !occupied && (next() & 1) != 0 ? nurseryEpoch : 0;
            result = FromMetadata(m_metadata.Store(
                LxrSideMetadataKind::PhaseEpoch, block, blockEpoch, SideMetadataMemoryOrder::SequentiallyConsistent));
        }
        if (result != LxrRcResult::Success)
        {
            return result;
        }
        if (m_block_flags != nullptr)
        {
            uint8_t flags = next() % 100 < pinnedPercent ? ImmixAllocationManager::PinnedBlockFlag : 0;
            m_block_flags[index].store(flags, std::memory_order_relaxed);
        }
        bool source = next() % 100 < sourcePercent;
        m_evacuation_sources[index].store(source ? 1 : 0, std::memory_order_relaxed);
        result = FromMetadata(m_metadata.Store(LxrSideMetadataKind::BlockDefrag, block,
            source ? UINT8_MAX : 0, SideMetadataMemoryOrder::SequentiallyConsistent));
        if (result != LxrRcResult::Success)
        {
            return result;
        }
        // The dead lines: each block has its own share of live lines.
        uint32_t liveShare = static_cast<uint32_t>(next() % 101);
        for (size_t line = 0; line < ImmixBlockGeometry::LinesPerBlock; line++)
        {
            uintptr_t lineStart = block + line * ImmixBlockGeometry::LineBytes;
            for (size_t slot = 0; slot < SlotsPerLine; slot++)
            {
                m_rc_metadata.Store(lineStart + slot * sizeof(uintptr_t), 0, SideMetadataMemoryOrder::Relaxed);
            }
            if (occupied && next() % 100 < liveShare * (100 - deadLinePercent) / 100)
            {
                m_rc_metadata.Store(lineStart + (next() % SlotsPerLine) * sizeof(uintptr_t), 1,
                    SideMetadataMemoryOrder::Relaxed);
            }
        }
        chunkBlocks--;
    }
    m_pause_kind = kind;
    m_select_headroom_override = headroomBytes;
    m_select_report_only = true;
    uint64_t differencesBefore = m_select_metrics[4].load(std::memory_order_relaxed);
    uint64_t eligibleBefore = m_mature_metrics[0].load(std::memory_order_relaxed);
    uint64_t selectedBefore = m_mature_metrics[1].load(std::memory_order_relaxed);
    LxrRcResult result = SelectMatureValidated();
    m_select_headroom_override = SIZE_MAX;
    m_select_report_only = false;
    *mismatches = m_select_metrics[4].load(std::memory_order_relaxed) - differencesBefore;
    *eligible = m_mature_metrics[0].load(std::memory_order_relaxed) - eligibleBefore;
    *selected = m_mature_metrics[1].load(std::memory_order_relaxed) - selectedBefore;
    return result;
}
#endif // LXR_SIDE_METADATA_TEST_PLATFORM

LxrRcResult LxrReferenceCounting::RetainPinnedMatureBlocks()
{
#if defined(LXR_S7_NO_RETAIN_CONTROL)
    // Control: a pinned mature block stays a candidate.
    return LxrRcResult::Success;
#endif // LXR_S7_NO_RETAIN_CONTROL
    if (!m_configuration.evacuateMature)
    {
        return LxrRcResult::Success;
    }
    for (size_t index = 0; index < m_pins.Size(); index++)
    {
        Object* object = reinterpret_cast<Object*>(m_pins[index]);
        uintptr_t start = Start(object);
        if (!IsMatureCandidate(start))
        {
            continue;
        }
        size_t block = (start - GetReservationStart()) / ImmixBlockGeometry::BlockBytes;
        if ((m_evacuation_sources[block].load(std::memory_order_acquire) & RetainedSource) != 0)
        {
            continue;
        }
        // A pinned young object has no count before the increments of the pause. It is promoted in place and never
        // moves, so only a counted object keeps its block here; the call that follows the increments sees the rest.
        if (m_rc_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) == 0 ||
            ResolveForwarded(object) != object)
        {
            continue;
        }
        if ((m_evacuation_sources[block].fetch_or(RetainedSource, std::memory_order_acq_rel) &
                RetainedSource) == 0)
        {
            m_mature_metrics[2].fetch_add(1, std::memory_order_relaxed);
        }
    }
    // The old walk also kept the block of an object whose header carries BIT_SBLK_GC_RESERVE. In this collector no
    // code sets that bit (only Satori does), so no list is kept for it. The nursery copy decision still reads it.
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::RememberMatureSlot(Object** slot, Object* target)
{
    if (!m_configuration.evacuateMature ||
        !(m_marking.load(std::memory_order_relaxed) || m_pause_kind == LxrPauseKind::FinalMark) ||
        !IsMatureCandidate(Start(target)))
    {
        return LxrRcResult::Success;
    }
    uintptr_t address = reinterpret_cast<uintptr_t>(slot);
    if (!OwnsAddress(address))
    {
        return Fail(LxrRcResult::UnsupportedLifetime);
    }
    uintptr_t reuse = (IsImmix(address) ? m_line_reuse_metadata : m_page_reuse_metadata).Load(
        address, SideMetadataMemoryOrder::SequentiallyConsistent);
    for (;;)
    {
        RememberedChunk* head = m_remembered_chunks.load(std::memory_order_acquire);
        if (head != nullptr)
        {
            size_t index = head->reserved.load(std::memory_order_relaxed);
            while (index < BufferCapacity)
            {
                if (head->reserved.compare_exchange_weak(index, index + 1, std::memory_order_relaxed))
                {
                    head->entries[index] = {slot, static_cast<uint8_t>(reuse)};
                    head->published[index].store(1, std::memory_order_release);
                    m_mature_metrics[7].fetch_add(1, std::memory_order_relaxed);
                    return LxrRcResult::Success;
                }
            }
        }
        RememberedChunk* chunk = new (std::nothrow) RememberedChunk;
        if (chunk == nullptr)
        {
            return Fail(LxrRcResult::OutOfMemory);
        }
        chunk->next = head;
        if (!m_remembered_chunks.compare_exchange_strong(head, chunk, std::memory_order_acq_rel))
        {
            delete chunk;
        }
    }
}

void LxrReferenceCounting::DeleteRememberedChunks()
{
    RememberedChunk* chunk = m_remembered_chunks.exchange(nullptr, std::memory_order_acq_rel);
    while (chunk != nullptr)
    {
        RememberedChunk* next = chunk->next;
        delete chunk;
        chunk = next;
    }
    chunk = m_replay_chunks;
    m_replay_chunks = nullptr;
    while (chunk != nullptr)
    {
        RememberedChunk* next = chunk->next;
        delete chunk;
        chunk = next;
    }
}

LxrRcResult LxrReferenceCounting::ReplayMatureRememberedSlots()
{
    CostScope cost(this, 6);
    // The replay works through the chunks that exist now. The trace of the objects that it copies adds entries to a new
    // list, and these are never replayed (they are deleted at the end, as before).
    RememberedChunk* chunks = m_remembered_chunks.exchange(nullptr, std::memory_order_acq_rel);
    m_replay_chunks = chunks;
    LxrRcResult status = LxrRcResult::Success;
    for (RememberedChunk* chunk = chunks; chunk != nullptr && status == LxrRcResult::Success; chunk = chunk->next)
    {
        if (m_pause_trace_open.load(std::memory_order_acquire))
        {
            // One packet for each chunk: every worker takes chunks, and each worker copies with its own allocator.
            Work* work = NewWork(WorkKind::ReplayRemembered, nullptr, 0, LxrWorkBucketStage::ConcurrentResumable, true);
            if (work == nullptr)
            {
                return GetFailure();
            }
            work->chunk = chunk;
            status = DispatchTraceWork(work);
        }
        else
        {
            status = ReplayRememberedChunk(chunk, false);
        }
    }
    if (status != LxrRcResult::Success)
    {
        return status;
    }
    LxrRcResult drained = DrainTraceInsidePause();
    if (drained == LxrRcResult::Success)
    {
        m_replay_chunks = nullptr;
        while (chunks != nullptr)
        {
            RememberedChunk* next = chunks->next;
            delete chunks;
            chunks = next;
        }
        DeleteRememberedChunks();
    }
    return drained;
}

LxrRcResult LxrReferenceCounting::ProcessReplayWork(Work* work)
{
    bool expected = false;
    if (!work->traceProcessed.compare_exchange_strong(expected, true, std::memory_order_acq_rel))
    {
        return LxrRcResult::Success;
    }
    m_pause_trace_metrics[1].fetch_add(1, std::memory_order_relaxed);
    LxrRcResult result = ReplayRememberedChunk(work->chunk, true);
    // The packets that the replay made are counted before this packet is.
    if (m_trace_pending.fetch_sub(1, std::memory_order_acq_rel) == 0)
    {
        return Fail(LxrRcResult::InvariantViolation);
    }
    return result == LxrRcResult::Success ? result : Fail(result);
}

// Replays the entries of one chunk. batched: the targets of the copies are queued as one packet for each 1 024 entries,
// not one packet for each entry.
LxrRcResult LxrReferenceCounting::ReplayRememberedChunk(RememberedChunk* chunk, bool batched)
{
    uintptr_t targets[BufferCapacity];
    size_t targetCount = 0;
    {
        for (size_t index = 0; index < chunk->reserved.load(std::memory_order_acquire); index++)
        {
            if (chunk->published[index].load(std::memory_order_acquire) != 1)
            {
                return Fail(LxrRcResult::InvariantViolation);
            }
            RememberedChunk::Entry entry = chunk->entries[index];
            uintptr_t address = reinterpret_cast<uintptr_t>(entry.slot);
            if (!OwnsAddress(address))
            {
                // The slot was in a block that the give-back returned after the slot was remembered. The planes of the
                // block are gone, so the reuse metadata cannot be read, and the block held no object.
                m_mature_metrics[8].fetch_add(1, std::memory_order_relaxed);
                continue;
            }
            uintptr_t reuse = (IsImmix(address) ? m_line_reuse_metadata : m_page_reuse_metadata).Load(
                address, SideMetadataMemoryOrder::SequentiallyConsistent);
            LxrRcResult result = LxrRcResult::Success;
            if (reuse != entry.reuse)
            {
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_REMSET_STALE_CONTROL)
                m_mature_metrics[8].fetch_add(1, std::memory_order_relaxed);
                continue;
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_REMSET_STALE_CONTROL
            }
            if (IsImmix(address))
            {
                ImmixBlockState state;
                uint8_t unavailable;
                result = FromMetadata(m_blocks.GetStateFast(address & ~ImmixBlockGeometry::BlockMask,
                    &state, &unavailable));
                if (result != LxrRcResult::Success)
                {
                    return result;
                }
                if (state == ImmixBlockState::Unallocated)
                {
                    continue;
                }
            }
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_REMSET_SHAPE_CONTROL)
            Object* owner = nullptr;
            result = FindContainingObject(address, &owner, true);
            if (result == LxrRcResult::InvalidArgument)
            {
                m_mature_metrics[12].fetch_add(1, std::memory_order_relaxed);
                continue;
            }
            if (result != LxrRcResult::Success)
            {
                return Fail(result);
            }
            if (ResolveForwarded(owner) != owner)
            {
                continue;
            }
            uintptr_t ownerCount = 0;
            uintptr_t ownerMark = 0;
            result = FromMetadata(m_counts.LoadFast(Start(owner), &ownerCount));
            if (result == LxrRcResult::Success)
            {
                ownerMark = m_mark_metadata.Load(Start(owner), SideMetadataMemoryOrder::SequentiallyConsistent);
            }
            if (result != LxrRcResult::Success)
            {
                return result;
            }
            if (ownerCount == 0 || ownerMark == 0)
            {
                m_mature_metrics[12].fetch_add(1, std::memory_order_relaxed);
                continue;
            }
            bool referenceField = false;
            // Byte tags may wrap. Inspect only a current metadata-live owner's shape, never stale payload.
            GCReferenceRanges::Enumerate(owner, [&](Object** first, size_t count)
            {
                uintptr_t start = reinterpret_cast<uintptr_t>(first);
                referenceField = address >= start && (address - start) / sizeof(Object*) < count &&
                    (address - start) % sizeof(Object*) == 0;
                return !referenceField;
            });
            if (!referenceField)
            {
                m_mature_metrics[12].fetch_add(1, std::memory_order_relaxed);
                continue;
            }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_REMSET_SHAPE_CONTROL
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
            if (m_watched_remembered_slot.load(std::memory_order_acquire) == address)
            {
                m_remembered_payload_reads.fetch_add(1, std::memory_order_relaxed);
            }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
            Object* target = *entry.slot;
            if (target == nullptr || !IsMatureCandidate(Start(target)))
            {
                continue;
            }
            uintptr_t count;
            result = LoadCount(target, &count);
            if (result != LxrRcResult::Success)
            {
                return Fail(result);
            }
            if (count == 0)
            {
                continue;
            }
            Object* original = target;
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
            if (m_replay_store_hook != nullptr)
            {
                m_replay_store_hook(m_replay_store_context, entry.slot, 0);
            }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
            result = TryCopyMature(&target);
            if (result == LxrRcResult::Success)
            {
                // Another worker can copy the owner now (the owner is only a source if it is not forwarded yet), and it
                // stores the forwarding pointer in the first field of the owner. A plain store of the target could replace
                // that pointer. The exchange only stores while the field still holds the old target; the trace of the copy
                // of the owner makes the slot of the copy right.
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_PAUSE_TRACE_REPLAY_STORE_CONTROL)
                *entry.slot = target;
                (void)original;
#else
                if (target != original)
                {
                    Interlocked::CompareExchangePointer(entry.slot, target, original);
                }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_PAUSE_TRACE_REPLAY_STORE_CONTROL
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
                if (m_replay_store_hook != nullptr)
                {
                    m_replay_store_hook(m_replay_store_context, entry.slot, 1);
                }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
                uintptr_t value = reinterpret_cast<uintptr_t>(target);
                if (batched)
                {
                    targets[targetCount++] = value;
                    if (targetCount == BufferCapacity)
                    {
                        result = QueueTrace(targets, targetCount);
                        targetCount = 0;
                    }
                    m_pause_trace_metrics[2].fetch_add(1, std::memory_order_relaxed);
                }
                else
                {
                    result = QueueTrace(&value, 1);
                }
                m_mature_metrics[9].fetch_add(1, std::memory_order_relaxed);
            }
            if (result != LxrRcResult::Success)
            {
                return result;
            }
        }
    }
    return targetCount == 0 ? LxrRcResult::Success : QueueTrace(targets, targetCount);
}

LxrRcResult LxrReferenceCounting::AllocateCopy(size_t size, size_t worker, uintptr_t* copyStart)
{
    _ASSERTE(worker <= m_configuration.workers);
    *copyStart = 0;
    ImmixAllocationRequest request{size, sizeof(uintptr_t), 0};
    ImmixAllocationStatus status;
    ImmixBumpAllocator* allocator = &m_copy_allocators[2 * worker];
    SideMetadataResult allocated = allocator->TryAllocate(request, copyStart, &status);
    if (allocated == SideMetadataResult::Success && status != ImmixAllocationStatus::Allocated &&
        size > ImmixBlockGeometry::LineBytes)
    {
        allocator = &m_copy_allocators[2 * worker + 1];
        allocated = allocator->TryAllocate(request, copyStart, &status);
    }
    bool recycled = size <= ImmixBlockGeometry::LineBytes && m_configuration.copyLineRecycling;
    size_t attempts = GetImmixCapacityBytes() / ImmixBlockGeometry::LineBytes;
    while (allocated == SideMetadataResult::Success && status != ImmixAllocationStatus::Allocated &&
        attempts-- != 0)
    {
        allocated = allocator->TryRefill(
            recycled ? ImmixAllocationSource::GcCopyRecycled : ImmixAllocationSource::GcCopyFresh,
            GetTracePhase() == LxrTracePhase::Concurrent || GetTracePhase() == LxrTracePhase::FinalMark
                ? ImmixMarkingContext::ConcurrentOrFinalMark : ImmixMarkingContext::Inactive, &status);
        if (allocated == SideMetadataResult::Success && status == ImmixAllocationStatus::Exhausted && recycled)
        {
            recycled = false;
            continue;
        }
        if (allocated != SideMetadataResult::Success || status != ImmixAllocationStatus::Ready)
        {
            break;
        }
        allocated = allocator->TryAllocate(request, copyStart, &status);
    }
    if (allocated != SideMetadataResult::Success)
    {
        return FromMetadata(allocated);
    }
    return status == ImmixAllocationStatus::Allocated
        ? LxrRcResult::Success : LxrRcResult::AllocationFailure;
}

LxrRcResult LxrReferenceCounting::TryCopyMature(Object** reference)
{
    Object* source = *reference;
    *reference = ResolveForwardedChecked(source);
    if (*reference != source || source == nullptr || !IsMatureCandidate(Start(source)) ||
        (GetTracePhase() != LxrTracePhase::Full && GetTracePhase() != LxrTracePhase::FinalMark))
    {
        return LxrRcResult::Success;
    }
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_MATURE_DEAD_SCAN_CONTROL)
    if (!IsObjectStart(source))
    {
        return LxrRcResult::Success;
    }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_MATURE_DEAD_SCAN_CONTROL
    size_t block = (Start(source) - GetReservationStart()) / ImmixBlockGeometry::BlockBytes;
    ObjHeader* header = source->GetHeader();
    auto staysInPlace = [&]()
    {
        return (m_evacuation_sources[block].load(std::memory_order_acquire) & RetainedSource) != 0 ||
            m_mature_copy_exhausted.load(std::memory_order_relaxed);
    };
    if (staysInPlace())
    {
        // Another worker may have claimed the source before the block was retained or the to-space ran out (the claim
        // precedes its own second look at the flags, below). The source then moves, so this thread must use the copy.
        std::atomic_thread_fence(std::memory_order_seq_cst);
        if (header->GetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift) != 0)
        {
            *reference = ResolveForwarded(source);
        }
        return LxrRcResult::Success;
    }
    uintptr_t count = m_rc_metadata.Load(Start(source), SideMetadataMemoryOrder::Relaxed);
    LxrRcResult result = LxrRcResult::Success;
    if (count == 0)
    {
        return result;
    }
    size_t index = (Start(source) - GetReservationStart()) / sizeof(uintptr_t);
    uint32_t observed = header->CompareExchangeGCReservedBits(
        m_configuration.forwardingMask, m_configuration.forwardingShift, 2, 0);
    if (observed != 0)
    {
        *reference = ResolveForwarded(source);
        return LxrRcResult::Success;
    }
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_PAUSE_TRACE_CLAIM_CONTROL)
    // The claim is visible now. A worker that saw the block retained or the to-space exhausted and found no claim has
    // already left the source in place, so this thread must not move it.
    if (staysInPlace())
    {
        header->SetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift, 0);
        return LxrRcResult::Success;
    }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_PAUSE_TRACE_CLAIM_CONTROL
    // The source is claimed and its first field is intact, so the size comes from the object.
    size_t size = ObjectSize(source);
    const size_t worker = GetExecutionWorker();
    uintptr_t copyStart;
    {
        CostScope cost(this, 3);
        result = AllocateCopy(size, worker, &copyStart);
    }
    if (result == LxrRcResult::AllocationFailure)
    {
        m_mature_copy_exhausted.store(true, std::memory_order_relaxed);
        m_evacuation_sources[block].fetch_or(RetainedSource, std::memory_order_acq_rel);
        m_mature_metrics[6].fetch_add(1, std::memory_order_relaxed);
        header->SetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift, 0);
        return LxrRcResult::Success;
    }
    if (result == LxrRcResult::Success)
    {
        result = ReserveCopyRecord(worker);
    }
    if (result == LxrRcResult::Success)
    {
        CostScope cost(this, 4);
        result = FromMetadata(m_counts.CopyObjectQuiescent(Start(source), copyStart, size));
    }
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_MATURE_COUNT_CONTROL)
    if (result == LxrRcResult::Success)
    {
        result = FromMetadata(m_metadata.Store(LxrSideMetadataKind::ReferenceCount,
            copyStart, 1, SideMetadataMemoryOrder::SequentiallyConsistent));
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_MATURE_COUNT_CONTROL
    if (result != LxrRcResult::Success)
    {
        header->SetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift, 0);
        return result;
    }
    Object* destination = Reference(copyStart);
    {
        CostScope cost(this, 5);
        memcpy(reinterpret_cast<void*>(copyStart), reinterpret_cast<void*>(Start(source)), size);
    }
    destination->GetHeader()->SetGCReservedBitsExclusive(m_configuration.forwardingMask, m_configuration.forwardingShift, 0);
    size_t destinationIndex = (copyStart - GetReservationStart()) / sizeof(uintptr_t);
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    OracleBirth(copyStart, size);
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    TraceScan* scan = FindTraceScan(index);
    if (scan != nullptr)
    {
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_MATURE_SCAN_REBASE_CONTROL)
        scan->iterator.Relocate(destination);
        if (scan->range.start != nullptr)
        {
            uintptr_t offset = reinterpret_cast<uintptr_t>(scan->range.start) -
                reinterpret_cast<uintptr_t>(source);
            scan->range.start = reinterpret_cast<Object**>(reinterpret_cast<uintptr_t>(destination) + offset);
        }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_MATURE_SCAN_REBASE_CONTROL
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        m_mature_scan_receipt[0].store(reinterpret_cast<uintptr_t>(source), std::memory_order_release);
        m_mature_scan_receipt[1].store(reinterpret_cast<uintptr_t>(destination), std::memory_order_release);
        m_mature_scan_receipt[2].store(reinterpret_cast<uintptr_t>(scan->range.start), std::memory_order_release);
        m_mature_scan_receipt[3].store(scan->users.load(std::memory_order_acquire), std::memory_order_release);
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        scan->object = destination;
#if !defined(LXR_S7_SCAN_MOVE_CONTROL)
        MoveTraceScan(index, destinationIndex, scan);
#else
        (void)destinationIndex;
#endif // !LXR_S7_SCAN_MOVE_CONTROL
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        // Receipts 4 and 5: the index holds the scan under the new address and no longer under the old one.
        m_mature_scan_receipt[4].store(FindTraceScan(destinationIndex) == scan ? 1 : 0, std::memory_order_release);
        m_mature_scan_receipt[5].store(FindTraceScan(index) == scan ? 1 : 0, std::memory_order_release);
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    }
    m_mark_metadata.Store(copyStart, 0, SideMetadataMemoryOrder::SequentiallyConsistent);
#ifdef LXR_HAS_LIFETIME_PROBE
    uintptr_t watched = Start(source);
    // Arming precedes the pause; only this source's copying worker can move its watch.
    if (m_probe_start.load(std::memory_order_acquire) == watched)
    {
        m_probe_start.compare_exchange_strong(watched, copyStart, std::memory_order_acq_rel);
    }
#endif // LXR_HAS_LIFETIME_PROBE
    CommitCopyRecord(worker, Start(source), size);
    Object** forwarding = reinterpret_cast<Object**>(reinterpret_cast<uint8_t*>(source) + sizeof(uintptr_t));
    // The pointer needs no atomic exchange of its own: the claim holder is the only writer of the slot, and the
    // compare-exchange that sets the state to 3 is a full barrier after it. A reader sees the pointer after it has read the state 3.
    VolatileStore(forwarding, destination);
    header->SetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift, 3);
    CountWorker(worker, MatureCopiedObjects, 1);
    CountWorker(worker, MatureCopiedBytes, size);
    // Most copies find the flag set: the read keeps the cache line of the block shared between the workers that copy from it.
    if ((m_evacuation_sources[block].load(std::memory_order_relaxed) & CopiedSource) == 0)
    {
        m_evacuation_sources[block].fetch_or(CopiedSource, std::memory_order_release);
    }
    CountWorker(worker, PauseMatureCopiedBytes, size);
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_MATURE_PREDICTOR_CONTROL)
    m_pause_copied_bytes.fetch_add(size, std::memory_order_relaxed);
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_MATURE_PREDICTOR_CONTROL
    *reference = destination;
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::TryCopyNursery(
    Object* source, Object** destination, size_t worker, bool* copied,
    bool* incrementedInPlace, uintptr_t* previous)
{
    *destination = source;
    *copied = false;
    *incrementedInPlace = false;
    uintptr_t start = Start(source);
    uintptr_t count;
    LxrRcResult result = FromMetadata(m_counts.LoadFast(start, &count));
    if (result != LxrRcResult::Success || count != 0)
    {
        *destination = ResolveForwarded(source);
        return result;
    }
    ObjHeader* header = source->GetHeader();
    if (m_configuration.maximumYoungCopyBytes == SIZE_MAX)
    {
        // The Java LXR tests everything that does not need the claim before the claim (rc.rs: dont_evacuate), so an
        // object that stays in place costs no write to its header. Without a size limit, nothing in the decision reads
        // the first field that the forwarding pointer overwrites.
        bool earlyNursery = false;
        result = FromMetadata(m_blocks.IsNurseryFast(start & ~ImmixBlockGeometry::BlockMask, &earlyNursery));
        if (result != LxrRcResult::Success)
        {
            return result;
        }
        if (!earlyNursery || m_copy_exhausted.load(std::memory_order_relaxed) ||
            (header->GetBits() & BIT_SBLK_GC_RESERVE) != 0 || IsPinnedObject(source))
        {
            // The block state and the exhausted flag only change from "copy" to "stay". A worker that decided to copy
            // this object has claimed it before it read these inputs, so the claim is visible here if our reading
            // of an input is newer than its reading. No claim: every worker stays, and the caller counts in place.
            std::atomic_thread_fence(std::memory_order_acquire);
            if (header->GetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift) != 0)
            {
                *destination = ResolveForwarded(source);
            }
            return LxrRcResult::Success;
        }
    }
    // P22 (D2). The source gets its ValidObject bit before the claim, so that every reader that sees the forwarding
    // state also sees the bit (ResolveForwarded). RetireSourceChunk clears the bit.
    MarkPromotedStart(start);
    uint32_t observed = header->CompareExchangeGCReservedBits(
        m_configuration.forwardingMask, m_configuration.forwardingShift, 2, 0);
    if (observed != 0)
    {
        *destination = ResolveForwarded(source);
        return LxrRcResult::Success;
    }
    // A different worker may have retained this source block in place before the claim.
    result = FromMetadata(m_counts.LoadFast(start, &count));
    if (result != LxrRcResult::Success || count != 0)
    {
        header->SetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift, 0);
        return result;
    }
    bool nursery = false;
    result = FromMetadata(m_blocks.IsNurseryFast(start & ~ImmixBlockGeometry::BlockMask, &nursery));
    if (result != LxrRcResult::Success)
    {
        header->SetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift, 0);
        return result;
    }
    size_t size = 0;
    bool copyable = nursery && !m_copy_exhausted.load(std::memory_order_relaxed) &&
        (source->GetHeader()->GetBits() & BIT_SBLK_GC_RESERVE) == 0 &&
        !IsPinnedObject(source);
    if (copyable)
    {
        // The source is claimed and its first field is intact, so the size comes from the object.
        size = ObjectSize(source);
        copyable = size < m_configuration.maximumYoungCopyBytes;
    }
    if (!copyable)
    {
        // The ValidObject bit was set before the claim, and the count is still zero under the claim.
        SideMetadataUpdateStatus updated;
        result = FromMetadata(m_counts.IncrementFast(start, previous, &updated));
        *incrementedInPlace = result == LxrRcResult::Success;
        header->SetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift, 0);
        return result;
    }
    uintptr_t copyStart = 0;
    {
        CostScope cost(this, 0);
        result = AllocateCopy(size, worker, &copyStart);
    }
    if (result != LxrRcResult::Success && result != LxrRcResult::AllocationFailure)
    {
        header->SetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift, 0);
        return result;
    }
    if (result == LxrRcResult::AllocationFailure)
    {
        // The oracle's to-space overflow is an in-place promotion, not an abandoned claim.
        m_copy_exhausted.store(true, std::memory_order_relaxed);
        m_nursery_metrics[3].fetch_add(1, std::memory_order_relaxed);
        SideMetadataUpdateStatus updated;
        result = FromMetadata(m_counts.IncrementFast(start, previous, &updated));
        *incrementedInPlace = result == LxrRcResult::Success;
        header->SetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift, 0);
        return result;
    }
    result = ReserveCopyRecord(worker);
    if (result != LxrRcResult::Success)
    {
        header->SetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift, 0);
        return result;
    }
    Object* copy = Reference(copyStart);
    {
        CostScope cost(this, 1);
        // P28 (F10). The destination is a range that a copy refill has prepared (the ValidObject plane is clear; the counts and the
        // straddle marks are zero, F9). The Java LXR does nothing to the metadata of the destination before it counts the copy
        // (rc.rs: process_inc_and_evacuate: inc(new)), so only the ValidObject bit (forced by .NET) is set; Debug builds check
        // the range as before.
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F10_COPY_NO_VALID_CONTROL)
        // Control: the copy gets no ValidObject bit.
        result = LxrRcResult::Success;
#else
        result = FromMetadata(m_counts.RegisterObjectInClearedSpan(copyStart, size));
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F10_COPY_NO_VALID_CONTROL
        SideMetadataUpdateStatus updated;
        if (result == LxrRcResult::Success)
        {
            result = FromMetadata(m_counts.IncrementFast(copyStart, previous, &updated));
        }
        if (result == LxrRcResult::Success)
        {
            result = FromMetadata(m_counts.MarkStraddleLinesFast(copyStart, size));
        }
        if (result != LxrRcResult::Success)
        {
            header->SetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift, 0);
            return result;
        }
    }
    {
        CostScope cost(this, 2);
        memcpy(reinterpret_cast<void*>(copyStart), reinterpret_cast<void*>(start), size);
    }
    // The copy is not published yet and no other thread has its address (the forwarding state of the source is still 2), so
    // its header needs no atomic operation.
    copy->GetHeader()->SetGCReservedBitsExclusive(m_configuration.forwardingMask, m_configuration.forwardingShift, 0);
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    OracleBirth(copyStart, size);
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
#ifdef LXR_HAS_LIFETIME_PROBE
    uintptr_t watched = start;
    if (m_probe_start.load(std::memory_order_acquire) == watched)
    {
        m_probe_start.compare_exchange_strong(watched, copyStart, std::memory_order_acq_rel);
    }
#endif // LXR_HAS_LIFETIME_PROBE
    CommitCopyRecord(worker, start, size);
    Object** forwarding = reinterpret_cast<Object**>(reinterpret_cast<uint8_t*>(source) + sizeof(uintptr_t));
    // See TryCopyMature: the compare-exchange that sets the state to 3 publishes the pointer.
    VolatileStore(forwarding, copy);
    // Destination RC/VO/extent/flags and the full copied header precede publication.
    header->SetGCReservedBits(m_configuration.forwardingMask, m_configuration.forwardingShift, 3);
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F10_SHARED_COPY_COUNTERS_CONTROL)
    // Control: the copy counters are not counted.
#else
    CountWorker(worker, CopiedObjects, 1);
    CountWorker(worker, CopiedBytes, size);
    CountWorker(worker, PauseCopiedBytes, size);
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F10_SHARED_COPY_COUNTERS_CONTROL
    *destination = copy;
    *copied = true;
    return LxrRcResult::Success;
}

// The unlog bits of the lines that an object promoted in place covers. One line of 256 bytes has 32 bits of unlog table, so one
// 32-bit store sets a line (rc.rs: scan_nursery_object). The first field is 16 bytes after the start (header and method table).
// A line that holds a young object holds no old object: holes are whole lines.
void LxrReferenceCounting::SetUnloggedLines(uintptr_t start, size_t size)
{
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F10_NO_INPLACE_UNLOG_CONTROL)
    (void)start;
    (void)size;
    return;
#else
    uintptr_t line = (start + (2 * sizeof(uintptr_t))) & ~ImmixBlockGeometry::LineMask;
    uintptr_t end = (start + size + ImmixBlockGeometry::LineMask) & ~ImmixBlockGeometry::LineMask;
    SideMetadataLocation location = m_unlogged_metadata.Locate(line);
    uint32_t* bits = reinterpret_cast<uint32_t*>(const_cast<uintptr_t*>(location.word)) + (location.shift >> 5);
    for (size_t index = 0; index < (end - line) / ImmixBlockGeometry::LineBytes; index++)
    {
        VolatileStoreWithoutBarrier(bits + index, 0xffffffffu);
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F10_NO_INPLACE_UNLOG_CONTROL
}

LxrRcResult LxrReferenceCounting::ProcessIncrement(
    Object** reference, bool trace, ReferenceBatch* children, size_t worker)
{
    Object* object = *reference;
    if (object == nullptr)
    {
        return LxrRcResult::Success;
    }
    uintptr_t start = Start(object);
    if (!IsImmix(start) && !IsLarge(start))
    {
        if (m_object_model.isImmortal != nullptr &&
            m_object_model.isImmortal(object, m_object_model.context))
        {
            return LxrRcResult::Success;
        }
        return Fail(LxrRcResult::UnsupportedLifetime);
    }
    Object* source = object;
    // An object that has a count and is not in an evacuation source block is not forwarded (a young source keeps the
    // count zero, and the mature sources are the candidates), so its ValidObject and header lines are not read. The
    // children take the same shortcut (ScanIncrements).
    uintptr_t existing = 0;
    bool counted = false;
    if (IsImmix(start) && (m_configuration.evacuateNursery || m_configuration.evacuateMature))
    {
        counted = FromMetadata(m_counts.LoadFast(start, &existing)) == LxrRcResult::Success && existing != 0 &&
            !IsMatureCandidate(start);
    }
    if (!counted)
    {
        object = ResolveForwarded(object);
    }
    *reference = object;
    start = Start(object);
    MethodTable* type = object->GetGCSafeMethodTable();
    if ((type->Collectible() && m_object_model.getLoaderAllocatorSlot == nullptr) ||
        (type->HasFinalizer() && !m_object_model.admitFinalizable))
    {
        return Fail(LxrRcResult::UnsupportedLifetime);
    }
    bool copied = false;
    bool incrementedInPlace = false;
    uintptr_t previous = 0;
    // P28 (F10). The order of the Java LXR (rc.rs: process_inc_and_evacuate, dont_evacuate): an object that has a count is
    // not evacuated and is counted where it is, before anything else is asked of it.
    if (m_configuration.evacuateNursery && !counted && IsImmix(start) && object == source &&
        FromMetadata(m_counts.LoadFast(start, &existing)) == LxrRcResult::Success && existing == 0)
    {
        LxrRcResult copying = TryCopyNursery(object, &object, worker, &copied, &incrementedInPlace, &previous);
        if (copying != LxrRcResult::Success)
        {
            return copying;
        }
        *reference = object;
        start = Start(object);
    }
    SideMetadataUpdateStatus status;
    LxrRcResult result = LxrRcResult::Success;
    if (!copied && !incrementedInPlace)
    {
        if (IsImmix(start))
        {
            MarkPromotedStart(start);
        }
        result = FromMetadata(m_counts.IncrementFast(start, &previous, &status));
    }
    if (result == LxrRcResult::Success && trace && NeedsIncrementTrace(start))
    {
        uintptr_t target = reinterpret_cast<uintptr_t>(object);
        m_increment_traces.fetch_add(1, std::memory_order_relaxed);
        result = QueueTrace(&target, 1);
    }
    if (result != LxrRcResult::Success || previous != 0)
    {
        return result;
    }
#ifdef _DEBUG
    // P25 (F1). Black allocation, checked as in the Java LXR (rc.rs: promote, debug_assert is_marked): an object that is
    // counted for the first time inside the window of a trace was allocated or copied after the snapshot, so its mark
    // bit is set. If it is not, a trace packet is the only thing that can still mark it.
    if (IsIncrementTraceWindow() && m_mark_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) == 0)
    {
        return Fail(LxrRcResult::InvariantViolation);
    }
#endif // _DEBUG
    size_t size;
    result = GetObjectSize(object, &size, worker);
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    if (IsImmix(start))
    {
        bool nursery;
        result = FromMetadata(m_blocks.IsNurseryFast(start & ~ImmixBlockGeometry::BlockMask, &nursery));
        if (result != LxrRcResult::Success)
        {
            return result;
        }
        if (copied || nursery)
        {
            // Only objects in clean nursery blocks can be evacuated, so only they size the to-space.
            CountWorker(worker, PromotedBytes, size);
        }
        if (nursery)
        {
            ImmixBlockOperationStatus promoted;
            do
            {
                result = FromMetadata(m_blocks.TryPromoteInPlace(
                    start & ~ImmixBlockGeometry::BlockMask, &promoted));
                if (promoted == ImmixBlockOperationStatus::Contended)
                {
                    YieldProcessor();
                }
            } while (result == LxrRcResult::Success && promoted == ImmixBlockOperationStatus::Contended);
            if (result != LxrRcResult::Success ||
                (promoted != ImmixBlockOperationStatus::Updated &&
                 promoted != ImmixBlockOperationStatus::Unchanged))
            {
                return Fail(LxrRcResult::MetadataFailure);
            }
            if (promoted == ImmixBlockOperationStatus::Updated)
            {
                // The block stays; the lazy sweep after the pause finds out which of its lines are free.
                m_mature_candidates[(start - GetReservationStart()) / ImmixBlockGeometry::BlockBytes].store(
                    1, std::memory_order_release);
#ifdef LXR_S4_EARLY_REUSABLE_CONTROL
                ImmixBlockOperationStatus early;
                m_blocks.SetReusable(start & ~ImmixBlockGeometry::BlockMask, 1, &early);
#endif // LXR_S4_EARLY_REUSABLE_CONTROL
            }
        }
        if (!copied)
        {
            result = FromMetadata(m_counts.MarkStraddleLinesFast(start, size));
            // P28 (F10). As in the Java LXR (rc.rs: scan_nursery_object, in_place_promotion): an object without reference fields
            // needs no unlog bits, and the bits of the others are set one line at a time with a store of 32 bits.
            if (result == LxrRcResult::Success && type->ContainsGCPointers())
            {
                SetUnloggedLines(start, size);
            }
        }
    }
    else
    {
        LxrLargeObjectAllocation allocation;
        bool promoted;
        if (m_large.GetAllocation(start, &allocation) != LxrLargeObjectResult::Success ||
            m_large.TryMarkAndPromote(start, allocation.owner, &promoted, type->ContainsGCPointers()) !=
                LxrLargeObjectResult::Success)
        {
            return Fail(LxrRcResult::MetadataFailure);
        }
    }
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    CountWorker(worker, Promotions, 1);
    if (!copied && m_configuration.evacuateNursery &&
        ((object->GetHeader()->GetBits() & BIT_SBLK_GC_RESERVE) != 0 || IsPinnedObject(object)))
    {
        m_nursery_metrics[2].fetch_add(size, std::memory_order_relaxed);
    }
    // The children of a black object are traced when they have no mark: an unmarked old object can hide behind it
    // (a weak target that a program stored into the newborn). The test of the child's own mark bit keeps the common
    // case, a black child, free of any packet.
    bool childrenTrace = trace ||
        (IsIncrementTraceWindow() && m_mark_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) != 0);
    return ScanIncrements(object, childrenTrace, children, worker);
}

bool LxrReferenceCounting::IsIncrementTraceWindow() const
{
    LxrTracePhase phase = GetTracePhase();
    return phase == LxrTracePhase::Concurrent || phase == LxrTracePhase::FinalMark;
}

bool LxrReferenceCounting::NeedsIncrementTrace(uintptr_t start) const
{
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F1_QUEUE_ALL_CONTROL)
    // Control: the behaviour before P25 (F1). Every counted object gets a packet while a trace is active.
    (void)start;
    return GetTracePhase() != LxrTracePhase::Inactive;
#elif defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F1_SKIP_ALL_CONTROL)
    // Control: the skip is too wide. An unmarked object is skipped as well.
    (void)start;
    return false;
#else
    return IsIncrementTraceWindow() && m_mark_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) == 0;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F1_QUEUE_ALL_CONTROL
}

LxrRcResult LxrReferenceCounting::ScanIncrements(
    Object* object, bool trace, ReferenceBatch* children, size_t worker)
{
    LxrRcResult result = LxrRcResult::Success;
    CountWorker(worker, ObjectScans, 1);
    GCReferenceRanges::Enumerate(object, [&](Object** first, size_t length)
    {
        for (size_t index = 0; index < length; index++)
        {
            Object* child = first[index];
            if (child == nullptr)
            {
                continue;
            }
            // P28 (F10). As in the Java LXR (rc.rs: scan_nursery_object) a child that has a count already is counted here, from
            // its count alone, and only a child without a count (a young object) becomes a slot for the next level. The header
            // of an old child is not read. The block of the child must not be a source of the mature evacuation (a source can be
            // forwarded, and the slot would need the copy); large objects take the longer path.
            uintptr_t childStart = Start(child);
            uintptr_t childCount = 0;
            if (IsImmix(childStart) &&
                FromMetadata(m_counts.LoadFast(childStart, &childCount)) == LxrRcResult::Success &&
                childCount != 0 && !IsMatureCandidate(childStart))
            {
                CountWorker(worker, IncrementEntries, 1);
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F10_INLINE_NO_INC_CONTROL)
                // Control: the increment of a counted child is left out.
                if (false)
#else
                if (childCount != m_counts.GetMaximumCount())
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F10_INLINE_NO_INC_CONTROL
                {
                    uintptr_t previous;
                    SideMetadataUpdateStatus status;
                    result = FromMetadata(m_counts.IncrementFast(childStart, &previous, &status));
                    if (result != LxrRcResult::Success)
                    {
                        return false;
                    }
                }
                if (trace && NeedsIncrementTrace(childStart))
                {
                    uintptr_t target = reinterpret_cast<uintptr_t>(child);
                    m_increment_traces.fetch_add(1, std::memory_order_relaxed);
                    result = QueueTrace(&target, 1);
                    if (result != LxrRcResult::Success)
                    {
                        return false;
                    }
                }
                continue;
            }
            if (childCount != 0)
            {
                result = RememberMatureSlot(first + index, child);
                if (result != LxrRcResult::Success)
                {
                    return false;
                }
            }
            result = children->Append(reinterpret_cast<uintptr_t>(first + index), trace);
            if (result != LxrRcResult::Success)
            {
                return false;
            }
        }
        return true;
    });
    Object* loaderAllocator = GetLoaderAllocator(object);
    if (result == LxrRcResult::Success && loaderAllocator != nullptr)
    {
        uintptr_t target = reinterpret_cast<uintptr_t>(loaderAllocator);
        result = SubmitWork(NewWork(WorkKind::Targets, &target, 1, LxrRCProcessIncs, trace));
    }
    return result;
}

LxrRcResult LxrReferenceCounting::ScanDecrements(Object* object, ReferenceBatch* children, size_t worker)
{
    LxrRcResult result = LxrRcResult::Success;
    CountWorker(worker, ObjectScans, 1);
    // P28 (F10). As in the Java LXR (rc.rs: process_dead_object) a child whose count is zero or at the maximum gets no decrement,
    // so it is not queued. While dead objects mark (MarkDeadObjects) every child of a dead object belongs to the snapshot and
    // is queued.
    const bool queueAll = MarkDeadObjects();
    const uintptr_t maximumCount = m_counts.GetMaximumCount();
    GCReferenceRanges::Enumerate(object, [&](Object** first, size_t length)
    {
        for (size_t index = 0; index < length; index++)
        {
            Object* child = first[index];
            if (child == nullptr)
            {
                continue;
            }
            if (!queueAll && OwnsAddress(Start(child)))
            {
                uintptr_t childCount = m_rc_metadata.Load(Start(child), SideMetadataMemoryOrder::Relaxed);
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F10_SKIP_DECS_CONTROL)
                // Control: the skip is too wide: a child with count 1 gets no decrement either.
                if (childCount <= 1 || childCount == maximumCount)
#else
                if (childCount == 0 || childCount == maximumCount)
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F10_SKIP_DECS_CONTROL
                {
                    continue;
                }
            }
            result = children->Append(reinterpret_cast<uintptr_t>(child), true);
            if (result != LxrRcResult::Success)
            {
                return false;
            }
        }
        return true;
    });
    Object* loaderAllocator = GetLoaderAllocator(object);
    if (result == LxrRcResult::Success && loaderAllocator != nullptr)
    {
        result = children->Append(reinterpret_cast<uintptr_t>(loaderAllocator), true);
    }
    return result;
}

Object* LxrReferenceCounting::GetLoaderAllocator(Object* object) const
{
    MethodTable* type = object->GetGCSafeMethodTable();
    if (!type->Collectible())
    {
        return nullptr;
    }
    Object** slot = m_object_model.getLoaderAllocatorSlot(type);
    return slot == nullptr ? nullptr :
        Interlocked::CompareExchangePointer(slot, static_cast<Object*>(nullptr), nullptr);
}

SideMetadataResult LxrReferenceCounting::Death(uintptr_t start, void* context)
{
    DeathContext* death = static_cast<DeathContext*>(context);
    LxrReferenceCounting* engine = death->engine;
    Object* object = Reference(start);
    if (engine->m_marking.load(std::memory_order_acquire))
    {
#ifdef LXR_HAS_PHASE_TIMERS
        engine->CountWindow(0, 1);
#endif // LXR_HAS_PHASE_TIMERS
        if (engine->MarkDeadObjects() && engine->FinishDyingTraceObject(object) != LxrRcResult::Success)
        {
            return SideMetadataResult::InconsistentState;
        }
    }
    if (engine->ScanDecrements(object, death->children, death->worker) != LxrRcResult::Success)
    {
        return SideMetadataResult::InconsistentState;
    }
    if (engine->IsImmix(start))
    {
        size_t size;
        if (engine->GetObjectSize(object, &size, death->worker) != LxrRcResult::Success)
        {
            return SideMetadataResult::InconsistentState;
        }
        SideMetadataResult result = engine->m_counts.ClearStraddleLinesFast(start, size);
        if (result == SideMetadataResult::Success)
        {
            if (engine->Reclaim(start) != LxrRcResult::Success)
            {
                return SideMetadataResult::InconsistentState;
            }
            size_t block = (start - reinterpret_cast<uintptr_t>(engine->m_arena)) /
                ImmixBlockGeometry::BlockBytes;
            // Most deaths find the flag set: the read keeps the cache line of the flags (one byte per block) shared between
            // the workers, and only the first death of a block writes it.
            std::atomic<uint8_t>& candidate = engine->m_mature_candidates[block];
            if (candidate.load(std::memory_order_relaxed) == 0)
            {
                candidate.store(1, std::memory_order_release);
            }
        }
        return result;
    }
    size_t index = engine->m_dead_large_count.fetch_add(1, std::memory_order_relaxed);
    if (index >= engine->m_dead_large_capacity.load(std::memory_order_relaxed))
    {
        engine->Fail(LxrRcResult::CapacityExceeded);
        return SideMetadataResult::InconsistentState;
    }
    engine->m_dead_large[index] = start;
    if (engine->Reclaim(start) != LxrRcResult::Success)
    {
        return SideMetadataResult::InconsistentState;
    }
    return SideMetadataResult::Success;
}

LxrRcResult LxrReferenceCounting::ProcessDecrements(Work* work, LxrWorkPacket* packet, size_t worker)
{
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    if (work->ownedReferences == nullptr && m_log_reader_hold.load(std::memory_order_acquire))
    {
        m_log_reader_active.store(true, std::memory_order_release);
        while (m_log_reader_hold.load(std::memory_order_acquire))
        {
            GCToOSInterface::YieldThread(0);
        }
        m_log_reader_active.store(false, std::memory_order_release);
    }
#ifdef LXR_LOG_DROP_CONTROL
    if (work->ownedReferences == nullptr)
    {
        return LxrRcResult::Success;
    }
#endif // LXR_LOG_DROP_CONTROL
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    ReferenceBatch children{this, WorkKind::Decrements, packet, {}};
    DeathContext death{this, &children, worker};
    for (size_t index = 0; index < packet->GetReferenceCount(); index++)
    {
        uintptr_t reference = work->GetReference(index);
        bool previousRoot = work->kind == WorkKind::PreviousRoots;
        if (previousRoot)
        {
            reference &= ~static_cast<uintptr_t>(1);
        }
        LxrRcResult result = ProcessDecrementEntry(reference, previousRoot, &death, worker);
        if (result != LxrRcResult::Success)
        {
            return result;
        }
        // The Java LXR prefetches the object of the entry that is 8 ahead (rc.rs: process_decs).
        LxrPrefetch::Ahead(work->references, packet->GetReferenceCount(), index, false, previousRoot ? 1 : 0);
    }
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F4_NO_RECURSION_CONTROL)
    // Control: the behaviour before P25 (F4). The children of dead objects become a new packet.
    return children.Flush();
#else
    // P25 (F4). The children of the dead objects are processed in this packet, one level after the other, as the Java LXR
    // does (rc.rs ProcessDecs::do_work). A full buffer still becomes a new packet (ReferenceBatch::Append), so that other
    // workers can take it.
    uintptr_t objects[BufferCapacity];
    while (children.count != 0)
    {
        size_t count = children.count;
        if (MarkDeadObjects())
        {
            // The children of a dead object are part of the snapshot (SATB), as in ReferenceBatch::Flush.
#ifdef LXR_HAS_PHASE_TIMERS
            CountWindow(2, count);
#endif // LXR_HAS_PHASE_TIMERS
            LxrRcResult queued = QueueTrace(children.references, count);
            if (queued != LxrRcResult::Success)
            {
                return queued;
            }
        }
        memcpy(objects, children.references, count * sizeof(uintptr_t));
        children.count = 0;
        for (size_t index = 0; index < count; index++)
        {
            LxrRcResult result = ProcessDecrementEntry(objects[index], false, &death, worker);
            if (result != LxrRcResult::Success)
            {
                return result;
            }
            LxrPrefetch::Ahead(objects, count, index, false);
        }
    }
    return LxrRcResult::Success;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F4_NO_RECURSION_CONTROL
}

LxrRcResult LxrReferenceCounting::ProcessDecrementEntry(
    uintptr_t reference, bool previousRoot, DeathContext* death, size_t worker)
{
    Object* object = reinterpret_cast<Object*>(reference);
    if (object == nullptr)
    {
        return LxrRcResult::Success;
    }
    if (!OwnsAddress(Start(object)))
    {
        if (IsReturnedImmix(Start(object)) || (m_object_model.isImmortal != nullptr &&
            m_object_model.isImmortal(object, m_object_model.context)))
        {
            return LxrRcResult::Success;
        }
        return Fail(LxrRcResult::UnsupportedLifetime);
    }
    if (m_cycle_sweep.load(std::memory_order_acquire) && !IsMarked(object))
    {
        return LxrRcResult::Success;
    }
    uintptr_t start = Start(object);
    uintptr_t count = m_rc_metadata.Load(start, SideMetadataMemoryOrder::Relaxed);
    CountWorker(worker, DecrementEntries, 1);
    if (previousRoot)
    {
        CountWorker(worker, RootDecrements, 1);
    }
    if (count == 0 || count == m_counts.GetMaximumCount())
    {
        return LxrRcResult::Success;
    }
    uintptr_t previous;
    SideMetadataUpdateStatus status;
    LxrRcResult result = FromMetadata(m_counts.DecrementWithDeathFast(
        start, Death, death, &previous, &status));
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    if (status == SideMetadataUpdateStatus::Updated && previous == 1)
    {
        CountWorker(worker, Deaths, 1);
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::ReleaseBlockIfDead(
    uintptr_t block, size_t index, const LxrBlockReleaseGuard* guard, ImmixBlockOperationStatus* status)
{
    bool selected = IsMatureCandidate(block);
    bool copied = selected && (m_evacuation_sources[index].load(std::memory_order_acquire) & CopiedSource) != 0;
    LxrRcResult result = FromMetadata(m_blocks.TryReleaseIfRcDead(block, guard, status));
    if (result != LxrRcResult::Success || *status != ImmixBlockOperationStatus::Updated)
    {
        return result;
    }
    if (FromMetadata(m_allocation.NotifyBlockReleased()) != LxrRcResult::Success)
    {
        return GetFailure();
    }
    m_metrics[BlocksReleased].fetch_add(1, std::memory_order_relaxed);
    if (copied)
    {
        m_mature_metrics[11].fetch_add(1, std::memory_order_relaxed);
    }
    if (selected)
    {
        m_evacuation_sources[index].store(0, std::memory_order_release);
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::RefreshBlock(uintptr_t block, size_t index, const LxrBlockReleaseGuard& guard)
{
    ImmixBlockState state;
    uint8_t unavailable;
    LxrRcResult result = FromMetadata(m_blocks.GetState(block, &state, &unavailable));
    if (result != LxrRcResult::Success || state == ImmixBlockState::Unallocated)
    {
        return result;
    }
    bool reusing;
    result = FromMetadata(m_blocks.IsReusing(block, &reusing));
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    if (reusing)
    {
        // An allocator took the block in this epoch. Its young objects have no counts yet, so the lines cannot
        // be counted before the next pause.
        m_mature_candidates[index].store(1, std::memory_order_release);
        return LxrRcResult::Success;
    }
    size_t freeLines = 0;
    result = FromMetadata(m_allocation.CountHoleLines(block, &freeLines));
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    if (freeLines == 0 || freeLines >= ImmixBlockGeometry::LinesPerBlock)
    {
        return LxrRcResult::Success;
    }
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_S4_NO_REFRESH_CONTROL)
    ImmixBlockOperationStatus status;
    result = FromMetadata(m_blocks.TrySetReusableLazily(
        block, &guard, static_cast<uint8_t>(ImmixBlockGeometry::LinesPerBlock - freeLines), &status));
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    if (status == ImmixBlockOperationStatus::Contended || status == ImmixBlockOperationStatus::Stale)
    {
        m_mature_candidates[index].store(1, std::memory_order_release);
    }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_S4_NO_REFRESH_CONTROL
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::Sweep(const LxrBlockReleaseGuard& guard)
{
    if (m_cycle_sweep.load(std::memory_order_acquire))
    {
        LxrRcResult result = SweepCycles();
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    size_t blocks = m_immix_capacity.load(std::memory_order_acquire) / ImmixBlockGeometry::BlockBytes;
    bool held = false;
    LxrRcResult result = SweepBlockRange(0, blocks, guard, &held);
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    return ReleaseDeadLargeRange(0, m_dead_large_count.load(std::memory_order_acquire), guard);
}

LxrRcResult LxrReferenceCounting::SweepBlockRange(
    size_t first, size_t limit, const LxrBlockReleaseGuard& guard, bool* held)
{
    uintptr_t arena = reinterpret_cast<uintptr_t>(m_arena);
    // Only the blocks that a death, a promotion in place, a copy or an allocator touched carry a flag. The flag is
    // taken before the block is looked at: a flag that is set again meanwhile, or by RefreshBlock for a block that
    // is not ready, stays for the next sweep.
    for (size_t candidate = first; candidate < limit; candidate++)
    {
        if (m_mature_candidates[candidate].load(std::memory_order_acquire) == 0)
        {
            continue;
        }
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        if (!*held)
        {
            *held = true;
            m_sweep_holding.store(true, std::memory_order_release);
            while (m_sweep_hold.load(std::memory_order_acquire))
            {
                GCToOSInterface::YieldThread(0);
            }
            m_sweep_holding.store(false, std::memory_order_release);
        }
#else
        (void)held;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        m_mature_candidates[candidate].store(0, std::memory_order_release);
        uintptr_t block = arena + candidate * ImmixBlockGeometry::BlockBytes;
        ImmixBlockOperationStatus status;
        LxrRcResult result = ReleaseBlockIfDead(block, candidate, &guard, &status);
        if (result != LxrRcResult::Success)
        {
            return result;
        }
        if (status == ImmixBlockOperationStatus::Contended)
        {
            m_mature_candidates[candidate].store(1, std::memory_order_release);
        }
        else if (status != ImmixBlockOperationStatus::Updated)
        {
            result = RefreshBlock(block, candidate, guard);
            if (result != LxrRcResult::Success)
            {
                return result;
            }
        }
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::ReleaseDeadLargeRange(size_t first, size_t limit, const LxrBlockReleaseGuard& guard)
{
    for (size_t index = first; index < limit; index++)
    {
        LxrLargeObjectAllocation allocation;
        if (m_large.GetAllocation(m_dead_large[index], &allocation) != LxrLargeObjectResult::Success ||
            m_large.ReleaseAfterDecrements(allocation.object, allocation.owner, guard) !=
                LxrLargeObjectResult::Success)
        {
            return Fail(LxrRcResult::MetadataFailure);
        }
        m_metrics[LargeObjectBytesReleased].fetch_add(
            allocation.page_count * LxrLargeObjectSpace::PageBytes, std::memory_order_relaxed);
    }
    return LxrRcResult::Success;
}

namespace
{
    // Sizes of the units of the parallel lazy sweep. A unit is the smallest piece that a packet takes from the shared cursor.
    // A cycle unit covers 64 Ki slots (512 KiB of heap, aligned to 64 slots, so the ValidObject words are not shared). A
    // block unit covers 64 blocks (2 MiB). A large-object unit releases 8 dead large objects.
    constexpr size_t SweepCycleUnitSlots = static_cast<size_t>(1) << 16;
    constexpr size_t SweepBlockUnitBlocks = 64;
    constexpr size_t SweepLargeUnitObjects = 8;
    // A unit of the eager mark clear covers 1 MiB of heap, which is 16 KiB of ObjectMark bytes.
    constexpr size_t MarkClearUnitBytes = static_cast<size_t>(1) << 20;
}

LxrRcResult LxrReferenceCounting::BeginParallelSweep(LxrMatureSweepContinuation* continuation)
{
    if (!m_cycle_sweep.load(std::memory_order_acquire))
    {
        return SubmitSweepBatch(SweepPhase::Blocks, continuation, nullptr);
    }
    // The leases go before the first object is swept. The decrements have drained, so nothing reads the roots now.
    LxrRcResult result = ClearDeadRootLeases();
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    if (m_pause_timers.Enabled())
    {
        m_lazy_cycle_stamp.store(m_pause_timers.Stamp(), std::memory_order_relaxed);
    }
    return SubmitSweepBatch(SweepPhase::Cycles, continuation, nullptr);
}

// Submits the packets of one phase of the sweep. They share the cursor and take units until it is exhausted, so a worker
// that is busy with other work only delays its own packet. With a continuation the packets are children of the
// decrement counter's continuation; with a parent they are children of the packet that finished the previous phase. Both
// keep the lazy counter above zero until the last packet of the last phase has completed.
LxrRcResult LxrReferenceCounting::SubmitSweepBatch(
    SweepPhase phase, LxrMatureSweepContinuation* continuation, const LxrWorkPacket* parent)
{
    size_t units;
    if (phase == SweepPhase::Cycles)
    {
        m_sweep_immix_slots = GetImmixCapacityBytes() / sizeof(uintptr_t);
        m_sweep_large_first_slot = m_configuration.immixBytes / sizeof(uintptr_t);
        m_sweep_large_slots = m_large.GetAdmittedBytes() / sizeof(uintptr_t);
        m_sweep_immix_units = (m_sweep_immix_slots + SweepCycleUnitSlots - 1) / SweepCycleUnitSlots;
        units = m_sweep_immix_units + (m_sweep_large_slots + SweepCycleUnitSlots - 1) / SweepCycleUnitSlots;
        if (units == 0)
        {
            return SubmitSweepBatch(SweepPhase::Blocks, continuation, parent);
        }
    }
    else
    {
        m_sweep_blocks = m_immix_capacity.load(std::memory_order_acquire) / ImmixBlockGeometry::BlockBytes;
        m_sweep_block_units = (m_sweep_blocks + SweepBlockUnitBlocks - 1) / SweepBlockUnitBlocks;
        size_t dead = m_dead_large_count.load(std::memory_order_acquire);
        units = m_sweep_block_units + (dead + SweepLargeUnitObjects - 1) / SweepLargeUnitObjects;
        if (units == 0)
        {
            return LxrRcResult::Success;
        }
    }
    size_t packets = m_configuration.workers == 0 ? 1 : m_configuration.workers;
    if (packets > units)
    {
        packets = units;
    }
    m_sweep_units = units;
    m_sweep_cursor.store(0, std::memory_order_relaxed);
    m_sweep_remaining.store(packets, std::memory_order_relaxed);
    m_sweep_phase.store(phase, std::memory_order_release);
    m_lazy_metrics[LazySweepPackets].fetch_add(packets, std::memory_order_relaxed);
    for (size_t index = 0; index < packets; index++)
    {
        Work* work = NewWork(WorkKind::MatureSweep, nullptr, 0, LxrWorkBucketStage::Unconstrained);
        if (work == nullptr)
        {
            return GetFailure();
        }
        LxrRcResult result = parent != nullptr
            ? FromScheduler(m_scheduler.SubmitTrackedChild(
                parent, &work->packet, LxrCompletionWorkKind::MatureSweep))
            : FromScheduler(m_scheduler.SubmitMatureSweep(continuation, &work->packet));
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    return LxrRcResult::Success;
}

void LxrReferenceCounting::RunSweepPacket(LxrWorkPacket* packet, const LxrBlockReleaseGuard& guard)
{
    SweepPhase phase = m_sweep_phase.load(std::memory_order_acquire);
    bool held = false;
    while (GetFailure() == LxrRcResult::Success)
    {
        size_t unit = m_sweep_cursor.fetch_add(1, std::memory_order_relaxed);
        if (unit >= m_sweep_units)
        {
            break;
        }
        LxrRcResult result = phase == SweepPhase::Cycles ? SweepCycleUnit(unit) : SweepBlockUnit(unit, guard, &held);
        if (result != LxrRcResult::Success)
        {
            break;
        }
    }
    if (m_sweep_remaining.fetch_sub(1, std::memory_order_acq_rel) != 1 || phase != SweepPhase::Cycles)
    {
        return;
    }
    // This packet finished the last unit of the dead-cycle batch. The block batch needs the cleared counts, so it starts now.
    int64_t cycleStart = m_lazy_cycle_stamp.exchange(0, std::memory_order_relaxed);
    if (cycleStart != 0)
    {
        m_lazy_metrics[LazyCycleSweeps].fetch_add(1, std::memory_order_relaxed);
        m_lazy_metrics[LazyCycleSweepNs].fetch_add(
            TicksToNanoseconds(m_pause_timers.Stamp() - cycleStart), std::memory_order_relaxed);
    }
    if (GetFailure() == LxrRcResult::Success)
    {
        SubmitSweepBatch(SweepPhase::Blocks, nullptr, packet);
    }
}

LxrRcResult LxrReferenceCounting::SweepCycleUnit(size_t unit)
{
    size_t first;
    size_t limit;
    if (unit < m_sweep_immix_units)
    {
        first = unit * SweepCycleUnitSlots;
        limit = first + SweepCycleUnitSlots < m_sweep_immix_slots ? first + SweepCycleUnitSlots : m_sweep_immix_slots;
    }
    else
    {
        size_t offset = (unit - m_sweep_immix_units) * SweepCycleUnitSlots;
        first = m_sweep_large_first_slot + offset;
        limit = m_sweep_large_first_slot +
            (offset + SweepCycleUnitSlots < m_sweep_large_slots ? offset + SweepCycleUnitSlots : m_sweep_large_slots);
    }
    return SweepCycleRange(first, limit);
}

LxrRcResult LxrReferenceCounting::SweepBlockUnit(size_t unit, const LxrBlockReleaseGuard& guard, bool* held)
{
    if (unit < m_sweep_block_units)
    {
        size_t first = unit * SweepBlockUnitBlocks;
        size_t limit = first + SweepBlockUnitBlocks < m_sweep_blocks ? first + SweepBlockUnitBlocks : m_sweep_blocks;
        return SweepBlockRange(first, limit, guard, held);
    }
    size_t dead = m_dead_large_count.load(std::memory_order_acquire);
    size_t first = (unit - m_sweep_block_units) * SweepLargeUnitObjects;
    size_t limit = first + SweepLargeUnitObjects < dead ? first + SweepLargeUnitObjects : dead;
    return ReleaseDeadLargeRange(first, limit, guard);
}

void LxrReferenceCounting::ScheduleMarkClear()
{
    if (!m_configuration.eagerMarkClear || GetFailure() != LxrRcResult::Success ||
        GetPhase() != LxrRcPhase::Mutator ||
        m_trace_phase.load(std::memory_order_acquire) != LxrTracePhase::Inactive ||
        m_marking.load(std::memory_order_acquire))
    {
        return;
    }
    MarkPlane expected = MarkPlane::Dirty;
    if (!m_mark_plane.compare_exchange_strong(expected, MarkPlane::Clearing, std::memory_order_acq_rel))
    {
        return;
    }
    // The capacity and the admitted large-object bytes change only inside a pause, and a pause waits for these packets.
    size_t immixBytes = GetImmixCapacityBytes();
    m_mark_clear_immix_units = (immixBytes + MarkClearUnitBytes - 1) / MarkClearUnitBytes;
    m_mark_clear_large_start = GetReservationStart() + m_configuration.immixBytes;
    m_mark_clear_large_bytes = m_large.GetAdmittedBytes();
    m_mark_clear_units = m_mark_clear_immix_units + (m_mark_clear_large_bytes + MarkClearUnitBytes - 1) / MarkClearUnitBytes;
    size_t packets = m_configuration.workers == 0 ? 1 : m_configuration.workers;
    if (packets > m_mark_clear_units)
    {
        packets = m_mark_clear_units;
    }
    if (packets == 0)
    {
        m_mark_plane.store(MarkPlane::Clean, std::memory_order_release);
        return;
    }
    m_mark_clear_cursor.store(0, std::memory_order_relaxed);
    m_mark_clear_remaining.store(packets, std::memory_order_relaxed);
    if (m_pause_timers.Enabled())
    {
        m_mark_clear_stamp.store(m_pause_timers.Stamp(), std::memory_order_relaxed);
    }
    for (size_t index = 0; index < packets; index++)
    {
        Work* work = NewWork(WorkKind::MarkClear, nullptr, 0, LxrWorkBucketStage::Unconstrained);
        if (work == nullptr || m_scheduler.Submit(&work->packet) != LxrWorkSchedulerResult::Success)
        {
            if (index == 0)
            {
                m_mark_plane.store(MarkPlane::Dirty, std::memory_order_release);
            }
            return;
        }
    }
}

void LxrReferenceCounting::RunMarkClearPacket()
{
    while (GetFailure() == LxrRcResult::Success)
    {
        size_t unit = m_mark_clear_cursor.fetch_add(1, std::memory_order_relaxed);
        if (unit >= m_mark_clear_units)
        {
            break;
        }
        uintptr_t start;
        size_t bytes;
        if (unit < m_mark_clear_immix_units)
        {
            start = GetReservationStart() + unit * MarkClearUnitBytes;
            bytes = GetImmixCapacityBytes() - unit * MarkClearUnitBytes;
        }
        else
        {
            size_t offset = (unit - m_mark_clear_immix_units) * MarkClearUnitBytes;
            start = m_mark_clear_large_start + offset;
            bytes = m_mark_clear_large_bytes - offset;
        }
        if (bytes > MarkClearUnitBytes)
        {
            bytes = MarkClearUnitBytes;
        }
        if (FromMetadata(m_metadata.FillRangeBytes(LxrSideMetadataKind::ObjectMark, start, bytes, 0)) !=
            LxrRcResult::Success)
        {
            return;
        }
    }
    if (m_mark_clear_remaining.fetch_sub(1, std::memory_order_acq_rel) != 1 ||
        GetFailure() != LxrRcResult::Success)
    {
        return;
    }
    int64_t started = m_mark_clear_stamp.exchange(0, std::memory_order_relaxed);
    if (started != 0)
    {
        m_lazy_metrics[MarkClearEager].fetch_add(1, std::memory_order_relaxed);
        m_lazy_metrics[MarkClearEagerNs].fetch_add(
            TicksToNanoseconds(m_pause_timers.Stamp() - started), std::memory_order_relaxed);
    }
    m_mark_plane.store(MarkPlane::Clean, std::memory_order_release);
}

uint64_t LxrReferenceCounting::GetLazyMetric(size_t metric) const
{
    return metric < LazyMetricCount ? m_lazy_metrics[metric].load(std::memory_order_relaxed) : UINT64_MAX;
}

LxrRcResult LxrReferenceCounting::SweepLargeNursery()
{
    LxrRcPhase phase = GetPhase();
    m_phase.store(LxrRcPhase::Sweeping, std::memory_order_release);
#ifdef LXR_RC_SWEEP_QUERY_CONTROL
    size_t forbidden;
    return GetObjectSize(nullptr, &forbidden);
#endif
    size_t released;
    if (m_large.SweepNursery(&released) != LxrLargeObjectResult::Success)
    {
        return Fail(LxrRcResult::MetadataFailure);
    }
    m_metrics[LargeObjectBytesReleased].fetch_add(released, std::memory_order_relaxed);
    m_phase.store(phase, std::memory_order_release);
    return LxrRcResult::Success;
}

void LxrReferenceCounting::FixPacketReferences(Work* work)
{
    if (work->kind == WorkKind::Slots || work->kind == WorkKind::NurserySlots || work->ownedReferences == nullptr)
    {
        return;
    }
    for (size_t index = 0; index < work->packet.GetReferenceCount(); index++)
    {
        uintptr_t flags = work->kind == WorkKind::Roots || work->kind == WorkKind::PreviousRoots
            ? work->ownedReferences[index] & 1 : 0;
        Object* object = reinterpret_cast<Object*>(work->ownedReferences[index] & ~flags);
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_PENDING_READER_REBASE_CONTROL)
        if (work->watchedReader)
        {
            continue;
        }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_PENDING_READER_REBASE_CONTROL
        Object* fixedObject = ResolveForwardedChecked(object);
        if (fixedObject != object)
        {
            m_walk[4].fetch_add(1, std::memory_order_relaxed);
        }
        work->ownedReferences[index] = reinterpret_cast<uintptr_t>(fixedObject) | flags;
    }
}

LxrRcResult LxrReferenceCounting::FixForwardingReaders()
{
    if (!m_configuration.evacuateNursery && !m_configuration.evacuateMature)
    {
        return LxrRcResult::Success;
    }
    for (size_t bank = 0; bank < 2; bank++)
    {
        for (size_t index = 0; index < m_root_counts[bank]; index++)
        {
            uintptr_t flags = m_roots[bank][index] & 1;
            Object* object = reinterpret_cast<Object*>(m_roots[bank][index] & ~static_cast<uintptr_t>(1));
            Object* fixedRoot = ResolveForwarded(object);
            if (fixedRoot != object)
            {
                m_walk[4].fetch_add(1, std::memory_order_relaxed);
            }
            m_roots[bank][index] = reinterpret_cast<uintptr_t>(fixedRoot) | flags;
        }
    }
    // P26 (F2). The decrement log. The Java LXR resolves a forwarded target when it uses it (rc.rs: ProcessDecs, only for
    // the objects that a mature evacuation moves) and never walks the log in a pause. Here:
    //  - the log bank that the lazy decrements of the last pause have consumed is not read again;
    //  - the log of this pause holds the values that the fields had at the last pause. These are counted objects, which the
    //    nursery copy does not move (it copies objects with count 0). So a pause that copies only young objects does not
    //    walk it. Debug builds walk it and check that no entry names a source of this pause.
    //  - a pause that can copy mature objects (FinalMark, Full) rewrites it, as before.
    bool copiesMature = m_configuration.evacuateMature &&
        (m_pause_kind == LxrPauseKind::Full || m_pause_kind == LxrPauseKind::FinalMark);
    (void)copiesMature;
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F2_WALK_LOGS_CONTROL)
    // Control: the walk of both banks, as before P26.
    for (size_t bank = 0; bank < 2; bank++)
#else
    size_t bank = m_producer_bank.load(std::memory_order_relaxed);
#ifndef _DEBUG
    if (copiesMature)
#endif // !_DEBUG
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F2_WALK_LOGS_CONTROL
    {
        LogStream& decrements = m_banks[bank].decrements;
        size_t limit = decrements.reserved.load(std::memory_order_acquire);
        for (size_t cursor = 0; cursor < limit;)
        {
            size_t count;
            LxrRcResult result = ReadPublishedRange(decrements, cursor, limit, &count);
            if (result != LxrRcResult::Success)
            {
                return result;
            }
            for (size_t index = cursor; index < cursor + count; index++)
            {
                m_walk[2].fetch_add(1, std::memory_order_relaxed);
                uintptr_t value = decrements.references[index];
                uintptr_t publication = decrements.published == nullptr && index == cursor
                    ? value & ~LogReferenceMask : 0;
                Object* target = reinterpret_cast<Object*>(value & LogReferenceMask);
                Object* fixedDec = ResolveForwarded(target);
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_F2_WALK_LOGS_CONTROL)
                if (!copiesMature)
                {
                    if (fixedDec != target)
                    {
                        return Fail(LxrRcResult::InvariantViolation);
                    }
                    continue;
                }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_F2_WALK_LOGS_CONTROL
                if (fixedDec != target)
                {
                    m_walk[4].fetch_add(1, std::memory_order_relaxed);
                }
                decrements.references[index] = reinterpret_cast<uintptr_t>(fixedDec) | publication;
            }
            cursor += count;
        }
    }
    // The packets that the pause itself made are in m_work (the packets of earlier pauses are freed at Suspend) and in
    // m_trace_unsubmitted. The trace packets that wait in the scheduler are not visited: ProcessTraceObject checks the
    // address when it uses it (see there), so a packet that names a moved young source needs no rewrite.
    for (Work* work = m_work.load(std::memory_order_acquire); work != nullptr; work = work->next)
    {
        m_walk[1].fetch_add(1, std::memory_order_relaxed);
        FixPacketReferences(work);
    }
    for (Work* work = m_trace_unsubmitted.load(std::memory_order_acquire); work != nullptr; work = work->traceNext)
    {
        m_walk[1].fetch_add(1, std::memory_order_relaxed);
        FixPacketReferences(work);
    }
    for (TraceScan* scan = m_scan_work.load(std::memory_order_acquire); scan != nullptr; scan = scan->next)
    {
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_MATURE_DEAD_SCAN_CONTROL)
        if (scan->complete.load(std::memory_order_acquire))
        {
            continue;
        }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_MATURE_DEAD_SCAN_CONTROL
        if (ResolveForwarded(scan->object) != scan->object)
        {
            return Fail(LxrRcResult::InvariantViolation);
        }
        scan->loaderAllocator = ResolveForwarded(scan->loaderAllocator);
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::ReleaseSetup()
{
    ReleaseStage& stage = m_release;
    stage.blocks = 0;
    stage.blockCursor.store(0, std::memory_order_relaxed);
    stage.chunks = nullptr;
    stage.chunkCount = 0;
    stage.chunkCursor.store(0, std::memory_order_relaxed);
    if (m_configuration.evacuateNursery || m_configuration.evacuateMature)
    {
        for (size_t worker = 0; worker < 2 * (m_configuration.workers + 1); worker++)
        {
            ImmixAllocationStatus status;
            LxrRcResult result = FromMetadata(m_copy_allocators[worker].Reset(&status));
            if (result != LxrRcResult::Success || status != ImmixAllocationStatus::Ready)
            {
                return Fail(LxrRcResult::AllocationFailure);
            }
        }
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_MATURE_EARLY_RELEASE_CONTROL)
        for (size_t block = 0; block < GetImmixCapacityBytes() / ImmixBlockGeometry::BlockBytes; block++)
        {
            if (m_evacuation_sources != nullptr &&
                (m_evacuation_sources[block].load() & RetainedSource) != 0)
            {
                if (FromMetadata(m_counts.ResetBlockQuiescent(
                        GetReservationStart() + block * ImmixBlockGeometry::BlockBytes)) != LxrRcResult::Success)
                {
                    return GetFailure();
                }
            }
        }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_MATURE_EARLY_RELEASE_CONTROL
        // The copy path logged every forwarded source with its size, so no walk over the heap is needed. The chunks of
        // the logs are handed to the packets of the stage one by one.
        size_t chunks = 0;
        for (size_t slot = 0; slot <= m_configuration.workers; slot++)
        {
            for (CopyLog::Chunk* chunk = m_copy_logs[slot].head; chunk != nullptr; chunk = chunk->next)
            {
                chunks++;
            }
        }
        if (chunks != 0)
        {
            void** list = new (std::nothrow) void*[chunks];
            if (list == nullptr)
            {
                return Fail(LxrRcResult::OutOfMemory);
            }
            size_t index = 0;
            for (size_t slot = 0; slot <= m_configuration.workers; slot++)
            {
                CopyLog& log = m_copy_logs[slot];
                for (CopyLog::Chunk* chunk = log.head; chunk != nullptr; chunk = chunk->next)
                {
                    list[index++] = chunk;
                }
                log.head = nullptr;
            }
            stage.chunks = list;
            stage.chunkCount = chunks;
        }
    }
    // Mutators are suspended, so nothing adds to the list while it is read.
    stage.blocks = m_allocated_count.exchange(0, std::memory_order_acq_rel);
    return LxrRcResult::Success;
}

void LxrReferenceCounting::DeleteReleaseChunks()
{
    ReleaseStage& stage = m_release;
    for (size_t index = 0; index < stage.chunkCount; index++)
    {
        delete static_cast<CopyLog::Chunk*>(stage.chunks[index]);
    }
    delete[] stage.chunks;
    stage.chunks = nullptr;
    stage.chunkCount = 0;
}

LxrRcResult LxrReferenceCounting::RetireSourceChunks()
{
    ReleaseStage& stage = m_release;
    LxrRcResult status = LxrRcResult::Success;
    while (status == LxrRcResult::Success && GetFailure() == LxrRcResult::Success)
    {
        size_t index = stage.chunkCursor.fetch_add(1, std::memory_order_acq_rel);
        if (index >= stage.chunkCount)
        {
            break;
        }
        CopyLog::Chunk* chunk = static_cast<CopyLog::Chunk*>(stage.chunks[index]);
        stage.chunks[index] = nullptr;
        status = RetireSourceChunk(chunk);
        delete chunk;
    }
    return status;
}

LxrRcResult LxrReferenceCounting::RetireSourceChunk(void* opaque)
{
    CopyLog::Chunk* chunk = static_cast<CopyLog::Chunk*>(opaque);
    uint64_t matureBytes = 0;
    uint64_t nurseryBytes = 0;
    LxrRcResult status = LxrRcResult::Success;
    // The same test as IsMatureCandidate, kept out of the loop: a pause that evacuates no mature block has no such source.
    bool mayHaveMature = m_evacuation_sources != nullptr &&
        !(GetTracePhase() == LxrTracePhase::Inactive && m_pause_kind == LxrPauseKind::RefCount);
    // The count plane of a source is in memory that the loop has not touched since the copy: it is prefetched Step entries
    // ahead.
    size_t cachedBlock = SIZE_MAX;
    bool cachedReleased = false;
    for (size_t entry = 0; entry < chunk->count && entry < LxrPrefetch::Step; entry++)
    {
        uint32_t shift;
        LxrPrefetch::Read(const_cast<const uint8_t*>(m_rc_metadata.LocateByte(chunk->entries[entry].start, &shift)));
    }
    for (size_t entry = 0; entry < chunk->count; entry++)
    {
        if (entry + LxrPrefetch::Step < chunk->count)
        {
            uint32_t shift;
            uintptr_t ahead = chunk->entries[entry + LxrPrefetch::Step].start;
            LxrPrefetch::Read(const_cast<const uint8_t*>(m_rc_metadata.LocateByte(ahead, &shift)));
        }
        uintptr_t start = chunk->entries[entry].start;
        size_t size = chunk->entries[entry].size;
        uintptr_t count;
        LxrRcResult result = FromMetadata(m_counts.LoadFast(start, &count));
        bool mature = mayHaveMature && IsMatureCandidate(start);
        if (result != LxrRcResult::Success || (count != 0 && !mature))
        {
            status = Fail(LxrRcResult::InvariantViolation);
            break;
        }
        if (mature)
        {
            result = FromMetadata(m_counts.ClearStraddleLinesFast(start, size));
            if (result == LxrRcResult::Success)
            {
                m_rc_metadata.Store(start, 0, SideMetadataMemoryOrder::SequentiallyConsistent);
                m_mature_candidates[(start - GetReservationStart()) / ImmixBlockGeometry::BlockBytes].store(
                    1, std::memory_order_release);
            }
            if (result != LxrRcResult::Success)
            {
                status = result;
                break;
            }
        }
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        OracleRetire(start);
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        if (mature)
        {
            matureBytes += size;
        }
        else
        {
            nurseryBytes += size;
        }
#ifndef LXR_S4_NO_NURSERY_RELEASE_CONTROL
        // A source in a block that the release of this stage empties needs no store: the release fills the ValidObject plane
        // of the block (ReleaseNurseryBatchQuiescent). The flag of the block says that the release has not started on it,
        // and the state of the block does not change in the stage, so the answer holds until the fill.
        if (!mature)
        {
            size_t blockIndex = (start - GetReservationStart()) / ImmixBlockGeometry::BlockBytes;
            if (blockIndex != cachedBlock)
            {
                cachedBlock = blockIndex;
                cachedReleased = false;
                if (m_allocated_block_storage.Get()[blockIndex].load(std::memory_order_acquire) != 0)
                {
                    result = FromMetadata(m_blocks.IsReleasableNurseryQuiescent(
                        start & ~static_cast<uintptr_t>(ImmixBlockGeometry::BlockMask), &cachedReleased));
                    if (result != LxrRcResult::Success)
                    {
                        status = result;
                        break;
                    }
                }
            }
            if (cachedReleased)
            {
                continue;
            }
        }
#endif // !LXR_S4_NO_NURSERY_RELEASE_CONTROL
#ifdef LXR_OBJECT_START_WALK
        // The walk variant has no ValidObject plane to clear. A nursery source is young: it has no count, and the span
        // that named it is gone at Resume. (The header of the source keeps its forwarding state; a reader finds out that
        // the value is not a source of the pause because no span of the pause starts an object there.)
        (void)cachedBlock;
        (void)cachedReleased;
#else
        m_valid_metadata.Store(start, 0, SideMetadataMemoryOrder::SequentiallyConsistent);
#endif // LXR_OBJECT_START_WALK
    }
    if (matureBytes != 0)
    {
        m_mature_metrics[10].fetch_add(matureBytes, std::memory_order_relaxed);
    }
    if (nurseryBytes != 0)
    {
        m_nursery_metrics[5].fetch_add(nurseryBytes, std::memory_order_relaxed);
    }
    return status;
}

LxrRcResult LxrReferenceCounting::ReserveCopyRecord(size_t slot)
{
    CopyLog& log = m_copy_logs[slot];
    if (slot == m_configuration.workers)
    {
        while (log.lock.test_and_set(std::memory_order_acquire))
        {
            YieldProcessor();
        }
    }
    LxrRcResult result = LxrRcResult::Success;
    if (log.head == nullptr || log.head->count == CopyLog::Chunk::Capacity)
    {
        CopyLog::Chunk* chunk = new (std::nothrow) CopyLog::Chunk;
        if (chunk == nullptr)
        {
            result = Fail(LxrRcResult::OutOfMemory);
        }
        else
        {
            chunk->next = log.head;
            log.head = chunk;
        }
    }
    if (slot == m_configuration.workers)
    {
        log.lock.clear(std::memory_order_release);
    }
    return result;
}

void LxrReferenceCounting::CommitCopyRecord(size_t slot, uintptr_t start, size_t size)
{
    CopyLog& log = m_copy_logs[slot];
    if (slot == m_configuration.workers)
    {
        while (log.lock.test_and_set(std::memory_order_acquire))
        {
            YieldProcessor();
        }
    }
    // ReserveCopyRecord left room in the head chunk. Only one thread appends to a worker slot.
    log.head->entries[log.head->count++] = {start, size};
    if (slot == m_configuration.workers)
    {
        log.lock.clear(std::memory_order_release);
    }
}

void LxrReferenceCounting::DeleteCopyLogs()
{
    if (m_copy_logs == nullptr)
    {
        return;
    }
    for (size_t slot = 0; slot <= m_configuration.workers; slot++)
    {
        CopyLog::Chunk* chunk = m_copy_logs[slot].head;
        while (chunk != nullptr)
        {
            CopyLog::Chunk* next = chunk->next;
            delete chunk;
            chunk = next;
        }
        m_copy_logs[slot].head = nullptr;
    }
}

size_t LxrReferenceCounting::ObjectSize(Object* object)
{
    return (object->GetSize() + sizeof(uintptr_t) - 1) & ~(sizeof(uintptr_t) - 1);
}

// The serial end of the release stage. The packet that finishes last runs it: the stage has retired every copied source
// and released every clean nursery block by then (RunReleasePacket).
LxrRcResult LxrReferenceCounting::ReleaseEpoch()
{
    LxrRcResult result = FinishNurseryRelease();
    if (result == LxrRcResult::Success)
    {
        LxrPhaseScope scope(m_pause_timers, LxrPauseTimers::SweepBlocks);
        result = SweepLargeNursery();
    }
    if (result == LxrRcResult::Success && m_configuration.evacuateMature &&
        (m_pause_kind == LxrPauseKind::Full || m_pause_kind == LxrPauseKind::FinalMark))
    {
        DeleteRememberedChunks();
        // The sources of the evacuation have lost their copied objects. A source that is dead now is released here,
        // as before; a source that stays loses its defrag mark.
        for (size_t block = 0; block < GetImmixCapacityBytes() / ImmixBlockGeometry::BlockBytes; block++)
        {
            if (m_evacuation_sources[block].load(std::memory_order_acquire) == 0)
            {
                continue;
            }
            uintptr_t address = GetReservationStart() + block * ImmixBlockGeometry::BlockBytes;
            ImmixBlockOperationStatus released;
            result = ReleaseBlockIfDead(address, block, nullptr, &released);
            if (result != LxrRcResult::Success)
            {
                break;
            }
            if (released == ImmixBlockOperationStatus::Updated)
            {
                m_mature_candidates[block].store(0, std::memory_order_release);
                continue;
            }
            if (m_evacuation_sources[block].exchange(0, std::memory_order_acq_rel) != 0)
            {
                result = FromMetadata(m_metadata.Store(LxrSideMetadataKind::BlockDefrag,
                    address, 0, SideMetadataMemoryOrder::SequentiallyConsistent));
                if (result != LxrRcResult::Success)
                {
                    break;
                }
            }
        }
    }
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    if (m_overshoot)
    {
        result = GiveBackSuspended();
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    m_current_roots ^= 1;
    m_root_counts[m_current_roots] = 0;
    ClearPins();
    ImmixBlockOperationStatus status;
    {
        LxrPhaseScope flip(m_pause_timers, LxrPauseTimers::ReleaseFlip);
        if (m_blocks.ReleaseGcPause(&status) != SideMetadataResult::Success ||
            status != ImmixBlockOperationStatus::Updated ||
            m_large.EndCollection() != LxrLargeObjectResult::Success)
        {
            return Fail(LxrRcResult::InvalidPhase);
        }
    }
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_F9_NO_PAUSE_RESET_CONTROL)
    m_allocation.ResetCursors();
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_F9_NO_PAUSE_RESET_CONTROL
    size_t bank = m_producer_bank.load(std::memory_order_relaxed) ^ 1;
    result = ResetBank(m_banks[bank]);
    if (result != LxrRcResult::Success)
    {
        return result;
    }
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_LOG_REUSE_CONTROL)
    bank ^= 1;
    result = ResetBank(m_banks[bank]);
    if (result != LxrRcResult::Success)
    {
        return result;
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_LOG_REUSE_CONTROL
    m_producer_bank.store(bank, std::memory_order_release);
    m_metrics[Epochs].fetch_add(1, std::memory_order_relaxed);
    m_epoch_fresh_blocks.store(m_allocation.GetFreshBlocksAcquired(), std::memory_order_release);
    m_epoch_recycled_lines.store(m_allocation.GetRecycledLinesConsumed(), std::memory_order_release);
    if (m_pause_kind == LxrPauseKind::InitialMark)
    {
        m_trace_phase.store(LxrTracePhase::Concurrent, std::memory_order_release);
        // The lazy decrements that start after this pause belong to the epoch before the snapshot (P46).
        m_first_window.store(true, std::memory_order_release);
        m_marking.store(true, std::memory_order_release);
        m_trace_metrics[InitialMarks].fetch_add(1, std::memory_order_relaxed);
    }
    else if (m_pause_kind == LxrPauseKind::FinalMark || m_pause_kind == LxrPauseKind::Full)
    {
        m_trace_metrics[m_pause_kind == LxrPauseKind::Full ? FullMarks : FinalMarks].fetch_add(
            1, std::memory_order_relaxed);
    }
    if (m_marking.load(std::memory_order_acquire) &&
        m_large.BeginCollection(true, false) != LxrLargeObjectResult::Success)
    {
        return Fail(LxrRcResult::InvalidPhase);
    }
    if (m_pause_timers.Enabled())
    {
        m_release_end_stamp.store(m_pause_timers.Stamp(), std::memory_order_relaxed);
    }
    return LxrRcResult::Success;
}

// A NurserySweep packet. The first one of a pause sets the stage up and submits as many packets of the same stage as the work
// needs; every packet then takes sources and blocks from the shared cursors. The packet that finishes last runs
// ReleaseEpoch. The stage has no other ordering: a retired source and a released block are independent (a source in a block
// that the stage releases is not touched; the release fills the ValidObject plane of the block).
void LxrReferenceCounting::RunReleasePacket()
{
    ReleaseStage& stage = m_release;
    if (!stage.started.exchange(true, std::memory_order_acq_rel))
    {
        stage.startStamp = m_pause_timers.Enabled() ? m_pause_timers.Stamp() : 0;
        {
            LxrPhaseScope scope(m_pause_timers, LxrPauseTimers::ReleaseSetup);
            ReleaseSetup();
        }
        stage.ready.store(true, std::memory_order_release);
    }
    else
    {
        // The packet that sets the stage up is running, so the wait is short.
        for (unsigned spins = 0; !stage.ready.load(std::memory_order_acquire); spins++)
        {
            if (spins < 64)
            {
                YieldProcessor();
            }
            else
            {
                GCToOSInterface::YieldThread(0);
            }
        }
    }
    if (GetFailure() == LxrRcResult::Success)
    {
        LxrPhaseScope scope(m_pause_timers, LxrPauseTimers::RetireSources);
        RetireSourceChunks();
    }
    {
        LxrPhaseScope scope(m_pause_timers, LxrPauseTimers::ReclaimNursery);
        while (GetFailure() == LxrRcResult::Success)
        {
            size_t begin = stage.blockCursor.fetch_add(ReleaseBlockRange, std::memory_order_acq_rel);
            if (begin >= stage.blocks)
            {
                break;
            }
            size_t end = begin + ReleaseBlockRange < stage.blocks ? begin + ReleaseBlockRange : stage.blocks;
            LxrRcResult result = ReleaseNurseryRange(begin, end);
            if (result != LxrRcResult::Success)
            {
                Fail(result);
            }
        }
    }
    if (stage.active.fetch_sub(1, std::memory_order_acq_rel) != 1)
    {
        return;
    }
    // Every other packet of the stage has finished. A chunk that is still in the list was not processed (a failure).
    DeleteReleaseChunks();
    if (GetFailure() == LxrRcResult::Success)
    {
        LxrPhaseScope scope(m_pause_timers, LxrPauseTimers::ReleaseTail);
        ReleaseEpoch();
    }
    if (stage.startStamp != 0)
    {
        m_pause_timers.Add(LxrPauseTimers::ReleaseEpoch, m_pause_timers.Stamp() - stage.startStamp);
    }
    stage.ready.store(false, std::memory_order_relaxed);
    stage.started.store(false, std::memory_order_release);
}

// PrepareWork calls it for the release stage. All the packets are in the stage before it opens, so that the scheduler wakes
// the workers at once (one wake-up for the stage, not one for each packet that a running packet submits). A small nursery
// needs one packet only; the packets of a large one find the sources and the blocks through shared cursors.
LxrRcResult LxrReferenceCounting::SubmitReleasePackets()
{
    ReleaseStage& stage = m_release;
    size_t packets = m_allocated_count.load(std::memory_order_acquire) / ReleaseBlocksPerPacket;
    if (packets > m_configuration.workers + 1)
    {
        packets = m_configuration.workers + 1;
    }
    if (packets == 0)
    {
        packets = 1;
    }
    stage.started.store(false, std::memory_order_relaxed);
    stage.ready.store(false, std::memory_order_relaxed);
    stage.active.store(packets, std::memory_order_release);
    stage.packets.store(packets, std::memory_order_relaxed);
    for (size_t packet = 0; packet < packets; packet++)
    {
        LxrRcResult result = SubmitWork(NewWork(WorkKind::NurserySweep, nullptr, 0, LxrWorkBucketStage::Release));
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    return LxrRcResult::Success;
}

namespace
{
    // The worker index of the packet that this thread runs now (the pause thread has the index `workers`), or SIZE_MAX when
    // the thread runs no packet. The copy of a mature object takes the copy allocator and the copy log of this worker.
    thread_local size_t t_execution_worker = SIZE_MAX;

    class ExecutionScope final
    {
    public:
        explicit ExecutionScope(size_t worker) : m_saved(t_execution_worker) { t_execution_worker = worker; }
        ~ExecutionScope() { t_execution_worker = m_saved; }
        ExecutionScope(const ExecutionScope&) = delete;
        ExecutionScope& operator=(const ExecutionScope&) = delete;

    private:
        size_t m_saved;
    };
}

size_t LxrReferenceCounting::GetExecutionWorker() const
{
    return t_execution_worker <= m_configuration.workers ? t_execution_worker : m_configuration.workers;
}

void LxrReferenceCounting::Execute(LxrWorkPacket* packet, size_t worker, void* context)
{
    Work* work = static_cast<Work*>(context);
    LxrReferenceCounting* engine = work->engine;
    if (engine->GetFailure() != LxrRcResult::Success)
    {
        return;
    }
    ExecutionScope executionScope(worker);
    LxrPauseTimers::Group group = LxrPauseTimers::MatureSweep;
    bool timed = true;
    switch (work->kind)
    {
        case WorkKind::Slots:
        case WorkKind::NurserySlots:
        case WorkKind::Roots:
        case WorkKind::Targets: group = LxrPauseTimers::Increments; break;
        case WorkKind::Decrements:
        case WorkKind::PreviousRoots: group = LxrPauseTimers::Decrements; break;
        case WorkKind::Satb: group = LxrPauseTimers::Satb; break;
        case WorkKind::TraceObjects:
        case WorkKind::TraceScan:
        case WorkKind::ReplayRemembered:
            // The trace packets of a pause that runs on all workers are timed: busy time over span shows the use of the workers.
            group = LxrPauseTimers::TracePackets;
            timed = engine->m_pause_trace_open.load(std::memory_order_relaxed);
            break;
        case WorkKind::MarkClear: timed = false; break;
        case WorkKind::SelectMature: timed = false; break;
        case WorkKind::PrepareTrace: group = LxrPauseTimers::PrepareTrace; break;
        case WorkKind::WeakClosure: group = LxrPauseTimers::WeakClosure; break;
        case WorkKind::EpochClosure: group = LxrPauseTimers::EpochClosure; break;
        case WorkKind::NurserySweep: group = LxrPauseTimers::NurserySweep; break;
        default: group = LxrPauseTimers::MatureSweep; break;
    }
    PacketScope packetTimer(engine->m_pause_timers, group, timed);
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    if (work->kind == WorkKind::Decrements || work->kind == WorkKind::PreviousRoots || work->kind == WorkKind::MatureSweep)
    {
        uint32_t delay = engine->m_lazy_delay_us.load(std::memory_order_acquire);
        if (delay != 0 && packet->GetStage() == LxrWorkBucketStage::Unconstrained)
        {
            std::this_thread::sleep_for(std::chrono::microseconds(delay));
        }
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    switch (work->kind)
    {
        case WorkKind::Slots:
        case WorkKind::NurserySlots:
        case WorkKind::Roots:
        case WorkKind::Targets:
        {
            engine->ProcessIncrementWork(work, worker);
            break;
        }
        case WorkKind::Satb:
            engine->m_trace_metrics[SatbEntries].fetch_add(
                packet->GetReferenceCount(), std::memory_order_relaxed);
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_TRACE_SKIP_SATB_CONTROL)
            engine->QueueTrace(work->references, packet->GetReferenceCount(), nullptr, true);
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_TRACE_SKIP_SATB_CONTROL
            break;
        case WorkKind::PrepareTrace:
        {
            LxrRcResult result = LxrRcResult::Success;
            {
                LxrPhaseScope setup(engine->m_pause_timers, LxrPauseTimers::TraceSetup);
                result = engine->FixForwardingReaders();
                if (result == LxrRcResult::Success && engine->m_object_model.finishRelocation != nullptr)
                {
                    result = engine->m_object_model.finishRelocation(engine, engine->m_object_model.context);
                }
                if (result == LxrRcResult::Success && engine->m_pause_kind != LxrPauseKind::RefCount)
                {
                    if (engine->m_configuration.evacuateMature &&
                        (engine->m_pause_kind == LxrPauseKind::Full || engine->m_pause_kind == LxrPauseKind::FinalMark))
                    {
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
                        result = engine->ReclaimLargeNursery();
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
                        size_t released = 0;
                        if (result == LxrRcResult::Success &&
                            engine->m_large.SweepNursery(&released) != LxrLargeObjectResult::Success)
                        {
                            result = engine->Fail(LxrRcResult::MetadataFailure);
                        }
                        engine->m_metrics[LargeObjectBytesReleased].fetch_add(released, std::memory_order_relaxed);
                    }
                    if (result == LxrRcResult::Success &&
                        (engine->m_pause_kind == LxrPauseKind::Full || engine->m_pause_kind == LxrPauseKind::FinalMark))
                    {
                        result = engine->RetainPinnedMatureBlocks();
                    }
                }
            }
            if (result == LxrRcResult::Success && engine->m_pause_kind != LxrPauseKind::RefCount)
            {
                LxrPhaseScope roots(engine->m_pause_timers, LxrPauseTimers::TraceRoots);
                // A FinalMark or Full pause traces on all workers; the stage is open until the replay has ended.
                engine->OpenPauseTrace(&result);
                if (result == LxrRcResult::Success)
                {
                    result = engine->PrepareTrace();
                }
            }
            if (result == LxrRcResult::Success && engine->m_pause_kind == LxrPauseKind::FinalMark)
            {
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_REMSET_REPLAY_CONTROL)
                LxrPhaseScope replay(engine->m_pause_timers, LxrPauseTimers::TraceReplay);
                result = engine->ReplayMatureRememberedSlots();
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_REMSET_REPLAY_CONTROL
            }
            engine->ClosePauseTrace();
            {
                LxrPhaseScope fixup(engine->m_pause_timers, LxrPauseTimers::TraceFixup);
                // The relocation above is complete for a RefCount pause: nothing between the two passes copies or moves an
                // object there, and both passes are idempotent, so the second walk would find nothing to rewrite.
                if (result == LxrRcResult::Success && engine->m_pause_kind != LxrPauseKind::RefCount)
                {
                    result = engine->FixForwardingReaders();
                }
                if (result == LxrRcResult::Success && engine->m_pause_kind != LxrPauseKind::RefCount &&
                    engine->m_object_model.finishRelocation != nullptr)
                {
                    result = engine->m_object_model.finishRelocation(engine, engine->m_object_model.context);
                }
            }
            if (result != LxrRcResult::Success)
            {
                engine->Fail(result);
            }
            break;
        }
        case WorkKind::WeakClosure:
            if (engine->m_object_model.finishTrace != nullptr)
            {
                LxrRcResult result = engine->m_object_model.finishTrace(
                    engine, engine->m_object_model.context);
                if (result != LxrRcResult::Success)
                {
                    engine->Fail(result);
                }
            }
            if (engine->GetFailure() == LxrRcResult::Success)
            {
                LxrRcResult result = engine->FixForwardingReaders();
                if (result == LxrRcResult::Success && engine->m_object_model.finishRelocation != nullptr)
                {
                    result = engine->m_object_model.finishRelocation(engine, engine->m_object_model.context);
                }
                if (result != LxrRcResult::Success)
                {
                    engine->Fail(result);
                }
            }
            break;
        case WorkKind::TraceObjects:
        case WorkKind::TraceScan:
            engine->ProcessTraceWork(work);
            // The scheduler is still busy with the packet, so the memory is freed at the next Suspend, not here.
            engine->RetireTraceWork(work);
            break;
        case WorkKind::ReplayRemembered:
            engine->ProcessReplayWork(work);
            engine->RetireTraceWork(work);
            break;
        case WorkKind::EpochClosure:
        {
            LxrRcResult result = LxrRcResult::Success;
            if (engine->m_object_model.finishEpoch != nullptr)
            {
                result = engine->m_object_model.finishEpoch(engine, engine->m_object_model.context);
            }
            if (result != LxrRcResult::Success)
            {
                engine->Fail(result);
            }
            break;
        }
        case WorkKind::Decrements:
        case WorkKind::PreviousRoots:
            engine->ProcessDecrements(work, packet, worker);
            break;
        case WorkKind::NurserySweep:
            engine->RunReleasePacket();
            break;
        case WorkKind::MatureSweep:
        {
            LxrBlockReleaseGuard guard;
            if (packet->CreateBlockReleaseGuard(&guard) != LxrCompletionResult::Success ||
                !guard.IsValid() || guard.GetGeneration() != packet->GetGeneration())
            {
                engine->Fail(LxrRcResult::InvariantViolation);
                return;
            }
            if (engine->m_configuration.parallelSweep)
            {
                engine->RunSweepPacket(packet, guard);
                break;
            }
            engine->Sweep(guard);
            break;
        }
        case WorkKind::MarkClear:
            engine->RunMarkClearPacket();
            break;
        case WorkKind::SelectMature:
        {
            SelectionRun* run = engine->m_select_run.load(std::memory_order_acquire);
            if (run != nullptr)
            {
                engine->RunSelectionLoop(run);
            }
            break;
        }
    }
}

LxrRcResult LxrReferenceCounting::ProcessIncrementWork(Work* work, size_t worker)
{
    ReferenceBatch children{this, WorkKind::NurserySlots, nullptr, {}};
    for (size_t index = 0; index < work->packet.GetReferenceCount(); index++)
    {
        Object* object;
        Object* original = nullptr;
        Object** ownedSlot = nullptr;
        bool trace = work->trace;
        if (work->kind == WorkKind::Roots || work->kind == WorkKind::Targets)
        {
            uintptr_t reference = work->GetReference(index);
            if (work->kind == WorkKind::Roots)
            {
                trace = (reference & 1) == 0;
                reference &= ~static_cast<uintptr_t>(1);
            }
            object = reinterpret_cast<Object*>(reference);
            if (work->kind == WorkKind::Roots)
            {
                CountWorker(worker, RootIncrements, 1);
            }
        }
        else
        {
            Object** slot = reinterpret_cast<Object**>(work->GetReference(index));
            ownedSlot = slot;
            object = *slot;
            original = object;
            if (work->kind == WorkKind::Slots)
            {
                m_unlogged_metadata.Store(
                    reinterpret_cast<uintptr_t>(slot), 1, SideMetadataMemoryOrder::Relaxed);
            }
            CountWorker(worker, IncrementEntries, 1);
        }
        LxrRcResult result = ProcessIncrement(&object, trace, &children, worker);
        if (result != LxrRcResult::Success)
        {
            return result;
        }
        if (ownedSlot != nullptr && object != nullptr)
        {
            result = RememberMatureSlot(ownedSlot, object);
            if (result != LxrRcResult::Success)
            {
                return result;
            }
        }
        if (ownedSlot != nullptr && object != original)
        {
            // Only a forwarded target changes the slot; an unchanged slot stays clean (rc.rs: process_slot).
            *ownedSlot = object;
        }
        // The Java LXR prefetches the object of the entry that is 8 ahead (rc.rs: process_incs).
        LxrPrefetch::Ahead(
            work->references, work->packet.GetReferenceCount(), index, ownedSlot != nullptr,
            work->kind == WorkKind::Roots ? 1 : 0);
    }
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F4_NO_RECURSION_CONTROL)
    // Control: the behaviour before P25 (F4). The children of the packet become a new packet: one packet for each level.
    return children.Flush();
#else
    return ProcessChildIncrements(&children, worker);
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F4_NO_RECURSION_CONTROL
}

// P25 (F4). The increments of the children of the counted objects run in the packet that counted their parents, one level
// after the other (Java LXR: rc.rs ProcessIncs::do_work, the loop over new_incs). A chain of 63 young nodes costs one packet
// and no hand-off. A buffer that fills up (1 024 slots) still becomes a new packet (ReferenceBatch::Append), so a wide
// object gives work to the other workers.
LxrRcResult LxrReferenceCounting::ProcessChildIncrements(ReferenceBatch* children, size_t worker)
{
    uintptr_t slots[BufferCapacity];
    while (children->count != 0)
    {
        size_t count = children->count;
        bool trace = children->trace;
        memcpy(slots, children->references, count * sizeof(uintptr_t));
        children->count = 0;
        for (size_t index = 0; index < count; index++)
        {
            Object** slot = reinterpret_cast<Object**>(slots[index]);
            Object* object = *slot;
            Object* original = object;
            LxrRcResult result = ProcessIncrement(&object, trace, children, worker);
            if (result != LxrRcResult::Success)
            {
                CountWorker(worker, IncrementEntries, index + 1);
                return result;
            }
            if (object != nullptr)
            {
                result = RememberMatureSlot(slot, object);
                if (result != LxrRcResult::Success)
                {
                    CountWorker(worker, IncrementEntries, index + 1);
                    return result;
                }
            }
            if (object != original)
            {
                *slot = object;
            }
            LxrPrefetch::Ahead(slots, count, index, true);
        }
        CountWorker(worker, IncrementEntries, count);
    }
    return LxrRcResult::Success;
}

LxrCompletionCallbackResult LxrReferenceCounting::DecrementsDrained(
    LxrMatureSweepContinuation* continuation, void* context)
{
    LxrReferenceCounting* engine = static_cast<LxrReferenceCounting*>(context);
    if (engine->GetFailure() != LxrRcResult::Success)
    {
        return LxrCompletionCallbackResult::Failure;
    }
    if (engine->m_pause_timers.Enabled())
    {
        engine->m_lazy_sweep_stamp.store(engine->m_pause_timers.Stamp(), std::memory_order_relaxed);
    }
    if (engine->m_configuration.parallelSweep)
    {
        return engine->BeginParallelSweep(continuation) == LxrRcResult::Success
            ? LxrCompletionCallbackResult::Success : LxrCompletionCallbackResult::Failure;
    }
    Work* work = engine->NewWork(
        WorkKind::MatureSweep, nullptr, 0, LxrWorkBucketStage::Unconstrained);
    if (work == nullptr ||
        engine->FromScheduler(engine->m_scheduler.SubmitMatureSweep(continuation, &work->packet)) !=
            LxrRcResult::Success)
    {
        return LxrCompletionCallbackResult::Failure;
    }
    return LxrCompletionCallbackResult::Success;
}

LxrCompletionCallbackResult LxrReferenceCounting::OverallDrained(uint64_t, void* context)
{
    LxrReferenceCounting* engine = static_cast<LxrReferenceCounting*>(context);
    if (engine->m_pause_timers.Enabled())
    {
        int64_t now = engine->m_pause_timers.Stamp();
        int64_t start = engine->m_lazy_start_stamp.load(std::memory_order_relaxed);
        int64_t sweep = engine->m_lazy_sweep_stamp.load(std::memory_order_relaxed);
        if (start != 0 && now > start)
        {
            uint64_t window = TicksToNanoseconds(now - start);
            engine->m_lazy_metrics[LazyWindows].fetch_add(1, std::memory_order_relaxed);
            engine->m_lazy_metrics[LazyWindowNs].fetch_add(window, std::memory_order_relaxed);
            uint64_t high = engine->m_lazy_metrics[LazyWindowMaxNs].load(std::memory_order_relaxed);
            while (high < window && !engine->m_lazy_metrics[LazyWindowMaxNs].compare_exchange_weak(
                high, window, std::memory_order_relaxed))
            {
            }
        }
        if (sweep != 0 && now > sweep)
        {
            uint64_t span = TicksToNanoseconds(now - sweep);
            engine->m_lazy_metrics[LazySweepNs].fetch_add(span, std::memory_order_relaxed);
            uint64_t high = engine->m_lazy_metrics[LazySweepMaxNs].load(std::memory_order_relaxed);
            while (high < span && !engine->m_lazy_metrics[LazySweepMaxNs].compare_exchange_weak(
                high, span, std::memory_order_relaxed))
            {
            }
        }
        engine->m_lazy_start_stamp.store(0, std::memory_order_relaxed);
        engine->m_lazy_sweep_stamp.store(0, std::memory_order_relaxed);
    }
    if (engine->m_cycle_sweep.load(std::memory_order_acquire))
    {
        engine->m_trace_phase.store(LxrTracePhase::Inactive, std::memory_order_release);
        // The sweep has read every dead object it needed. Only now may the runtime free type data.
        engine->m_cycle_sweep.store(false, std::memory_order_release);
    }
    if (engine->m_object_model.finishLazy != nullptr)
    {
        LxrRcResult result = engine->m_object_model.finishLazy(engine, engine->m_object_model.context);
        if (result != LxrRcResult::Success)
        {
            engine->Fail(result);
        }
    }
    return engine->GetFailure() == LxrRcResult::Success
        ? LxrCompletionCallbackResult::Success : LxrCompletionCallbackResult::Failure;
}

LxrRcResult LxrReferenceCounting::LoadCount(Object* object, uintptr_t* count) const
{
    if (object == nullptr || count == nullptr)
    {
        return LxrRcResult::InvalidArgument;
    }
    SideMetadataResult result = m_metadata.Load(
        LxrSideMetadataKind::ReferenceCount, Start(object), SideMetadataMemoryOrder::Relaxed, count);
    return result == SideMetadataResult::Success ? LxrRcResult::Success : LxrRcResult::MetadataFailure;
}

static int64_t LookupClock()
{
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    return std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::steady_clock::now().time_since_epoch()).count();
#else
    return minipal_hires_ticks();
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
}

LxrRcResult LxrReferenceCounting::FindContainingObject(uintptr_t address, Object** object, bool requireCounted) const
{
    int64_t begin = LookupClock();
    LxrRcResult result = FindContainingObjectCore(address, object, requireCounted);
    m_lookup_metrics[0].fetch_add(1, std::memory_order_relaxed);
    m_lookup_metrics[1].fetch_add(static_cast<uint64_t>(LookupClock() - begin), std::memory_order_relaxed);
    return result;
}

LxrRcResult LxrReferenceCounting::FindContainingObjectCore(uintptr_t address, Object** object, bool requireCounted) const
{
    if (object == nullptr || (!IsImmix(address) && !IsLarge(address)))
    {
        return LxrRcResult::InvalidArgument;
    }
    *object = nullptr;
    if (IsLarge(address))
    {
        LxrLargeObjectAllocation allocation;
        LxrLargeObjectResult result = m_large.GetContainingAllocation(address, &allocation);
        if (result == LxrLargeObjectResult::NotFound)
        {
            return LxrRcResult::InvalidArgument;
        }
        if (result != LxrLargeObjectResult::Success)
        {
            return LxrRcResult::MetadataFailure;
        }
        Object* candidate = Reference(allocation.object);
#ifdef LXR_OBJECT_START_WALK
        if (address < reinterpret_cast<uintptr_t>(candidate))
#else
        if (address < reinterpret_cast<uintptr_t>(candidate) || !IsObjectStart(candidate))
#endif // LXR_OBJECT_START_WALK
        {
            return LxrRcResult::InvalidArgument;
        }
        *object = candidate;
        return LxrRcResult::Success;
    }
    uintptr_t lower = IsImmix(address)
        ? address & ~ImmixBlockGeometry::BlockMask : m_large.GetReservationStart();
    uintptr_t upper = lower + ImmixBlockGeometry::BlockBytes;
    if (m_young_spans.Size() != 0
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_D2_NO_SPAN_LOOKUP_CONTROL)
        && false
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_D2_NO_SPAN_LOOKUP_CONTROL
        &&
        FindInYoungSpans(address, object, requireCounted) == LxrRcResult::Success)
    {
        m_lookup_metrics[2].fetch_add(1, std::memory_order_relaxed);
        return LxrRcResult::Success;
    }
#ifdef LXR_OBJECT_START_WALK
    // The walk variant has no ValidObject plane. The nearest counted start at or below the address is the start of the
    // object that contains it: the count of a dead object is cleared when the object dies (F9), an interior slot of a live
    // object has no count, and the first slot of each continuation line of a long object is skipped (FindCountedSlotBelow).
    // A young object has no count: an address inside it is found above, in the spans of the pause.
    {
        uintptr_t arena = GetReservationStart();
        size_t found = FindCountedSlotBelow(
            (lower - arena) / sizeof(uintptr_t), (address - arena) / sizeof(uintptr_t));
        if (found == SIZE_MAX)
        {
            return LxrRcResult::InvalidArgument;
        }
        uintptr_t cursor = arena + found * sizeof(uintptr_t);
        Object* candidate = Reference(cursor);
        // A forwarded source has its first field overwritten by the forwarding pointer, so an array or string
        // length is lost. The copy has the same size.
        Object* sizeSource = ResolveForwarded(candidate);
        if (sizeSource != candidate && !IsObjectStart(sizeSource))
        {
            return LxrRcResult::InvalidArgument;
        }
        size_t size = ObjectSize(sizeSource);
        if (size < MIN_OBJECT_SIZE || size > upper - cursor)
        {
            return LxrRcResult::InvalidArgument;
        }
#if defined(LXR_WALK_NO_EXTENT_CONTROL)
        // Control: the candidate is accepted without asking whether it contains the address.
        if (address >= reinterpret_cast<uintptr_t>(candidate))
#else
        if (address >= reinterpret_cast<uintptr_t>(candidate) && address - cursor < size)
#endif // LXR_WALK_NO_EXTENT_CONTROL
        {
            *object = candidate;
            return LxrRcResult::Success;
        }
        return LxrRcResult::InvalidArgument;
    }
#else
    uintptr_t cursor = address & ~(sizeof(uintptr_t) - 1);
    // The nearest ValidObject bit at or below the address is the start of the object that contains it. An object
    // range is cleared (ValidObject and counts) before an allocation or a copy reuses it, and only the start of a
    // promoted object is set afterwards, so no stale bit lies between the start of a live object and an address
    // inside it. The size comes from the object, which is intact: it is live. A stale bit of a dead object can be
    // the nearest bit only for an address outside every live object; the checks below turn most such addresses into
    // an error, but the result is not defined for an address that no live object contains. A young object has no
    // bit: an address inside it is found above, in the spans of the pause.
    while (cursor >= lower)
    {
        // The address is inside the admitted Immix space and the scan stays in its block.
        if (m_valid_metadata.Load(cursor, SideMetadataMemoryOrder::Relaxed) != 0)
        {
            if (requireCounted && m_rc_metadata.Load(cursor, SideMetadataMemoryOrder::Relaxed) == 0)
            {
                return LxrRcResult::InvalidArgument;
            }
            Object* candidate = Reference(cursor);
            // A forwarded source has its first field overwritten by the forwarding pointer, so an array or string
            // length is lost. The copy has the same size.
            Object* sizeSource = ResolveForwarded(candidate);
            if (sizeSource != candidate && !IsObjectStart(sizeSource))
            {
                return LxrRcResult::InvalidArgument;
            }
            size_t size = ObjectSize(sizeSource);
            if (size < MIN_OBJECT_SIZE || size > upper - cursor)
            {
                return LxrRcResult::InvalidArgument;
            }
            if (address >= reinterpret_cast<uintptr_t>(candidate) && address - cursor < size)
            {
                *object = candidate;
                return LxrRcResult::Success;
            }
            return LxrRcResult::InvalidArgument;
        }
        if (cursor == lower)
        {
            break;
        }
        cursor -= sizeof(uintptr_t);
    }
    return LxrRcResult::InvalidArgument;
#endif // LXR_OBJECT_START_WALK
}

LxrRcMetrics LxrReferenceCounting::GetMetrics() const
{
    const uint64_t loggedSlots = m_metrics[LoggedSlots].load() + SumMutatorCounter(&LxrRcMutator::loggedSlots);
    const uint64_t oldTargetChecks =
        m_metrics[OldTargetChecks].load() + SumMutatorCounter(&LxrRcMutator::oldTargetChecks);
    return {
        m_metrics[Allocations].load(), m_metrics[AllocatedBytes].load(),
        loggedSlots, SumWorker(IncrementEntries),
        SumWorker(DecrementEntries), m_metrics[BufferFlushes].load(),
        m_metrics[ThreadFlushes].load(), SumWorker(Promotions),
        SumWorker(Deaths), SumWorker(RootIncrements),
        SumWorker(RootDecrements), SumWorker(ObjectScans),
        SumWorker(ObjectSizeQueries), oldTargetChecks,
        m_metrics[ChildRegistrations].load(), m_metrics[BlocksReleased].load(),
        m_metrics[LargeObjectBytesReleased].load(), m_metrics[Epochs].load(),
    };
}

LxrRcResult LxrReferenceCounting::GetFailure() const
{
    return m_failure.load(std::memory_order_acquire);
}

LxrRcPhase LxrReferenceCounting::GetPhase() const
{
    return m_phase.load(std::memory_order_acquire);
}

uintptr_t LxrReferenceCounting::GetFieldMetadataBase() const
{
    return m_metadata.GetBiasedBase(LxrSideMetadataKind::FieldUnlogged);
}

void LxrReferenceCounting::GetReferenceCountPlane(
    uintptr_t* base, uint8_t* logBytesPerValue, uint8_t* logBitsPerValue) const
{
    *base = m_rc_metadata.IsValid() ? m_rc_metadata.GetBaseAddress() : 0;
    *logBytesPerValue = m_rc_metadata.GetLogBytesPerValue();
    *logBitsPerValue = m_rc_metadata.GetLogBitsPerValue();
}

LxrRcResult LxrReferenceCounting::Reclaim(uintptr_t start)
{
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    LxrRcResult result = OracleReclaim(start);
    if (result != LxrRcResult::Success)
    {
        return result;
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
#ifdef LXR_HAS_LIFETIME_PROBE
    NoteProbeReclaim(start);
#else
    (void)start;
#endif // LXR_HAS_LIFETIME_PROBE
    return LxrRcResult::Success;
}

#ifdef LXR_HAS_LIFETIME_PROBE
void LxrReferenceCounting::NoteProbeReclaim(uintptr_t start)
{
    // The product has no per-object record of a young death, so the probe is also checked once per pause
    // (ClearAllocatedBlocks). The flag makes the two sources count one retirement per birth.
    if (m_probe_start.load(std::memory_order_acquire) == start &&
        !m_probe_retired.exchange(true, std::memory_order_acq_rel))
    {
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_PROBE_SKIP_RECLAIM_CONTROL)
        m_probe_reclaims.fetch_add(1, std::memory_order_release);
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_PROBE_SKIP_RECLAIM_CONTROL
    }
}
#endif // LXR_HAS_LIFETIME_PROBE

#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
void LxrReferenceCounting::OracleBirth(uintptr_t start, size_t size)
{
    size_t index = (start - GetReservationStart()) / sizeof(uintptr_t);
    ExtentSlot(index).store(size, std::memory_order_release);
    m_live_objects[index / 64].fetch_or(UINT64_C(1) << (index % 64), std::memory_order_release);
    m_live_bytes.fetch_add(size, std::memory_order_relaxed);
}

LxrRcResult LxrReferenceCounting::OracleReclaim(uintptr_t start)
{
    size_t index = (start - GetReservationStart()) / sizeof(uintptr_t);
    size_t size = ExtentSlot(index).exchange(0, std::memory_order_acq_rel);
    if (size == 0)
    {
        return Fail(LxrRcResult::InvariantViolation);
    }
    m_live_objects[index / 64].fetch_and(~(UINT64_C(1) << (index % 64)), std::memory_order_acq_rel);
    m_live_bytes.fetch_sub(size, std::memory_order_relaxed);
    m_reclaimed_bytes.fetch_add(size, std::memory_order_relaxed);
    return LxrRcResult::Success;
}

void LxrReferenceCounting::OracleRetire(uintptr_t start)
{
    size_t index = (start - GetReservationStart()) / sizeof(uintptr_t);
    size_t size = ExtentSlot(index).exchange(0, std::memory_order_acq_rel);
    m_live_objects[index / 64].fetch_and(~(UINT64_C(1) << (index % 64)), std::memory_order_acq_rel);
    m_live_bytes.fetch_sub(size, std::memory_order_relaxed);
}

size_t LxrReferenceCounting::GetObjectExtent(Object* object) const
{
    uintptr_t start = Start(object);
    if (!OwnsAddress(start) || start % sizeof(uintptr_t) != 0)
    {
        return 0;
    }
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_OBJECT_INTERIOR_CONTROL)
    if (IsLarge(start) && start % LxrLargeObjectSpace::PageBytes != 0)
    {
        return 0;
    }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_OBJECT_INTERIOR_CONTROL
    return ExtentSlot((start - GetReservationStart()) / sizeof(uintptr_t)).load(std::memory_order_acquire);
}

size_t LxrReferenceCounting::FindNextOracleSlot(size_t first) const
{
    size_t total = GetReservationBytes() / sizeof(uintptr_t);
    size_t immixEnd = GetImmixCapacityBytes() / sizeof(uintptr_t);
    size_t largeStart = m_configuration.immixBytes / sizeof(uintptr_t);
    size_t largeEnd = largeStart + m_large.GetAdmittedBytes() / sizeof(uintptr_t);
    while (first < largeEnd)
    {
        if (first >= immixEnd && first < largeStart)
        {
            first = largeStart;
        }
        uint64_t bits = m_live_objects[first / 64].load(std::memory_order_acquire) &
            (UINT64_MAX << (first % 64));
#ifdef TARGET_WINDOWS
        DWORD bit;
#else
        uint32_t bit;
#endif // TARGET_WINDOWS
        if (BitScanForward64(&bit, bits))
        {
            return first / 64 * 64 + bit;
        }
        first = (first / 64 + 1) * 64;
    }
    return total;
}
#endif // LXR_SIDE_METADATA_TEST_PLATFORM

LxrRcResult LxrReferenceCounting::ArmLifetimeProbe(Object* object)
{
#ifdef LXR_HAS_LIFETIME_PROBE
    if (GetPhase() != LxrRcPhase::Mutator || !IsOwnedObject(object))
    {
        return LxrRcResult::InvalidArgument;
    }
    uintptr_t expected = 0;
    if (!m_probe_start.compare_exchange_strong(expected, Start(object), std::memory_order_release))
    {
        return LxrRcResult::InvalidPhase;
    }
    m_probe_retired.store(false, std::memory_order_release);
    return LxrRcResult::Success;
#else
    (void)object;
    return LxrRcResult::UnsupportedLifetime;
#endif // LXR_HAS_LIFETIME_PROBE
}

uint64_t LxrReferenceCounting::GetLifetimeProbeBirths() const
{
#ifdef LXR_HAS_LIFETIME_PROBE
    return m_probe_births.load(std::memory_order_acquire);
#else
    return 0;
#endif // LXR_HAS_LIFETIME_PROBE
}

uint64_t LxrReferenceCounting::GetLifetimeProbeReclaims() const
{
#ifdef LXR_HAS_LIFETIME_PROBE
    return m_probe_reclaims.load(std::memory_order_acquire);
#else
    return 0;
#endif // LXR_HAS_LIFETIME_PROBE
}

uint64_t LxrReferenceCounting::GetStorageMetric(size_t metric) const
{
    if (metric == 57)
    {
        return m_increment_traces.load(std::memory_order_acquire);
    }
    if (metric >= 52 && metric < 57)
    {
        return m_storage_diagnostics.load(std::memory_order_acquire)
            ? m_trace_batch_metrics[metric - 52].load(std::memory_order_acquire) : UINT64_MAX;
    }
    if (metric >= 28 && metric < 52 && m_banks != nullptr)
    {
        size_t index = (metric - 28) / 6;
        const LogBank& bank = m_banks[index / 2];
        const LogStream& stream = index % 2 == 0 ? bank.increments : bank.decrements;
        switch ((metric - 28) % 6)
        {
            case 0: return stream.capacity;
            case 1: return stream.reserved.load(std::memory_order_acquire);
            case 2: return m_storage_diagnostics.load() ? stream.highWater.load() : UINT64_MAX;
            case 3: return m_storage_diagnostics.load() ? stream.flushes.load() : UINT64_MAX;
            case 4: return m_storage_diagnostics.load() ? stream.copiedBytes.load() : UINT64_MAX;
            case 5: return m_storage_diagnostics.load() ? stream.resetBytes.load() : UINT64_MAX;
        }
    }
    if (metric < 7)
    {
        return m_storage_metrics[metric].load(std::memory_order_acquire);
    }
    switch (metric)
    {
        // Components 7 and 10 were the size table and the live-object bitmap. P17 S3 removed them from the product;
        // the test platform keeps them as an oracle.
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        case 7: return m_extent_storage.GetCommittedBytes();
#else
        case 7: return 0;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        case 8: return 0;
        case 9: return 0;
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        case 10: return m_live_storage.GetCommittedBytes();
#else
        case 10: return 0;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        case 23: return m_storage_metrics[7].load(std::memory_order_acquire);
        case 24: return m_storage_metrics[8].load(std::memory_order_acquire);
        case 25: return 2 * m_configuration.maximumRoots * sizeof(uintptr_t);
        case 26: return m_configuration.maximumMutators * (sizeof(LxrRcMutator) + 2 * sizeof(LogChunk));
        case 27: return m_allocated_block_storage.GetCommittedBytes();
        default: break;
    }
    size_t streamMetric = metric - 11;
    if (streamMetric < 12 && m_banks != nullptr)
    {
        if (streamMetric % 6 >= 4)
        {
            return 0;
        }
        const LogBank& bank = m_banks[streamMetric / 6];
        const LogStream* stream = streamMetric % 6 < 2 ? &bank.increments : &bank.decrements;
        return streamMetric % 2 == 0 ? stream->referenceStorage.GetCommittedBytes() :
            stream->publicationStorage.GetCommittedBytes();
    }
    return UINT64_MAX;
}

LxrRcResult LxrReferenceCounting::GetStorageRange(
    size_t component, uintptr_t* start, size_t* bytes) const
{
    if (start == nullptr || bytes == nullptr || m_arena == nullptr)
    {
        return LxrRcResult::InvalidArgument;
    }
    *start = 0;
    *bytes = 0;
    switch (component)
    {
        case 0:
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
            *start = reinterpret_cast<uintptr_t>(m_extent_storage.Get());
            *bytes = m_extent_storage.GetReservedBytes();
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
            return LxrRcResult::Success;
        case 1:
        case 2:
            // The lock table and the scan table were removed in P17 S7. Nothing is indexed by heap address now.
            return LxrRcResult::Success;
        case 3:
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
            *start = reinterpret_cast<uintptr_t>(m_live_storage.Get());
            *bytes = m_live_storage.GetReservedBytes();
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
            return LxrRcResult::Success;
        default: break;
    }
    if (component < 16)
    {
        size_t index = component - 4;
        if (index % 6 >= 4)
        {
            return LxrRcResult::Success;
        }
        const LogBank& bank = m_banks[index / 6];
        const LogStream* stream = index % 6 < 2 ? &bank.increments : &bank.decrements;
        if (index % 2 == 0)
        {
            *start = reinterpret_cast<uintptr_t>(stream->referenceStorage.Get());
            *bytes = stream->referenceStorage.GetReservedBytes();
        }
        else
        {
            *start = reinterpret_cast<uintptr_t>(stream->publicationStorage.Get());
            *bytes = stream->publicationStorage.GetReservedBytes();
        }
        return LxrRcResult::Success;
    }
    if (component >= 17 && component - 17 < LxrSideMetadataLayout::SpecCount)
    {
        const SideMetadataSpec& spec = m_layout.GetSpecs()[component - 17];
        uintptr_t first = GetReservationStart() >> spec.log_bytes_per_value;
        uintptr_t last = (GetReservationStart() + GetReservationBytes() - 1) >> spec.log_bytes_per_value;
        size_t valueBytes = 1;
        if (spec.log_bits_per_value <= 3)
        {
            first >>= 3 - spec.log_bits_per_value;
            last >>= 3 - spec.log_bits_per_value;
        }
        else
        {
            first <<= spec.log_bits_per_value - 3;
            last <<= spec.log_bits_per_value - 3;
            valueBytes <<= spec.log_bits_per_value - 3;
        }
        size_t page = GCToOSInterface::GetPageSize();
        *start = (spec.base_address + first) & ~(page - 1);
        uintptr_t end = (spec.base_address + last + valueBytes + page - 1) & ~(page - 1);
        *bytes = end - *start;
        return LxrRcResult::Success;
    }
    if (component == 17 + LxrSideMetadataLayout::SpecCount)
    {
        *start = reinterpret_cast<uintptr_t>(m_allocated_block_storage.Get());
        *bytes = m_allocated_block_storage.GetReservedBytes();
        return LxrRcResult::Success;
    }
    return LxrRcResult::InvalidArgument;
}

LxrRcResult LxrReferenceCounting::EnableStorageDiagnostics(uint32_t timeout)
{
    if (GetPhase() != LxrRcPhase::Mutator || GetTracePhase() != LxrTracePhase::Inactive ||
        m_storage_diagnostics.load(std::memory_order_acquire))
    {
        return LxrRcResult::InvalidPhase;
    }
    LxrRcResult result = Drain(timeout);
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    uint64_t workBytes = 0;
    for (Work* work = m_work.load(std::memory_order_acquire); work != nullptr; work = work->next)
    {
        workBytes += sizeof(Work) +
            (work->ownedReferences == nullptr ? 0 : work->packet.GetReferenceCount() * sizeof(uintptr_t));
    }
    uint64_t scanBytes = 0;
    for (TraceScan* scan = m_scan_work.load(std::memory_order_acquire); scan != nullptr; scan = scan->next)
    {
        scanBytes += sizeof(TraceScan);
    }
    m_storage_metrics[1].store(workBytes, std::memory_order_relaxed);
    m_storage_metrics[2].store(workBytes, std::memory_order_relaxed);
    m_storage_metrics[5].store(scanBytes, std::memory_order_relaxed);
    m_storage_metrics[6].store(scanBytes, std::memory_order_relaxed);
    m_storage_diagnostics.store(true, std::memory_order_release);
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::ReleaseNurseryRange(size_t begin, size_t end)
{
    for (size_t entry = begin; entry < end; entry++)
    {
        size_t block = m_allocated_list[entry];
        m_allocated_block_storage.Get()[block].store(0, std::memory_order_release);
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        LxrRcResult result = OracleReclaimNursery(block);
        if (result != LxrRcResult::Success)
        {
            return result;
        }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    }
#ifndef LXR_S4_NO_NURSERY_RELEASE_CONTROL
    size_t released = 0;
    LxrRcResult status = FromMetadata(m_blocks.ReleaseNurseryBatchQuiescent(
        GetReservationStart(), m_allocated_list + begin, end - begin, &released));
    if (status != LxrRcResult::Success)
    {
        return status;
    }
    if (released != 0)
    {
        if (FromMetadata(m_allocation.NotifyBlocksReleased(released)) != LxrRcResult::Success)
        {
            return GetFailure();
        }
        m_metrics[BlocksReleased].fetch_add(released, std::memory_order_relaxed);
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        m_pause_released_blocks.fetch_add(released, std::memory_order_relaxed);
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    }
#endif // !LXR_S4_NO_NURSERY_RELEASE_CONTROL
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::FinishNurseryRelease()
{
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    LxrRcResult large = ReclaimLargeNursery();
    if (large != LxrRcResult::Success)
    {
        return large;
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
#ifdef LXR_HAS_LIFETIME_PROBE
    // Every live young object has its first increment by now, so a probed object with count zero is dead.
    uintptr_t probe = m_probe_start.load(std::memory_order_acquire);
    if (probe != 0 && !m_probe_retired.load(std::memory_order_acquire) && OwnsAddress(probe) &&
        m_rc_metadata.Load(probe, SideMetadataMemoryOrder::Relaxed) == 0)
    {
        NoteProbeReclaim(probe);
    }
#endif // LXR_HAS_LIFETIME_PROBE
    return LxrRcResult::Success;
}

#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
LxrRcResult LxrReferenceCounting::OracleReclaimNursery(size_t block)
{
    size_t blockSlots = ImmixBlockGeometry::BlockBytes / sizeof(uintptr_t);
    for (size_t index = FindNextOracleSlot(block * blockSlots); index < (block + 1) * blockSlots;
         index = FindNextOracleSlot(index + 1))
    {
        uintptr_t start = GetReservationStart() + index * sizeof(uintptr_t);
        uintptr_t references;
        LxrRcResult result = FromMetadata(m_counts.LoadFast(start, &references));
        if (result != LxrRcResult::Success)
        {
            return result;
        }
        if (references == 0 && OracleReclaim(start) != LxrRcResult::Success)
        {
            return GetFailure();
        }
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::ReclaimLargeNursery()
{
    size_t slots = GetReservationBytes() / sizeof(uintptr_t);
    for (size_t index = FindNextOracleSlot(m_configuration.immixBytes / sizeof(uintptr_t)); index < slots;
         index = FindNextOracleSlot(index + 1))
    {
        uintptr_t start = GetReservationStart() + index * sizeof(uintptr_t);
        uintptr_t references;
        LxrRcResult result = FromMetadata(m_counts.LoadFast(start, &references));
        if (result != LxrRcResult::Success)
        {
            return result;
        }
        if (references == 0 && OracleReclaim(start) != LxrRcResult::Success)
        {
            return GetFailure();
        }
    }
    return LxrRcResult::Success;
}
#endif // LXR_SIDE_METADATA_TEST_PLATFORM

uintptr_t LxrReferenceCounting::GetReservationStart() const
{
    return reinterpret_cast<uintptr_t>(m_arena);
}

size_t LxrReferenceCounting::GetReservationBytes() const
{
    return m_configuration.immixBytes + m_configuration.largeObjectBytes;
}

size_t LxrReferenceCounting::GetUsedBytesEstimate() const
{
    return m_allocation.GetBlocksInUse() * ImmixBlockGeometry::BlockBytes + m_large.GetCommittedBytes();
}

#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
size_t LxrReferenceCounting::GetLiveBytes() const
{
    return m_live_bytes.load(std::memory_order_acquire);
}

uint64_t LxrReferenceCounting::GetReclaimedBytes() const
{
    return m_reclaimed_bytes.load(std::memory_order_acquire);
}
#endif // LXR_SIDE_METADATA_TEST_PLATFORM

bool LxrReferenceCounting::OwnsAddress(uintptr_t address) const
{
    return m_arena != nullptr && (IsImmix(address) || IsLarge(address));
}

Object* LxrReferenceCounting::FindNextObject(Object* object) const
{
    uintptr_t start = Start(object);
    if (!OwnsAddress(start) || start % sizeof(uintptr_t) != 0)
    {
        return nullptr;
    }
    if (GetPhase() == LxrRcPhase::Mutator)
    {
        // Outside a pause the neighbour is found from the size of the object. The size of a registered object
        // is intact. A stale bit can make the neighbour a dead young object; the caller filters by liveness.
        if (!IsObjectStart(object))
        {
            return nullptr;
        }
        size_t size = IsLarge(start) ? GetLargeObjectSize(start) : ObjectSize(object);
        if (size == 0 || size >= GetReservationBytes() - (start - GetReservationStart()))
        {
            return nullptr;
        }
        Object* next = Reference(start + size);
        return IsObjectStart(next) ? next : nullptr;
    }
    size_t slots = GetReservationBytes() / sizeof(uintptr_t);
    size_t first = (start - GetReservationStart()) / sizeof(uintptr_t) + 1;
    for (size_t index = FindNextValidSlot(first); index < slots;
         index = FindNextValidSlot(index + 1))
    {
        uintptr_t candidateStart = GetReservationStart() + index * sizeof(uintptr_t);
        Object* candidate = Reference(candidateStart);
        // A stale bit belongs to a dead young object, and its count is zero.
        if (m_rc_metadata.Load(candidateStart, SideMetadataMemoryOrder::Relaxed) != 0 &&
            ResolveForwarded(candidate) == candidate)
        {
            return candidate;
        }
    }
    return nullptr;
}

size_t LxrReferenceCounting::GetMarkedBytes() const
{
    size_t bytes = 0;
    size_t slots = GetReservationBytes() / sizeof(uintptr_t);
    for (size_t index = FindNextValidSlot(0); index < slots;
         index = FindNextValidSlot(index + 1))
    {
        uintptr_t start = GetReservationStart() + index * sizeof(uintptr_t);
        Object* object = Reference(start);
        if (IsMarked(object))
        {
            bytes += IsLarge(start) ? GetLargeObjectSize(start) : ObjectSize(object);
        }
    }
    return bytes;
}

size_t LxrReferenceCounting::GetCommittedBytes() const
{
    return m_immix_capacity.load(std::memory_order_acquire) + m_large.GetCommittedBytes();
}

// Commits the per-block tables for the first immixBytes of the Immix reservation (a table never shrinks).
bool LxrReferenceCounting::GrowBlockTables(size_t immixBytes)
{
    size_t blocks = immixBytes / ImmixBlockGeometry::BlockBytes;
    size_t committed = m_block_table_blocks.load(std::memory_order_relaxed);
    if (blocks <= committed)
    {
        return true;
    }
    if (!m_mature_candidates_storage.Commit(committed, blocks - committed, false) ||
        !m_block_flags_storage.Commit(committed, blocks - committed, false) ||
        !m_allocated_list_storage.Commit(committed, blocks - committed, false) ||
        (m_zero_state != nullptr && !m_zero_state_storage.Commit(committed, blocks - committed, false)) ||
        (m_evacuation_sources != nullptr && !m_evacuation_sources_storage.Commit(committed, blocks - committed, false)))
    {
        return false;
    }
    m_block_table_blocks.store(blocks, std::memory_order_release);
    return true;
}

// Commits the entries of the dead large-object list for the admitted LOS pages (a large object takes at least one page).
bool LxrReferenceCounting::GrowDeadLarge()
{
    size_t pages = m_large.GetAdmittedBytes() / LxrLargeObjectSpace::PageBytes;
    size_t committed = m_dead_large_capacity.load(std::memory_order_relaxed);
    if (pages <= committed)
    {
        return true;
    }
    if (!m_dead_large_storage.Commit(committed, pages - committed, false))
    {
        return false;
    }
    m_dead_large_capacity.store(pages, std::memory_order_release);
    return true;
}

size_t LxrReferenceCounting::GetImmixReservationBytes() const
{
    return m_configuration.immixBytes;
}

size_t LxrReferenceCounting::GetImmixCapacityBytes() const
{
    return m_immix_capacity.load(std::memory_order_acquire);
}

size_t LxrReferenceCounting::GetAdmittedBytes() const
{
    return GetImmixCapacityBytes() + m_large.GetAdmittedBytes();
}

uint64_t LxrReferenceCounting::GetGrowthCount() const
{
    return m_growth_count.load(std::memory_order_acquire);
}

size_t LxrReferenceCounting::GetNativePlaneCommittedBytes() const
{
    size_t bytes = m_allocated_block_storage.GetCommittedBytes();
    for (size_t bank = 0; bank < 2; bank++)
    {
        for (const LogStream* stream : {
                &m_banks[bank].increments, &m_banks[bank].decrements})
        {
            bytes += stream->referenceStorage.GetCommittedBytes() +
                stream->publicationStorage.GetCommittedBytes();
        }
    }
    return bytes;
}

size_t LxrReferenceCounting::FindNextValidSlot(size_t first, size_t limit) const
{
    size_t total = GetReservationBytes() / sizeof(uintptr_t);
    size_t immixEnd = GetImmixCapacityBytes() / sizeof(uintptr_t);
    size_t largeStart = m_configuration.immixBytes / sizeof(uintptr_t);
    size_t largeEnd = largeStart + m_large.GetAdmittedBytes() / sizeof(uintptr_t);
    uintptr_t arena = GetReservationStart();
#ifdef LXR_OBJECT_START_WALK
    // The walk variant has no ValidObject plane: the starts of the counted objects are the non-zero counts, except the first
    // slot of each continuation line of a long object.
    uint32_t bits = static_cast<uint32_t>(1) << m_rc_metadata.GetLogBitsPerValue();
    uint64_t lowest = 0;
    for (uint32_t position = 0; position < 64; position += bits)
    {
        lowest |= UINT64_C(1) << position;
    }
    while (first < largeEnd && first < limit)
    {
        if (first >= immixEnd && first < largeStart)
        {
            first = largeStart;
        }
        SideMetadataLocation location = m_rc_metadata.Locate(arena + first * sizeof(uintptr_t));
        uint64_t word = static_cast<uint64_t>(
            SideMetadataAccessor::LoadWord(location.word, SideMetadataMemoryOrder::Relaxed)) >> location.shift;
        uint64_t folded = word;
        for (uint32_t shift = 1; shift < bits; shift <<= 1)
        {
            folded |= folded >> shift;
        }
        folded &= lowest;
        while (folded != 0)
        {
#ifdef TARGET_WINDOWS
            DWORD bit;
#else
            uint32_t bit;
#endif // TARGET_WINDOWS
            BitScanForward64(&bit, folded);
            size_t candidate = first + bit / bits;
            // The first slot of a line of the large-object space is never a continuation slot.
#if defined(LXR_WALK_ENUM_CONTINUATION_CONTROL)
            // Control: the enumeration takes the count of a continuation line for an object start.
            if (true)
#else
            if (candidate >= limit || candidate >= largeStart || !IsContinuationSlot(candidate))
#endif // LXR_WALK_ENUM_CONTINUATION_CONTROL
            {
                std::atomic_thread_fence(std::memory_order_acquire);
                return candidate;
            }
            folded &= folded - 1;
        }
        first += (64 - location.shift) / bits;
    }
    return total;
#else
    while (first < largeEnd && first < limit)
    {
        if (first >= immixEnd && first < largeStart)
        {
            first = largeStart;
        }
        // One ValidObject word covers 64 slots, and the arena start is aligned to a block, so a slot index and
        // the bit position in its word agree.
        SideMetadataLocation location = m_valid_metadata.Locate(arena + first * sizeof(uintptr_t));
        uint64_t bits = static_cast<uint64_t>(
            SideMetadataAccessor::LoadWord(location.word, SideMetadataMemoryOrder::Relaxed)) >> location.shift;
#ifdef TARGET_WINDOWS
        DWORD bit;
#else
        uint32_t bit;
#endif // TARGET_WINDOWS
        if (BitScanForward64(&bit, bits))
        {
            std::atomic_thread_fence(std::memory_order_acquire);
            return first + bit;
        }
        first += 64 - location.shift;
    }
    return total;
#endif // LXR_OBJECT_START_WALK
}

LxrRcResult LxrReferenceCounting::RequestGrowth(bool large, size_t minimumBytes)
{
    if (GetPhase() != LxrRcPhase::Mutator || m_grow_bytes != 0 || minimumBytes == 0)
    {
        return LxrRcResult::InvalidArgument;
    }
    size_t current = large ? m_large.GetAdmittedBytes() : GetImmixCapacityBytes();
    size_t reserved = large ? m_configuration.largeObjectBytes : m_configuration.immixBytes;
    size_t remaining = m_configuration.hardLimitBytes - GetAdmittedBytes();
    if (reserved == current || remaining == 0 ||
        (large ? minimumBytes > reserved : minimumBytes > reserved - current || minimumBytes > remaining))
    {
        return LxrRcResult::AllocationFailure;
    }
    m_grow_large = large;
    m_grow_exact = false;
    m_grow_bytes = minimumBytes;
    m_growth_result = LxrRcResult::AllocationFailure;
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::RequestImmixCapacity(size_t targetBytes)
{
    if (GetPhase() != LxrRcPhase::Mutator || m_grow_bytes != 0)
    {
        return LxrRcResult::InvalidArgument;
    }
    size_t current = GetImmixCapacityBytes();
    size_t reserved = m_configuration.immixBytes;
    size_t remaining = m_configuration.hardLimitBytes > GetAdmittedBytes()
        ? m_configuration.hardLimitBytes - GetAdmittedBytes() : 0;
    size_t room = reserved - current < remaining ? reserved - current : remaining;
    size_t wanted = targetBytes > current ? targetBytes - current : 0;
    size_t increment = (wanted < room ? wanted : room) & ~(ImmixBlockGeometry::BlockBytes - 1);
    if (increment == 0)
    {
        return LxrRcResult::AllocationFailure;
    }
    m_grow_large = false;
    m_grow_exact = true;
    m_grow_bytes = increment;
    m_growth_result = LxrRcResult::AllocationFailure;
    return LxrRcResult::Success;
}

size_t LxrReferenceCounting::GetImmixOccupiedBytes() const
{
    size_t blockBytes = GetNonFreeBlocks() * ImmixBlockGeometry::BlockBytes;
    size_t freeBytes = m_blocks.GetReusableFreeLines() * ImmixBlockGeometry::LineBytes;
    return blockBytes > freeBytes ? blockBytes - freeBytes : 0;
}

size_t LxrReferenceCounting::GetCleanNurseryBlocks() const
{
    return m_allocation.GetFreshBlocksAcquired() - m_epoch_fresh_blocks.load(std::memory_order_acquire);
}

size_t LxrReferenceCounting::GetYoungAllocationBytes() const
{
    size_t lines = m_allocation.GetRecycledLinesConsumed() -
        m_epoch_recycled_lines.load(std::memory_order_acquire);
    return GetCleanNurseryBlocks() * ImmixBlockGeometry::BlockBytes +
        (lines >> 4) * LxrLargeObjectSpace::PageBytes;
}

size_t LxrReferenceCounting::GetImmixFreePages() const
{
    size_t capacity = GetImmixCapacityBytes();
    size_t used = m_allocation.GetBlocksInUse() * ImmixBlockGeometry::BlockBytes;
    return used >= capacity ? 0 : (capacity - used) / LxrLargeObjectSpace::PageBytes;
}

size_t LxrReferenceCounting::GetPendingIncrementEntries() const
{
    return m_banks[m_producer_bank.load(std::memory_order_acquire)].increments.reserved.load(
        std::memory_order_acquire);
}

LxrRcResult LxrReferenceCounting::DrainLazySweep(uint32_t timeout)
{
    return DrainEpoch(timeout);
}

LxrRcResult LxrReferenceCounting::GetMaturePages(size_t* pages) const
{
    if (pages == nullptr)
    {
        return LxrRcResult::InvalidArgument;
    }
    size_t bytes;
    if (m_large.GetMatureCommittedBytes(&bytes) != LxrLargeObjectResult::Success)
    {
        return LxrRcResult::MetadataFailure;
    }
    bytes += GetNonFreeBlocks() * ImmixBlockGeometry::BlockBytes;
    *pages = bytes / LxrLargeObjectSpace::PageBytes;
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::GetOccupiedPages(size_t* pages) const
{
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F5_BLOCK_PAGES_CONTROL)
    // Control: count whole blocks again (the behaviour before P25 F5).
    return GetMaturePages(pages);
#else
    if (pages == nullptr)
    {
        return LxrRcResult::InvalidArgument;
    }
    size_t bytes;
    if (m_large.GetMatureCommittedBytes(&bytes) != LxrLargeObjectResult::Success)
    {
        return LxrRcResult::MetadataFailure;
    }
    size_t blockBytes = GetNonFreeBlocks() * ImmixBlockGeometry::BlockBytes;
    size_t freeBytes = m_blocks.GetReusableFreeLines() * ImmixBlockGeometry::LineBytes;
    bytes += blockBytes > freeBytes ? blockBytes - freeBytes : 0;
    *pages = bytes / LxrLargeObjectSpace::PageBytes;
    return LxrRcResult::Success;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F5_BLOCK_PAGES_CONTROL
}

size_t LxrReferenceCounting::GetNonFreeBlocks() const
{
    return m_blocks.GetNonFreeBlocks();
}

bool LxrReferenceCounting::BlockCountersMatchWalk(size_t* walkedNonFree, size_t* walkedFreeLines) const
{
    size_t nonFree = 0;
    size_t freeLines = 0;
    for (size_t offset = 0; offset < GetImmixCapacityBytes(); offset += ImmixBlockGeometry::BlockBytes)
    {
        ImmixBlockState state;
        uint8_t unavailable;
        if (m_blocks.GetState(GetReservationStart() + offset, &state, &unavailable) != SideMetadataResult::Success)
        {
            return false;
        }
        if (state != ImmixBlockState::Unallocated)
        {
            nonFree++;
        }
        if (state == ImmixBlockState::Reusable)
        {
            freeLines += ImmixBlockGeometry::LinesPerBlock - unavailable;
        }
    }
    if (walkedNonFree != nullptr)
    {
        *walkedNonFree = nonFree;
    }
    if (walkedFreeLines != nullptr)
    {
        *walkedFreeLines = freeLines;
    }
    return nonFree == m_blocks.GetNonFreeBlocks() && freeLines == m_blocks.GetReusableFreeLines();
}

LxrRcResult LxrReferenceCounting::GetGrowthResult() const
{
    return m_growth_result;
}

LxrRcResult LxrReferenceCounting::Grow(
    bool large, size_t minimumBytes, LxrRcCaptureRoots capture, void* context, uint32_t timeout)
{
    LxrRcResult result = RequestGrowth(large, minimumBytes);
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    result = Collect(capture, context, timeout);
    return result == LxrRcResult::Success ? GetGrowthResult() : result;
}

LxrRcResult LxrReferenceCounting::GrowSuspended()
{
    size_t current = m_grow_large ? m_large.GetAdmittedBytes() : GetImmixCapacityBytes();
    size_t reserved = m_grow_large ? m_configuration.largeObjectBytes : m_configuration.immixBytes;
    size_t granularity = m_grow_large ? LxrLargeObjectSpace::PageBytes : ImmixBlockGeometry::BlockBytes;
    size_t available = m_configuration.hardLimitBytes - GetAdmittedBytes();
    size_t maximum = current + (reserved - current < available ? reserved - current : available);
    maximum &= ~(granularity - 1);
    size_t required = m_grow_large ? m_large.GetRequiredGrowthBytes(m_grow_bytes) : m_grow_bytes;
    if (maximum <= current || required == 0 || required > maximum - current)
    {
        return LxrRcResult::AllocationFailure;
    }
    size_t minimum = (required + granularity - 1) & ~(granularity - 1);
    // With overshoot the capacity above the floor is borrowed to-space, so the doubling starts from the kept part.
    size_t base = m_overshoot && !m_grow_large && m_immix_floor != 0 ? m_immix_floor : current;
    size_t increment = m_grow_exact ? minimum : (base > minimum ? base : minimum);
    increment = increment > maximum - current ? maximum - current : increment;
    if (!FitAdmission(m_grow_large, current, minimum, &increment))
    {
        return LxrRcResult::OutOfMemory;
    }
    LxrRcResult result = AdmitSuspended(m_grow_large, current, increment, true);
    if (result == LxrRcResult::Success && !m_grow_large)
    {
        // A regular growth is permanent: the borrowed range below it stays with the heap.
        m_immix_floor = GetImmixCapacityBytes();
        m_allocation.SetMutatorFreshBlocksQuiescent(m_immix_floor / ImmixBlockGeometry::BlockBytes);
    }
    return result;
}

bool LxrReferenceCounting::FitAdmission(bool large, size_t current, size_t minimum, size_t* increment)
{
    size_t granularity = large ? LxrLargeObjectSpace::PageBytes : ImmixBlockGeometry::BlockBytes;
    size_t offset = large ? m_configuration.immixBytes : 0;
    size_t first = (offset + current) / sizeof(uintptr_t);
    auto fits = [&](size_t candidate)
    {
        size_t slots = candidate / sizeof(uintptr_t);
        size_t nativeBytes = 0;
        size_t blockSlots = ImmixBlockGeometry::BlockBytes / sizeof(uintptr_t);
        nativeBytes += large ? m_allocated_block_storage.GetCommittedBytes() :
            m_allocated_block_storage.ProjectedCommittedBytes(first / blockSlots, slots / blockSlots);
        for (size_t bank = 0; bank < 2; bank++)
        {
            for (LogStream* stream : {
                    &m_banks[bank].increments, &m_banks[bank].decrements})
            {
                nativeBytes += stream->referenceStorage.GetCommittedBytes() +
                    stream->publicationStorage.GetCommittedBytes();
            }
        }
        return m_object_model.canAdmitRange(first, slots, nativeBytes, m_object_model.context);
    };
    if (m_object_model.canAdmitRange != nullptr && !fits(*increment))
    {
        if (!fits(minimum))
        {
            return false;
        }
        size_t low = minimum / granularity;
        size_t high = *increment / granularity;
        while (low < high)
        {
            size_t middle = low + (high - low + 1) / 2;
            if (fits(middle * granularity))
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }
        *increment = low * granularity;
    }
    return true;
}

LxrRcResult LxrReferenceCounting::AdmitSuspended(
    bool large, size_t current, size_t increment, bool initialize)
{
    size_t next = current + increment;
    size_t offset = large ? m_configuration.immixBytes : 0;
    size_t first = (offset + current) / sizeof(uintptr_t);
    size_t previousLogCapacity = GetAdmittedBytes() / sizeof(uintptr_t);
    size_t count = increment / sizeof(uintptr_t);
    if (!CommitPlanes(first, count, initialize))
    {
        return LxrRcResult::OutOfMemory;
    }
    if (m_object_model.admitRange != nullptr &&
        !m_object_model.admitRange(first, count, initialize, m_object_model.context))
    {
        return LxrRcResult::OutOfMemory;
    }
    uintptr_t start = GetReservationStart() + offset + current;
    if (m_metadata.CommitDataRange(start, increment) != SideMetadataResult::Success)
    {
        return LxrRcResult::OutOfMemory;
    }
    if (large)
    {
        if (m_large.GrowQuiescent(next) != LxrLargeObjectResult::Success || !GrowDeadLarge())
        {
            return LxrRcResult::OutOfMemory;
        }
    }
    else
    {
        if (!GCToOSInterface::VirtualCommit(reinterpret_cast<void*>(start), increment))
        {
            return LxrRcResult::OutOfMemory;
        }
        if (!GrowBlockTables(next))
        {
            return LxrRcResult::OutOfMemory;
        }
        if (m_allocation.GrowQuiescent(next) != SideMetadataResult::Success)
        {
            return Fail(LxrRcResult::InvariantViolation);
        }
        m_immix_capacity.store(next, std::memory_order_release);
    }
    for (size_t bank = 0; bank < 2; bank++)
    {
        size_t desired = previousLogCapacity + count;
        if (desired > m_banks[bank].capacity)
        {
            m_banks[bank].capacity = desired;
            m_banks[bank].increments.capacity = desired;
            m_banks[bank].decrements.capacity = desired;
        }
    }
    m_growth_count.fetch_add(1, std::memory_order_release);
    return LxrRcResult::Success;
}

namespace
{
uint64_t TicksToNanoseconds(int64_t ticks)
{
    int64_t frequency = LxrPauseTimers::Frequency();
    return static_cast<uint64_t>(ticks / frequency * 1000000000 + ticks % frequency * 1000000000 / frequency);
}
}

size_t LxrReferenceCounting::GetBorrowedBytes() const
{
    size_t capacity = GetImmixCapacityBytes();
    return capacity > m_immix_floor ? capacity - m_immix_floor : 0;
}

uint64_t LxrReferenceCounting::GetOvershootMetric(size_t metric) const
{
    if (metric >= OvershootMetricCount)
    {
        return UINT64_MAX;
    }
    if (metric == BorrowedNow)
    {
        return GetBorrowedBytes();
    }
    return m_overshoot_metrics[metric].load(std::memory_order_relaxed);
}

LxrRcResult LxrReferenceCounting::BorrowSuspended()
{
    size_t request = m_borrow_request_pages;
    m_borrow_request_pages = 0;
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_S5B_NO_BORROW_CONTROL)
    return LxrRcResult::Success;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_S5B_NO_BORROW_CONTROL
    size_t free = GetImmixFreePages();
    SetOvershoot(BorrowRequestPages, request);
    SetOvershoot(BorrowFreePages, free);
    SetOvershoot(BorrowBytesLast, 0);
    SetOvershoot(BorrowNsLast, 0);
    if (request <= free)
    {
        return LxrRcResult::Success;
    }
    LxrPhaseScope scope(m_pause_timers, LxrPauseTimers::Borrow);
    int64_t started = LxrPauseTimers::Now();
    size_t current = GetImmixCapacityBytes();
    size_t block = ImmixBlockGeometry::BlockBytes;
    size_t missing = ((request - free) * LxrLargeObjectSpace::PageBytes + block - 1) & ~(block - 1);
    // The hard limit equals the whole reservation here (see m_overshoot), so the room is what is left of it, up to the
    // borrow ceiling of the collector.
    size_t borrowReserve = m_configuration.immixBytes;
    size_t ceiling = m_borrow_ceiling.load(std::memory_order_relaxed);
    if (ceiling < borrowReserve)
    {
        borrowReserve = ceiling;
    }
    size_t room = borrowReserve > current ? borrowReserve - current : 0;
    size_t limitRoom = m_configuration.hardLimitBytes - GetAdmittedBytes();
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_S5B_IGNORE_HARD_LIMIT_CONTROL)
    limitRoom = room;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_S5B_IGNORE_HARD_LIMIT_CONTROL
    size_t available = (room < limitRoom ? room : limitRoom) & ~(block - 1);
    size_t increment = missing < available ? missing : available;
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_S5B_IGNORE_PLANE_LIMIT_CONTROL)
    bool fitted = increment != 0;
#else
    bool fitted = increment != 0 && FitAdmission(false, current, block, &increment);
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_S5B_IGNORE_PLANE_LIMIT_CONTROL
    if (!fitted)
    {
        CountOvershoot(BorrowRefused, 1);
        return LxrRcResult::Success;
    }
    // The planes of a borrowed range are zero from the operating system, and most of the range is never used, so the
    // storage is committed without writing to it.
    LxrRcResult result = AdmitSuspended(false, current, increment, false);
    if (result != LxrRcResult::Success)
    {
        // A failed commit leaves the capacity as it was; the pause copies into what is free, as without overshoot.
        CountOvershoot(BorrowRefused, 1);
        return result == LxrRcResult::OutOfMemory ? LxrRcResult::Success : result;
    }
    uint64_t nanoseconds = TicksToNanoseconds(LxrPauseTimers::Now() - started);
    CountOvershoot(BorrowPauses, 1);
    CountOvershoot(BorrowBytesTotal, increment);
    SetOvershoot(BorrowBytesLast, increment);
    if (increment > m_overshoot_metrics[BorrowBytesMax].load(std::memory_order_relaxed))
    {
        SetOvershoot(BorrowBytesMax, increment);
    }
    CountOvershoot(BorrowNsTotal, nanoseconds);
    SetOvershoot(BorrowNsLast, nanoseconds);
    if (GetImmixCapacityBytes() > m_overshoot_metrics[PeakCapacityBytes].load(std::memory_order_relaxed))
    {
        SetOvershoot(PeakCapacityBytes, GetImmixCapacityBytes());
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::GiveBackSuspended()
{
    size_t capacity = GetImmixCapacityBytes();
    SetOvershoot(GiveBackBytesLast, 0);
    SetOvershoot(GiveBackNsLast, 0);
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_S5B_NO_GIVEBACK_CONTROL)
    return LxrRcResult::Success;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_S5B_NO_GIVEBACK_CONTROL
    if (capacity <= m_immix_floor)
    {
        return LxrRcResult::Success;
    }
    LxrPhaseScope scope(m_pause_timers, LxrPauseTimers::GiveBack);
    int64_t started = LxrPauseTimers::Now();
    CountOvershoot(GiveBackAttempts, 1);
    size_t block = ImmixBlockGeometry::BlockBytes;
    size_t index = capacity / block;
    size_t floorBlocks = m_immix_floor / block;
    uintptr_t arena = GetReservationStart();
    // Only a free tail can go back: the walk stops at the first block that holds an object or that an allocator owns.
    while (index > floorBlocks)
    {
        size_t candidate = index - 1;
        uintptr_t address = arena + candidate * block;
        bool free;
        LxrRcResult result = FromMetadata(m_blocks.IsFreeBlock(address, &free));
        if (result != LxrRcResult::Success)
        {
            return result;
        }
        if (!free)
        {
            // A copy block that stayed empty is released now, as the lazy sweep would do after the pause. The sweep
            // runs while mutators allocate, so it could not give the block back.
            if (m_mature_candidates[candidate].load(std::memory_order_acquire) == 0)
            {
                break;
            }
            ImmixBlockOperationStatus status;
            result = ReleaseBlockIfDead(address, candidate, nullptr, &status);
            if (result != LxrRcResult::Success)
            {
                return result;
            }
            if (status != ImmixBlockOperationStatus::Updated)
            {
                break;
            }
            CountOvershoot(EmptyCopyBlocksReleased, 1);
        }
        index = candidate;
    }
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_S5B_ALWAYS_RETURN_CONTROL)
    index = floorBlocks;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_S5B_ALWAYS_RETURN_CONTROL
    size_t newCapacity = index * block;
    if (newCapacity < capacity)
    {
        LxrRcResult result = ReturnTailSuspended(newCapacity);
        if (result != LxrRcResult::Success)
        {
            return result;
        }
        CountOvershoot(GiveBackSuccesses, 1);
        CountOvershoot(GiveBackBytesTotal, capacity - newCapacity);
        SetOvershoot(GiveBackBytesLast, capacity - newCapacity);
        if (newCapacity == m_immix_floor)
        {
            CountOvershoot(GiveBackComplete, 1);
        }
    }
    uint64_t nanoseconds = TicksToNanoseconds(LxrPauseTimers::Now() - started);
    CountOvershoot(GiveBackNsTotal, nanoseconds);
    SetOvershoot(GiveBackNsLast, nanoseconds);
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::ReturnTailSuspended(size_t newCapacity)
{
    size_t oldCapacity = GetImmixCapacityBytes();
    if (GetPhase() != LxrRcPhase::Suspended || newCapacity < m_immix_floor || newCapacity >= oldCapacity ||
        newCapacity % ImmixBlockGeometry::BlockBytes != 0)
    {
        return LxrRcResult::InvalidArgument;
    }
    size_t increment = oldCapacity - newCapacity;
    for (size_t block = newCapacity / ImmixBlockGeometry::BlockBytes;
         block < oldCapacity / ImmixBlockGeometry::BlockBytes; block++)
    {
        // The blocks hold no object, so no flag of the lazy sweep can still be meant for them.
        m_mature_candidates[block].store(0, std::memory_order_release);
        if (m_evacuation_sources != nullptr)
        {
            m_evacuation_sources[block].store(0, std::memory_order_release);
        }
    }
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_S5B_NO_ALLOC_SHRINK_CONTROL)
    m_immix_capacity.store(newCapacity, std::memory_order_release);
    return LxrRcResult::Success;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_S5B_NO_ALLOC_SHRINK_CONTROL
    // Lower the capacity first: from here on the range is not an Immix range, so a stale lookup is refused by
    // IsImmix and the checked metadata accessors, and no allocator can pick a block of it.
    if (m_allocation.ShrinkQuiescent(newCapacity) != SideMetadataResult::Success)
    {
        return Fail(LxrRcResult::InvariantViolation);
    }
    m_immix_capacity.store(newCapacity, std::memory_order_release);
    size_t first = newCapacity / sizeof(uintptr_t);
    size_t count = increment / sizeof(uintptr_t);
    // No Immix block follows the old end when the capacity is below the reservation, so the storage pages that hold
    // the last slots of the range are not shared with an admitted neighbour.
    bool tailIsLast = oldCapacity < m_configuration.immixBytes;
    if ((m_object_model.releaseRange != nullptr &&
            !m_object_model.releaseRange(first, count, tailIsLast, m_object_model.context)) ||
        !DecommitPlanes(first, count, tailIsLast))
    {
        return Fail(LxrRcResult::OutOfMemory);
    }
    uintptr_t start = GetReservationStart() + newCapacity;
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_S5B_NO_PLANE_DECOMMIT_CONTROL)
    if (m_metadata.DecommitDataRange(start, increment) != SideMetadataResult::Success)
    {
        return Fail(LxrRcResult::MetadataFailure);
    }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_S5B_NO_PLANE_DECOMMIT_CONTROL
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_S5B_NO_DATA_DECOMMIT_CONTROL)
    if (!GCToOSInterface::VirtualDecommit(reinterpret_cast<void*>(start), increment))
    {
        return Fail(LxrRcResult::OutOfMemory);
    }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_S5B_NO_DATA_DECOMMIT_CONTROL
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::GetHeapStatistics(size_t* heapBytes, size_t* fragmentationBytes) const
{
    if (heapBytes == nullptr || fragmentationBytes == nullptr)
    {
        return LxrRcResult::InvalidArgument;
    }
    size_t largeBytes;
    size_t largeSlack;
    if (m_large.GetMatureStatistics(&largeBytes, &largeSlack) != LxrLargeObjectResult::Success)
    {
        return LxrRcResult::MetadataFailure;
    }
    // Before P17 S4 this walked every counted object. A walk needs the paused heap and costs time in every pause, so
    // the figures come from counters that block transitions maintain. Heap size counts whole blocks; fragmentation
    // counts the free lines of Reusable blocks (found by the lazy sweep) and the unused tail pages of large objects.
    *heapBytes = GetNonFreeBlocks() * ImmixBlockGeometry::BlockBytes + largeBytes;
    *fragmentationBytes = m_blocks.GetReusableFreeLines() * ImmixBlockGeometry::LineBytes + largeSlack;
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::StopWorkers()
{
    m_scan_checkpoint.store(0, std::memory_order_release);
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    m_gate_armed.store(false, std::memory_order_release);
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    return FromScheduler(m_scheduler.Shutdown());
}

bool LxrReferenceCounting::HasPendingDetaches() const
{
    for (size_t index = 0; index < m_configuration.maximumMutators; index++)
    {
        if (m_mutators[index].state.load(std::memory_order_acquire) == 3)
        {
            return true;
        }
    }
    return false;
}

LxrRcResult LxrReferenceCounting::WalkObjects(LxrRcWalkObject visit, void* context) const
{
    if (visit == nullptr || m_observer || GetPhase() != LxrRcPhase::Suspended)
    {
        return LxrRcResult::InvalidPhase;
    }
    size_t slots = GetReservationBytes() / sizeof(uintptr_t);
    for (size_t index = FindNextValidSlot(0); index < slots;
         index = FindNextValidSlot(index + 1))
    {
        uintptr_t start = GetReservationStart() + index * sizeof(uintptr_t);
        // A count of zero marks a dead young object with a stale bit, or a young object that has no increment yet.
        if (m_rc_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) == 0)
        {
            continue;
        }
        Object* object = Reference(start);
        if (ResolveForwarded(object) != object)
        {
            continue;
        }
        size_t size = IsLarge(start) ? GetLargeObjectSize(start) : ObjectSize(object);
        if (size != 0 && !visit(object, size, context))
        {
            break;
        }
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::WalkObjectStarts(LxrRcWalkObject visit, void* context) const
{
    if (visit == nullptr || m_observer || GetPhase() != LxrRcPhase::Suspended)
    {
        return LxrRcResult::InvalidPhase;
    }
    size_t slots = GetReservationBytes() / sizeof(uintptr_t);
    for (size_t index = FindNextValidSlot(0); index < slots;
         index = FindNextValidSlot(index + 1))
    {
        Object* object = Reference(GetReservationStart() + index * sizeof(uintptr_t));
        if (!visit(object, 0, context))
        {
            break;
        }
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::WalkForwarded(LxrRcWalkForwarded visit, void* context) const
{
    if (visit == nullptr || GetPhase() != LxrRcPhase::Suspended)
    {
        return LxrRcResult::InvalidPhase;
    }
    if (!m_configuration.evacuateNursery && !m_configuration.evacuateMature)
    {
        return LxrRcResult::Success;
    }
    // The copy path logged every forwarded source with its size; the release stage consumes the log later.
    for (size_t slot = 0; slot <= m_configuration.workers; slot++)
    {
        for (CopyLog::Chunk* chunk = m_copy_logs[slot].head; chunk != nullptr; chunk = chunk->next)
        {
            for (size_t entry = 0; entry < chunk->count; entry++)
            {
                Object* source = Reference(chunk->entries[entry].start);
                Object* destination = ResolveForwarded(source);
                if (destination != source && !visit(source, destination, chunk->entries[entry].size, context))
                {
                    return LxrRcResult::Success;
                }
            }
        }
    }
    return LxrRcResult::Success;
}

void LxrReferenceCounting::EnterTraceLock(size_t index)
{
    uint8_t expected = 0;
    while (!TraceLockSlot(index).compare_exchange_weak(
        expected, 1, std::memory_order_acquire, std::memory_order_relaxed))
    {
        expected = 0;
        YieldProcessor();
    }
}

void LxrReferenceCounting::ExitTraceLock(size_t index)
{
    TraceLockSlot(index).store(0, std::memory_order_release);
}

LxrRcResult LxrReferenceCounting::QueueTrace(
    const uintptr_t* objects, size_t count, TraceScan* scan, bool logged)
{
    Work* work = NewWork(scan == nullptr ? WorkKind::TraceObjects : WorkKind::TraceScan,
        objects, count, LxrWorkBucketStage::ConcurrentResumable, true, scan, false, logged);
    if (work == nullptr)
    {
        return GetFailure();
    }
    return DispatchTraceWork(work);
}

LxrRcResult LxrReferenceCounting::DispatchTraceWork(Work* work)
{
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    if (work->watchedReader && m_reader_held.load(std::memory_order_acquire))
    {
        PushTraceUnsubmitted(work);
        return LxrRcResult::Success;
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    if (!m_pause_trace_open.load(std::memory_order_acquire) &&
        (GetTracePhase() != LxrTracePhase::Concurrent ||
        GetPhase() != LxrRcPhase::Mutator
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
        || m_hold_trace.load(std::memory_order_acquire)
#endif // LXR_HAS_DIAGNOSTIC_PROBES
        ))
    {
        PushTraceUnsubmitted(work);
        return LxrRcResult::Success;
    }
    // The packet may run and be retired as soon as Submit returns, so it is not touched after this call.
    LxrWorkSchedulerResult result = m_scheduler.Submit(&work->packet);
    if (result == LxrWorkSchedulerResult::Success)
    {
        return LxrRcResult::Success;
    }
    PushTraceUnsubmitted(work);
    if (result == LxrWorkSchedulerResult::StageDisabled ||
        result == LxrWorkSchedulerResult::Busy || result == LxrWorkSchedulerResult::InvalidState)
    {
        return LxrRcResult::Success;
    }
    return FromScheduler(result);
}

LxrRcResult LxrReferenceCounting::PublishTraceWork()
{
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
    if (m_hold_trace.load(std::memory_order_acquire))
    {
        return LxrRcResult::Success;
    }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
    LxrRcResult enabled = FromScheduler(m_scheduler.EnableConcurrentStage(
        LxrWorkBucketStage::ConcurrentResumable));
    if (enabled != LxrRcResult::Success)
    {
        return enabled;
    }
    // P26 (F2). Only the packets that could not go to the scheduler are visited (m_trace_unsubmitted). The pending packets
    // that are queued in the scheduler stay where they are, so the cost does not follow the trace backlog.
    Work* batch = m_trace_unsubmitted.exchange(nullptr, std::memory_order_acquire);
    LxrRcResult status = LxrRcResult::Success;
    while (batch != nullptr)
    {
        Work* work = batch;
        batch = work->traceNext;
        if (status == LxrRcResult::Success)
        {
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
            if (work->watchedReader && m_reader_held.load(std::memory_order_acquire))
            {
                PushTraceUnsubmitted(work);
                continue;
            }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
            m_walk[3].fetch_add(1, std::memory_order_relaxed);
            LxrWorkSchedulerResult result = m_scheduler.Submit(&work->packet);
            if (result == LxrWorkSchedulerResult::Success)
            {
                continue;
            }
            status = FromScheduler(result);
        }
        PushTraceUnsubmitted(work);
    }
    return status;
}

LxrRcResult LxrReferenceCounting::ProcessTraceWork(Work* work)
{
    // P26 (F4b). Every trace packet collects its children in one batch and loops over it (see ProcessTraceWork below).
    return ProcessFullTraceWork(work);
}

NOINLINE LxrRcResult LxrReferenceCounting::ProcessFullTraceWork(Work* work)
{
    TraceBatch targets(this);
    return GetFailure() == LxrRcResult::Success ? ProcessTraceWork(work, &targets) : GetFailure();
}

// P26 (F4b). The trace loop of a packet stops when a pause wants the workers: the scheduler has been asked to interrupt the
// concurrent work (the pause waits for this packet), or the engine left the mutator phase. In a pause the loop never stops.
bool LxrReferenceCounting::ShouldStopTraceLoop() const
{
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F4B_NO_TRACE_LOOP_CONTROL)
    return true;
#else
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    if (m_trace_loop_off.load(std::memory_order_acquire))
    {
        return true;
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    return GetFailure() != LxrRcResult::Success ||
        (GetPhase() == LxrRcPhase::Mutator && GetTracePhase() == LxrTracePhase::Concurrent &&
            m_scheduler.IsInterruptionRequested());
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F4B_NO_TRACE_LOOP_CONTROL
}

LxrRcResult LxrReferenceCounting::ProcessTraceWork(Work* work, TraceBatch* targets)
{
    bool expected = false;
    if (!work->traceProcessed.compare_exchange_strong(expected, true, std::memory_order_acq_rel))
    {
        return LxrRcResult::Success;
    }
    LxrRcResult result = LxrRcResult::Success;
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    if (work->watchedReader)
    {
        m_reader_consumed.store(work->references[0], std::memory_order_release);
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    if (work->scan != nullptr)
    {
        result = ProcessTraceScan(work->scan, false, targets);
    }
    else
    {
        size_t directBudget = BufferCapacity;
        for (size_t index = 0; index < work->packet.GetReferenceCount(); index++)
        {
            if (targets != nullptr)
            {
                // Full batches join separate object packets, not their per-object scan limits.
                directBudget = BufferCapacity;
            }
            result = ProcessTraceObject(
                reinterpret_cast<Object*>(work->references[index]), &directBudget, targets);
            if (result != LxrRcResult::Success)
            {
                break;
            }
            // The Java LXR prefetches the object that is 8 ahead in its trace loops too (cm.rs).
            LxrPrefetch::Ahead(work->references, work->packet.GetReferenceCount(), index, false);
        }
    }
    // P26 (F4b). The Java LXR trace packet loops: it traces its objects, swaps in the children that it found, and goes on
    // until no child is left (cm.rs: LXRConcurrentTraceObjects::do_work). A chain of children costs one packet. Only a
    // batch that fills up (8 192 children) becomes a new packet, so that other workers can take it. Before, every
    // object that had children made its own packet.
    while (result == LxrRcResult::Success && targets != nullptr && targets->count != 0 && !ShouldStopTraceLoop())
    {
        size_t levelCount = targets->count;
        uintptr_t level[FullTraceBatchCapacity];
        memcpy(level, targets->references, levelCount * sizeof(uintptr_t));
        targets->count = 0;
        size_t directBudget = BufferCapacity;
        for (size_t index = 0; index < levelCount; index++)
        {
            directBudget = BufferCapacity;
            result = ProcessTraceObject(reinterpret_cast<Object*>(level[index]), &directBudget, targets);
            if (result != LxrRcResult::Success)
            {
                break;
            }
            LxrPrefetch::Ahead(level, levelCount, index, false);
            if ((index & 255) == 255 && index + 1 < levelCount && ShouldStopTraceLoop())
            {
                for (size_t rest = index + 1; rest < levelCount && result == LxrRcResult::Success; rest++)
                {
                    result = targets->Append(level[rest]);
                }
                break;
            }
        }
    }
    if (result == LxrRcResult::Success && targets != nullptr)
    {
        result = targets->Flush();
    }
    uint64_t previous = m_trace_pending.fetch_sub(1, std::memory_order_acq_rel);
    if (previous == 0)
    {
        return Fail(LxrRcResult::InvariantViolation);
    }
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    if (work->watchedReader && result == LxrRcResult::Success)
    {
        m_reader_completions.fetch_add(1, std::memory_order_release);
    }
    if (m_gate_armed.load(std::memory_order_acquire) && GetPhase() == LxrRcPhase::Mutator &&
        GetTracePhase() == LxrTracePhase::Concurrent && m_gate_remaining.fetch_sub(1, std::memory_order_acq_rel) <= 1)
    {
        m_gate_blocked.store(true, std::memory_order_release);
        while (m_gate_armed.load(std::memory_order_acquire) && GetPhase() == LxrRcPhase::Mutator)
        {
            GCToOSInterface::YieldThread(0);
        }
        m_gate_blocked.store(false, std::memory_order_release);
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
    return result;
}

#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
void LxrReferenceCounting::ArmTraceGateForTest(uint64_t packets)
{
    m_gate_remaining.store(static_cast<int64_t>(packets), std::memory_order_release);
    m_gate_armed.store(true, std::memory_order_release);
}

LxrRcResult LxrReferenceCounting::QueueTraceBacklogForTest(Object* object, size_t count)
{
    if (GetPhase() != LxrRcPhase::Mutator || GetTracePhase() != LxrTracePhase::Concurrent)
    {
        return LxrRcResult::InvalidPhase;
    }
    uintptr_t reference = reinterpret_cast<uintptr_t>(object);
    for (size_t index = 0; index < count; index++)
    {
        LxrRcResult result = QueueTrace(&reference, 1);
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    return LxrRcResult::Success;
}
#endif // LXR_SIDE_METADATA_TEST_PLATFORM

#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
LxrRcResult LxrReferenceCounting::CopyMatureForTest(Object* source, Object** copy)
{
    Object* object = source;
    LxrRcResult result = TryCopyMature(&object);
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    *copy = object;
    uintptr_t value = reinterpret_cast<uintptr_t>(object);
    return QueueTrace(&value, 1);
}

LxrRcResult LxrReferenceCounting::ValidateTraceBatchForTest(Object* object, size_t count)
{
    if (GetPhase() != LxrRcPhase::Mutator || GetTracePhase() != LxrTracePhase::Inactive ||
        m_trace_pending.load() != 0 || m_work.load() != nullptr ||
        !IsObjectStart(object) || count == 0 || count > FullTraceBatchCapacity + 1)
    {
        return LxrRcResult::InvalidPhase;
    }
    {
        TraceBatch targets(this);
        if (m_trace_pending.load() != 1)
        {
            return Fail(LxrRcResult::InvariantViolation);
        }
        for (size_t index = 0; index < count; index++)
        {
            LxrRcResult result = targets.Append(reinterpret_cast<uintptr_t>(object));
            if (result != LxrRcResult::Success)
            {
                return result;
            }
        }
        if (m_trace_pending.load() == 0)
        {
            return Fail(LxrRcResult::InvariantViolation);
        }
        LxrRcResult result = targets.Flush();
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    if (GetFailure() != LxrRcResult::Success)
    {
        return GetFailure();
    }
    LxrRcResult result = DrainTraceInsidePause();
    if (result == LxrRcResult::Success && m_trace_pending.load() != 0)
    {
        return Fail(LxrRcResult::InvariantViolation);
    }
    DeleteWork();
    return result;
}

LxrRcResult LxrReferenceCounting::WatchRememberedSlot(Object** slot)
{
    if (GetPhase() != LxrRcPhase::Mutator || slot == nullptr)
    {
        return LxrRcResult::InvalidPhase;
    }
    m_watched_remembered_slot.store(reinterpret_cast<uintptr_t>(slot), std::memory_order_release);
    m_remembered_payload_reads.store(0, std::memory_order_release);
    return LxrRcResult::Success;
}

uint64_t LxrReferenceCounting::GetRememberedPayloadReads() const
{
    return m_remembered_payload_reads.load(std::memory_order_acquire);
}

uintptr_t LxrReferenceCounting::GetMatureScanReceipt(size_t field) const
{
    return field < 6 ? m_mature_scan_receipt[field].load(std::memory_order_acquire) : UINTPTR_MAX;
}

LxrRcResult LxrReferenceCounting::HoldTracePacketsForTest(bool hold)
{
    if (GetPhase() != LxrRcPhase::Mutator || GetTracePhase() != LxrTracePhase::Concurrent)
    {
        return LxrRcResult::InvalidPhase;
    }
    m_hold_trace.store(hold, std::memory_order_release);
    return LxrRcResult::Success;
}

size_t LxrReferenceCounting::GetCompletedScanUsers(Object* object) const
{
    for (TraceScan* scan = m_scan_work.load(std::memory_order_acquire); scan != nullptr; scan = scan->next)
    {
        if (scan->object == object && scan->complete.load(std::memory_order_acquire))
        {
            return scan->users.load(std::memory_order_acquire);
        }
    }
    return 0;
}

LxrRcResult LxrReferenceCounting::ArmPendingReader(Object* object)
{
    uintptr_t count;
    if (GetPhase() != LxrRcPhase::Mutator || GetTracePhase() != LxrTracePhase::Concurrent ||
        m_reader_target.load(std::memory_order_acquire) != 0 ||
        LoadCount(object, &count) != LxrRcResult::Success ||
        (count != 0 && !(m_configuration.evacuateMature && IsMatureCandidate(Start(object)))))
    {
        return LxrRcResult::InvalidPhase;
    }
    m_reader_target.store(reinterpret_cast<uintptr_t>(object), std::memory_order_release);
    m_reader_held.store(true, std::memory_order_release);
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::ReleasePendingReader()
{
    if (m_reader_work.load(std::memory_order_acquire) == nullptr ||
        GetPhase() != LxrRcPhase::Mutator || GetTracePhase() != LxrTracePhase::Concurrent)
    {
        return LxrRcResult::InvalidPhase;
    }
    m_reader_held.store(false, std::memory_order_release);
    m_reader_work.store(nullptr, std::memory_order_release);
    return PublishTraceWork();
}

uintptr_t LxrReferenceCounting::GetPendingReaderValue() const
{
    Work* work = m_reader_work.load(std::memory_order_acquire);
    return work == nullptr ? 0 : work->references[0];
}

uintptr_t LxrReferenceCounting::GetPendingReaderInitialValue() const
{
    return m_reader_initial.load(std::memory_order_acquire);
}

uintptr_t LxrReferenceCounting::GetPendingReaderConsumedValue() const
{
    return m_reader_consumed.load(std::memory_order_acquire);
}

uint64_t LxrReferenceCounting::GetPendingReaderId() const
{
    return m_reader_id.load(std::memory_order_acquire);
}

uint64_t LxrReferenceCounting::GetPendingReaderCompletions() const
{
    return m_reader_completions.load(std::memory_order_acquire);
}
#endif // LXR_SIDE_METADATA_TEST_PLATFORM

// The trace of a pause that runs on all workers. The object is claimed by the compare-exchange of its mark, not by a stripe
// lock: no decrement runs in the window (a pause drains the lazy work before it starts, and its own decrements run in a later
// stage), so nothing scans a dying object here, and the claim is the only thing that the lock has to give.
LxrRcResult LxrReferenceCounting::ProcessPauseTraceObject(
    Object* object, uintptr_t start, size_t* directBudget, TraceBatch* targets)
{
    if (m_rc_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) == 0 || !IsObjectStart(object))
    {
        return LxrRcResult::Success;
    }
    Object* loader = GetLoaderAllocator(object);
    size_t maximumReferences = object->GetGCSafeMethodTable()->ContainsGCPointers()
        ? ObjectSize(object) / sizeof(uintptr_t) + 1
        : (loader == nullptr ? 0 : 1);
    uintptr_t observed;
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_PAUSE_TRACE_MARK_CONTROL)
    // Control: the claim is a load and a store, so two workers can both win it.
    if (m_mark_metadata.Load(start, SideMetadataMemoryOrder::SequentiallyConsistent) != 0)
    {
        return LxrRcResult::Success;
    }
    for (volatile int spin = 0; spin < 4000; spin++)
    {
    }
    m_mark_metadata.Store(start, 1, SideMetadataMemoryOrder::SequentiallyConsistent);
#else
    if (!m_mark_metadata.CompareExchange(start, 1, 0, SideMetadataMemoryOrder::SequentiallyConsistent, &observed))
    {
        return LxrRcResult::Success;
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_PAUSE_TRACE_MARK_CONTROL
    (void)observed;
    CountWorker(GetExecutionWorker(), TraceObjectsCounter, 1);
    if (maximumReferences <= *directBudget)
    {
        TraceScan local(object, loader);
        LxrRcResult result = ScanTraceLocked(&local, true, targets);
        *directBudget -= maximumReferences;
        return result;
    }
    TraceScan* scan = new (std::nothrow) TraceScan(object, loader);
    if (scan == nullptr)
    {
        return Fail(LxrRcResult::OutOfMemory);
    }
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
    if (m_storage_diagnostics.load(std::memory_order_relaxed))
    {
        m_storage_metrics[4].fetch_add(1, std::memory_order_relaxed);
        uint64_t current = m_storage_metrics[5].fetch_add(sizeof(TraceScan), std::memory_order_relaxed) +
            sizeof(TraceScan);
        uint64_t high = m_storage_metrics[6].load(std::memory_order_relaxed);
        while (high < current && !m_storage_metrics[6].compare_exchange_weak(
            high, current, std::memory_order_relaxed))
        {
        }
    }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
    TraceScan* head = m_scan_work.load(std::memory_order_relaxed);
    do
    {
        scan->next = head;
    } while (!m_scan_work.compare_exchange_weak(
        head, scan, std::memory_order_release, std::memory_order_relaxed));
#if !defined(LXR_S7_SCAN_LINK_CONTROL)
    LinkTraceScan((start - GetReservationStart()) / sizeof(uintptr_t), scan);
#endif // !LXR_S7_SCAN_LINK_CONTROL
    return QueueTrace(nullptr, 0, scan);
}

LxrRcResult LxrReferenceCounting::ProcessTraceObject(
    Object* object, size_t* directBudget, TraceBatch* targets)
{
    object = ResolveForwardedChecked(object);
    if (object == nullptr)
    {
        return LxrRcResult::Success;
    }
    uintptr_t start = Start(object);
    if (!OwnsAddress(start))
    {
        if (IsReturnedImmix(start))
        {
            return LxrRcResult::Success;
        }
        return m_object_model.isImmortal != nullptr &&
            m_object_model.isImmortal(object, m_object_model.context)
            ? LxrRcResult::Success : Fail(LxrRcResult::UnsupportedLifetime);
    }
    if (start % sizeof(uintptr_t) != 0)
    {
        return Fail(LxrRcResult::InvalidArgument);
    }
    if (m_configuration.evacuateMature)
    {
        LxrRcResult copied = TryCopyMature(&object);
        if (copied != LxrRcResult::Success)
        {
            return copied;
        }
        start = Start(object);
    }
    if (m_mark_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) != 0)
    {
        return LxrRcResult::Success;
    }
    if (m_pause_trace_open.load(std::memory_order_relaxed))
    {
        return ProcessPauseTraceObject(object, start, directBudget, targets);
    }
    size_t index = (start - GetReservationStart()) / sizeof(uintptr_t);
    EnterTraceLock(index);
    uintptr_t count = m_rc_metadata.Load(start, SideMetadataMemoryOrder::SequentiallyConsistent);
    uintptr_t mark = m_mark_metadata.Load(start, SideMetadataMemoryOrder::SequentiallyConsistent);
    LxrRcResult result = LxrRcResult::Success;
    if (count == 0 || mark != 0 || !IsObjectStart(object))
    {
        ExitTraceLock(index);
        return result;
    }
    Object* loader = GetLoaderAllocator(object);
    size_t maximumReferences = object->GetGCSafeMethodTable()->ContainsGCPointers()
        ? ObjectSize(object) / sizeof(uintptr_t) + 1
        : (loader == nullptr ? 0 : 1);
    if (maximumReferences <= *directBudget)
    {
        m_mark_metadata.Store(start, 1, SideMetadataMemoryOrder::SequentiallyConsistent);
        CountWorker(GetExecutionWorker(), TraceObjectsCounter, 1);
        TraceScan local(object, loader);
        result = ScanTraceLocked(&local, true, targets);
        *directBudget -= maximumReferences;
        if (GetPhase() == LxrRcPhase::Mutator && GetTracePhase() == LxrTracePhase::Concurrent)
        {
            m_trace_metrics[ConcurrentSlices].fetch_add(1, std::memory_order_relaxed);
        }
        ExitTraceLock(index);
        return result;
    }
    TraceScan* scan = new (std::nothrow) TraceScan(object, loader);
    if (scan == nullptr)
    {
        ExitTraceLock(index);
        return Fail(LxrRcResult::OutOfMemory);
    }
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
    if (m_storage_diagnostics.load(std::memory_order_relaxed))
    {
        m_storage_metrics[4].fetch_add(1, std::memory_order_relaxed);
        uint64_t current = m_storage_metrics[5].fetch_add(sizeof(TraceScan), std::memory_order_relaxed) +
            sizeof(TraceScan);
        uint64_t high = m_storage_metrics[6].load(std::memory_order_relaxed);
        while (high < current && !m_storage_metrics[6].compare_exchange_weak(
            high, current, std::memory_order_relaxed))
        {
        }
    }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
    TraceScan* head = m_scan_work.load(std::memory_order_relaxed);
    do
    {
        scan->next = head;
    } while (!m_scan_work.compare_exchange_weak(
        head, scan, std::memory_order_release, std::memory_order_relaxed));
#if !defined(LXR_S7_SCAN_LINK_CONTROL)
    LinkTraceScan(index, scan);
#endif // !LXR_S7_SCAN_LINK_CONTROL
    m_mark_metadata.Store(start, 1, SideMetadataMemoryOrder::SequentiallyConsistent);
    CountWorker(GetExecutionWorker(), TraceObjectsCounter, 1);
    result = QueueTrace(nullptr, 0, scan);
    ExitTraceLock(index);
    return result;
}

LxrRcResult LxrReferenceCounting::ScanTraceLocked(TraceScan* scan, bool finish, TraceBatch* targets)
{
    CostScope cost(this, 8);
    uintptr_t children[BufferCapacity];
    size_t count = 0;
    size_t visited = 0;
    const bool pausePhase = GetPhase() == LxrRcPhase::Suspended;
    auto append = [&](uintptr_t target)
    {
        if (targets != nullptr)
        {
            return targets->Append(target);
        }
        children[count++] = target;
        if (count != BufferCapacity)
        {
            return LxrRcResult::Success;
        }
        LxrRcResult result = QueueTrace(children, count);
        count = 0;
        return result;
    };
    while (!scan->complete)
    {
        if (scan->cursor == scan->range.count)
        {
            scan->cursor = 0;
            if (!scan->iterator.Next(&scan->range))
            {
                if (scan->loaderAllocator != nullptr)
                {
                    LxrRcResult result = append(reinterpret_cast<uintptr_t>(scan->loaderAllocator));
                    if (result != LxrRcResult::Success)
                    {
                        return result;
                    }
                    scan->loaderAllocator = nullptr;
                }
                scan->complete = true;
                break;
            }
        }
        Object** slot = scan->range.start + scan->cursor++;
        // A pause has no mutator that writes the slot, so it reads the slot without an exclusive cache line.
        Object* child = pausePhase ? VolatileLoadWithoutBarrier(slot)
            : Interlocked::CompareExchangePointer(slot, static_cast<Object*>(nullptr), nullptr);
        visited++;
        if (child != nullptr)
        {
            if (m_configuration.evacuateMature)
            {
                Object* original = child;
                LxrRcResult result = RememberMatureSlot(slot, child);
                if (result == LxrRcResult::Success)
                {
                    result = TryCopyMature(&child);
                }
                if (result != LxrRcResult::Success)
                {
                    return result;
                }
                // Only a moved child changes the slot: a store of the same value only makes the cache line dirty.
                if (child != original &&
                    (GetTracePhase() == LxrTracePhase::Full || GetTracePhase() == LxrTracePhase::FinalMark))
                {
                    *slot = child;
                }
            }
            LxrRcResult result = append(reinterpret_cast<uintptr_t>(child));
            if (result != LxrRcResult::Success)
            {
                return result;
            }
        }
        if (!finish && visited == BufferCapacity)
        {
            break;
        }
    }
    CountWorker(GetExecutionWorker(), TraceReferencesCounter, visited);
    if (count != 0)
    {
        LxrRcResult result = QueueTrace(children, count);
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::ProcessTraceScan(TraceScan* scan, bool finish, TraceBatch* targets)
{
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_MATURE_DEAD_SCAN_CONTROL)
    if (scan->complete.load(std::memory_order_acquire))
    {
        return LxrRcResult::Success;
    }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_MATURE_DEAD_SCAN_CONTROL
    Object* source = scan->object;
    LxrRcResult copied = TryCopyMature(&scan->object);
    if (copied != LxrRcResult::Success)
    {
        return copied;
    }
    if (source != scan->object)
    {
        uintptr_t destination = reinterpret_cast<uintptr_t>(scan->object);
        copied = targets == nullptr ? QueueTrace(&destination, 1) : targets->Append(destination);
        if (copied != LxrRcResult::Success)
        {
            return copied;
        }
    }
    size_t index = (Start(scan->object) - GetReservationStart()) / sizeof(uintptr_t);
    EnterTraceLock(index);
    LxrRcResult result = scan->complete ? LxrRcResult::Success : ScanTraceLocked(scan, finish, targets);
    bool complete = scan->complete;
    ExitTraceLock(index);
    if (!finish && GetPhase() == LxrRcPhase::Mutator && GetTracePhase() == LxrTracePhase::Concurrent)
    {
        m_trace_metrics[ConcurrentSlices].fetch_add(1, std::memory_order_relaxed);
#ifdef LXR_HAS_DIAGNOSTIC_PROBES
        uintptr_t target = reinterpret_cast<uintptr_t>(scan->object);
        if (!complete && m_scan_checkpoint.load(std::memory_order_acquire) == target)
        {
            // The children of the slice must be visible while the scan waits (the packets are made before the wait).
            if (result == LxrRcResult::Success && targets != nullptr)
            {
                result = targets->Flush();
            }
            // Stop after a real slice, outside the object lock. A pause releases the checkpoint before quiescing workers.
            m_scan_checkpoint_active.store(true, std::memory_order_release);
            while (m_scan_checkpoint.load(std::memory_order_acquire) == target &&
                GetPhase() == LxrRcPhase::Mutator)
            {
                GCToOSInterface::YieldThread(0);
            }
            m_scan_checkpoint_active.store(false, std::memory_order_release);
        }
#endif // LXR_HAS_DIAGNOSTIC_PROBES
    }
    if (result == LxrRcResult::Success && !complete)
    {
        m_trace_metrics[SplitPackets].fetch_add(1, std::memory_order_relaxed);
        result = QueueTrace(nullptr, 0, scan);
    }
    return result;
}

LxrRcResult LxrReferenceCounting::FinishDyingTraceObject(Object* object)
{
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_TRACE_SKIP_DELETION_CONTROL)
    return LxrRcResult::Success;
#else
    uintptr_t start = Start(object);
    size_t index = (start - GetReservationStart()) / sizeof(uintptr_t);
    EnterTraceLock(index);
    LxrRcResult result = LxrRcResult::Success;
    if (m_mark_metadata.Load(start, SideMetadataMemoryOrder::SequentiallyConsistent) == 0)
    {
        m_mark_metadata.Store(start, 1, SideMetadataMemoryOrder::SequentiallyConsistent);
        m_trace_metrics[DeletionMarks].fetch_add(1, std::memory_order_relaxed);
#ifdef LXR_HAS_PHASE_TIMERS
        CountWindow(1, 1);
#endif // LXR_HAS_PHASE_TIMERS
    }
    TraceScan* scan = FindTraceScan(index);
    if (result == LxrRcResult::Success && scan != nullptr && !scan->complete)
    {
        m_trace_metrics[ForcedScans].fetch_add(1, std::memory_order_relaxed);
        result = ScanTraceLocked(scan, true);
    }
    ExitTraceLock(index);
    // ScanDecrements copies every child into trace work before publishing zero.
    return result;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_TRACE_SKIP_DELETION_CONTROL
}

// Runs the trace packets that wait in m_trace_unsubmitted on the calling thread.
LxrRcResult LxrReferenceCounting::ProcessUnsubmittedTrace(bool* progressed)
{
    Work* batch = m_trace_unsubmitted.exchange(nullptr, std::memory_order_acquire);
    while (batch != nullptr)
    {
        Work* work = batch;
        batch = work->traceNext;
        LxrRcResult result = work->kind == WorkKind::ReplayRemembered
            ? ProcessReplayWork(work) : ProcessTraceWork(work);
        RetireTraceWork(work);
        if (result != LxrRcResult::Success)
        {
            while (batch != nullptr)
            {
                Work* rest = batch;
                batch = rest->traceNext;
                PushTraceUnsubmitted(rest);
            }
            return result;
        }
        *progressed = true;
    }
    return LxrRcResult::Success;
}

bool LxrReferenceCounting::CanTraceInParallel() const
{
    return m_configuration.parallelPauseTrace && GetPhase() == LxrRcPhase::Suspended &&
        (m_pause_kind == LxrPauseKind::FinalMark || m_pause_kind == LxrPauseKind::Full) &&
        t_execution_worker <= m_configuration.workers && !m_pause_trace_open.load(std::memory_order_acquire);
}

// Opens the trace of the pause to all workers. The caller is a packet of a pause stage. Returns false when the trace stays on
// the calling thread (the setting is off, or the caller is not a scheduler thread of a FinalMark or Full pause).
bool LxrReferenceCounting::OpenPauseTrace(LxrRcResult* result)
{
    *result = LxrRcResult::Success;
    if (!CanTraceInParallel())
    {
        return false;
    }
    LxrWorkSchedulerResult opened = m_scheduler.OpenPauseTrace();
    if (opened != LxrWorkSchedulerResult::Success)
    {
        // The stage cannot be opened (the engine is shutting down): the serial drain still works.
        return false;
    }
    m_pause_trace_open.store(true, std::memory_order_release);
    m_trace_batch_limit.store(
        std::max<size_t>(1, std::min<size_t>(m_configuration.pauseTraceBatch, FullTraceBatchCapacity)),
        std::memory_order_release);
    m_pause_trace_metrics[0].fetch_add(1, std::memory_order_relaxed);
    *result = SubmitUnsubmittedTrace();
    return true;
}

void LxrReferenceCounting::ClosePauseTrace()
{
    if (!m_pause_trace_open.exchange(false, std::memory_order_acq_rel))
    {
        return;
    }
    m_trace_batch_limit.store(FullTraceBatchCapacity, std::memory_order_release);
    LxrWorkSchedulerResult closed = m_scheduler.ClosePauseTrace(GetExecutionWorker());
    if (closed != LxrWorkSchedulerResult::Success)
    {
        Fail(FromScheduler(closed));
    }
}

// The packets that were made before the trace was open (the SATB entries, the roots) wait in m_trace_unsubmitted. The ones
// that the scheduler does not take stay there; the helper loop runs them on its own thread.
LxrRcResult LxrReferenceCounting::SubmitUnsubmittedTrace()
{
    Work* batch = m_trace_unsubmitted.exchange(nullptr, std::memory_order_acquire);
    while (batch != nullptr)
    {
        Work* work = batch;
        batch = work->traceNext;
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        if (work->watchedReader && m_reader_held.load(std::memory_order_acquire))
        {
            PushTraceUnsubmitted(work);
            continue;
        }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        if (m_scheduler.Submit(&work->packet) != LxrWorkSchedulerResult::Success)
        {
            PushTraceUnsubmitted(work);
        }
    }
    return GetFailure();
}

bool LxrReferenceCounting::PauseTraceDone(void* context)
{
    LxrReferenceCounting* engine = static_cast<LxrReferenceCounting*>(context);
    if (engine->m_trace_unsubmitted.load(std::memory_order_relaxed) != nullptr)
    {
        bool progressed = false;
        LxrRcResult result = engine->ProcessUnsubmittedTrace(&progressed);
        if (result != LxrRcResult::Success)
        {
            engine->Fail(result);
        }
    }
    return engine->m_trace_pending.load(std::memory_order_acquire) == 0 ||
        engine->GetFailure() != LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::DrainTraceParallel()
{
    LxrWorkSchedulerResult helped = m_scheduler.HelpPauseTrace(GetExecutionWorker(), PauseTraceDone, this);
    if (helped != LxrWorkSchedulerResult::Success)
    {
        return Fail(FromScheduler(helped));
    }
    return GetFailure();
}

LxrRcResult LxrReferenceCounting::DrainTraceInsidePause()
{
    if (m_pause_trace_open.load(std::memory_order_acquire))
    {
        LxrRcResult submitted = SubmitUnsubmittedTrace();
        return submitted == LxrRcResult::Success ? DrainTraceParallel() : submitted;
    }
    while (m_trace_pending.load(std::memory_order_acquire) != 0)
    {
        bool progressed = false;
        // P26 (F2). The pending trace packets are in two places: the packets that were made in this pause (or never went to
        // the scheduler) are in m_trace_unsubmitted; the packets of the concurrent phase wait in the scheduler. The stage is
        // closed in a pause, so the pause thread runs them itself.
        LxrRcResult unsubmitted = ProcessUnsubmittedTrace(&progressed);
        if (unsubmitted != LxrRcResult::Success)
        {
            return unsubmitted;
        }
        uint64_t ran = 0;
        LxrWorkSchedulerResult inlineResult = m_scheduler.RunQueuedInline(
            LxrWorkBucketStage::ConcurrentResumable, m_configuration.workers, &ran);
        // InvalidState: the stage is open or a packet runs. Outside a pause nothing is queued that the caller must run.
        if (inlineResult != LxrWorkSchedulerResult::Success && inlineResult != LxrWorkSchedulerResult::InvalidState)
        {
            return FromScheduler(inlineResult);
        }
        if (ran != 0)
        {
            progressed = true;
        }
        if (GetFailure() != LxrRcResult::Success)
        {
            return GetFailure();
        }
        if (!progressed && m_trace_pending.load(std::memory_order_acquire) != 0)
        {
            return Fail(LxrRcResult::InvariantViolation);
        }
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::PrepareTrace()
{
    CostScope cost(this, 7);
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_F1_NO_ROOT_TRACE_CONTROL)
    // Control: the roots of the pause are not queued. A pause that does not queue increment targets then marks nothing.
    return m_pause_kind == LxrPauseKind::InitialMark ? LxrRcResult::Success : DrainTraceInsidePause();
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_F1_NO_ROOT_TRACE_CONTROL
    uintptr_t roots[BufferCapacity];
    size_t count = 0;
    for (size_t offset = 0; offset < m_root_counts[m_current_roots]; offset++)
    {
        uintptr_t root = m_roots[m_current_roots][offset];
        if ((root & 1) == 0)
        {
            roots[count++] = root;
            if (count == BufferCapacity)
            {
                LxrRcResult result = QueueTrace(roots, count);
                if (result != LxrRcResult::Success)
                {
                    return result;
                }
                count = 0;
            }
        }
    }
    if (count != 0)
    {
        LxrRcResult result = QueueTrace(roots, count);
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    return m_pause_kind == LxrPauseKind::InitialMark
        ? LxrRcResult::Success : DrainTraceInsidePause();
}

LxrRcResult LxrReferenceCounting::TraceRoot(Object* object)
{
    if (GetPhase() != LxrRcPhase::Suspended ||
        (GetTracePhase() != LxrTracePhase::Full && GetTracePhase() != LxrTracePhase::FinalMark))
    {
        return LxrRcResult::InvalidPhase;
    }
    uintptr_t root = reinterpret_cast<uintptr_t>(object);
    LxrRcResult result = QueueTrace(&root, 1);
    return result == LxrRcResult::Success ? DrainTraceInsidePause() : result;
}

LxrRcResult LxrReferenceCounting::TraceRoots(const uintptr_t* objects, size_t count)
{
    if (GetPhase() != LxrRcPhase::Suspended ||
        (GetTracePhase() != LxrTracePhase::Full && GetTracePhase() != LxrTracePhase::FinalMark))
    {
        return LxrRcResult::InvalidPhase;
    }
    for (size_t offset = 0; offset < count; offset += BufferCapacity)
    {
        size_t batch = count - offset;
        if (batch > BufferCapacity)
        {
            batch = BufferCapacity;
        }
        LxrRcResult result = QueueTrace(objects + offset, batch);
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    return count == 0 ? LxrRcResult::Success : DrainTraceInsidePause();
}

bool LxrReferenceCounting::IsMarked(Object* object) const
{
    object = ResolveForwardedChecked(object);
    if (object == nullptr || !OwnsAddress(Start(object)) || Start(object) % sizeof(uintptr_t) != 0)
    {
        return false;
    }
    // A black-allocated young object keeps its mark and its ValidObject bit after it dies, so liveness also needs a
    // count above zero. Every live object has one by the time a pause scans weak references.
    return IsObjectStart(object) &&
        m_rc_metadata.Load(Start(object), SideMetadataMemoryOrder::Relaxed) != 0 &&
        m_mark_metadata.Load(Start(object), SideMetadataMemoryOrder::SequentiallyConsistent) != 0;
}

bool LxrReferenceCounting::IsObjectStart(Object* object) const
{
    uintptr_t start = Start(object);
    if (!OwnsAddress(start) || start % sizeof(uintptr_t) != 0)
    {
        return false;
    }
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_OBJECT_INTERIOR_CONTROL)
    if (IsLarge(start) && start % LxrLargeObjectSpace::PageBytes != 0)
    {
        return false;
    }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_OBJECT_INTERIOR_CONTROL
#ifdef LXR_OBJECT_START_WALK
    // No start plane. A large object starts where the large-object space has an allocation; an Immix object starts where
    // the count is not zero (and the slot is not the first slot of a continuation line, which holds a count too).
    if (IsLarge(start))
    {
        LxrLargeObjectAllocation allocation;
        return m_large.GetAllocation(start, &allocation) == LxrLargeObjectResult::Success;
    }
    return m_rc_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) != 0 &&
        !IsContinuationSlot((start - GetReservationStart()) / sizeof(uintptr_t));
#else
    return m_valid_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) != 0;
#endif // LXR_OBJECT_START_WALK
}

bool LxrReferenceCounting::IsLargeObject(Object* object) const
{
#ifdef LXR_OBJECT_START_WALK
    return IsLarge(Start(object)) && IsObjectStart(object);
#else
    return IsObjectStart(object) && IsLarge(Start(object));
#endif // LXR_OBJECT_START_WALK
}

size_t LxrReferenceCounting::GetLargeObjectSize(uintptr_t start) const
{
    LxrLargeObjectAllocation allocation;
    return m_large.GetAllocation(start, &allocation) == LxrLargeObjectResult::Success
        ? allocation.object_bytes : 0;
}

size_t LxrReferenceCounting::GetLiveObjectSize(Object* object) const
{
    if (!IsObjectStart(object))
    {
        return 0;
    }
    uintptr_t start = Start(object);
    if (IsLarge(start))
    {
        return GetLargeObjectSize(start);
    }
    return m_rc_metadata.Load(start, SideMetadataMemoryOrder::Relaxed) != 0 ? ObjectSize(object) : 0;
}

bool LxrReferenceCounting::IsCycleSweepPending() const
{
#if defined(LXR_CYCLE_GATE_CONTROL)
    return false;
#else
    return m_cycle_sweep.load(std::memory_order_acquire);
#endif // LXR_CYCLE_GATE_CONTROL
}

LxrRcResult LxrReferenceCounting::SweepCycles()
{
    LxrRcResult result = ClearDeadRootLeases();
    if (result != LxrRcResult::Success)
    {
        return result;
    }
    return SweepCycleRange(0, GetReservationBytes() / sizeof(uintptr_t));
}

LxrRcResult LxrReferenceCounting::ClearDeadRootLeases()
{
    if (m_trace_pending.load(std::memory_order_acquire) != 0)
    {
        return Fail(LxrRcResult::InvariantViolation);
    }
    // Root packets have drained and the next pause cannot start before this generation retires.
    // Remove leases before a swept address can become a different allocation.
    for (size_t bank = 0; bank < 2; bank++)
    {
        for (size_t index = 0; index < m_root_counts[bank]; index++)
        {
            uintptr_t reference = m_roots[bank][index] & ~static_cast<uintptr_t>(1);
            if (reference != 0 && !IsMarked(reinterpret_cast<Object*>(reference)))
            {
                m_roots[bank][index] = 0;
            }
        }
    }
    return LxrRcResult::Success;
}

// Sweeps the dead cycles whose first slot is in [first, limit). Objects are independent: a chunk touches only the
// metadata of the objects that start in it, so chunks can run on different workers at the same time.
LxrRcResult LxrReferenceCounting::SweepCycleRange(size_t first, size_t limit)
{
    // The totals go to the shared counters once per range: a counter that every worker adds to for every dead object is a
    // contended cache line.
    uint64_t objects = 0;
    uint64_t bytes = 0;
    LxrRcResult status = LxrRcResult::Success;
    for (size_t index = FindNextValidSlot(first, limit); index < limit;
         index = FindNextValidSlot(index + 1, limit))
    {
        uintptr_t start = GetReservationStart() + index * sizeof(uintptr_t);
        // Only an object with a count and without a mark is a dead cycle. A stale bit of a dead young object has
        // count zero, so the sweep never reads such an object.
        uintptr_t count = m_rc_metadata.Load(start, SideMetadataMemoryOrder::SequentiallyConsistent);
        uintptr_t mark = m_mark_metadata.Load(start, SideMetadataMemoryOrder::SequentiallyConsistent);
        if (count == 0 || mark != 0)
        {
            continue;
        }
        Object* object = Reference(start);
        if (ResolveForwarded(object) != object)
        {
            continue;
        }
        LxrRcResult result;
        size_t size;
        if (IsImmix(start))
        {
            // The size comes from the dead object, as in the Java LXR. For an object of a collectible type this
            // reads the type data of a loader allocator that the runtime may free once it knows that the loader is
            // dead. The runtime frees it only from a finalizer, and the collector holds finalization back while
            // m_cycle_sweep is set (see IsCycleSweepPending), so the data is valid until this sweep has finished.
            size = ObjectSize(object);
            result = FromMetadata(m_counts.ClearStraddleLinesFast(start, size));
            if (result != LxrRcResult::Success)
            {
                status = result;
                break;
            }
            size_t block = (start - GetReservationStart()) / ImmixBlockGeometry::BlockBytes;
            // The flags of 64 blocks share a cache line. Write only the first time, so that workers that sweep
            // neighbouring ranges do not take the line from each other for every dead object.
            if (m_mature_candidates[block].load(std::memory_order_relaxed) == 0)
            {
                m_mature_candidates[block].store(1, std::memory_order_release);
            }
        }
        else
        {
            size = GetLargeObjectSize(start);
            size_t dead = m_dead_large_count.fetch_add(1, std::memory_order_relaxed);
            if (dead >= m_dead_large_capacity.load(std::memory_order_relaxed))
            {
                status = Fail(LxrRcResult::CapacityExceeded);
                break;
            }
            m_dead_large[dead] = start;
        }
        result = Reclaim(start);
        if (result != LxrRcResult::Success)
        {
            status = result;
            break;
        }
        m_rc_metadata.Store(start, 0, SideMetadataMemoryOrder::SequentiallyConsistent);
#ifndef LXR_OBJECT_START_WALK
        m_valid_metadata.Store(start, 0, SideMetadataMemoryOrder::SequentiallyConsistent);
#endif // !LXR_OBJECT_START_WALK
        objects++;
        bytes += size;
    }
    if (objects != 0)
    {
        m_trace_metrics[CycleObjects].fetch_add(objects, std::memory_order_relaxed);
        m_trace_metrics[CycleBytes].fetch_add(bytes, std::memory_order_relaxed);
    }
    return status;
}

LxrTracePhase LxrReferenceCounting::GetTracePhase() const
{
    return m_trace_phase.load(std::memory_order_acquire);
}

bool LxrReferenceCounting::IsTraceExhausted() const
{
    return GetFailure() == LxrRcResult::Success && GetTracePhase() == LxrTracePhase::Concurrent &&
        m_trace_pending.load(std::memory_order_acquire) == 0;
}

LxrRcResult LxrReferenceCounting::HoldTrace(bool hold)
{
    if (GetPhase() != LxrRcPhase::Mutator || (hold && GetTracePhase() != LxrTracePhase::Inactive))
    {
        return LxrRcResult::InvalidPhase;
    }
    m_hold_trace.store(hold, std::memory_order_release);
    return !hold && GetTracePhase() == LxrTracePhase::Concurrent
        ? PublishTraceWork() : LxrRcResult::Success;
}

LxrTraceMetrics LxrReferenceCounting::GetTraceMetrics() const
{
    return {
        m_trace_metrics[InitialMarks].load(), m_trace_metrics[FinalMarks].load(),
        m_trace_metrics[FullMarks].load(), SumWorker(TraceObjectsCounter) + m_trace_metrics[TracedObjects].load(),
        SumWorker(TraceReferencesCounter) + m_trace_metrics[TracedReferences].load(), m_trace_metrics[SatbEntries].load(),
        m_trace_metrics[DeletionMarks].load(), m_trace_metrics[ForcedScans].load(),
        m_trace_metrics[SplitPackets].load(), m_trace_metrics[CycleObjects].load(),
        m_trace_metrics[CycleBytes].load(), m_trace_metrics[Interruptions].load(),
        m_trace_metrics[ConcurrentSlices].load(), m_trace_metrics[CheckpointInterruptions].load(),
        WindowMetric(0), WindowMetric(1), WindowMetric(2), WindowMetric(3), WindowMetric(4), WindowMetric(5),
        WindowMetric(6),
    };
}

uint64_t LxrReferenceCounting::WindowMetric(size_t index) const
{
#ifdef LXR_HAS_PHASE_TIMERS
    return m_window_metrics[index].load(std::memory_order_relaxed);
#else
    (void)index;
    return 0;
#endif // LXR_HAS_PHASE_TIMERS
}

LxrPauseWalkMetrics LxrReferenceCounting::GetPauseWalkMetrics() const
{
    return {
        m_walk[0].load(std::memory_order_relaxed), m_walk[1].load(std::memory_order_relaxed),
        m_walk[2].load(std::memory_order_relaxed), m_walk[3].load(std::memory_order_relaxed),
        m_walk[4].load(std::memory_order_relaxed),
    };
}

LxrRcResult LxrReferenceCounting::ArmScanCheckpoint(Object* object)
{
    if (GetPhase() != LxrRcPhase::Mutator || GetTracePhase() != LxrTracePhase::Inactive ||
        !IsOwnedObject(object) || m_scan_checkpoint_active.load(std::memory_order_acquire))
    {
        return LxrRcResult::InvalidPhase;
    }
    m_scan_checkpoint.store(reinterpret_cast<uintptr_t>(object), std::memory_order_release);
    return LxrRcResult::Success;
}

bool LxrReferenceCounting::IsScanCheckpointActive() const
{
    return m_scan_checkpoint_active.load(std::memory_order_acquire);
}

#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
void LxrReferenceCounting::HoldLogReaderForTest(bool hold)
{
    m_log_reader_hold.store(hold, std::memory_order_release);
}

bool LxrReferenceCounting::IsLogReaderHeldForTest() const
{
    return m_log_reader_active.load(std::memory_order_acquire);
}

void LxrReferenceCounting::HoldSweepForTest(bool hold)
{
    m_sweep_hold.store(hold, std::memory_order_release);
}

bool LxrReferenceCounting::IsSweepHeldForTest() const
{
    return m_sweep_holding.load(std::memory_order_acquire);
}

LxrRcResult LxrReferenceCounting::GetBlockStateForTest(
    size_t block, ImmixBlockState* state, uint8_t* unavailableLines, bool* nursery) const
{
    uintptr_t address = GetReservationStart() + block * ImmixBlockGeometry::BlockBytes;
    if (state == nullptr || unavailableLines == nullptr || nursery == nullptr ||
        block >= GetImmixCapacityBytes() / ImmixBlockGeometry::BlockBytes)
    {
        return LxrRcResult::InvalidArgument;
    }
    SideMetadataResult result = m_blocks.GetState(address, state, unavailableLines);
    return result == SideMetadataResult::Success && m_blocks.IsNursery(address, nursery) == SideMetadataResult::Success
        ? LxrRcResult::Success : LxrRcResult::MetadataFailure;
}

size_t LxrReferenceCounting::GetFreshBlocksReleasedInPauseForTest() const
{
    return m_pause_released_blocks.load(std::memory_order_acquire);
}

bool LxrReferenceCounting::IsDataOwnedForTest(uintptr_t address) const
{
    return m_metadata.IsDataAddressOwned(address);
}

LxrRcResult LxrReferenceCounting::PoisonRangeForTest(uintptr_t start, size_t bytes, bool counts)
{
    for (uintptr_t address = start; address < start + bytes; address += sizeof(uintptr_t))
    {
#ifndef LXR_OBJECT_START_WALK
        m_valid_metadata.Store(address, 1, SideMetadataMemoryOrder::SequentiallyConsistent);
#endif // !LXR_OBJECT_START_WALK
        if (counts)
        {
            m_rc_metadata.Store(address, 1, SideMetadataMemoryOrder::SequentiallyConsistent);
        }
        m_unlogged_metadata.Store(address, 1, SideMetadataMemoryOrder::SequentiallyConsistent);
        m_mark_metadata.Store(address, 1, SideMetadataMemoryOrder::SequentiallyConsistent);
    }
    for (uintptr_t line = start; counts && line < start + bytes; line += ImmixBlockGeometry::LineBytes)
    {
        LxrRcResult result = FromMetadata(m_metadata.Store(
            LxrSideMetadataKind::ReferenceCountStraddle, line, 1, SideMetadataMemoryOrder::SequentiallyConsistent));
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::GetRangeStateForTest(
    uintptr_t start, size_t bytes, RangeStateForTest* state)
{
    *state = {};
    for (uintptr_t address = start; address < start + bytes; address += sizeof(uintptr_t))
    {
#ifndef LXR_OBJECT_START_WALK
        state->validSlots += m_valid_metadata.Load(address, SideMetadataMemoryOrder::Relaxed) != 0;
#endif // !LXR_OBJECT_START_WALK
        state->countedSlots += m_rc_metadata.Load(address, SideMetadataMemoryOrder::Relaxed) != 0;
        state->unloggedSlots += m_unlogged_metadata.Load(address, SideMetadataMemoryOrder::Relaxed) != 0;
        state->markedSlots += m_mark_metadata.Load(address, SideMetadataMemoryOrder::Relaxed) != 0;
    }
    for (uintptr_t line = start; line < start + bytes; line += ImmixBlockGeometry::LineBytes)
    {
        uintptr_t straddle = 0;
        if (m_metadata.Load(LxrSideMetadataKind::ReferenceCountStraddle, line,
                SideMetadataMemoryOrder::Relaxed, &straddle) != SideMetadataResult::Success)
        {
            return LxrRcResult::MetadataFailure;
        }
        state->straddleLines += straddle != 0;
    }
    return LxrRcResult::Success;
}

LxrRcResult LxrReferenceCounting::ProcessQueuedTraceReferenceForTest(uintptr_t address)
{
    size_t budget = BufferCapacity;
    return ProcessTraceObject(reinterpret_cast<Object*>(address), &budget, nullptr);
}

LxrRcResult LxrReferenceCounting::ValidateLogStorageForTest(uint8_t test)
{
    if (GetPhase() != LxrRcPhase::Mutator || m_work.load() != nullptr)
    {
        return LxrRcResult::InvalidPhase;
    }
    LogBank& bank = m_banks[m_producer_bank.load()];
    LogStream& stream = bank.increments;
    if ((test == 1 || test == 3 || test == 4) && !stream.EnsureCommitted(this, 1))
    {
        return Fail(LxrRcResult::OutOfMemory);
    }
    if (test == 1)
    {
        stream.reserved.store(1);
        size_t count;
        return ReadPublishedRange(stream, 0, 1, &count);
    }
    uintptr_t values[BufferCapacity];
    for (size_t index = 0; index < BufferCapacity; index++)
    {
        values[index] = LxrSideMetadataLayout::AddressLimit - (index + 1) * sizeof(uintptr_t);
    }
    values[1] = 0;
    if (test == 8)
    {
        return PublishReferences(stream, stream.capacity, values, 1);
    }
    if (test == 2 || test == 6)
    {
        values[1] = UINTPTR_MAX;
        LxrRcResult result = PublishReferences(stream, stream.capacity, values, 2);
        if (result != LxrRcResult::Success || test == 2)
        {
            return result;
        }
        size_t count;
        return ReadPublishedRange(stream, 0, 2, &count) == LxrRcResult::Success &&
            count == 2 && stream.references[0] == values[0] && stream.references[1] == values[1]
            ? ResetBank(bank) : LxrRcResult::InvariantViolation;
    }
    if (test == 3 || test == 4)
    {
        size_t count = test == 3 ? BufferCapacity + 1 : 2;
        stream.references[0] = static_cast<uintptr_t>(count) << LogCountShift;
        stream.reserved.store(test == 3 ? count : 1);
        return ReadPublishedRange(stream, 0, stream.reserved.load(), &count);
    }
    if (test == 7)
    {
        constexpr size_t Producers = 4;
        size_t rounds = stream.capacity / (Producers * BufferCapacity);
        if (rounds == 0 || stream.committed.load() != 0)
        {
            return LxrRcResult::InvalidArgument;
        }
        std::atomic<bool> ready{false};
        std::atomic<size_t> waiting{0};
        std::atomic<size_t> failures{0};
        std::thread producers[Producers];
        for (size_t producer = 0; producer < Producers; producer++)
        {
            producers[producer] = std::thread([&, producer]()
            {
                uintptr_t entries[BufferCapacity];
                std::fill_n(entries, BufferCapacity, producer + 1);
                waiting.fetch_add(1);
                while (!ready.load(std::memory_order_acquire))
                {
                    YieldProcessor();
                }
                for (size_t round = 0; round < rounds; round++)
                {
                    if (PublishReferences(stream, stream.capacity, entries, BufferCapacity - producer) !=
                        LxrRcResult::Success)
                    {
                        failures.fetch_add(1);
                        return;
                    }
                }
            });
        }
        while (waiting.load() != Producers)
        {
            YieldProcessor();
        }
        ready.store(true, std::memory_order_release);
        for (std::thread& producer : producers)
        {
            producer.join();
        }
        if (failures.load() != 0)
        {
            return LxrRcResult::InvariantViolation;
        }
        size_t seen[Producers]{};
        size_t limit = stream.reserved.load();
        for (size_t cursor = 0; cursor < limit;)
        {
            size_t count;
            if (ReadPublishedRange(stream, cursor, limit, &count) != LxrRcResult::Success)
            {
                return LxrRcResult::InvariantViolation;
            }
            uintptr_t value = stream.references[cursor] & LogReferenceMask;
            if (value == 0 || value > Producers || count != BufferCapacity - (value - 1))
            {
                return LxrRcResult::InvariantViolation;
            }
            for (size_t index = 1; index < count; index++)
            {
                if (stream.references[cursor + index] != value)
                {
                    return LxrRcResult::InvariantViolation;
                }
            }
            seen[value - 1]++;
            cursor += count;
        }
        for (size_t count : seen)
        {
            if (count != rounds)
            {
                return LxrRcResult::InvariantViolation;
            }
        }
        if (stream.referenceStorage.GetCommittedBytes() < limit * sizeof(uintptr_t) ||
            bank.decrements.referenceStorage.GetCommittedBytes() != 0 ||
            ResetBank(bank) != LxrRcResult::Success ||
            PublishReferences(stream, stream.capacity, values, 1) != LxrRcResult::Success ||
            ResetBank(bank) != LxrRcResult::Success ||
            stream.referenceStorage.GetCommittedBytes() != LogStream::CommitChunkSlots * sizeof(uintptr_t) ||
            (m_observer && stream.publicationStorage.GetCommittedBytes() !=
                LogStream::CommitChunkSlots * sizeof(std::atomic<uint16_t>)))
        {
            return LxrRcResult::InvariantViolation;
        }
        // The released tail must be usable again, not just absent from the accounting.
        for (size_t index = 0; index < rounds * Producers; index++)
        {
            if (PublishReferences(stream, stream.capacity, values, BufferCapacity) != LxrRcResult::Success)
            {
                return LxrRcResult::InvariantViolation;
            }
        }
        return ResetBank(bank);
    }
    if (m_observer)
    {
        return LxrRcResult::InvalidArgument;
    }
    for (size_t width = 1; width <= BufferCapacity; width++)
    {
        values[0] = width % 2 == 0 ? LxrSideMetadataLayout::AddressLimit - sizeof(uintptr_t) : 0;
        if (PublishReferences(stream, stream.capacity, values, width) != LxrRcResult::Success)
        {
            return LxrRcResult::InvariantViolation;
        }
        size_t count;
        if (ReadPublishedRange(stream, 0, width, &count) != LxrRcResult::Success || count != width)
        {
            return LxrRcResult::InvariantViolation;
        }
        Work borrowed(this, 1, WorkKind::Slots, stream.references, count, LxrRCProcessIncs,
            true, nullptr, true, false);
        Work copied(this, 2, WorkKind::Satb, stream.references, count,
            LxrWorkBucketStage::FinishConcurrentWork, true, nullptr, false, true);
        if (copied.ownedReferences == nullptr)
        {
            return LxrRcResult::OutOfMemory;
        }
        for (size_t index = 0; index < count; index++)
        {
            if (borrowed.GetReference(index) != values[index] || copied.GetReference(index) != values[index])
            {
                return LxrRcResult::InvariantViolation;
            }
        }
        if (ResetBank(bank) != LxrRcResult::Success || stream.reserved.load() != 0 ||
            stream.references[0] != values[0])
        {
            return LxrRcResult::InvariantViolation;
        }
    }
    // Every possible entry can be a separately published partial flush.
    for (size_t index = 0; index < stream.capacity; index++)
    {
        if (PublishReferences(stream, stream.capacity, values, 1) != LxrRcResult::Success)
        {
            return LxrRcResult::InvariantViolation;
        }
    }
    for (size_t index = 0; index < stream.capacity; index++)
    {
        size_t count;
        if (ReadPublishedRange(stream, index, stream.capacity, &count) != LxrRcResult::Success ||
            count != 1 || (stream.references[index] & LogReferenceMask) != values[0])
        {
            return LxrRcResult::InvariantViolation;
        }
    }
    return PublishReferences(stream, stream.capacity, values, 1);
}

LxrRcResult LxrReferenceCounting::MeasureLogCopy(size_t width, size_t* copies, uint64_t* nanoseconds)
{
    if (width == 0 || width > BufferCapacity || copies == nullptr || nanoseconds == nullptr ||
        GetPhase() != LxrRcPhase::Mutator || m_work.load() != nullptr)
    {
        return LxrRcResult::InvalidArgument;
    }
    LogBank& bank = m_banks[m_producer_bank.load()];
    LogStream& stream = bank.increments;
    if (stream.reserved.load() != 0)
    {
        return LxrRcResult::InvalidPhase;
    }
    uintptr_t values[BufferCapacity];
    for (size_t index = 0; index < width; index++)
    {
        values[index] = index + 1;
    }
    *copies = bank.capacity / width < 256 ? bank.capacity / width : 256;
    std::chrono::steady_clock::time_point start = std::chrono::steady_clock::now();
    for (size_t copy = 0; copy < *copies; copy++)
    {
        LxrRcResult result = PublishReferences(stream, bank.capacity, values, width);
        if (result != LxrRcResult::Success)
        {
            return result;
        }
    }
    *nanoseconds = std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::steady_clock::now() - start).count();
    for (size_t copy = 0; copy < *copies; copy++)
    {
        size_t offset = copy * width;
        size_t count;
        if (ReadPublishedRange(stream, offset, *copies * width, &count) != LxrRcResult::Success ||
            count != width || (stream.references[offset] & LogReferenceMask) != values[0] ||
            (width > 1 && memcmp(stream.references + offset + 1, values + 1,
                (width - 1) * sizeof(uintptr_t)) != 0))
        {
            return LxrRcResult::InvariantViolation;
        }
    }
    return ResetBank(bank);
}
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
