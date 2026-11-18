// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "laxori_pause_schedule.h"

#include <cstddef>

namespace
{
    constexpr uint16_t StageBit(LaxoriWorkBucketStage stage)
    {
        return static_cast<uint16_t>(UINT16_C(1) << static_cast<uint8_t>(stage));
    }

    constexpr uint16_t CommonMask =
        StageBit(LaxoriWorkBucketStage::FinishConcurrentWork) |
        StageBit(LaxoriWorkBucketStage::Initial) |
        StageBit(LaxoriWorkBucketStage::Release) |
        StageBit(LaxoriWorkBucketStage::Final);
    constexpr uint16_t PrepareMask = StageBit(LaxoriWorkBucketStage::Prepare);
    constexpr uint16_t ClosureMask = StageBit(LaxoriWorkBucketStage::Closure);
    constexpr uint16_t DecrementsMask = StageBit(LaxoriWorkBucketStage::STWRCDecsAndSweep);

    constexpr const char* PauseKindNames[] = {
        "Full",
        "RefCount",
        "InitialMark",
        "FinalMark",
    };

    constexpr LaxoriPauseSchedule PauseSchedules[] = {
        {LaxoriPauseKind::Full, CommonMask | PrepareMask | ClosureMask | DecrementsMask},
        {LaxoriPauseKind::RefCount, CommonMask},
        {LaxoriPauseKind::InitialMark, CommonMask | PrepareMask},
        {LaxoriPauseKind::FinalMark, CommonMask | PrepareMask | ClosureMask},
    };

    static_assert((sizeof(PauseKindNames) / sizeof(PauseKindNames[0])) ==
        static_cast<size_t>(LaxoriPauseKind::Count));
    static_assert((sizeof(PauseSchedules) / sizeof(PauseSchedules[0])) ==
        static_cast<size_t>(LaxoriPauseKind::Count));
}

const char* LaxoriGetPauseKindName(LaxoriPauseKind kind)
{
    size_t index = static_cast<size_t>(kind);
    return index < static_cast<size_t>(LaxoriPauseKind::Count) ?
        PauseKindNames[index] :
        "Invalid";
}

bool LaxoriTryGetPauseSchedule(LaxoriPauseKind kind, LaxoriPauseSchedule* schedule)
{
    size_t index = static_cast<size_t>(kind);
    if (schedule == nullptr || index >= static_cast<size_t>(LaxoriPauseKind::Count))
    {
        return false;
    }

    *schedule = PauseSchedules[index];
    return true;
}

bool LaxoriIsPauseStageEnabled(const LaxoriPauseSchedule& schedule, LaxoriWorkBucketStage stage)
{
    return LaxoriIsStwStage(stage) &&
        (schedule.enabledStageMask & StageBit(stage)) != 0;
}
