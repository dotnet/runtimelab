// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef LAXORI_PAUSE_COORDINATOR_H
#define LAXORI_PAUSE_COORDINATOR_H

#include "laxori_pause_schedule.h"
#include "laxori_work_scheduler.h"

#ifdef byte
#pragma push_macro("byte")
#define LAXORI_PAUSE_COORDINATOR_RESTORE_BYTE
#undef byte
#endif
#ifdef __valid
#pragma push_macro("__valid")
#define LAXORI_PAUSE_COORDINATOR_RESTORE_VALID
#undef __valid
#endif

#include <array>
#include <chrono>
#include <cstdint>

#ifdef LAXORI_PAUSE_COORDINATOR_RESTORE_BYTE
#pragma pop_macro("byte")
#undef LAXORI_PAUSE_COORDINATOR_RESTORE_BYTE
#endif
#ifdef LAXORI_PAUSE_COORDINATOR_RESTORE_VALID
#pragma pop_macro("__valid")
#undef LAXORI_PAUSE_COORDINATOR_RESTORE_VALID
#endif

enum class LaxoriPauseCoordinatorResult : uint8_t
{
    Success,
    InvalidArgument,
    NotInitialized,
    AlreadyInitialized,
    InvalidState,
    SchedulerFailure,
    Timeout,
};

enum class LaxoriPauseCoordinatorState : uint8_t
{
    Uninitialized,
    Idle,
    Prepared,
    Active,
    Faulted,
};

using LaxoriPauseSuspendCallback = void (*)(void* context);
using LaxoriPauseResumeCallback = void (*)(bool finishedGc, void* context);

struct LaxoriPauseRuntimeAdapter
{
    LaxoriPauseSuspendCallback suspend;
    LaxoriPauseResumeCallback resume;
    void* context;
    bool finishedGc;
};

struct LaxoriPauseKindMetrics
{
    uint64_t count;
    uint64_t totalNanoseconds;
    uint64_t maximumNanoseconds;
};

class LaxoriPauseCoordinator final
{
public:
    LaxoriPauseCoordinator();
    LaxoriPauseCoordinator(const LaxoriPauseCoordinator&) = delete;
    LaxoriPauseCoordinator& operator=(const LaxoriPauseCoordinator&) = delete;

    LaxoriPauseCoordinatorResult Initialize(
        LaxoriWorkScheduler* scheduler,
        const LaxoriPauseRuntimeAdapter& adapter);
    LaxoriPauseCoordinatorResult PreparePause(
        LaxoriPauseKind kind, bool eagerDecrements = false, bool prepareRelocation = false);
    LaxoriPauseCoordinatorResult StartPause(uint32_t timeoutMilliseconds);
    LaxoriPauseCoordinatorResult CompletePause(uint32_t timeoutMilliseconds);
    LaxoriPauseCoordinatorResult AbortPause();

    LaxoriPauseCoordinatorState GetState() const;
    LaxoriPauseKind GetCurrentPauseKind() const;
    LaxoriPauseKindMetrics GetMetrics(LaxoriPauseKind kind) const;

private:
    static LaxoriPauseCoordinatorResult FromSchedulerResult(LaxoriWorkSchedulerResult result);
    void RollBackInterruption();
    void SetFaulted();

    LaxoriWorkScheduler* m_scheduler;
    LaxoriPauseRuntimeAdapter m_adapter;
    LaxoriPauseCoordinatorState m_state;
    LaxoriPauseKind m_current_kind;
    std::chrono::steady_clock::time_point m_pause_start;
    std::array<LaxoriPauseKindMetrics, static_cast<size_t>(LaxoriPauseKind::Count)> m_metrics;
};

#endif // LAXORI_PAUSE_COORDINATOR_H
