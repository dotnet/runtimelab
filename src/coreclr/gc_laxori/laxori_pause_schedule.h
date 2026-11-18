// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef LAXORI_PAUSE_SCHEDULE_H
#define LAXORI_PAUSE_SCHEDULE_H

#include "laxori_work_packet.h"

#include <cstdint>

enum class LaxoriPauseKind : uint8_t
{
    Full,
    RefCount,
    InitialMark,
    FinalMark,
    Count,
};

static_assert(static_cast<uint8_t>(LaxoriPauseKind::Full) == 0);
static_assert(static_cast<uint8_t>(LaxoriPauseKind::RefCount) == 1);
static_assert(static_cast<uint8_t>(LaxoriPauseKind::InitialMark) == 2);
static_assert(static_cast<uint8_t>(LaxoriPauseKind::FinalMark) == 3);
static_assert(static_cast<uint8_t>(LaxoriPauseKind::Count) == 4);

struct LaxoriPauseSchedule
{
    LaxoriPauseKind kind;
    uint16_t enabledStageMask;
};

const char* LaxoriGetPauseKindName(LaxoriPauseKind kind);
bool LaxoriTryGetPauseSchedule(LaxoriPauseKind kind, LaxoriPauseSchedule* schedule);
bool LaxoriIsPauseStageEnabled(const LaxoriPauseSchedule& schedule, LaxoriWorkBucketStage stage);

#endif // LAXORI_PAUSE_SCHEDULE_H
