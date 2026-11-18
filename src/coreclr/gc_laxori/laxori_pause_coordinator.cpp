// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "laxori_pause_coordinator.h"

#ifdef byte
#pragma push_macro("byte")
#define LAXORI_PAUSE_COORDINATOR_CPP_RESTORE_BYTE
#undef byte
#endif
#ifdef __valid
#pragma push_macro("__valid")
#define LAXORI_PAUSE_COORDINATOR_CPP_RESTORE_VALID
#undef __valid
#endif

#include <algorithm>

#ifdef LAXORI_PAUSE_COORDINATOR_CPP_RESTORE_BYTE
#pragma pop_macro("byte")
#undef LAXORI_PAUSE_COORDINATOR_CPP_RESTORE_BYTE
#endif
#ifdef LAXORI_PAUSE_COORDINATOR_CPP_RESTORE_VALID
#pragma pop_macro("__valid")
#undef LAXORI_PAUSE_COORDINATOR_CPP_RESTORE_VALID
#endif

LaxoriPauseCoordinator::LaxoriPauseCoordinator()
    : m_scheduler(nullptr),
      m_adapter{},
      m_state(LaxoriPauseCoordinatorState::Uninitialized),
      m_current_kind(LaxoriPauseKind::Count),
      m_pause_start{},
      m_metrics{}
{
}

LaxoriPauseCoordinatorResult LaxoriPauseCoordinator::Initialize(
    LaxoriWorkScheduler* scheduler,
    const LaxoriPauseRuntimeAdapter& adapter)
{
    if (scheduler == nullptr || adapter.suspend == nullptr || adapter.resume == nullptr)
    {
        return LaxoriPauseCoordinatorResult::InvalidArgument;
    }
    if (m_state != LaxoriPauseCoordinatorState::Uninitialized)
    {
        return LaxoriPauseCoordinatorResult::AlreadyInitialized;
    }

    m_scheduler = scheduler;
    m_adapter = adapter;
    m_state = LaxoriPauseCoordinatorState::Idle;
    return LaxoriPauseCoordinatorResult::Success;
}

LaxoriPauseCoordinatorResult LaxoriPauseCoordinator::PreparePause(
    LaxoriPauseKind kind, bool eagerDecrements, bool prepareRelocation)
{
    if (m_state == LaxoriPauseCoordinatorState::Uninitialized)
    {
        return LaxoriPauseCoordinatorResult::NotInitialized;
    }
    if (m_state != LaxoriPauseCoordinatorState::Idle)
    {
        return LaxoriPauseCoordinatorResult::InvalidState;
    }

    LaxoriPauseSchedule schedule{};
    if (!LaxoriTryGetPauseSchedule(kind, &schedule))
    {
        return LaxoriPauseCoordinatorResult::InvalidArgument;
    }
    if (eagerDecrements)
    {
        schedule.enabledStageMask |=
            UINT16_C(1) << static_cast<uint8_t>(LaxoriWorkBucketStage::STWRCDecsAndSweep);
    }
    if (prepareRelocation)
    {
        schedule.enabledStageMask |= UINT16_C(1) << static_cast<uint8_t>(LaxoriWorkBucketStage::Prepare);
    }

    if (m_scheduler->IsCycleCompleted())
    {
        LaxoriWorkSchedulerResult reset = m_scheduler->ResetCompletedCycle();
        if (reset != LaxoriWorkSchedulerResult::Success)
        {
            return FromSchedulerResult(reset);
        }
    }

    LaxoriWorkSchedulerResult configured =
        m_scheduler->ConfigureStwStages(schedule.enabledStageMask);
    if (configured != LaxoriWorkSchedulerResult::Success)
    {
        return FromSchedulerResult(configured);
    }

    m_current_kind = kind;
    m_state = LaxoriPauseCoordinatorState::Prepared;
    return LaxoriPauseCoordinatorResult::Success;
}

LaxoriPauseCoordinatorResult LaxoriPauseCoordinator::StartPause(uint32_t timeoutMilliseconds)
{
    if (m_state == LaxoriPauseCoordinatorState::Uninitialized)
    {
        return LaxoriPauseCoordinatorResult::NotInitialized;
    }
    if (m_state != LaxoriPauseCoordinatorState::Prepared)
    {
        return LaxoriPauseCoordinatorResult::InvalidState;
    }

    LaxoriWorkSchedulerResult interrupted =
        m_scheduler->InterruptConcurrentWork(timeoutMilliseconds);
    if (interrupted != LaxoriWorkSchedulerResult::Success)
    {
        RollBackInterruption();
        return FromSchedulerResult(interrupted);
    }

    m_pause_start = std::chrono::steady_clock::now();
    m_adapter.suspend(m_adapter.context);

    LaxoriWorkSchedulerResult started = m_scheduler->BeginStwCycle(true);
    if (started != LaxoriWorkSchedulerResult::Success)
    {
        m_adapter.resume(false, m_adapter.context);
        RollBackInterruption();
        return FromSchedulerResult(started);
    }

    m_state = LaxoriPauseCoordinatorState::Active;
    return LaxoriPauseCoordinatorResult::Success;
}

LaxoriPauseCoordinatorResult LaxoriPauseCoordinator::CompletePause(uint32_t timeoutMilliseconds)
{
    if (m_state == LaxoriPauseCoordinatorState::Uninitialized)
    {
        return LaxoriPauseCoordinatorResult::NotInitialized;
    }
    if (m_state != LaxoriPauseCoordinatorState::Active)
    {
        return LaxoriPauseCoordinatorResult::InvalidState;
    }

    LaxoriWorkSchedulerResult completed =
        m_scheduler->RunStwCycle(timeoutMilliseconds);
    if (completed != LaxoriWorkSchedulerResult::Success)
    {
        return FromSchedulerResult(completed);
    }

    bool deferred = m_scheduler->GetDeferredWorkState().enabled;
    if (!deferred)
    {
        LaxoriWorkSchedulerResult reset = m_scheduler->ResetCompletedCycle();
        if (reset != LaxoriWorkSchedulerResult::Success)
        {
            m_adapter.resume(false, m_adapter.context);
            SetFaulted();
            return FromSchedulerResult(reset);
        }
    }
    LaxoriWorkSchedulerResult resumed = m_scheduler->ResumeInterruptedConcurrentWork(true);
    if (resumed != LaxoriWorkSchedulerResult::Success)
    {
        m_adapter.resume(false, m_adapter.context);
        SetFaulted();
        return FromSchedulerResult(resumed);
    }

    m_adapter.resume(m_adapter.finishedGc, m_adapter.context);
    m_scheduler->WakeAfterResume();
    uint64_t elapsed = static_cast<uint64_t>(
        std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::steady_clock::now() - m_pause_start).count());
    LaxoriPauseKindMetrics& metrics = m_metrics[static_cast<size_t>(m_current_kind)];
    metrics.count++;
    metrics.totalNanoseconds += elapsed;
    metrics.maximumNanoseconds = std::max(metrics.maximumNanoseconds, elapsed);

    if (deferred)
    {
        uint64_t generation = m_scheduler->GetGeneration();
        LaxoriWorkSchedulerResult recorded = m_scheduler->RecordMutatorResume(generation);
        if (recorded != LaxoriWorkSchedulerResult::Success)
        {
            SetFaulted();
            return FromSchedulerResult(recorded);
        }
        LaxoriWorkSchedulerResult published =
            m_scheduler->PublishDeferredConcurrentWork(generation);
        if (published != LaxoriWorkSchedulerResult::Success)
        {
            SetFaulted();
            return FromSchedulerResult(published);
        }
    }

    m_current_kind = LaxoriPauseKind::Count;
    m_state = LaxoriPauseCoordinatorState::Idle;
    return LaxoriPauseCoordinatorResult::Success;
}

LaxoriPauseCoordinatorResult LaxoriPauseCoordinator::AbortPause()
{
    if (m_state == LaxoriPauseCoordinatorState::Uninitialized)
    {
        return LaxoriPauseCoordinatorResult::NotInitialized;
    }
    if (m_state != LaxoriPauseCoordinatorState::Active)
    {
        return LaxoriPauseCoordinatorResult::InvalidState;
    }

    LaxoriWorkSchedulerResult shutdown = m_scheduler->Shutdown();
    m_adapter.resume(false, m_adapter.context);
    SetFaulted();
    return FromSchedulerResult(shutdown);
}

LaxoriPauseCoordinatorState LaxoriPauseCoordinator::GetState() const
{
    return m_state;
}

LaxoriPauseKind LaxoriPauseCoordinator::GetCurrentPauseKind() const
{
    return m_current_kind;
}

LaxoriPauseKindMetrics LaxoriPauseCoordinator::GetMetrics(LaxoriPauseKind kind) const
{
    size_t index = static_cast<size_t>(kind);
    return index < m_metrics.size() ? m_metrics[index] : LaxoriPauseKindMetrics{};
}

LaxoriPauseCoordinatorResult LaxoriPauseCoordinator::FromSchedulerResult(
    LaxoriWorkSchedulerResult result)
{
    switch (result)
    {
        case LaxoriWorkSchedulerResult::Success:
            return LaxoriPauseCoordinatorResult::Success;
        case LaxoriWorkSchedulerResult::InvalidArgument:
        case LaxoriWorkSchedulerResult::InvalidStage:
            return LaxoriPauseCoordinatorResult::InvalidArgument;
        case LaxoriWorkSchedulerResult::NotInitialized:
            return LaxoriPauseCoordinatorResult::NotInitialized;
        case LaxoriWorkSchedulerResult::Timeout:
            return LaxoriPauseCoordinatorResult::Timeout;
        case LaxoriWorkSchedulerResult::InvalidState:
        case LaxoriWorkSchedulerResult::AlreadyInitialized:
        case LaxoriWorkSchedulerResult::StaleGeneration:
        case LaxoriWorkSchedulerResult::PacketAlreadyOwned:
        case LaxoriWorkSchedulerResult::StageDisabled:
        case LaxoriWorkSchedulerResult::Busy:
        case LaxoriWorkSchedulerResult::Shutdown:
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
        case LaxoriWorkSchedulerResult::CompletionDisabled:
        case LaxoriWorkSchedulerResult::CompletionOverflow:
        case LaxoriWorkSchedulerResult::CompletionUnderflow:
        case LaxoriWorkSchedulerResult::CompletionFailed:
#endif
            return LaxoriPauseCoordinatorResult::SchedulerFailure;
    }

    return LaxoriPauseCoordinatorResult::SchedulerFailure;
}

void LaxoriPauseCoordinator::RollBackInterruption()
{
    if (m_scheduler->CancelConcurrentInterruption() ==
        LaxoriWorkSchedulerResult::Success)
    {
        m_state = LaxoriPauseCoordinatorState::Prepared;
        return;
    }

    SetFaulted();
}

void LaxoriPauseCoordinator::SetFaulted()
{
    m_current_kind = LaxoriPauseKind::Count;
    m_state = LaxoriPauseCoordinatorState::Faulted;
}
