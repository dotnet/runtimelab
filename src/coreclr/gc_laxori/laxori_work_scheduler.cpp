// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "laxori_work_scheduler.h"

#ifdef byte
#pragma push_macro("byte")
#define LAXORI_WORK_SCHEDULER_CPP_RESTORE_BYTE
#undef byte
#endif
#ifdef __valid
#pragma push_macro("__valid")
#define LAXORI_WORK_SCHEDULER_CPP_RESTORE_VALID
#undef __valid
#endif

#include <array>
#include <cassert>
#include <chrono>
#include <condition_variable>
#include <deque>
#include <mutex>
#include <new>
#include <thread>
#include <utility>
#include <vector>
#if defined(_MSC_VER)
#include <intrin.h>
#endif

#ifdef LAXORI_WORK_SCHEDULER_CPP_RESTORE_BYTE
#pragma pop_macro("byte")
#undef LAXORI_WORK_SCHEDULER_CPP_RESTORE_BYTE
#endif
#ifdef LAXORI_WORK_SCHEDULER_CPP_RESTORE_VALID
#pragma pop_macro("__valid")
#undef LAXORI_WORK_SCHEDULER_CPP_RESTORE_VALID
#endif

namespace
{
    enum class BucketState : uint8_t
    {
        Closed,
        Open,
        Completed,
    };

    constexpr size_t StageCount = static_cast<size_t>(LaxoriWorkBucketStage::Count);
    constexpr size_t FirstStwIndex = static_cast<size_t>(LaxoriFirstStwStage);

    size_t StageIndex(LaxoriWorkBucketStage stage)
    {
        return static_cast<size_t>(stage);
    }

#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    LaxoriWorkSchedulerResult MapCompletionResult(LaxoriCompletionResult result)
    {
        switch (result)
        {
            case LaxoriCompletionResult::Success:
            case LaxoriCompletionResult::AlreadyCompleted:
                return LaxoriWorkSchedulerResult::Success;
            case LaxoriCompletionResult::StaleGeneration:
                return LaxoriWorkSchedulerResult::StaleGeneration;
            case LaxoriCompletionResult::Overflow:
                return LaxoriWorkSchedulerResult::CompletionOverflow;
            case LaxoriCompletionResult::Underflow:
                return LaxoriWorkSchedulerResult::CompletionUnderflow;
            case LaxoriCompletionResult::Cancelled:
                return LaxoriWorkSchedulerResult::Shutdown;
            case LaxoriCompletionResult::CallbackFailed:
                return LaxoriWorkSchedulerResult::CompletionFailed;
            case LaxoriCompletionResult::InvalidArgument:
                return LaxoriWorkSchedulerResult::InvalidArgument;
            default:
                return LaxoriWorkSchedulerResult::InvalidState;
        }
    }
#endif
}

namespace
{
    // P26 (F4b). Queues of the scheduler. They replace the one global mutex of the scheduler on the hot paths: a worker takes
    // and adds packets without the mutex. The mutex is for the control plane (stages, pauses, deferred queues, parking).

    constexpr size_t StealBatchCapacity = 16;

    // The scheduler that the current thread works for, the index of the worker, and whether the packet that the worker runs
    // now is a trace packet. A packet that a worker submits goes to the deque of this worker.
#if defined(__GNUC__) || defined(__clang__)
#define LAXORI_SCHEDULER_TLS __thread __attribute__((tls_model("initial-exec")))
#else
#define LAXORI_SCHEDULER_TLS thread_local
#endif
    LAXORI_SCHEDULER_TLS const void* tlsScheduler = nullptr;
    LAXORI_SCHEDULER_TLS size_t tlsWorkerIndex = 0;
    LAXORI_SCHEDULER_TLS bool tlsRunningTrace = false;
    // True while the pause thread runs the packets of a stop-the-world cycle itself (RunStwCycle).
    LAXORI_SCHEDULER_TLS bool tlsPauseThread = false;

    // P29. The pause thread asks for help from the workers only when more than this many packets wait for it (it runs that
    // many packets one after the other faster than a sleeping worker wakes up), and a stage with at most this many packets
    // wakes nobody when the pause thread helps.
    constexpr size_t PauseThreadQueueLimit = 2;
    constexpr size_t InlineStagePackets = 2;
    // The pause thread spins this long, waiting for the workers to finish a stage, before it sleeps.
    constexpr int64_t PauseSpinMicroseconds = 150;
    inline void CpuRelax()
    {
#if defined(_MSC_VER) && (defined(_M_X64) || defined(_M_IX86))
        _mm_pause();
#elif defined(__x86_64__) || defined(__i386__)
        __builtin_ia32_pause();
#elif defined(__aarch64__)
        __asm__ __volatile__("yield");
#else
        std::this_thread::yield();
#endif
    }

    // A small test-and-test-and-set lock for the injectors. The thread yields after a few spins, so a machine with one or two
    // processors does not burn its time slice.
    class SpinLock final
    {
    public:
        void Lock()
        {
            unsigned spins = 0;
            while (m_held.exchange(true, std::memory_order_acquire))
            {
                while (m_held.load(std::memory_order_relaxed))
                {
                    if (++spins > 32)
                    {
                        std::this_thread::yield();
                    }
                }
            }
        }

        void Unlock()
        {
            m_held.store(false, std::memory_order_release);
        }

    private:
        std::atomic<bool> m_held{false};
    };

    // The queue of a bucket for packets that no worker owns: packets from threads that are not workers, packets for a stage
    // that is not open yet, and packets that a worker spills. Workers take packets in batches. The Java LAXORI uses a lock-free
    // crossbeam Injector here. This one holds a spin lock for a few instructions; a batch take keeps the number of lock
    // operations low (one for up to 16 packets). The count is read without the lock.
    class Injector final
    {
    public:
        void Push(LaxoriWorkPacket* packet)
        {
            m_lock.Lock();
            m_items.push_back(packet);
            m_count.store(m_items.size(), std::memory_order_seq_cst);
            m_lock.Unlock();
        }

        size_t PopBatch(LaxoriWorkPacket** out, size_t maximum)
        {
            if (m_count.load(std::memory_order_seq_cst) == 0)
            {
                return 0;
            }
            m_lock.Lock();
            size_t count = m_items.size() < maximum ? m_items.size() : maximum;
            for (size_t index = 0; index < count; index++)
            {
                out[index] = m_items.front();
                m_items.pop_front();
            }
            m_count.store(m_items.size(), std::memory_order_seq_cst);
            m_lock.Unlock();
            return count;
        }

        size_t Count() const
        {
            return m_count.load(std::memory_order_seq_cst);
        }

    private:
        SpinLock m_lock;
        std::deque<LaxoriWorkPacket*> m_items;
        std::atomic<size_t> m_count{0};
    };

    // Chase-Lev work-stealing deque of packets (Le, Pop, Cohen, Zappa Nardelli: "Correct and Efficient Work-Stealing for Weak
    // Memory Models", with sequentially consistent operations instead of fences, so that ThreadSanitizer can check it).
    // The owner pushes and pops at the bottom; any thread may steal from the top. The Java LAXORI uses the crossbeam deque.
    // The ring grows when it is full. An old ring stays alive until the deque is destroyed, because a thief may still read it.
    class WorkDeque final
    {
    public:
        WorkDeque()
            : m_top(0), m_bottom(0), m_ring(new Ring(64))
        {
        }

        ~WorkDeque()
        {
            delete m_ring.load();
            for (Ring* ring : m_retired)
            {
                delete ring;
            }
        }

        WorkDeque(const WorkDeque&) = delete;
        WorkDeque& operator=(const WorkDeque&) = delete;

        enum class StealResult
        {
            Empty,
            Success,
            Retry,
        };

        // Owner only.
        void Push(LaxoriWorkPacket* packet)
        {
            int64_t bottom = m_bottom.load(std::memory_order_relaxed);
            int64_t top = m_top.load(std::memory_order_seq_cst);
            Ring* ring = m_ring.load(std::memory_order_relaxed);
            if (bottom - top >= static_cast<int64_t>(ring->capacity))
            {
                Ring* grown = new Ring(ring->capacity * 2);
                for (int64_t index = top; index < bottom; index++)
                {
                    grown->Put(index, ring->Get(index));
                }
                m_retired.push_back(ring);
                m_ring.store(grown, std::memory_order_seq_cst);
                ring = grown;
            }
            ring->Put(bottom, packet);
            m_bottom.store(bottom + 1, std::memory_order_seq_cst);
        }

        // Owner only. Returns null when the deque is empty.
        LaxoriWorkPacket* Pop()
        {
            int64_t bottom = m_bottom.load(std::memory_order_relaxed) - 1;
            Ring* ring = m_ring.load(std::memory_order_relaxed);
            m_bottom.store(bottom, std::memory_order_seq_cst);
            int64_t top = m_top.load(std::memory_order_seq_cst);
            if (top > bottom)
            {
                m_bottom.store(bottom + 1, std::memory_order_relaxed);
                return nullptr;
            }
            LaxoriWorkPacket* packet = ring->Get(bottom);
            if (top == bottom)
            {
                if (!m_top.compare_exchange_strong(top, top + 1, std::memory_order_seq_cst))
                {
                    packet = nullptr;
                }
                m_bottom.store(bottom + 1, std::memory_order_relaxed);
            }
            return packet;
        }

        // Any thread.
        StealResult Steal(LaxoriWorkPacket** packet)
        {
            int64_t top = m_top.load(std::memory_order_seq_cst);
            int64_t bottom = m_bottom.load(std::memory_order_seq_cst);
            if (top >= bottom)
            {
                return StealResult::Empty;
            }
            Ring* ring = m_ring.load(std::memory_order_seq_cst);
            LaxoriWorkPacket* value = ring->Get(top);
#if defined(LAXORI_SIDE_METADATA_TEST_PLATFORM) && defined(LAXORI_F4B_STEAL_NO_CAS_CONTROL)
            // Control: the thief takes the packet without claiming it. The owner or another thief can take it too.
            m_top.store(top + 1, std::memory_order_seq_cst);
#else
            if (!m_top.compare_exchange_strong(top, top + 1, std::memory_order_seq_cst))
            {
                return StealResult::Retry;
            }
#endif // LAXORI_SIDE_METADATA_TEST_PLATFORM && LAXORI_F4B_STEAL_NO_CAS_CONTROL
            *packet = value;
            return StealResult::Success;
        }

        bool MaybeNonEmpty() const
        {
            return m_bottom.load(std::memory_order_seq_cst) > m_top.load(std::memory_order_seq_cst);
        }

    private:
        struct Ring final
        {
            explicit Ring(size_t slots)
                : capacity(slots), items(new std::atomic<LaxoriWorkPacket*>[slots])
            {
            }

            ~Ring()
            {
                delete[] items;
            }

            LaxoriWorkPacket* Get(int64_t index) const
            {
                return items[static_cast<size_t>(index) & (capacity - 1)].load(std::memory_order_relaxed);
            }

            void Put(int64_t index, LaxoriWorkPacket* packet)
            {
                items[static_cast<size_t>(index) & (capacity - 1)].store(packet, std::memory_order_relaxed);
            }

            size_t capacity;
            std::atomic<LaxoriWorkPacket*>* items;
        };

        std::atomic<int64_t> m_top;
        std::atomic<int64_t> m_bottom;
        std::atomic<Ring*> m_ring;
        std::vector<Ring*> m_retired;
    };
}

class LaxoriWorkScheduler::Impl final
{
public:
    // The state of a bucket changes only under the mutex, but workers read it without the mutex, so the fields are atomic.
    struct alignas(64) Bucket final
    {
        std::atomic<BucketState> state{BucketState::Closed};
        std::atomic<bool> enabled{true};
        std::atomic<bool> accepting{true};
        // Packets that are queued or running, and packets that run. A packet is counted in outstanding from Submit until
        // it has completed. The bucket is drained when both are zero.
        std::atomic<uint64_t> outstanding{0};
        std::atomic<uint64_t> executing{0};
        Injector injector;
    };

    // A worker has two deques. The general deque holds packets of the stages that are always runnable while they are open
    // (Unconstrained and the current STW stage). The trace deque holds packets of the concurrent trace stages. A pause
    // closes these stages. Their packets stay in the deque, so a pause does not move them (P26 F2).
    struct alignas(64) Worker final
    {
        WorkDeque general;
        WorkDeque trace;
        std::thread thread;
        bool parked = false;
        std::atomic<uint64_t> executed{0};
        std::atomic<uint64_t> stolen{0};
    };

    struct DeferredQueueSet final
    {
        std::deque<LaxoriWorkPacket*> normal;
        std::deque<LaxoriWorkPacket*> prioritized;
        uint64_t generation = 0;
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
        LaxoriLazyCompletionGenerationState completion;
        LaxoriLazyCompletionRegistration completionSeed;
#endif

        bool IsEmpty() const
        {
            return normal.empty() && prioritized.empty();
        }

        uint64_t Count() const
        {
            return static_cast<uint64_t>(normal.size() + prioritized.size());
        }
    };

    // The published deferred packets. Workers take them with one atomic increment each, without the mutex. The vectors are
    // filled at publication, when no worker reads them, and cleared at the end of the next cycle, when every worker is parked.
    struct ActiveDeferred final
    {
        std::vector<LaxoriWorkPacket*> prioritized;
        std::vector<LaxoriWorkPacket*> normal;
        std::atomic<size_t> prioritizedNext{0};
        std::atomic<size_t> normalNext{0};
        std::atomic<bool> ready{false};

        uint64_t Remaining() const
        {
            if (!ready.load(std::memory_order_acquire))
            {
                return 0;
            }
            size_t prioritizedTaken = prioritizedNext.load(std::memory_order_seq_cst);
            size_t normalTaken = normalNext.load(std::memory_order_seq_cst);
            uint64_t remaining = 0;
            remaining += prioritizedTaken < prioritized.size() ? prioritized.size() - prioritizedTaken : 0;
            remaining += normalTaken < normal.size() ? normal.size() - normalTaken : 0;
            return remaining;
        }

        LaxoriWorkPacket* Take()
        {
            if (!ready.load(std::memory_order_seq_cst))
            {
                return nullptr;
            }
            if (prioritizedNext.load(std::memory_order_seq_cst) < prioritized.size())
            {
                size_t index = prioritizedNext.fetch_add(1, std::memory_order_seq_cst);
                if (index < prioritized.size())
                {
                    return prioritized[index];
                }
            }
            if (normalNext.load(std::memory_order_seq_cst) < normal.size())
            {
                size_t index = normalNext.fetch_add(1, std::memory_order_seq_cst);
                if (index < normal.size())
                {
                    return normal[index];
                }
            }
            return nullptr;
        }
    };

    explicit Impl(
        size_t workerCount,
        bool enableDeferredConcurrentWork
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
        ,
        bool enableLazyCompletionCounter,
        LaxoriDecrementsDrainedCallback decrementsDrained,
        LaxoriOverallDrainedCallback overallDrained,
        void* completionContext,
        uint64_t maximumCompletionOutstanding
#endif
        )
        : workers(workerCount),
          shuttingDown(false),
          cycleActive(false),
          cycleCompleted(false),
          interruptionRequested(false),
          concurrentInterrupted(false),
          generation(0),
          currentStwStage(LaxoriWorkBucketStage::Count),
          deferredEnabled(enableDeferredConcurrentWork),
          activeDeferredIndex(enableDeferredConcurrentWork ? 0 : LaxoriNoDeferredQueue),
          producerDeferredIndex(enableDeferredConcurrentWork ? 1 : LaxoriNoDeferredQueue),
          sealedDeferredIndex(LaxoriNoDeferredQueue),
          resumeRecordedGeneration(0),
          sealedGeneration(0),
          publishedGeneration(0),
          submitted(0),
          executed(0),
          stolen(0),
          spilled(0),
          rejected(0),
          ownershipViolations(0),
          parks(0),
          wakes(0),
          deferredSubmitted(0),
          deferredPublished(0),
          deferredExecuted(0),
          deferredFlips(0),
          deferredEmptyFlips(0)
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
          ,
          completionEnabled(enableLazyCompletionCounter),
          decrementsDrainedCallback(decrementsDrained),
          overallDrainedCallback(overallDrained),
          completionCallbackContext(completionContext),
          maximumCompletionOutstanding(maximumCompletionOutstanding),
          completionFailure(false)
#endif
    {
        buckets[StageIndex(LaxoriWorkBucketStage::Unconstrained)].state = BucketState::Open;
        buckets[StageIndex(LaxoriWorkBucketStage::Concurrent)].enabled = false;
        buckets[StageIndex(LaxoriWorkBucketStage::ConcurrentResumable)].enabled = false;
        if (deferredEnabled)
        {
            deferredQueues[producerDeferredIndex].generation = 1;
        }
        concurrentWasEnabled[0] = false;
        concurrentWasEnabled[1] = false;
        concurrentWasOpen[0] = false;
        concurrentWasOpen[1] = false;
    }

#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    LaxoriCompletionResult InitializeCompletion()
    {
        if (!completionEnabled)
        {
            return LaxoriCompletionResult::Success;
        }
        if (!deferredEnabled ||
            decrementsDrainedCallback == nullptr ||
            overallDrainedCallback == nullptr ||
            producerDeferredIndex >= deferredQueues.size())
        {
            return LaxoriCompletionResult::InvalidArgument;
        }

        DeferredQueueSet& producer = deferredQueues[producerDeferredIndex];
        return producer.completion.Initialize(
            producer.generation,
            decrementsDrainedCallback,
            overallDrainedCallback,
            completionCallbackContext,
            &producer.completionSeed,
            maximumCompletionOutstanding);
    }
#endif

    ~Impl()
    {
        Shutdown();
    }

    void Start()
    {
        for (size_t index = 0; index < workers.size(); index++)
        {
            workers[index].thread = std::thread([this, index]() { WorkerLoop(index); });
        }
    }

    // Takes a packet that Shutdown cancels back to the caller. The caller holds the mutex or is the only thread.
    void CancelPacket(LaxoriWorkPacket* packet, bool decrementOutstanding, std::vector<LaxoriWorkPacket*>* cancelledPackets)
    {
        if (packet->TryTransition(LaxoriWorkPacketState::Queued, LaxoriWorkPacketState::CallerOwned))
        {
            packet->SetGeneration(0);
            packet->SetPreferredWorker(LaxoriWorkScheduler::NoPreferredWorker);
            packet->SetDeferred(false);
            if (decrementOutstanding)
            {
                buckets[StageIndex(packet->GetStage())].outstanding--;
            }
            cancelledPackets->push_back(packet);
        }
        else
        {
            ownershipViolations++;
        }
    }

    void Shutdown()
    {
        std::vector<LaxoriWorkPacket*> cancelledPackets;
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
        uint64_t shutdownOwnershipViolations = 0;
        bool shutdownCompletionFailure = false;
        std::array<LaxoriLazyCompletionRegistration, 2> cancelledSeeds;
#endif
        {
            std::lock_guard<std::mutex> guard(lock);
            if (shuttingDown)
            {
                return;
            }

            shuttingDown = true;
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
            if (completionEnabled)
            {
                for (DeferredQueueSet& queueSet : deferredQueues)
                {
                    LaxoriCompletionSnapshot snapshot =
                        queueSet.completion.GetSnapshot();
                    if (snapshot.generation != 0 &&
                        snapshot.lifecycle !=
                            LaxoriCompletionLifecycle::Uninitialized &&
                        snapshot.lifecycle != LaxoriCompletionLifecycle::Drained &&
                        snapshot.lifecycle !=
                            LaxoriCompletionLifecycle::Cancelled)
                    {
                        queueSet.completion.Cancel(snapshot.generation);
                    }
                }
            }
#endif
            NotifyAllLocked();
        }

        // The workers leave their loop at the next poll. After they have joined, this thread is the only one that touches
        // the queues, so it may empty them without the mutex.
        for (Worker& worker : workers)
        {
            if (worker.thread.joinable())
            {
                worker.thread.join();
            }
        }

        {
            std::lock_guard<std::mutex> guard(lock);
            for (size_t index = 0; index < deferredQueues.size(); index++)
            {
                DeferredQueueSet& queueSet = deferredQueues[index];
                for (std::deque<LaxoriWorkPacket*>* queue : {&queueSet.prioritized, &queueSet.normal})
                {
                    while (!queue->empty())
                    {
                        LaxoriWorkPacket* packet = queue->front();
                        queue->pop_front();
                        CancelPacket(packet, false, &cancelledPackets);
                    }
                }
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
                if (completionEnabled && queueSet.completionSeed.IsActive())
                {
                    cancelledSeeds[index] =
                        std::move(queueSet.completionSeed);
                }
#endif
            }
            if (active.ready.load())
            {
                for (std::vector<LaxoriWorkPacket*>* packets : {&active.prioritized, &active.normal})
                {
                    size_t next = packets == &active.prioritized ? active.prioritizedNext.load() : active.normalNext.load();
                    for (size_t index = next; index < packets->size(); index++)
                    {
                        CancelPacket((*packets)[index], true, &cancelledPackets);
                    }
                    packets->clear();
                }
                active.ready.store(false);
            }
            for (Bucket& bucket : buckets)
            {
                LaxoriWorkPacket* batch[StealBatchCapacity];
                size_t count;
                while ((count = bucket.injector.PopBatch(batch, StealBatchCapacity)) != 0)
                {
                    for (size_t index = 0; index < count; index++)
                    {
                        CancelPacket(batch[index], true, &cancelledPackets);
                    }
                }
            }
            for (Worker& worker : workers)
            {
                for (WorkDeque* deque : {&worker.general, &worker.trace})
                {
                    while (LaxoriWorkPacket* packet = deque->Pop())
                    {
                        CancelPacket(packet, true, &cancelledPackets);
                    }
                }
            }
        }

#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
        for (LaxoriLazyCompletionRegistration& seed : cancelledSeeds)
        {
            if (seed.IsActive())
            {
                seed.Complete();
            }
        }
        for (LaxoriWorkPacket* packet : cancelledPackets)
        {
            LaxoriCompletionResult result = packet->CompleteRegistration();
            if (result != LaxoriCompletionResult::Success &&
                result != LaxoriCompletionResult::AlreadyCompleted &&
                result != LaxoriCompletionResult::Cancelled &&
                result != LaxoriCompletionResult::CallbackFailed)
            {
                shutdownCompletionFailure = true;
                shutdownOwnershipViolations++;
            }
        }
#endif

        if (cancelledCallback != nullptr)
        {
            for (LaxoriWorkPacket* packet : cancelledPackets)
            {
                cancelledCallback(packet, cancelledContext);
            }
        }

#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
        if (completionEnabled)
        {
            for (DeferredQueueSet& queueSet : deferredQueues)
            {
                LaxoriCompletionSnapshot snapshot =
                    queueSet.completion.GetSnapshot();
                if (snapshot.generation != 0 &&
                    snapshot.lifecycle !=
                        LaxoriCompletionLifecycle::Uninitialized &&
                    snapshot.lifecycle != LaxoriCompletionLifecycle::Drained)
                {
                    LaxoriCompletionResult result =
                        queueSet.completion.FinalizeCancellation(
                            snapshot.generation);
                    if (result != LaxoriCompletionResult::Success)
                    {
                        shutdownCompletionFailure = true;
                        shutdownOwnershipViolations++;
                    }
                }
            }
            completionFailure =
                completionFailure || shutdownCompletionFailure;
            ownershipViolations += shutdownOwnershipViolations;
        }
#endif
    }

    // A stage is eligible when its packets may run now: it is open and enabled, and either no pause runs and the stage is
    // concurrent, or it is the Unconstrained stage or the current STW stage. Workers call it without the mutex.
    bool IsEligible(LaxoriWorkBucketStage stage) const
    {
        const Bucket& bucket = buckets[StageIndex(stage)];
        if (!bucket.enabled.load(std::memory_order_seq_cst) ||
            bucket.state.load(std::memory_order_seq_cst) != BucketState::Open)
        {
            return false;
        }

        if (!cycleActive.load(std::memory_order_seq_cst))
        {
            return LaxoriIsConcurrentStage(stage);
        }

        // A pause may open the resumable trace stage for the length of its trace (OpenPauseTrace).
        return stage == LaxoriWorkBucketStage::Unconstrained ||
            stage == currentStwStage.load(std::memory_order_seq_cst) ||
            (stage == LaxoriWorkBucketStage::ConcurrentResumable && pauseTrace.load(std::memory_order_seq_cst));
    }

    bool IsEligibleLocked(LaxoriWorkBucketStage stage) const
    {
        return IsEligible(stage);
    }

    static bool IsTraceStage(LaxoriWorkBucketStage stage)
    {
        return stage == LaxoriWorkBucketStage::Concurrent || stage == LaxoriWorkBucketStage::ConcurrentResumable;
    }

    // Eligible, and not a trace packet beyond the limit of concurrent trace workers (P25 F4).
    bool IsRunnable(LaxoriWorkBucketStage stage) const
    {
        if (!IsEligible(stage))
        {
            return false;
        }
#if defined(LAXORI_SIDE_METADATA_TEST_PLATFORM) && defined(LAXORI_F4_NO_CONC_LIMIT_CONTROL)
        return true;
#else
        return !(IsTraceStage(stage) && !IsTraceLimitBypassed() &&
            concurrentTraceRunning.load(std::memory_order_seq_cst) >= concurrentTraceLimit.load(std::memory_order_relaxed));
#endif // LAXORI_SIDE_METADATA_TEST_PLATFORM && LAXORI_F4_NO_CONC_LIMIT_CONTROL
    }

    bool IsTraceClassRunnable() const
    {
        return IsRunnable(LaxoriWorkBucketStage::Concurrent) || IsRunnable(LaxoriWorkBucketStage::ConcurrentResumable);
    }

    enum class ClaimResult
    {
        Claimed,
        NotRunnable,
        Lost,
    };

    // A worker has taken the packet from a queue. It counts the packet as running first and only then checks that its stage
    // is still runnable. InterruptConcurrentWork closes the stage first and then waits for the count, so one of the two sees the
    // other: a packet never starts after the pause has seen the stage idle.
    ClaimResult TryClaim(LaxoriWorkPacket* packet, size_t workerIndex)
    {
        LaxoriWorkBucketStage stage = packet->GetStage();
        Bucket& bucket = buckets[StageIndex(stage)];
        bucket.executing.fetch_add(1, std::memory_order_seq_cst);
        bool trace = IsTraceStage(stage);
        bool runnable = IsEligible(stage);
        if (runnable && trace)
        {
            size_t running = concurrentTraceRunning.fetch_add(1, std::memory_order_seq_cst);
#if defined(LAXORI_SIDE_METADATA_TEST_PLATFORM) && defined(LAXORI_F4_NO_CONC_LIMIT_CONTROL)
            (void)running;
#else
            if (running >= concurrentTraceLimit.load(std::memory_order_relaxed) &&
                !IsTraceLimitBypassed())
            {
                concurrentTraceRunning.fetch_sub(1, std::memory_order_seq_cst);
                runnable = false;
            }
#endif // LAXORI_SIDE_METADATA_TEST_PLATFORM && LAXORI_F4_NO_CONC_LIMIT_CONTROL
        }
        if (!runnable)
        {
            bucket.executing.fetch_sub(1, std::memory_order_seq_cst);
            NotifyExternal();
            return ClaimResult::NotRunnable;
        }
        if (!packet->TryTransition(LaxoriWorkPacketState::Queued, LaxoriWorkPacketState::Executing))
        {
            ownershipViolations++;
            if (trace)
            {
                concurrentTraceRunning.fetch_sub(1, std::memory_order_seq_cst);
            }
            bucket.executing.fetch_sub(1, std::memory_order_seq_cst);
            NotifyExternal();
            return ClaimResult::Lost;
        }

        size_t preferredWorker = packet->GetPreferredWorker();
        if (preferredWorker != LaxoriWorkScheduler::NoPreferredWorker &&
            preferredWorker != workerIndex)
        {
            if (workerIndex < workers.size())
            {
                workers[workerIndex].stolen++;
            }
            stolen++;
        }
        if (!packet->IsDeferred())
        {
            size_t queued = queuedPackets.load(std::memory_order_relaxed);
            while (queued != 0 &&
                !queuedPackets.compare_exchange_weak(queued, queued - 1, std::memory_order_relaxed))
            {
            }
        }
        return ClaimResult::Claimed;
    }

    // Gives a packet back that the worker could not run after all. It goes to the injector of its stage.
    void Requeue(LaxoriWorkPacket* packet)
    {
        buckets[StageIndex(packet->GetStage())].injector.Push(packet);
    }

    // Finds a packet for the worker and claims it. No mutex.
    LaxoriWorkPacket* Poll(size_t workerIndex)
    {
        Worker& worker = workers[workerIndex];
        LaxoriWorkPacket* packet;

        // 1. The own deque of general packets, newest first.
        while ((packet = worker.general.Pop()) != nullptr)
        {
            ClaimResult claim = TryClaim(packet, workerIndex);
            if (claim == ClaimResult::Claimed)
            {
                return packet;
            }
            if (claim == ClaimResult::NotRunnable)
            {
                worker.general.Push(packet);
                break;
            }
        }

        // 2. The own deque of trace packets, if the stage is open and the limit allows one more.
        bool traceRunnable = IsTraceClassRunnable();
        if (traceRunnable)
        {
            while ((packet = worker.trace.Pop()) != nullptr)
            {
                ClaimResult claim = TryClaim(packet, workerIndex);
                if (claim == ClaimResult::Claimed)
                {
                    return packet;
                }
                if (claim == ClaimResult::NotRunnable)
                {
                    worker.trace.Push(packet);
                    break;
                }
            }
        }

        // 3. The injectors, in the order of the stages. A batch moves to the own deque, as the Java LAXORI does with
        // steal_batch_and_pop.
        for (size_t stageIndex = 0; stageIndex < StageCount; stageIndex++)
        {
            LaxoriWorkBucketStage stage = static_cast<LaxoriWorkBucketStage>(stageIndex);
            if (!IsRunnable(stage))
            {
                continue;
            }
            Bucket& bucket = buckets[stageIndex];
            if (stage == LaxoriWorkBucketStage::Unconstrained)
            {
                packet = active.Take();
                if (packet != nullptr)
                {
                    ClaimResult claim = TryClaim(packet, workerIndex);
                    if (claim == ClaimResult::Claimed)
                    {
                        return packet;
                    }
                    if (claim == ClaimResult::NotRunnable)
                    {
                        Requeue(packet);
                    }
                }
            }
            size_t available = bucket.injector.Count();
            if (available == 0)
            {
                continue;
            }
            LaxoriWorkPacket* batch[StealBatchCapacity];
            size_t want = available / workers.size() + 1;
            size_t count = bucket.injector.PopBatch(batch, want < StealBatchCapacity ? want : StealBatchCapacity);
            if (count == 0)
            {
                continue;
            }
            WorkDeque& local = IsTraceStage(stage) ? worker.trace : worker.general;
            for (size_t index = 1; index < count; index++)
            {
                local.Push(batch[index]);
            }
            ClaimResult claim = TryClaim(batch[0], workerIndex);
            if (claim == ClaimResult::Claimed)
            {
                return batch[0];
            }
            if (claim == ClaimResult::NotRunnable)
            {
                Requeue(batch[0]);
            }
        }

        // 4. Steal from the other workers.
#if defined(LAXORI_SIDE_METADATA_TEST_PLATFORM) && defined(LAXORI_F4B_NO_STEAL_CONTROL)
        if (false)
#else
        if (workers.size() > 1)
#endif // LAXORI_SIDE_METADATA_TEST_PLATFORM && LAXORI_F4B_NO_STEAL_CONTROL
        {
            for (size_t offset = 1; offset < workers.size(); offset++)
            {
                Worker& victim = workers[(workerIndex + offset) % workers.size()];
                for (int cls = 0; cls < 2; cls++)
                {
                    bool traceClass = cls == 1;
                    if (traceClass && !traceRunnable)
                    {
                        continue;
                    }
                    WorkDeque& deque = traceClass ? victim.trace : victim.general;
                    WorkDeque::StealResult result;
                    do
                    {
                        result = deque.Steal(&packet);
                    } while (result == WorkDeque::StealResult::Retry);
                    if (result != WorkDeque::StealResult::Success)
                    {
                        continue;
                    }
                    ClaimResult claim = TryClaim(packet, workerIndex);
                    if (claim == ClaimResult::Claimed)
                    {
                        return packet;
                    }
                    if (claim == ClaimResult::NotRunnable)
                    {
                        (traceClass ? worker.trace : worker.general).Push(packet);
                    }
                }
            }
        }
        return nullptr;
    }

    // True when Poll could find a packet now. The worker has registered as parked before it asks, so a packet that a producer
    // adds after this scan makes the producer see the parked worker.
    bool HasWork(size_t workerIndex) const
    {
        bool traceRunnable = IsTraceClassRunnable();
        for (size_t index = 0; index < workers.size(); index++)
        {
            const Worker& worker = workers[index];
            if (worker.general.MaybeNonEmpty() || (traceRunnable && worker.trace.MaybeNonEmpty()))
            {
                return true;
            }
        }
        (void)workerIndex;
        for (size_t stageIndex = 0; stageIndex < StageCount; stageIndex++)
        {
            LaxoriWorkBucketStage stage = static_cast<LaxoriWorkBucketStage>(stageIndex);
            if (buckets[stageIndex].injector.Count() != 0 && IsRunnable(stage))
            {
                return true;
            }
        }
        return IsEligible(LaxoriWorkBucketStage::Unconstrained) && active.Remaining() != 0;
    }

    bool AllWorkersParkedLocked() const
    {
        for (const Worker& worker : workers)
        {
            if (!worker.parked)
            {
                return false;
            }
        }
        return true;
    }

    bool IsBucketDrainedLocked(LaxoriWorkBucketStage stage) const
    {
        const Bucket& bucket = buckets[StageIndex(stage)];
        return bucket.outstanding.load(std::memory_order_seq_cst) == 0 &&
            bucket.executing.load(std::memory_order_seq_cst) == 0;
    }

    // The stage of the pause is complete when its bucket is drained. While the pause trace is open, the packets of the
    // trace stage count too: a stage does not end before the trace that a packet of the stage started.
    bool IsStageCompleteLocked(LaxoriWorkBucketStage stage) const
    {
        return IsBucketDrainedLocked(stage) &&
            (!pauseTrace.load(std::memory_order_seq_cst) ||
                IsBucketDrainedLocked(LaxoriWorkBucketStage::ConcurrentResumable));
    }

    // The limit of concurrent trace workers does not apply to the trace of a pause: every worker helps.
    bool IsTraceLimitBypassed() const
    {
#if defined(LAXORI_SIDE_METADATA_TEST_PLATFORM) && defined(LAXORI_PAUSE_TRACE_LIMIT_CONTROL)
        return false;
#else
        return pauseTrace.load(std::memory_order_seq_cst);
#endif // LAXORI_SIDE_METADATA_TEST_PLATFORM && LAXORI_PAUSE_TRACE_LIMIT_CONTROL
    }

    void RecordOpenLocked(LaxoriWorkBucketStage stage)
    {
        openTrace.push_back(stage);
    }

    bool SealDeferredCycleLocked()
    {
        if (!deferredEnabled)
        {
            return true;
        }

        assert(sealedDeferredIndex == LaxoriNoDeferredQueue);
        assert(producerDeferredIndex < deferredQueues.size());
        assert(deferredQueues[producerDeferredIndex].generation == generation);
        if (activeDeferredIndex != LaxoriNoDeferredQueue)
        {
            assert(activeDeferredIndex < deferredQueues.size());
            assert(deferredQueues[activeDeferredIndex].IsEmpty());
        }

        size_t nextProducerIndex = producerDeferredIndex == 0 ? 1 : 0;
        assert(nextProducerIndex == activeDeferredIndex);
        assert(deferredQueues[nextProducerIndex].IsEmpty());

#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
        if (completionEnabled)
        {
            DeferredQueueSet& sealed = deferredQueues[producerDeferredIndex];
            LaxoriCompletionResult sealResult =
                sealed.completion.Seal(generation);
            if (sealResult != LaxoriCompletionResult::Success)
            {
                completionFailure = true;
                return false;
            }

            DeferredQueueSet& nextProducer = deferredQueues[nextProducerIndex];
            LaxoriCompletionResult initializeResult =
                nextProducer.completion.Initialize(
                    generation + 1,
                    decrementsDrainedCallback,
                    overallDrainedCallback,
                    completionCallbackContext,
                    &nextProducer.completionSeed,
                    maximumCompletionOutstanding);
            if (initializeResult != LaxoriCompletionResult::Success)
            {
                completionFailure = true;
                return false;
            }
        }
#endif

        sealedDeferredIndex = producerDeferredIndex;
        sealedGeneration = generation;
        producerDeferredIndex = nextProducerIndex;
        deferredQueues[producerDeferredIndex].generation = generation + 1;
        activeDeferredIndex = LaxoriNoDeferredQueue;
        // Every worker is parked and the published packets have all been taken (the Unconstrained bucket is drained).
        active.ready.store(false, std::memory_order_seq_cst);
        active.prioritized.clear();
        active.normal.clear();
        active.prioritizedNext.store(0, std::memory_order_relaxed);
        active.normalNext.store(0, std::memory_order_relaxed);
        return true;
    }

    void RecordCompletionLocked(LaxoriWorkBucketStage stage)
    {
        completionTrace.push_back(stage);
    }

    bool AdvanceBucketsLocked()
    {
        bool openedWork = false;
        while (cycleActive && AllWorkersParkedLocked())
        {
            Bucket& current = buckets[StageIndex(currentStwStage)];
            if (!IsStageCompleteLocked(currentStwStage))
            {
                break;
            }

            current.state = BucketState::Completed;
            RecordCompletionLocked(currentStwStage);
            if (currentStwStage == LaxoriWorkBucketStage::Final)
            {
                if (!SealDeferredCycleLocked())
                {
                    cycleActive = false;
                    cycleCompleted = true;
                    NotifyCycleCompletedLocked();
                    break;
                }
                cycleActive = false;
                cycleCompleted = true;
                NotifyCycleCompletedLocked();
                break;
            }

            size_t nextIndex = StageIndex(currentStwStage) + 1;
            currentStwStage = static_cast<LaxoriWorkBucketStage>(nextIndex);
            Bucket& next = buckets[nextIndex];
            next.state = BucketState::Open;
            RecordOpenLocked(currentStwStage);
            if (next.outstanding != 0)
            {
                openedWork = true;
                break;
            }
        }
        return openedWork;
    }

    // Wakes the threads that wait for the scheduler (WaitFor...) when a counter that they may wait for has changed.
    void NotifyExternal()
    {
        if (externalWaiters.load(std::memory_order_seq_cst) != 0)
        {
            std::lock_guard<std::mutex> guard(lock);
            externalNotifies++;
            condition.notify_all();
        }
    }

    // Runs one claimed packet and completes it. The worker does not touch the packet after the state change to Completed:
    // the owner may free it then. A pause frees processed trace packets only after it has seen executing == 0.
    void RunPacket(LaxoriWorkPacket* packet, size_t workerIndex)
    {
        LaxoriWorkBucketStage stage = packet->GetStage();
        bool trace = IsTraceStage(stage);
        bool deferred = packet->IsDeferred();
        tlsRunningTrace = trace;
        packet->Execute(workerIndex);
        tlsRunningTrace = false;
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
        LaxoriCompletionResult completionResult = packet->CompleteRegistration();
#endif
        Bucket& bucket = buckets[StageIndex(stage)];
        if (packet->TryTransition(
            LaxoriWorkPacketState::Executing,
            LaxoriWorkPacketState::Completed))
        {
            bucket.outstanding--;
            if (workerIndex < workers.size())
            {
                workers[workerIndex].executed++;
            }
            executed++;
            if (deferred)
            {
                deferredExecuted++;
            }
        }
        else
        {
            ownershipViolations++;
        }
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
        if (completionResult != LaxoriCompletionResult::Success &&
            completionResult != LaxoriCompletionResult::AlreadyCompleted &&
            completionResult != LaxoriCompletionResult::Cancelled &&
            completionResult != LaxoriCompletionResult::CallbackFailed)
        {
            ownershipViolations++;
            completionFailure = true;
        }
        if (completionResult == LaxoriCompletionResult::CallbackFailed)
        {
            completionFailure = true;
        }
#endif
        if (trace)
        {
            concurrentTraceRunning.fetch_sub(1, std::memory_order_seq_cst);
        }
        bucket.executing.fetch_sub(1, std::memory_order_seq_cst);
#if defined(LAXORI_SIDE_METADATA_TEST_PLATFORM) && defined(LAXORI_F4_NOTIFY_ALL_CONTROL)
        std::lock_guard<std::mutex> guard(lock);
        NotifyAllLocked();
#else
        // P25 (F4). A completed packet wakes only the threads that wait for the scheduler, and only if there are any.
        NotifyExternal();
#endif // LAXORI_SIDE_METADATA_TEST_PLATFORM && LAXORI_F4_NOTIFY_ALL_CONTROL
    }

    void WorkerLoop(size_t workerIndex)
    {
        tlsScheduler = this;
        tlsWorkerIndex = workerIndex;
        while (!shuttingDown.load(std::memory_order_acquire))
        {
            LaxoriWorkPacket* packet = Poll(workerIndex);
            if (packet != nullptr)
            {
                RunPacket(packet, workerIndex);
                continue;
            }

            std::unique_lock<std::mutex> guard(lock);
            if (shuttingDown)
            {
                break;
            }
            Worker& worker = workers[workerIndex];
            worker.parked = true;
            parkedWorkers.fetch_add(1, std::memory_order_seq_cst);
            parks++;
            // A producer pushes first and reads parkedWorkers second; this thread raised it first and looks second.
            // One of the two sees the other, so a packet cannot be left in a queue while every worker sleeps.
            bool found = HasWork(workerIndex);
            bool openedWork = !found && AdvanceBucketsLocked();
            if (!found && openedWork)
            {
                // A stage opened. This worker runs the first packet itself; the others wake for the rest only.
                WakeForStageLocked(static_cast<size_t>(buckets[StageIndex(currentStwStage)].outstanding), true, true);
            }
            else if (!found)
            {
                workCondition.wait(guard);
                wakes++;
                if (wakeupsInFlight.load() != 0)
                {
                    wakeupsInFlight--;
                }
            }
            worker.parked = false;
            parkedWorkers.fetch_sub(1, std::memory_order_seq_cst);
        }
    }

    std::array<Bucket, StageCount> buckets;
    std::vector<Worker> workers;
    mutable std::mutex lock;
    std::condition_variable condition;
    // P25 (F4). Idle workers sleep on their own condition variable, so that a packet wakes one worker and a packet that
    // completes wakes nobody. The condition variable above is for the threads that wait for the scheduler (WaitFor...).
    std::condition_variable workCondition;
    std::atomic<size_t> parkedWorkers{0};
    // Workers that a submit has woken and that have not run yet, and packets that wait in a queue (not deferred ones).
    std::atomic<size_t> wakeupsInFlight{0};
    std::atomic<size_t> queuedPackets{0};
    std::atomic<size_t> externalWaiters{0};
    std::atomic<uint64_t> signals{0};
    std::atomic<uint64_t> externalWaits{0};
    std::atomic<uint64_t> externalNotifies{0};
    // P25 (F4). At most this many workers run the packets of the concurrent trace while the program runs. The Java LAXORI
    // uses ConcGCThreads of its ParallelGCThreads workers for the concurrent trace. Default: all workers.
    std::atomic<size_t> concurrentTraceLimit{SIZE_MAX};
    std::atomic<size_t> concurrentTraceRunning{0};
    // A pause has opened the resumable trace stage for its own trace (OpenPauseTrace). The limit of concurrent trace workers
    // does not apply while it is true: every worker helps.
    std::atomic<bool> pauseTrace{false};
    CancelledPacketCallback cancelledCallback = nullptr;
    void* cancelledContext = nullptr;
    ActiveDeferred active;

    // Wakes every worker and every waiting thread. For changes of state: a stage opens or closes, a pause starts, shutdown.
    void NotifyAllLocked()
    {
        signals++;
        condition.notify_all();
        workCondition.notify_all();
    }

    // The cycle is complete. Only the thread that waits for the pause needs to wake: every worker is parked, and no worker
    // has work that this change of state makes runnable.
    void NotifyCycleCompletedLocked()
    {
        signals++;
        condition.notify_all();
    }

    // Wakes up to count workers that sleep on the work condition. callerIsParked: the caller is a worker that has counted
    // itself as parked and does not sleep. The mutex is held.
    void WakeWorkersLocked(size_t count, bool callerIsParked)
    {
        size_t parked = parkedWorkers.load();
        size_t inFlight = wakeupsInFlight.load();
        size_t sleeping = parked > inFlight ? parked - inFlight : 0;
        if (callerIsParked && sleeping != 0)
        {
            sleeping--;
        }
        if (count > sleeping)
        {
            count = sleeping;
        }
        if (count == 0)
        {
            return;
        }
        signals++;
        wakeupsInFlight += count;
        if (count == sleeping)
        {
            workCondition.notify_all();
            return;
        }
        for (size_t index = 0; index < count; index++)
        {
            workCondition.notify_one();
        }
    }

    // The mutex is held. A stage opened with the given number of packets. callerRuns: the caller (a worker that opened the
    // stage, or the pause thread) runs one packet itself, so the others need to wake for the rest only. A small stage wakes
    // nobody when the pause thread helps: the caller finishes it before a sleeping worker could start.
    void WakeForStageLocked(size_t packets, bool callerIsParkedWorker, bool callerRuns)
    {
        size_t wake = callerRuns ? (packets > 0 ? packets - 1 : 0) : packets;
        if (pauseHelps && packets <= InlineStagePackets)
        {
            wake = 0;
        }
        WakeWorkersLocked(wake, callerIsParkedWorker);
    }

    // The pause thread takes one packet of the current stop-the-world stage from the injector of the stage. Packets that
    // workers push to their own deques stay with those workers, and a worker runs them before it parks. Lazy packets of
    // other stages are never run on this thread, so they cannot delay the pause.
    LaxoriWorkPacket* PollPause(size_t executionIndex)
    {
        LaxoriWorkBucketStage stage = currentStwStage.load(std::memory_order_seq_cst);
        if (StageIndex(stage) >= StageCount || !IsRunnable(stage))
        {
            return nullptr;
        }
        Bucket& bucket = buckets[StageIndex(stage)];
        LaxoriWorkPacket* packet = nullptr;
        while (bucket.injector.PopBatch(&packet, 1) == 1)
        {
            ClaimResult claim = TryClaim(packet, executionIndex);
            if (claim == ClaimResult::Claimed)
            {
                return packet;
            }
            if (claim == ClaimResult::NotRunnable)
            {
                Requeue(packet);
                return nullptr;
            }
        }
        return nullptr;
    }

    bool PauseHasQueuedWork() const
    {
        LaxoriWorkBucketStage stage = currentStwStage.load(std::memory_order_seq_cst);
        return StageIndex(stage) < StageCount && buckets[StageIndex(stage)].injector.Count() != 0;
    }

    // Takes one packet of the resumable trace stage for a thread that helps with the trace of a pause. A worker takes
    // from its own deque first, then from the injector of the stage, then from the deques of the other workers. The
    // pause thread has no deque. A packet of another stage that it meets goes back to the injector of its stage.
    LaxoriWorkPacket* PollPauseTrace(size_t executionIndex)
    {
        constexpr LaxoriWorkBucketStage TraceStage = LaxoriWorkBucketStage::ConcurrentResumable;
        if (!IsRunnable(TraceStage))
        {
            return nullptr;
        }
        LaxoriWorkPacket* packet = nullptr;
        if (executionIndex < workers.size())
        {
            WorkDeque& own = workers[executionIndex].trace;
            while ((packet = own.Pop()) != nullptr)
            {
                if (packet->GetStage() != TraceStage)
                {
                    Requeue(packet);
                    continue;
                }
                ClaimResult claim = TryClaim(packet, executionIndex);
                if (claim == ClaimResult::Claimed)
                {
                    return packet;
                }
                if (claim == ClaimResult::NotRunnable)
                {
                    own.Push(packet);
                    return nullptr;
                }
            }
        }
        Bucket& bucket = buckets[StageIndex(TraceStage)];
        while (bucket.injector.PopBatch(&packet, 1) == 1)
        {
            ClaimResult claim = TryClaim(packet, executionIndex);
            if (claim == ClaimResult::Claimed)
            {
                return packet;
            }
            if (claim == ClaimResult::NotRunnable)
            {
                Requeue(packet);
                return nullptr;
            }
        }
        for (size_t offset = 1; offset <= workers.size(); offset++)
        {
            size_t victimIndex = (executionIndex + offset) % (workers.size() + 1);
            if (victimIndex >= workers.size() || victimIndex == executionIndex)
            {
                continue;
            }
            WorkDeque& deque = workers[victimIndex].trace;
            WorkDeque::StealResult result;
            do
            {
                result = deque.Steal(&packet);
            } while (result == WorkDeque::StealResult::Retry);
            if (result != WorkDeque::StealResult::Success)
            {
                continue;
            }
            if (packet->GetStage() != TraceStage)
            {
                Requeue(packet);
                continue;
            }
            ClaimResult claim = TryClaim(packet, executionIndex);
            if (claim == ClaimResult::Claimed)
            {
                return packet;
            }
            if (claim == ClaimResult::NotRunnable)
            {
                Requeue(packet);
                return nullptr;
            }
        }
        return nullptr;
    }

    // Runs trace packets on the calling thread until done() says so. The caller is the pause thread or a worker that runs a
    // packet of a pause stage; the thread is not a pause thread that wants the workers to take small stages, so a packet
    // that the trace submits wakes a sleeping worker as usual.
    LaxoriWorkSchedulerResult HelpPauseTrace(size_t executionIndex, bool (*done)(void*), void* context)
    {
        PauseThreadReset reset;
        unsigned idle = 0;
        while (!done(context))
        {
            LaxoriWorkPacket* packet = PollPauseTrace(executionIndex);
            if (packet != nullptr)
            {
                RunPacket(packet, executionIndex);
                idle = 0;
                continue;
            }
            if (shuttingDown.load(std::memory_order_acquire))
            {
                return LaxoriWorkSchedulerResult::Shutdown;
            }
            Idle(&idle);
        }
        return LaxoriWorkSchedulerResult::Success;
    }

    // Ends the pause trace. It runs what is left (a pause that has failed leaves packets that finish without work), waits for
    // the packets that run, and closes the stage again. A packet can be left in a queue of the stage only after a failure.
    LaxoriWorkSchedulerResult ClosePauseTrace(size_t executionIndex)
    {
        PauseThreadReset reset;
        Bucket& bucket = buckets[StageIndex(LaxoriWorkBucketStage::ConcurrentResumable)];
        unsigned idle = 0;
        while (true)
        {
            if (IsBucketDrainedLocked(LaxoriWorkBucketStage::ConcurrentResumable))
            {
                std::lock_guard<std::mutex> guard(lock);
                if (IsBucketDrainedLocked(LaxoriWorkBucketStage::ConcurrentResumable))
                {
                    bucket.state = BucketState::Closed;
                    bucket.enabled = false;
                    bucket.accepting = false;
                    pauseTrace.store(false, std::memory_order_seq_cst);
                    return LaxoriWorkSchedulerResult::Success;
                }
            }
            LaxoriWorkPacket* packet = PollPauseTrace(executionIndex);
            if (packet != nullptr)
            {
                RunPacket(packet, executionIndex);
                idle = 0;
                continue;
            }
            if (shuttingDown.load(std::memory_order_acquire))
            {
                return LaxoriWorkSchedulerResult::Shutdown;
            }
            Idle(&idle);
        }
    }

    static void Idle(unsigned* idle)
    {
        if ((++*idle & 15) == 0)
        {
            std::this_thread::yield();
        }
        else
        {
            CpuRelax();
        }
    }

    struct PauseThreadReset final
    {
        bool saved = tlsPauseThread;
        PauseThreadReset() { tlsPauseThread = false; }
        ~PauseThreadReset() { tlsPauseThread = saved; }
    };

    // Spins while keepSpinning() is true, for at most the spin budget. Returns true when keepSpinning() became false.
    template <typename Predicate>
    bool SpinWhile(Predicate keepSpinning)
    {
        // A machine with one worker has one processor in the usual case. The thread that is waited for would not run.
        if (workers.size() < 2)
        {
            return !keepSpinning();
        }
        const auto end = std::chrono::steady_clock::now() + std::chrono::microseconds(PauseSpinMicroseconds);
        for (unsigned spins = 0;; spins++)
        {
            if (!keepSpinning())
            {
                return true;
            }
            if ((spins & 31) == 31 && std::chrono::steady_clock::now() >= end)
            {
                return false;
            }
            CpuRelax();
        }
    }

    // The pause thread runs the packets of the stop-the-world cycle together with the workers, and it opens the next stage
    // when it is the last thread that finishes one. A pause with few packets then needs no other thread at all.
    LaxoriWorkSchedulerResult RunStwCycle(uint32_t timeoutMilliseconds)
    {
        const size_t executionIndex = workers.size();
        const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(timeoutMilliseconds);
        struct PauseThreadScope final
        {
            PauseThreadScope() { tlsPauseThread = true; }
            ~PauseThreadScope() { tlsPauseThread = false; }
        } scope;
        while (true)
        {
            if (cycleCompleted.load(std::memory_order_acquire))
            {
                return LaxoriWorkSchedulerResult::Success;
            }
            LaxoriWorkPacket* packet = PollPause(executionIndex);
            if (packet != nullptr)
            {
                RunPacket(packet, executionIndex);
                continue;
            }

            std::unique_lock<std::mutex> guard(lock);
            if (cycleCompleted)
            {
                return LaxoriWorkSchedulerResult::Success;
            }
            if (shuttingDown)
            {
                return LaxoriWorkSchedulerResult::Shutdown;
            }
            if (!cycleActive)
            {
                return LaxoriWorkSchedulerResult::InvalidState;
            }
            if (PauseHasQueuedWork())
            {
                continue;
            }
            if (AllWorkersParkedLocked() && IsStageCompleteLocked(currentStwStage))
            {
                if (AdvanceBucketsLocked())
                {
                    WakeForStageLocked(
                        static_cast<size_t>(buckets[StageIndex(currentStwStage)].outstanding), false, true);
                }
                continue;
            }

            // Workers run packets of this stage. They open the next stage when the last of them parks.
            guard.unlock();
            if (SpinWhile([this]() { return !cycleCompleted.load(std::memory_order_acquire) && !PauseHasQueuedWork(); }))
            {
                continue;
            }
            guard.lock();
            if (cycleCompleted)
            {
                return LaxoriWorkSchedulerResult::Success;
            }
            externalWaits++;
            if (!condition.wait_until(guard, deadline, [this]() { return cycleCompleted.load() || shuttingDown.load(); }))
            {
                return LaxoriWorkSchedulerResult::Timeout;
            }
        }
    }

    // Wakes one idle worker for one new packet, but only if the workers that are on their way (woken, not yet running) do
    // not already cover the packets that wait. This is "wake as many workers as there is work". A trace packet that cannot
    // run because of the limit wakes nobody: the worker that finishes a trace packet takes it.
    void NotifyNewPacketLocked(LaxoriWorkBucketStage stage)
    {
#if defined(LAXORI_SIDE_METADATA_TEST_PLATFORM) && defined(LAXORI_F4_NOTIFY_ALL_CONTROL)
        // Control: the behaviour before P25 (F4). Every packet wakes everybody.
        (void)stage;
        NotifyAllLocked();
#else
        if (IsTraceStage(stage) && !IsTraceLimitBypassed() &&
            concurrentTraceRunning.load() >= concurrentTraceLimit.load())
        {
            return;
        }
        // A packet of a stage that no worker may run now (a stop-the-world stage before the cycle reaches it) wakes nobody:
        // the change that opens the stage wakes the workers for it.
        if (!IsRunnable(stage))
        {
            return;
        }
        // The pause thread runs the packets of the stage itself. It asks for help only when more packets wait than it
        // runs one after the other without delay.
        if (tlsPauseThread && queuedPackets.load() <= PauseThreadQueueLimit)
        {
            return;
        }
        size_t inFlight = wakeupsInFlight.load();
        if (parkedWorkers.load() > inFlight && queuedPackets.load() > inFlight)
        {
            signals++;
            wakeupsInFlight++;
            workCondition.notify_one();
        }
#endif // LAXORI_SIDE_METADATA_TEST_PLATFORM && LAXORI_F4_NOTIFY_ALL_CONTROL
    }

    // The same for a thread that does not hold the mutex. It takes the mutex only when a worker is parked.
    void NotifyNewPacket(LaxoriWorkBucketStage stage)
    {
#if defined(LAXORI_SIDE_METADATA_TEST_PLATFORM) && defined(LAXORI_F4_NOTIFY_ALL_CONTROL)
        std::lock_guard<std::mutex> guard(lock);
        NotifyNewPacketLocked(stage);
#else
        if (parkedWorkers.load(std::memory_order_seq_cst) == 0)
        {
            return;
        }
        std::lock_guard<std::mutex> guard(lock);
        NotifyNewPacketLocked(stage);
#endif // LAXORI_SIDE_METADATA_TEST_PLATFORM && LAXORI_F4_NOTIFY_ALL_CONTROL
    }

    // Puts a packet that Submit has accepted (and counted in outstanding) into a queue. A worker that submits a packet of a
    // stage that is open keeps it in its own deque; a trace packet only if the worker runs a trace packet now. All other
    // packets go to the injector of the stage.
    void PushQueued(LaxoriWorkPacket* packet, bool mutexHeld)
    {
        LaxoriWorkBucketStage stage = packet->GetStage();
        bool onWorker = tlsScheduler == this;
        bool trace = IsTraceStage(stage);
        if (onWorker && IsEligible(stage) && (!trace || tlsRunningTrace))
        {
            Worker& worker = workers[tlsWorkerIndex];
            (trace ? worker.trace : worker.general).Push(packet);
        }
        else
        {
            if (onWorker && IsEligible(stage))
            {
                spilled++;
            }
            buckets[StageIndex(stage)].injector.Push(packet);
        }
        queuedPackets++;
        if (mutexHeld)
        {
            NotifyNewPacketLocked(stage);
        }
        else
        {
            NotifyNewPacket(stage);
        }
    }

    template <typename Predicate>
    bool WaitFor(std::unique_lock<std::mutex>& guard, std::chrono::milliseconds timeout, Predicate predicate)
    {
        externalWaiters++;
        externalWaits++;
        bool result = condition.wait_for(guard, timeout, predicate);
        externalWaiters--;
        return result;
    }
    std::atomic<bool> shuttingDown;
    std::atomic<bool> cycleActive;
    std::atomic<bool> cycleCompleted;
    // Concurrent packets wait for workers that sleep; WakeAfterResume wakes them. Set under the mutex.
    bool wakePending = false;
    // The pause thread runs the packets of the current cycle together with the workers (RunStwCycle). Set by BeginStwCycle.
    bool pauseHelps = false;
    std::atomic<bool> interruptionRequested;
    bool concurrentInterrupted;
    bool concurrentWasEnabled[2];
    bool concurrentWasOpen[2];
    std::atomic<uint64_t> generation;
    std::atomic<LaxoriWorkBucketStage> currentStwStage;
    std::vector<LaxoriWorkBucketStage> openTrace;
    bool deferredEnabled;
    std::array<DeferredQueueSet, 2> deferredQueues;
    size_t activeDeferredIndex;
    size_t producerDeferredIndex;
    size_t sealedDeferredIndex;
    uint64_t resumeRecordedGeneration;
    uint64_t sealedGeneration;
    uint64_t publishedGeneration;
    std::vector<LaxoriWorkBucketStage> completionTrace;
    std::atomic<uint64_t> submitted;
    std::atomic<uint64_t> executed;
    std::atomic<uint64_t> stolen;
    std::atomic<uint64_t> spilled;
    std::atomic<uint64_t> rejected;
    std::atomic<uint64_t> ownershipViolations;
    std::atomic<uint64_t> parks;
    std::atomic<uint64_t> wakes;
    std::atomic<uint64_t> deferredSubmitted;
    std::atomic<uint64_t> deferredPublished;
    std::atomic<uint64_t> deferredExecuted;
    std::atomic<uint64_t> deferredFlips;
    std::atomic<uint64_t> deferredEmptyFlips;
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    bool completionEnabled;
    LaxoriDecrementsDrainedCallback decrementsDrainedCallback;
    LaxoriOverallDrainedCallback overallDrainedCallback;
    void* completionCallbackContext;
    uint64_t maximumCompletionOutstanding;
    std::atomic<bool> completionFailure;
#endif
};

LaxoriWorkScheduler::LaxoriWorkScheduler()
    : m_impl(nullptr)
{
}

LaxoriWorkScheduler::~LaxoriWorkScheduler()
{
    Shutdown();
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::Initialize(
    size_t workerCount,
    bool enableDeferredConcurrentWork
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    ,
    bool enableLazyCompletionCounter,
    LaxoriDecrementsDrainedCallback decrementsDrained,
    LaxoriOverallDrainedCallback overallDrained,
    void* completionContext,
    uint64_t maximumCompletionOutstanding
#endif
    )
{
    if (workerCount == 0)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    if (m_impl != nullptr)
    {
        return LaxoriWorkSchedulerResult::AlreadyInitialized;
    }

    Impl* implementation = new (std::nothrow) Impl(
        workerCount,
        enableDeferredConcurrentWork
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
        ,
        enableLazyCompletionCounter,
        decrementsDrained,
        overallDrained,
        completionContext,
        maximumCompletionOutstanding
#endif
        );
    if (implementation == nullptr)
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }

    m_impl = implementation;
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    LaxoriCompletionResult completionResult =
        implementation->InitializeCompletion();
    if (completionResult != LaxoriCompletionResult::Success)
    {
        delete implementation;
        m_impl = nullptr;
        return MapCompletionResult(completionResult);
    }
#endif
    implementation->Start();
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::Shutdown()
{
    Impl* implementation = m_impl;
    if (implementation == nullptr)
    {
        return LaxoriWorkSchedulerResult::Success;
    }

    implementation->Shutdown();
    delete implementation;
    m_impl = nullptr;
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::ConfigureConcurrentTraceWorkers(size_t limit)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (limit == 0 || limit > m_impl->workers.size())
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    m_impl->concurrentTraceLimit = limit;
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::SetCancelledPacketCallback(
    CancelledPacketCallback callback, void* context)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    std::lock_guard<std::mutex> guard(m_impl->lock);
    m_impl->cancelledCallback = callback;
    m_impl->cancelledContext = context;
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::RunQueuedInline(
    LaxoriWorkBucketStage stage, size_t executionIndex, uint64_t* ran)
{
    if (ran == nullptr || StageIndex(stage) >= StageCount)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    *ran = 0;
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    size_t stageIndex = StageIndex(stage);
    std::vector<LaxoriWorkPacket*> packets;
    {
        std::lock_guard<std::mutex> guard(m_impl->lock);
        Impl::Bucket& bucket = m_impl->buckets[stageIndex];
        if (m_impl->shuttingDown)
        {
            return LaxoriWorkSchedulerResult::Shutdown;
        }
        if (m_impl->IsEligibleLocked(stage) || bucket.executing != 0)
        {
            return LaxoriWorkSchedulerResult::InvalidState;
        }
        LaxoriWorkPacket* batch[StealBatchCapacity];
        size_t taken;
        while ((taken = bucket.injector.PopBatch(batch, StealBatchCapacity)) != 0)
        {
            for (size_t index = 0; index < taken; index++)
            {
                packets.push_back(batch[index]);
            }
        }
        // The calling thread acts as a thief on the deques of the class of the stage. A packet of another stage goes back to
        // the injector of its own stage.
        bool traceClass = Impl::IsTraceStage(stage);
        for (Impl::Worker& worker : m_impl->workers)
        {
            WorkDeque& deque = traceClass ? worker.trace : worker.general;
            while (true)
            {
                LaxoriWorkPacket* stolen = nullptr;
                WorkDeque::StealResult stealResult = deque.Steal(&stolen);
                if (stealResult == WorkDeque::StealResult::Retry)
                {
                    continue;
                }
                if (stealResult == WorkDeque::StealResult::Empty)
                {
                    break;
                }
                if (stolen->GetStage() == stage)
                {
                    packets.push_back(stolen);
                }
                else
                {
                    m_impl->Requeue(stolen);
                }
            }
        }
        std::vector<LaxoriWorkPacket*> claimed;
        for (LaxoriWorkPacket* packet : packets)
        {
            if (!packet->TryTransition(LaxoriWorkPacketState::Queued, LaxoriWorkPacketState::Executing))
            {
                m_impl->ownershipViolations++;
                continue;
            }
            bucket.executing++;
            size_t queued = m_impl->queuedPackets.load();
            while (queued != 0 && !m_impl->queuedPackets.compare_exchange_weak(queued, queued - 1))
            {
            }
            claimed.push_back(packet);
        }
        packets.swap(claimed);
    }

    for (LaxoriWorkPacket* packet : packets)
    {
        packet->Execute(executionIndex);
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
        LaxoriCompletionResult completionResult = packet->CompleteRegistration();
        if (completionResult != LaxoriCompletionResult::Success &&
            completionResult != LaxoriCompletionResult::AlreadyCompleted &&
            completionResult != LaxoriCompletionResult::Cancelled &&
            completionResult != LaxoriCompletionResult::CallbackFailed)
        {
            std::lock_guard<std::mutex> guard(m_impl->lock);
            m_impl->ownershipViolations++;
            m_impl->completionFailure = true;
        }
#endif
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    Impl::Bucket& bucket = m_impl->buckets[stageIndex];
    for (LaxoriWorkPacket* packet : packets)
    {
        bucket.executing--;
        if (packet->TryTransition(LaxoriWorkPacketState::Executing, LaxoriWorkPacketState::Completed))
        {
            bucket.outstanding--;
            m_impl->executed++;
        }
        else
        {
            m_impl->ownershipViolations++;
        }
    }
    *ran = packets.size();
    if (m_impl->externalWaiters != 0)
    {
        m_impl->externalNotifies++;
        m_impl->condition.notify_all();
    }
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::OpenPauseTrace()
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (m_impl->shuttingDown)
    {
        return LaxoriWorkSchedulerResult::Shutdown;
    }
    if (!m_impl->cycleActive || m_impl->pauseTrace.load())
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }
    Impl::Bucket& bucket = m_impl->buckets[StageIndex(LaxoriWorkBucketStage::ConcurrentResumable)];
    // The bucket is closed by BeginStwCycle; the packets of the concurrent trace that wait in the queues stay where they are.
    bucket.enabled = true;
    bucket.accepting = true;
    bucket.state = BucketState::Open;
    m_impl->pauseTrace.store(true, std::memory_order_seq_cst);
    uint64_t waiting = bucket.outstanding.load();
    if (waiting != 0)
    {
        m_impl->WakeWorkersLocked(static_cast<size_t>(waiting), false);
    }
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::HelpPauseTrace(size_t executionIndex, bool (*done)(void*), void* context)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    if (done == nullptr || executionIndex > m_impl->workers.size())
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    if (!m_impl->pauseTrace.load(std::memory_order_seq_cst))
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }
    return m_impl->HelpPauseTrace(executionIndex, done, context);
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::ClosePauseTrace(size_t executionIndex)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    if (executionIndex > m_impl->workers.size())
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    if (!m_impl->pauseTrace.load(std::memory_order_seq_cst))
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }
    return m_impl->ClosePauseTrace(executionIndex);
}

bool LaxoriWorkScheduler::IsPauseTraceOpen() const
{
    return m_impl != nullptr && m_impl->pauseTrace.load(std::memory_order_seq_cst);
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::Submit(
    LaxoriWorkPacket* packet,
    size_t preferredWorker)
{
    if (packet == nullptr)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    size_t stageIndex = StageIndex(packet->GetStage());
    if (stageIndex >= StageCount)
    {
        return LaxoriWorkSchedulerResult::InvalidStage;
    }
    if ((packet->m_reference_count != 0 && packet->m_references == nullptr) ||
        packet->m_callback == nullptr)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }

    // A worker submits without the mutex (P26 F4b); every other thread takes it, as before. The state that the checks read
    // is atomic, and a worker that submits is running a packet, so no stage can complete and no pause can start under it.
    bool onWorker = tlsScheduler == static_cast<const void*>(m_impl);
    std::unique_lock<std::mutex> guard(m_impl->lock, std::defer_lock);
    if (!onWorker)
    {
        guard.lock();
    }
    if (m_impl->shuttingDown)
    {
        return LaxoriWorkSchedulerResult::Shutdown;
    }

    Impl::Bucket& bucket = m_impl->buckets[stageIndex];
    if (bucket.state == BucketState::Completed)
    {
        m_impl->rejected++;
        return LaxoriWorkSchedulerResult::InvalidState;
    }
    if (!bucket.accepting ||
        (LaxoriIsStwStage(packet->GetStage()) && !bucket.enabled))
    {
        m_impl->rejected++;
        return LaxoriWorkSchedulerResult::StageDisabled;
    }

    uint64_t packetGeneration = m_impl->generation;
    if (LaxoriIsStwStage(packet->GetStage()) && !m_impl->cycleActive)
    {
        packetGeneration++;
    }
    if (packet->GetGeneration() != 0 && packet->GetGeneration() != packetGeneration)
    {
        m_impl->rejected++;
        return LaxoriWorkSchedulerResult::StaleGeneration;
    }
    if (!packet->TryTransition(
        LaxoriWorkPacketState::CallerOwned,
        LaxoriWorkPacketState::Queued))
    {
        m_impl->rejected++;
        return LaxoriWorkSchedulerResult::PacketAlreadyOwned;
    }

    packet->SetGeneration(packetGeneration);
    packet->SetDeferred(false);
    packet->SetPreferredWorker(
        preferredWorker < m_impl->workers.size() ?
            preferredWorker :
            NoPreferredWorker);
    bucket.outstanding++;
    m_impl->submitted++;
    m_impl->PushQueued(packet, !onWorker);
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::ConfigureStwStages(uint16_t enabledStageMask)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    constexpr uint16_t StwStageMask =
        static_cast<uint16_t>(
            ((UINT32_C(1) << static_cast<uint8_t>(LaxoriWorkBucketStage::Count)) - 1) &
            ~((UINT32_C(1) << static_cast<uint8_t>(LaxoriFirstStwStage)) - 1));
    if ((enabledStageMask & ~StwStageMask) != 0)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (m_impl->cycleActive ||
        m_impl->cycleCompleted ||
        m_impl->interruptionRequested)
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }
    for (size_t stageIndex = FirstStwIndex; stageIndex < StageCount; stageIndex++)
    {
        Impl::Bucket& bucket = m_impl->buckets[stageIndex];
        if (bucket.state != BucketState::Closed ||
            bucket.outstanding != 0 ||
            bucket.executing != 0)
        {
            return LaxoriWorkSchedulerResult::Busy;
        }
    }
    for (size_t stageIndex = FirstStwIndex; stageIndex < StageCount; stageIndex++)
    {
        Impl::Bucket& bucket = m_impl->buckets[stageIndex];
        bool enabled = (enabledStageMask & (UINT16_C(1) << stageIndex)) != 0;
        bucket.enabled = enabled;
        bucket.accepting = enabled;
    }
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::SubmitDeferredConcurrent(
    LaxoriWorkPacket* packet,
    LaxoriDeferredWorkPriority priority,
    size_t preferredWorker)
{
    if (packet == nullptr)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    if (packet->GetStage() != LaxoriWorkBucketStage::Unconstrained)
    {
        return LaxoriWorkSchedulerResult::InvalidStage;
    }
    if ((packet->m_reference_count != 0 && packet->m_references == nullptr) ||
        packet->m_callback == nullptr)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    if (priority != LaxoriDeferredWorkPriority::Normal &&
        priority != LaxoriDeferredWorkPriority::Prioritized)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (!m_impl->deferredEnabled)
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }
    if (m_impl->shuttingDown)
    {
        return LaxoriWorkSchedulerResult::Shutdown;
    }
    if (m_impl->producerDeferredIndex >= m_impl->deferredQueues.size())
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }

    Impl::DeferredQueueSet& producer =
        m_impl->deferredQueues[m_impl->producerDeferredIndex];
    uint64_t packetGeneration = producer.generation;
    if (packet->GetGeneration() != 0 &&
        packet->GetGeneration() != packetGeneration)
    {
        m_impl->rejected++;
        return LaxoriWorkSchedulerResult::StaleGeneration;
    }
    if (!packet->TryTransition(
        LaxoriWorkPacketState::CallerOwned,
        LaxoriWorkPacketState::Queued))
    {
        m_impl->rejected++;
        return LaxoriWorkSchedulerResult::PacketAlreadyOwned;
    }

    packet->SetGeneration(packetGeneration);
    packet->SetDeferred(true);
    packet->SetPreferredWorker(
        preferredWorker < m_impl->workers.size() ?
            preferredWorker :
            NoPreferredWorker);
    if (priority == LaxoriDeferredWorkPriority::Prioritized)
    {
        producer.prioritized.push_back(packet);
    }
    else
    {
        producer.normal.push_back(packet);
    }
    m_impl->submitted++;
    m_impl->deferredSubmitted++;
    return LaxoriWorkSchedulerResult::Success;
}

#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
LaxoriWorkSchedulerResult LaxoriWorkScheduler::SubmitTracked(
    LaxoriWorkPacket* packet,
    LaxoriCompletionWorkKind kind,
    size_t preferredWorker)
{
    if (packet == nullptr || !LaxoriIsStwStage(packet->GetStage()))
    {
        return packet == nullptr
            ? LaxoriWorkSchedulerResult::InvalidArgument
            : LaxoriWorkSchedulerResult::InvalidStage;
    }
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    if ((packet->m_reference_count != 0 && packet->m_references == nullptr) ||
        packet->m_callback == nullptr)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (!m_impl->completionEnabled)
    {
        return LaxoriWorkSchedulerResult::CompletionDisabled;
    }
    if (m_impl->shuttingDown)
    {
        return LaxoriWorkSchedulerResult::Shutdown;
    }
    if (m_impl->producerDeferredIndex >= m_impl->deferredQueues.size())
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }

    size_t stageIndex = StageIndex(packet->GetStage());
    Impl::Bucket& bucket = m_impl->buckets[stageIndex];
    if (bucket.state == BucketState::Completed)
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }
    uint64_t packetGeneration = m_impl->generation.load();
    if (!m_impl->cycleActive)
    {
        packetGeneration++;
    }
    Impl::DeferredQueueSet& producer =
        m_impl->deferredQueues[m_impl->producerDeferredIndex];
    if (producer.generation != packetGeneration)
    {
        return LaxoriWorkSchedulerResult::StaleGeneration;
    }
    if (!packet->TryTransition(
            LaxoriWorkPacketState::CallerOwned,
            LaxoriWorkPacketState::Queued))
    {
        return LaxoriWorkSchedulerResult::PacketAlreadyOwned;
    }

    LaxoriLazyCompletionRegistration registration;
    LaxoriCompletionResult completionResult = producer.completion.RegisterRoot(
        packetGeneration,
        kind,
        &registration);
    if (completionResult != LaxoriCompletionResult::Success)
    {
        packet->TryTransition(
            LaxoriWorkPacketState::Queued,
            LaxoriWorkPacketState::CallerOwned);
        return MapCompletionResult(completionResult);
    }
    if (!packet->AttachCompletionRegistration(std::move(registration)))
    {
        packet->TryTransition(
            LaxoriWorkPacketState::Queued,
            LaxoriWorkPacketState::CallerOwned);
        return LaxoriWorkSchedulerResult::InvalidState;
    }

    packet->SetGeneration(packetGeneration);
    packet->SetDeferred(false);
    packet->SetPreferredWorker(
        preferredWorker < m_impl->workers.size()
            ? preferredWorker
            : NoPreferredWorker);
    bucket.outstanding++;
    m_impl->submitted++;
    m_impl->PushQueued(packet, true);
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::SubmitDeferredConcurrentTracked(
    LaxoriWorkPacket* packet,
    LaxoriCompletionWorkKind kind,
    LaxoriDeferredWorkPriority priority,
    size_t preferredWorker)
{
    if (packet == nullptr)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    if (packet->GetStage() != LaxoriWorkBucketStage::Unconstrained)
    {
        return LaxoriWorkSchedulerResult::InvalidStage;
    }
    if ((packet->m_reference_count != 0 && packet->m_references == nullptr) ||
        packet->m_callback == nullptr ||
        (priority != LaxoriDeferredWorkPriority::Normal &&
            priority != LaxoriDeferredWorkPriority::Prioritized))
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (!m_impl->completionEnabled)
    {
        return LaxoriWorkSchedulerResult::CompletionDisabled;
    }
    if (m_impl->shuttingDown)
    {
        return LaxoriWorkSchedulerResult::Shutdown;
    }
    if (m_impl->producerDeferredIndex >= m_impl->deferredQueues.size())
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }

    Impl::DeferredQueueSet& producer =
        m_impl->deferredQueues[m_impl->producerDeferredIndex];
    uint64_t packetGeneration = producer.generation;
    if (packet->GetGeneration() != 0 &&
        packet->GetGeneration() != packetGeneration)
    {
        return LaxoriWorkSchedulerResult::StaleGeneration;
    }
    if (!packet->TryTransition(
            LaxoriWorkPacketState::CallerOwned,
            LaxoriWorkPacketState::Queued))
    {
        return LaxoriWorkSchedulerResult::PacketAlreadyOwned;
    }

    LaxoriLazyCompletionRegistration registration;
    LaxoriCompletionResult completionResult = producer.completion.RegisterRoot(
        packetGeneration,
        kind,
        &registration);
    if (completionResult != LaxoriCompletionResult::Success)
    {
        packet->TryTransition(
            LaxoriWorkPacketState::Queued,
            LaxoriWorkPacketState::CallerOwned);
        return MapCompletionResult(completionResult);
    }
    if (!packet->AttachCompletionRegistration(std::move(registration)))
    {
        packet->TryTransition(
            LaxoriWorkPacketState::Queued,
            LaxoriWorkPacketState::CallerOwned);
        return LaxoriWorkSchedulerResult::InvalidState;
    }

    packet->SetGeneration(packetGeneration);
    packet->SetDeferred(true);
    packet->SetPreferredWorker(
        preferredWorker < m_impl->workers.size()
            ? preferredWorker
            : NoPreferredWorker);
    if (priority == LaxoriDeferredWorkPriority::Prioritized)
    {
        producer.prioritized.push_back(packet);
    }
    else
    {
        producer.normal.push_back(packet);
    }
    m_impl->submitted++;
    m_impl->deferredSubmitted++;
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::SubmitTrackedChild(
    const LaxoriWorkPacket* parent,
    LaxoriWorkPacket* child,
    LaxoriCompletionWorkKind kind,
    size_t preferredWorker)
{
    if (parent == nullptr || child == nullptr)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    if (!parent->HasCompletionRegistration())
    {
        return LaxoriWorkSchedulerResult::CompletionDisabled;
    }

    LaxoriLazyCompletionRegistration registration;
    LaxoriCompletionResult completionResult =
        parent->GetCompletionRegistration().RegisterChild(
            kind,
            &registration);
    if (completionResult != LaxoriCompletionResult::Success)
    {
        return MapCompletionResult(completionResult);
    }
    if (!child->AttachCompletionRegistration(std::move(registration)))
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }

    LaxoriWorkSchedulerResult result = Submit(child, preferredWorker);
    if (result != LaxoriWorkSchedulerResult::Success)
    {
        LaxoriLazyCompletionRegistration detached =
            child->DetachCompletionRegistration();
        detached.Complete();
    }
    return result;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::SubmitMatureSweep(
    LaxoriMatureSweepContinuation* continuation,
    LaxoriWorkPacket* packet,
    size_t preferredWorker)
{
    if (continuation == nullptr || packet == nullptr)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    if (packet->GetStage() != LaxoriWorkBucketStage::Unconstrained)
    {
        return LaxoriWorkSchedulerResult::InvalidStage;
    }

    LaxoriLazyCompletionRegistration registration;
    LaxoriCompletionResult completionResult =
        continuation->RegisterSweep(&registration);
    if (completionResult != LaxoriCompletionResult::Success)
    {
        return MapCompletionResult(completionResult);
    }
    if (!packet->AttachCompletionRegistration(std::move(registration)))
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }

    LaxoriWorkSchedulerResult result = Submit(packet, preferredWorker);
    if (result != LaxoriWorkSchedulerResult::Success)
    {
        LaxoriLazyCompletionRegistration detached =
            packet->DetachCompletionRegistration();
        detached.Complete();
    }
    return result;
}
#endif

LaxoriWorkSchedulerResult LaxoriWorkScheduler::RecycleCompletedPacket(LaxoriWorkPacket* packet)
{
    if (packet == nullptr)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    if (StageIndex(packet->GetStage()) >= StageCount)
    {
        return LaxoriWorkSchedulerResult::InvalidStage;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    if (packet->HasCompletionRegistration())
    {
        return LaxoriWorkSchedulerResult::Busy;
    }
#endif
    if (!packet->TryTransition(
        LaxoriWorkPacketState::Completed,
        LaxoriWorkPacketState::CallerOwned))
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }
    packet->SetGeneration(0);
    packet->SetPreferredWorker(NoPreferredWorker);
    packet->SetDeferred(false);
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::EnableConcurrentStage(LaxoriWorkBucketStage stage)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    if (stage != LaxoriWorkBucketStage::Concurrent &&
        stage != LaxoriWorkBucketStage::ConcurrentResumable)
    {
        return LaxoriWorkSchedulerResult::InvalidStage;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (m_impl->cycleActive || m_impl->interruptionRequested)
    {
        return LaxoriWorkSchedulerResult::Busy;
    }
    Impl::Bucket& bucket = m_impl->buckets[StageIndex(stage)];
    bucket.enabled = true;
    bucket.accepting = true;
    bucket.state = BucketState::Open;
    m_impl->NotifyAllLocked();
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::InterruptConcurrentWork(uint32_t timeoutMilliseconds)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    std::unique_lock<std::mutex> guard(m_impl->lock);
    if (m_impl->cycleActive || m_impl->cycleCompleted || m_impl->concurrentInterrupted)
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }

    constexpr LaxoriWorkBucketStage ConcurrentStages[] = {
        LaxoriWorkBucketStage::Concurrent,
        LaxoriWorkBucketStage::ConcurrentResumable,
    };
    if (!m_impl->interruptionRequested)
    {
        for (size_t index = 0; index < std::size(ConcurrentStages); index++)
        {
            Impl::Bucket& bucket = m_impl->buckets[StageIndex(ConcurrentStages[index])];
            m_impl->concurrentWasEnabled[index] = bucket.enabled;
            m_impl->concurrentWasOpen[index] = bucket.state == BucketState::Open;
        }

        Impl::Bucket& resumable =
            m_impl->buckets[StageIndex(LaxoriWorkBucketStage::ConcurrentResumable)];
        resumable.accepting = false;
        resumable.enabled = false;
        resumable.state = BucketState::Closed;
        m_impl->interruptionRequested = true;
        if (m_impl->IsBucketDrainedLocked(LaxoriWorkBucketStage::Unconstrained) &&
            m_impl->IsBucketDrainedLocked(LaxoriWorkBucketStage::Concurrent))
        {
            // No worker has a packet to run or to finish, so no sleeping worker needs to learn of the interruption.
            m_impl->signals++;
            m_impl->condition.notify_all();
        }
        else
        {
            m_impl->NotifyAllLocked();
        }
    }

    bool ready = m_impl->WaitFor(
        guard,
        std::chrono::milliseconds(timeoutMilliseconds),
        [this]()
        {
            return m_impl->IsBucketDrainedLocked(LaxoriWorkBucketStage::Unconstrained) &&
                m_impl->IsBucketDrainedLocked(LaxoriWorkBucketStage::Concurrent) &&
                m_impl->buckets[
                    StageIndex(LaxoriWorkBucketStage::ConcurrentResumable)].executing == 0;
        });
    if (!ready)
    {
        return LaxoriWorkSchedulerResult::Timeout;
    }

    Impl::Bucket& concurrent =
        m_impl->buckets[StageIndex(LaxoriWorkBucketStage::Concurrent)];
    concurrent.accepting = false;
    concurrent.enabled = false;
    concurrent.state = BucketState::Closed;
    m_impl->concurrentInterrupted = true;
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::CancelConcurrentInterruption()
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (m_impl->cycleActive || !m_impl->interruptionRequested)
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }

    constexpr LaxoriWorkBucketStage ConcurrentStages[] = {
        LaxoriWorkBucketStage::Concurrent,
        LaxoriWorkBucketStage::ConcurrentResumable,
    };
    for (size_t index = 0; index < std::size(ConcurrentStages); index++)
    {
        Impl::Bucket& bucket = m_impl->buckets[StageIndex(ConcurrentStages[index])];
        bucket.accepting = true;
        bucket.enabled = m_impl->concurrentWasEnabled[index];
        bucket.state = m_impl->concurrentWasOpen[index] ?
            BucketState::Open :
            BucketState::Closed;
    }
    m_impl->interruptionRequested = false;
    m_impl->concurrentInterrupted = false;
    m_impl->NotifyAllLocked();
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::BeginStwCycle(bool pauseThreadRuns)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (m_impl->cycleActive || m_impl->cycleCompleted)
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }
    if (!m_impl->IsBucketDrainedLocked(LaxoriWorkBucketStage::Unconstrained) ||
        !m_impl->IsBucketDrainedLocked(LaxoriWorkBucketStage::Concurrent) ||
        m_impl->buckets[StageIndex(LaxoriWorkBucketStage::ConcurrentResumable)].executing != 0)
    {
        return LaxoriWorkSchedulerResult::Busy;
    }
    if (m_impl->deferredEnabled)
    {
        if (m_impl->sealedDeferredIndex != LaxoriNoDeferredQueue ||
            m_impl->producerDeferredIndex >= m_impl->deferredQueues.size() ||
            m_impl->deferredQueues[m_impl->producerDeferredIndex].generation !=
                m_impl->generation + 1)
        {
            return LaxoriWorkSchedulerResult::InvalidState;
        }
        if (m_impl->activeDeferredIndex != LaxoriNoDeferredQueue &&
            m_impl->active.Remaining() != 0)
        {
            return LaxoriWorkSchedulerResult::Busy;
        }
    }

    m_impl->buckets[StageIndex(LaxoriWorkBucketStage::Concurrent)].enabled = false;
    m_impl->buckets[StageIndex(LaxoriWorkBucketStage::Concurrent)].accepting = false;
    m_impl->buckets[StageIndex(LaxoriWorkBucketStage::Concurrent)].state = BucketState::Closed;
    m_impl->buckets[StageIndex(LaxoriWorkBucketStage::ConcurrentResumable)].enabled = false;
    m_impl->buckets[StageIndex(LaxoriWorkBucketStage::ConcurrentResumable)].accepting = false;
    m_impl->buckets[StageIndex(LaxoriWorkBucketStage::ConcurrentResumable)].state = BucketState::Closed;

    m_impl->generation++;
    m_impl->cycleActive = true;
    m_impl->currentStwStage = LaxoriFirstStwStage;
    Impl::Bucket& first = m_impl->buckets[FirstStwIndex];
    first.state = BucketState::Open;
    m_impl->RecordOpenLocked(LaxoriFirstStwStage);
    m_impl->pauseHelps = pauseThreadRuns;
    // Open the stages that have no packets here, without a round of wake-ups: every worker is usually parked, and then no
    // other thread needs to run for this. A worker that is still busy opens them when it parks.
    m_impl->AdvanceBucketsLocked();
    if (!m_impl->cycleCompleted)
    {
        size_t packets = static_cast<size_t>(
            m_impl->buckets[StageIndex(m_impl->currentStwStage)].outstanding);
        if (packets != 0)
        {
            m_impl->WakeForStageLocked(packets, false, m_impl->pauseHelps);
        }
    }
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::ResumeConcurrentWork()
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (m_impl->cycleActive || m_impl->interruptionRequested)
    {
        return LaxoriWorkSchedulerResult::Busy;
    }
    if (m_impl->deferredEnabled)
    {
        if (!m_impl->cycleCompleted ||
            m_impl->sealedDeferredIndex == LaxoriNoDeferredQueue ||
            m_impl->sealedGeneration != m_impl->generation ||
            m_impl->resumeRecordedGeneration == m_impl->generation)
        {
            return LaxoriWorkSchedulerResult::InvalidState;
        }
        m_impl->resumeRecordedGeneration = m_impl->generation;
    }
    for (LaxoriWorkBucketStage stage :
        {LaxoriWorkBucketStage::Concurrent, LaxoriWorkBucketStage::ConcurrentResumable})
    {
        Impl::Bucket& bucket = m_impl->buckets[StageIndex(stage)];
        bucket.enabled = true;
        bucket.accepting = true;
        bucket.state = BucketState::Open;
    }
    m_impl->NotifyAllLocked();
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::PublishDeferredConcurrentWork(
    uint64_t expectedGeneration)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    std::unique_lock<std::mutex> guard(m_impl->lock);
    if (!m_impl->deferredEnabled)
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }
    if (m_impl->shuttingDown)
    {
        return LaxoriWorkSchedulerResult::Shutdown;
    }
    if (m_impl->cycleActive)
    {
        return LaxoriWorkSchedulerResult::Busy;
    }
    if (!m_impl->cycleCompleted ||
        expectedGeneration != m_impl->generation ||
        m_impl->resumeRecordedGeneration != expectedGeneration ||
        m_impl->sealedGeneration != expectedGeneration ||
        m_impl->sealedDeferredIndex == LaxoriNoDeferredQueue ||
        m_impl->activeDeferredIndex != LaxoriNoDeferredQueue)
    {
        m_impl->rejected++;
        return LaxoriWorkSchedulerResult::InvalidState;
    }

    size_t publishedIndex = m_impl->sealedDeferredIndex;
    Impl::DeferredQueueSet& published = m_impl->deferredQueues[publishedIndex];
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    if (m_impl->completionEnabled)
    {
        LaxoriCompletionResult publishResult =
            published.completion.MarkPublished(expectedGeneration);
        if (publishResult != LaxoriCompletionResult::Success)
        {
            m_impl->completionFailure = true;
            return MapCompletionResult(publishResult);
        }
    }
#endif
    uint64_t publishedCount = published.Count();
    // The packets move to the structure that workers read without the mutex. No worker reads it now (ready is false).
    m_impl->active.prioritized.assign(published.prioritized.begin(), published.prioritized.end());
    m_impl->active.normal.assign(published.normal.begin(), published.normal.end());
    published.prioritized.clear();
    published.normal.clear();
    m_impl->active.prioritizedNext.store(0, std::memory_order_relaxed);
    m_impl->active.normalNext.store(0, std::memory_order_relaxed);
    m_impl->active.ready.store(true, std::memory_order_seq_cst);
    m_impl->activeDeferredIndex = publishedIndex;
    m_impl->sealedDeferredIndex = LaxoriNoDeferredQueue;
    m_impl->sealedGeneration = 0;
    m_impl->publishedGeneration = expectedGeneration;
    m_impl->deferredFlips++;
    m_impl->deferredPublished += publishedCount;
    m_impl->buckets[StageIndex(LaxoriWorkBucketStage::Unconstrained)].outstanding +=
        publishedCount;
    if (publishedCount == 0)
    {
        m_impl->deferredEmptyFlips++;
    }
    else
    {
        // As many workers as packets; the packets that these workers submit wake further workers.
        m_impl->WakeWorkersLocked(static_cast<size_t>(publishedCount), false);
    }
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    LaxoriLazyCompletionRegistration completionSeed;
    if (m_impl->completionEnabled)
    {
        completionSeed = std::move(published.completionSeed);
    }
    guard.unlock();
    LaxoriCompletionResult completionResult = completionSeed.IsActive()
        ? completionSeed.Complete()
        : LaxoriCompletionResult::Success;
    guard.lock();
    if (completionResult == LaxoriCompletionResult::CallbackFailed)
    {
        m_impl->completionFailure = true;
    }
    else if (completionResult != LaxoriCompletionResult::Success &&
        completionResult != LaxoriCompletionResult::AlreadyCompleted)
    {
        m_impl->completionFailure = true;
        m_impl->ownershipViolations++;
    }
    // The waiting threads (WaitForCompletion) learn of the completion. No worker needs to wake for it.
    m_impl->signals++;
    m_impl->condition.notify_all();
    if (completionResult == LaxoriCompletionResult::CallbackFailed)
    {
        return LaxoriWorkSchedulerResult::CompletionFailed;
    }
#endif
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::RecordMutatorResume(uint64_t expectedGeneration)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (m_impl->shuttingDown)
    {
        return LaxoriWorkSchedulerResult::Shutdown;
    }
    if (!m_impl->deferredEnabled ||
        m_impl->cycleActive ||
        !m_impl->cycleCompleted ||
        m_impl->interruptionRequested ||
        expectedGeneration != m_impl->generation ||
        m_impl->sealedGeneration != expectedGeneration ||
        m_impl->sealedDeferredIndex == LaxoriNoDeferredQueue ||
        m_impl->resumeRecordedGeneration == expectedGeneration)
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }

    m_impl->resumeRecordedGeneration = expectedGeneration;
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::ResumeInterruptedConcurrentWork(bool deferWake)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (m_impl->cycleActive ||
        (m_impl->cycleCompleted && !m_impl->deferredEnabled) ||
        !m_impl->interruptionRequested)
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }

    bool concurrentWork = false;
    for (LaxoriWorkBucketStage stage :
        {LaxoriWorkBucketStage::Concurrent, LaxoriWorkBucketStage::ConcurrentResumable})
    {
        Impl::Bucket& bucket = m_impl->buckets[StageIndex(stage)];
        bucket.accepting = true;
        bool hasWork = bucket.outstanding != 0;
        bucket.enabled = hasWork;
        bucket.state = hasWork ? BucketState::Open : BucketState::Closed;
        concurrentWork |= hasWork;
    }
    m_impl->interruptionRequested = false;
    m_impl->concurrentInterrupted = false;
    if (deferWake)
    {
        // The workers that must run the concurrent packets wake after the mutators have resumed (WakeAfterResume).
        m_impl->wakePending = concurrentWork;
        m_impl->signals++;
        m_impl->condition.notify_all();
    }
    else
    {
        m_impl->NotifyAllLocked();
    }
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::WakeAfterResume()
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (m_impl->wakePending)
    {
        m_impl->wakePending = false;
        m_impl->NotifyAllLocked();
    }
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::ResetCompletedCycle()
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (m_impl->cycleActive || !m_impl->cycleCompleted)
    {
        return LaxoriWorkSchedulerResult::InvalidState;
    }
    if (m_impl->deferredEnabled)
    {
        if (m_impl->publishedGeneration != m_impl->generation ||
            m_impl->sealedDeferredIndex != LaxoriNoDeferredQueue)
        {
            return LaxoriWorkSchedulerResult::InvalidState;
        }
        if (!m_impl->IsBucketDrainedLocked(LaxoriWorkBucketStage::Unconstrained) ||
            (m_impl->activeDeferredIndex != LaxoriNoDeferredQueue &&
                m_impl->active.Remaining() != 0))
        {
            return LaxoriWorkSchedulerResult::Busy;
        }
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
        if (m_impl->completionEnabled)
        {
            if (m_impl->activeDeferredIndex == LaxoriNoDeferredQueue)
            {
                return LaxoriWorkSchedulerResult::InvalidState;
            }
            Impl::DeferredQueueSet& completed =
                m_impl->deferredQueues[m_impl->activeDeferredIndex];
            LaxoriCompletionSnapshot snapshot =
                completed.completion.GetSnapshot();
            if (snapshot.generation != m_impl->generation)
            {
                return LaxoriWorkSchedulerResult::StaleGeneration;
            }
            if (snapshot.lifecycle == LaxoriCompletionLifecycle::Failed ||
                m_impl->completionFailure)
            {
                return LaxoriWorkSchedulerResult::CompletionFailed;
            }
            if (snapshot.lifecycle != LaxoriCompletionLifecycle::Drained ||
                snapshot.overallOutstanding != 0 ||
                snapshot.decrementOutstanding != 0 ||
                snapshot.callbackRunning)
            {
                return LaxoriWorkSchedulerResult::Busy;
            }
        }
#endif
    }
    for (size_t stageIndex = FirstStwIndex; stageIndex < StageCount; stageIndex++)
    {
        Impl::Bucket& bucket = m_impl->buckets[stageIndex];
        if (bucket.outstanding != 0 || bucket.executing != 0)
        {
            return LaxoriWorkSchedulerResult::Busy;
        }
        bucket.state = BucketState::Closed;
        bucket.enabled = true;
        bucket.accepting = true;
    }
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    if (m_impl->deferredEnabled &&
        m_impl->completionEnabled &&
        m_impl->activeDeferredIndex != LaxoriNoDeferredQueue)
    {
        Impl::DeferredQueueSet& completed =
            m_impl->deferredQueues[m_impl->activeDeferredIndex];
        LaxoriCompletionResult reuseResult =
            completed.completion.PrepareForReuse(m_impl->generation);
        if (reuseResult != LaxoriCompletionResult::Success)
        {
            return MapCompletionResult(reuseResult);
        }
    }
#endif
    m_impl->cycleCompleted = false;
    m_impl->currentStwStage = LaxoriWorkBucketStage::Count;
    m_impl->openTrace.clear();
    m_impl->resumeRecordedGeneration = 0;
    m_impl->completionTrace.clear();
    return LaxoriWorkSchedulerResult::Success;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::WaitForPacket(
    const LaxoriWorkPacket* packet,
    uint32_t timeoutMilliseconds)
{
    if (packet == nullptr)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    std::unique_lock<std::mutex> guard(m_impl->lock);
    bool completed = m_impl->WaitFor(
        guard,
        std::chrono::milliseconds(timeoutMilliseconds),
        [packet]() { return packet->GetState() == LaxoriWorkPacketState::Completed; });
    return completed ? LaxoriWorkSchedulerResult::Success : LaxoriWorkSchedulerResult::Timeout;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::RunStwCycle(uint32_t timeoutMilliseconds)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    return m_impl->RunStwCycle(timeoutMilliseconds);
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::WaitForCycleCompletion(uint32_t timeoutMilliseconds)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    std::unique_lock<std::mutex> guard(m_impl->lock);
    // AdvanceBucketsLocked changes this predicate and notifies under the same mutex.
    m_impl->externalWaits++;
    bool completed = m_impl->condition.wait_for(
        guard,
        std::chrono::milliseconds(timeoutMilliseconds),
        [this]() { return m_impl->cycleCompleted.load(); });
    return completed ? LaxoriWorkSchedulerResult::Success : LaxoriWorkSchedulerResult::Timeout;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::WaitForIdle(uint32_t timeoutMilliseconds)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    std::unique_lock<std::mutex> guard(m_impl->lock);
    bool idle = m_impl->WaitFor(
        guard,
        std::chrono::milliseconds(timeoutMilliseconds),
        [this]()
        {
            for (const Impl::Bucket& bucket : m_impl->buckets)
            {
                if (bucket.outstanding != 0 || bucket.executing != 0)
                {
                    return false;
                }
            }
            return true;
        });
    return idle ? LaxoriWorkSchedulerResult::Success : LaxoriWorkSchedulerResult::Timeout;
}

LaxoriWorkSchedulerResult LaxoriWorkScheduler::WaitForBucketIdle(
    LaxoriWorkBucketStage stage, uint32_t timeoutMilliseconds)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    if (StageIndex(stage) >= StageCount)
    {
        return LaxoriWorkSchedulerResult::InvalidStage;
    }
    std::unique_lock<std::mutex> guard(m_impl->lock);
    bool idle = m_impl->WaitFor(
        guard, std::chrono::milliseconds(timeoutMilliseconds),
        [this, stage]() { return m_impl->IsBucketDrainedLocked(stage); });
    return idle ? LaxoriWorkSchedulerResult::Success : LaxoriWorkSchedulerResult::Timeout;
}

#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
LaxoriWorkSchedulerResult LaxoriWorkScheduler::WaitForCompletion(
    uint64_t expectedGeneration,
    uint32_t timeoutMilliseconds)
{
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }
    if (!m_impl->completionEnabled)
    {
        return LaxoriWorkSchedulerResult::CompletionDisabled;
    }

    std::unique_lock<std::mutex> guard(m_impl->lock);
    bool completed = m_impl->WaitFor(
        guard,
        std::chrono::milliseconds(timeoutMilliseconds),
        [this, expectedGeneration]()
        {
            for (const Impl::DeferredQueueSet& queueSet :
                m_impl->deferredQueues)
            {
                LaxoriCompletionSnapshot snapshot =
                    queueSet.completion.GetSnapshot();
                if (snapshot.generation == expectedGeneration)
                {
                    return snapshot.lifecycle ==
                            LaxoriCompletionLifecycle::Drained ||
                        snapshot.lifecycle ==
                            LaxoriCompletionLifecycle::Cancelled ||
                        snapshot.lifecycle == LaxoriCompletionLifecycle::Failed;
                }
            }
            return false;
        });
    if (!completed)
    {
        return LaxoriWorkSchedulerResult::Timeout;
    }

    for (const Impl::DeferredQueueSet& queueSet : m_impl->deferredQueues)
    {
        LaxoriCompletionSnapshot snapshot = queueSet.completion.GetSnapshot();
        if (snapshot.generation == expectedGeneration)
        {
            return snapshot.lifecycle == LaxoriCompletionLifecycle::Drained
                ? LaxoriWorkSchedulerResult::Success
                : (snapshot.lifecycle == LaxoriCompletionLifecycle::Cancelled
                    ? LaxoriWorkSchedulerResult::Shutdown
                    : LaxoriWorkSchedulerResult::CompletionFailed);
        }
    }
    return LaxoriWorkSchedulerResult::StaleGeneration;
}
#endif

bool LaxoriWorkScheduler::IsStageOpen(LaxoriWorkBucketStage stage) const
{
    if (m_impl == nullptr || StageIndex(stage) >= StageCount)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(m_impl->lock);
    return m_impl->buckets[StageIndex(stage)].state == BucketState::Open;
}

bool LaxoriWorkScheduler::IsStageCompleted(LaxoriWorkBucketStage stage) const
{
    if (m_impl == nullptr || StageIndex(stage) >= StageCount)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(m_impl->lock);
    return m_impl->buckets[StageIndex(stage)].state == BucketState::Completed;
}

bool LaxoriWorkScheduler::IsStageEnabled(LaxoriWorkBucketStage stage) const
{
    if (m_impl == nullptr || StageIndex(stage) >= StageCount)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(m_impl->lock);
    return m_impl->buckets[StageIndex(stage)].enabled;
}

bool LaxoriWorkScheduler::IsInterruptionRequested() const
{
    return m_impl != nullptr && m_impl->interruptionRequested.load(std::memory_order_acquire);
}

bool LaxoriWorkScheduler::IsCycleCompleted() const
{
    if (m_impl == nullptr)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(m_impl->lock);
    return m_impl->cycleCompleted;
}

uint64_t LaxoriWorkScheduler::GetStageOutstanding(LaxoriWorkBucketStage stage) const
{
    if (m_impl == nullptr || StageIndex(stage) >= StageCount)
    {
        return 0;
    }
    std::lock_guard<std::mutex> guard(m_impl->lock);
    return m_impl->buckets[StageIndex(stage)].outstanding;
}

uint64_t LaxoriWorkScheduler::GetGeneration() const
{
    if (m_impl == nullptr)
    {
        return 0;
    }
    std::lock_guard<std::mutex> guard(m_impl->lock);
    return m_impl->generation;
}

size_t LaxoriWorkScheduler::GetWorkerCount() const
{
    return m_impl == nullptr ? 0 : m_impl->workers.size();
}

size_t LaxoriWorkScheduler::CopyOpenTrace(LaxoriWorkBucketStage* stages, size_t capacity) const
{
    if (m_impl == nullptr)
    {
        return 0;
    }
    std::lock_guard<std::mutex> guard(m_impl->lock);
    size_t count = m_impl->openTrace.size();
    if (stages != nullptr)
    {
        size_t copyCount = count < capacity ? count : capacity;
        for (size_t index = 0; index < copyCount; index++)
        {
            stages[index] = m_impl->openTrace[index];
        }
    }
    return count;
}

size_t LaxoriWorkScheduler::CopyCompletionTrace(LaxoriWorkBucketStage* stages, size_t capacity) const
{
    if (m_impl == nullptr)
    {
        return 0;
    }
    std::lock_guard<std::mutex> guard(m_impl->lock);
    size_t count = m_impl->completionTrace.size();
    if (stages != nullptr)
    {
        size_t copyCount = count < capacity ? count : capacity;
        for (size_t index = 0; index < copyCount; index++)
        {
            stages[index] = m_impl->completionTrace[index];
        }
    }
    return count;
}

LaxoriWorkSchedulerMetrics LaxoriWorkScheduler::GetMetrics() const
{
    LaxoriWorkSchedulerMetrics metrics{};
    if (m_impl == nullptr)
    {
        return metrics;
    }
    std::lock_guard<std::mutex> guard(m_impl->lock);
    metrics.submitted = m_impl->submitted;
    metrics.executed = m_impl->executed;
    metrics.stolen = m_impl->stolen;
    metrics.spilled = m_impl->spilled;
    metrics.rejected = m_impl->rejected;
    metrics.ownershipViolations = m_impl->ownershipViolations;
    metrics.parks = m_impl->parks;
    metrics.wakes = m_impl->wakes;
    metrics.deferredSubmitted = m_impl->deferredSubmitted;
    metrics.deferredPublished = m_impl->deferredPublished;
    metrics.deferredExecuted = m_impl->deferredExecuted;
    metrics.deferredFlips = m_impl->deferredFlips;
    metrics.deferredEmptyFlips = m_impl->deferredEmptyFlips;
    metrics.signals = m_impl->signals;
    metrics.externalWaits = m_impl->externalWaits;
    metrics.externalNotifies = m_impl->externalNotifies;
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    if (m_impl->completionEnabled)
    {
        for (const Impl::DeferredQueueSet& queueSet : m_impl->deferredQueues)
        {
            LaxoriCompletionSnapshot snapshot =
                queueSet.completion.GetSnapshot();
            metrics.completionRegistrations += snapshot.registrations;
            metrics.completionCompletions += snapshot.completions;
            metrics.decrementZeroTransitions +=
                snapshot.decrementZeroTransitions;
            metrics.overallZeroTransitions +=
                snapshot.overallZeroTransitions;
        }
    }
#endif
    return metrics;
}

LaxoriDeferredWorkState LaxoriWorkScheduler::GetDeferredWorkState() const
{
    LaxoriDeferredWorkState state{};
    state.activeIndex = LaxoriNoDeferredQueue;
    state.producerIndex = LaxoriNoDeferredQueue;
    state.sealedIndex = LaxoriNoDeferredQueue;
    if (m_impl == nullptr)
    {
        return state;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    state.enabled = m_impl->deferredEnabled;
    state.resumeRecorded =
        m_impl->resumeRecordedGeneration != 0 &&
        m_impl->resumeRecordedGeneration == m_impl->generation;
    state.activeIndex = m_impl->activeDeferredIndex;
    state.producerIndex = m_impl->producerDeferredIndex;
    state.sealedIndex = m_impl->sealedDeferredIndex;
    state.producerGeneration =
        m_impl->producerDeferredIndex < m_impl->deferredQueues.size() ?
            m_impl->deferredQueues[m_impl->producerDeferredIndex].generation :
            0;
    state.sealedGeneration = m_impl->sealedGeneration;
    state.publishedGeneration = m_impl->publishedGeneration;
    if (m_impl->producerDeferredIndex < m_impl->deferredQueues.size())
    {
        const Impl::DeferredQueueSet& producer =
            m_impl->deferredQueues[m_impl->producerDeferredIndex];
        state.pendingNormal = producer.normal.size();
        state.pendingPrioritized = producer.prioritized.size();
    }
    if (m_impl->sealedDeferredIndex < m_impl->deferredQueues.size())
    {
        const Impl::DeferredQueueSet& sealed =
            m_impl->deferredQueues[m_impl->sealedDeferredIndex];
        state.sealedNormal = sealed.normal.size();
        state.sealedPrioritized = sealed.prioritized.size();
    }
    return state;
}

#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
LaxoriWorkSchedulerResult LaxoriWorkScheduler::GetCompletionSnapshot(
    uint64_t expectedGeneration,
    LaxoriCompletionSnapshot* snapshot) const
{
    if (snapshot == nullptr)
    {
        return LaxoriWorkSchedulerResult::InvalidArgument;
    }
    *snapshot = {};
    if (m_impl == nullptr)
    {
        return LaxoriWorkSchedulerResult::NotInitialized;
    }

    std::lock_guard<std::mutex> guard(m_impl->lock);
    if (!m_impl->completionEnabled)
    {
        return LaxoriWorkSchedulerResult::CompletionDisabled;
    }
    for (const Impl::DeferredQueueSet& queueSet : m_impl->deferredQueues)
    {
        LaxoriCompletionSnapshot candidate =
            queueSet.completion.GetSnapshot();
        if (candidate.generation == expectedGeneration)
        {
            *snapshot = candidate;
            return LaxoriWorkSchedulerResult::Success;
        }
    }
    return LaxoriWorkSchedulerResult::StaleGeneration;
}
#endif
