// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef LAXORI_WORK_SCHEDULER_H
#define LAXORI_WORK_SCHEDULER_H

#include "laxori_work_packet.h"

#ifdef byte
#pragma push_macro("byte")
#define LAXORI_WORK_SCHEDULER_RESTORE_BYTE
#undef byte
#endif
#ifdef __valid
#pragma push_macro("__valid")
#define LAXORI_WORK_SCHEDULER_RESTORE_VALID
#undef __valid
#endif

#include <cstddef>
#include <cstdint>

enum class LaxoriWorkSchedulerResult : uint8_t
{
    Success,
    InvalidArgument,
    NotInitialized,
    AlreadyInitialized,
    InvalidStage,
    InvalidState,
    StaleGeneration,
    PacketAlreadyOwned,
    StageDisabled,
    Busy,
    Timeout,
    Shutdown,
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    CompletionDisabled,
    CompletionOverflow,
    CompletionUnderflow,
    CompletionFailed,
#endif
};

enum class LaxoriDeferredWorkPriority : uint8_t
{
    Normal,
    Prioritized,
};

constexpr size_t LaxoriNoDeferredQueue = static_cast<size_t>(-1);

struct LaxoriWorkSchedulerMetrics
{
    uint64_t submitted;
    uint64_t executed;
    uint64_t stolen;
    uint64_t spilled;
    uint64_t rejected;
    uint64_t ownershipViolations;
    uint64_t parks;
    uint64_t wakes;
    uint64_t deferredSubmitted;
    uint64_t deferredPublished;
    uint64_t deferredExecuted;
    uint64_t deferredFlips;
    uint64_t deferredEmptyFlips;
    // P25 (F4). Number of times the scheduler woke sleeping threads (one per notify call).
    uint64_t signals;
    // Waits and wake-ups of the threads that wait for the scheduler (the pause thread and the allocating threads).
    uint64_t externalWaits;
    uint64_t externalNotifies;
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    uint64_t completionRegistrations;
    uint64_t completionCompletions;
    uint64_t decrementZeroTransitions;
    uint64_t overallZeroTransitions;
#endif
};

struct LaxoriDeferredWorkState
{
    bool enabled;
    bool resumeRecorded;
    size_t activeIndex;
    size_t producerIndex;
    size_t sealedIndex;
    uint64_t producerGeneration;
    uint64_t sealedGeneration;
    uint64_t publishedGeneration;
    uint64_t pendingNormal;
    uint64_t pendingPrioritized;
    uint64_t sealedNormal;
    uint64_t sealedPrioritized;
};

class LaxoriWorkScheduler final
{
public:
    static constexpr size_t NoPreferredWorker = static_cast<size_t>(-1);

    LaxoriWorkScheduler();
    ~LaxoriWorkScheduler();
    LaxoriWorkScheduler(const LaxoriWorkScheduler&) = delete;
    LaxoriWorkScheduler& operator=(const LaxoriWorkScheduler&) = delete;

    LaxoriWorkSchedulerResult Initialize(
        size_t workerCount,
        bool enableDeferredConcurrentWork = false
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
        ,
        bool enableLazyCompletionCounter = false,
        LaxoriDecrementsDrainedCallback decrementsDrained = nullptr,
        LaxoriOverallDrainedCallback overallDrained = nullptr,
        void* completionContext = nullptr,
        uint64_t maximumCompletionOutstanding = UINT64_MAX
#endif
        );
    LaxoriWorkSchedulerResult Shutdown();
    // P25 (F4). The most workers that may run the packets of the concurrent trace stages while the program runs (1 to the
    // number of workers). The other workers still run all other packets. The default is every worker.
    LaxoriWorkSchedulerResult ConfigureConcurrentTraceWorkers(size_t limit);
    // P26 (F2). Shutdown calls the callback for every packet that it cancels, after the packet is caller owned again and
    // outside the lock of the scheduler. The owner uses it to free packets that nobody else knows.
    using CancelledPacketCallback = void (*)(LaxoriWorkPacket* packet, void* context);
    LaxoriWorkSchedulerResult SetCancelledPacketCallback(CancelledPacketCallback callback, void* context);
    // P26 (F2). Runs the queued packets of one stage on the calling thread. The stage must not be runnable (it is closed
    // for a pause) and none of its packets may be running. A pause uses it to finish the trace backlog without a list
    // of the packets. executionIndex is the worker index that the packets see. ran gets the number of packets.
    LaxoriWorkSchedulerResult RunQueuedInline(LaxoriWorkBucketStage stage, size_t executionIndex, uint64_t* ran);
    // The trace of a pause on all workers. A packet of a pause stage (it runs in a stage that is open) calls OpenPauseTrace. The
    // resumable trace stage is then runnable for every worker, without the limit of concurrent trace workers, and the packets
    // that the trace stage holds (the backlog of the concurrent trace) and the packets that the pause submits to it are run by
    // the workers. The calling thread helps with HelpPauseTrace until done() returns true, and ends the trace with
    // ClosePauseTrace, which waits for every packet of the stage and closes the stage again. The stop-the-world stage that is
    // current does not complete while the trace is open. executionIndex is the worker index of the calling thread, or
    // GetWorkerCount() for the pause thread.
    LaxoriWorkSchedulerResult OpenPauseTrace();
    LaxoriWorkSchedulerResult HelpPauseTrace(size_t executionIndex, bool (*done)(void*), void* context);
    LaxoriWorkSchedulerResult ClosePauseTrace(size_t executionIndex);
    bool IsPauseTraceOpen() const;

    LaxoriWorkSchedulerResult Submit(
        LaxoriWorkPacket* packet,
        size_t preferredWorker = NoPreferredWorker);
    LaxoriWorkSchedulerResult SubmitDeferredConcurrent(
        LaxoriWorkPacket* packet,
        LaxoriDeferredWorkPriority priority,
        size_t preferredWorker = NoPreferredWorker);
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    LaxoriWorkSchedulerResult SubmitTracked(
        LaxoriWorkPacket* packet,
        LaxoriCompletionWorkKind kind,
        size_t preferredWorker = NoPreferredWorker);
    LaxoriWorkSchedulerResult SubmitDeferredConcurrentTracked(
        LaxoriWorkPacket* packet,
        LaxoriCompletionWorkKind kind,
        LaxoriDeferredWorkPriority priority,
        size_t preferredWorker = NoPreferredWorker);
    LaxoriWorkSchedulerResult SubmitTrackedChild(
        const LaxoriWorkPacket* parent,
        LaxoriWorkPacket* child,
        LaxoriCompletionWorkKind kind,
        size_t preferredWorker = NoPreferredWorker);
    LaxoriWorkSchedulerResult SubmitMatureSweep(
        LaxoriMatureSweepContinuation* continuation,
        LaxoriWorkPacket* packet,
        size_t preferredWorker = NoPreferredWorker);
#endif
    LaxoriWorkSchedulerResult RecycleCompletedPacket(LaxoriWorkPacket* packet);
    LaxoriWorkSchedulerResult ConfigureStwStages(uint16_t enabledStageMask);
    LaxoriWorkSchedulerResult EnableConcurrentStage(LaxoriWorkBucketStage stage);
    LaxoriWorkSchedulerResult InterruptConcurrentWork(uint32_t timeoutMilliseconds);
    LaxoriWorkSchedulerResult CancelConcurrentInterruption();
    // pauseThreadRuns: the caller will call RunStwCycle, so it runs packets itself and workers need not wake for a small stage.
    LaxoriWorkSchedulerResult BeginStwCycle(bool pauseThreadRuns = false);
    LaxoriWorkSchedulerResult ResumeConcurrentWork();
    LaxoriWorkSchedulerResult RecordMutatorResume(uint64_t expectedGeneration);
    LaxoriWorkSchedulerResult PublishDeferredConcurrentWork(uint64_t expectedGeneration);
    // Wakes the workers for the concurrent packets that ResumeInterruptedConcurrentWork(true) found. The pause coordinator
    // calls it after the mutators have resumed, so that the wake-ups are not part of the pause.
    LaxoriWorkSchedulerResult WakeAfterResume();
    // deferWake: do not wake the workers now; the caller calls WakeAfterResume.
    LaxoriWorkSchedulerResult ResumeInterruptedConcurrentWork(bool deferWake = false);
    LaxoriWorkSchedulerResult ResetCompletedCycle();

    LaxoriWorkSchedulerResult WaitForPacket(
        const LaxoriWorkPacket* packet,
        uint32_t timeoutMilliseconds);
    LaxoriWorkSchedulerResult WaitForCycleCompletion(uint32_t timeoutMilliseconds);
    // Waits for the end of the stop-the-world cycle like WaitForCycleCompletion, but the calling thread (the pause thread)
    // also runs the packets of the current stage and opens the next stage when it finishes the last packet of a stage. It
    // runs them with the execution index GetWorkerCount().
    LaxoriWorkSchedulerResult RunStwCycle(uint32_t timeoutMilliseconds);
    LaxoriWorkSchedulerResult WaitForIdle(uint32_t timeoutMilliseconds);
    LaxoriWorkSchedulerResult WaitForBucketIdle(LaxoriWorkBucketStage stage, uint32_t timeoutMilliseconds);
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    LaxoriWorkSchedulerResult WaitForCompletion(
        uint64_t generation,
        uint32_t timeoutMilliseconds);
#endif

    bool IsStageOpen(LaxoriWorkBucketStage stage) const;
    bool IsStageEnabled(LaxoriWorkBucketStage stage) const;
    bool IsStageCompleted(LaxoriWorkBucketStage stage) const;
    bool IsCycleCompleted() const;
    // P26 (F4b). True from the moment that a pause asks the scheduler to interrupt the concurrent work until the work resumes.
    // A long packet polls it, so that the pause does not wait for the end of the packet. No mutex.
    bool IsInterruptionRequested() const;
    uint64_t GetStageOutstanding(LaxoriWorkBucketStage stage) const;
    uint64_t GetGeneration() const;
    size_t GetWorkerCount() const;
    size_t CopyOpenTrace(LaxoriWorkBucketStage* stages, size_t capacity) const;
    size_t CopyCompletionTrace(LaxoriWorkBucketStage* stages, size_t capacity) const;
    LaxoriWorkSchedulerMetrics GetMetrics() const;
    LaxoriDeferredWorkState GetDeferredWorkState() const;
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    LaxoriWorkSchedulerResult GetCompletionSnapshot(
        uint64_t generation,
        LaxoriCompletionSnapshot* snapshot) const;
#endif

private:
    class Impl;
    Impl* m_impl;
};

#ifdef LAXORI_WORK_SCHEDULER_RESTORE_BYTE
#pragma pop_macro("byte")
#undef LAXORI_WORK_SCHEDULER_RESTORE_BYTE
#endif
#ifdef LAXORI_WORK_SCHEDULER_RESTORE_VALID
#pragma pop_macro("__valid")
#undef LAXORI_WORK_SCHEDULER_RESTORE_VALID
#endif

#endif // LAXORI_WORK_SCHEDULER_H
