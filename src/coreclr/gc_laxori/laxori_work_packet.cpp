// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "laxori_work_packet.h"

#ifdef byte
#pragma push_macro("byte")
#define LAXORI_WORK_PACKET_CPP_RESTORE_BYTE
#undef byte
#endif
#ifdef __valid
#pragma push_macro("__valid")
#define LAXORI_WORK_PACKET_CPP_RESTORE_VALID
#undef __valid
#endif

#include <limits>
#include <utility>

#ifdef LAXORI_WORK_PACKET_CPP_RESTORE_BYTE
#pragma pop_macro("byte")
#undef LAXORI_WORK_PACKET_CPP_RESTORE_BYTE
#endif
#ifdef LAXORI_WORK_PACKET_CPP_RESTORE_VALID
#pragma pop_macro("__valid")
#undef LAXORI_WORK_PACKET_CPP_RESTORE_VALID
#endif

namespace
{
    constexpr const char* StageNames[] = {
        "Unconstrained",
        "Concurrent",
        "ConcurrentResumable",
        "FinishConcurrentWork",
        "Initial",
        "Prepare",
        "Closure",
        "Release",
        "STWRCDecsAndSweep",
        "Final",
    };

    static_assert((sizeof(StageNames) / sizeof(StageNames[0])) ==
        static_cast<size_t>(LaxoriWorkBucketStage::Count));
}

const char* LaxoriGetWorkBucketStageName(LaxoriWorkBucketStage stage)
{
    size_t index = static_cast<size_t>(stage);
    return index < static_cast<size_t>(LaxoriWorkBucketStage::Count) ?
        StageNames[index] :
        "Invalid";
}

bool LaxoriIsConcurrentStage(LaxoriWorkBucketStage stage)
{
    return stage == LaxoriWorkBucketStage::Unconstrained ||
        stage == LaxoriWorkBucketStage::Concurrent ||
        stage == LaxoriWorkBucketStage::ConcurrentResumable;
}

bool LaxoriIsStwStage(LaxoriWorkBucketStage stage)
{
    return stage >= LaxoriFirstStwStage && stage < LaxoriWorkBucketStage::Count;
}

bool LaxoriIsSupportedReferencePacketCapacity(size_t capacity)
{
    return capacity == 256 ||
        capacity == 512 ||
        capacity == 1024 ||
        capacity == 2048 ||
        capacity == LaxoriEdgePacketReferenceCapacity;
}

bool LaxoriTryGetReferencePacketCount(size_t referenceCount, size_t capacity, size_t* packetCount)
{
    if (packetCount == nullptr || !LaxoriIsSupportedReferencePacketCapacity(capacity))
    {
        return false;
    }

    if (referenceCount == 0)
    {
        *packetCount = 0;
        return true;
    }

    if (referenceCount > std::numeric_limits<size_t>::max() - (capacity - 1))
    {
        return false;
    }

    *packetCount = (referenceCount + capacity - 1) / capacity;
    return true;
}

LaxoriWorkPacket::LaxoriWorkPacket(
    uint64_t id,
    LaxoriWorkBucketStage stage,
    const uintptr_t* references,
    size_t referenceCount,
    ExecuteCallback callback,
    void* context)
    : m_id(id),
      m_stage(stage),
      m_references(references),
      m_reference_count(referenceCount),
      m_callback(callback),
      m_context(context),
      m_generation(0),
      m_state(LaxoriWorkPacketState::CallerOwned),
      m_preferred_worker(static_cast<size_t>(-1)),
      m_deferred(false)
{
}

uint64_t LaxoriWorkPacket::GetId() const
{
    return m_id;
}

LaxoriWorkBucketStage LaxoriWorkPacket::GetStage() const
{
    return m_stage;
}

uint64_t LaxoriWorkPacket::GetGeneration() const
{
    return m_generation.load(std::memory_order_acquire);
}

const uintptr_t* LaxoriWorkPacket::GetReferences() const
{
    return m_references;
}

size_t LaxoriWorkPacket::GetReferenceCount() const
{
    return m_reference_count;
}

LaxoriWorkPacketState LaxoriWorkPacket::GetState() const
{
    return m_state.load(std::memory_order_acquire);
}

bool LaxoriWorkPacket::TryTransition(LaxoriWorkPacketState expected, LaxoriWorkPacketState desired)
{
    return m_state.compare_exchange_strong(
        expected,
        desired,
        std::memory_order_acq_rel,
        std::memory_order_acquire);
}

void LaxoriWorkPacket::SetGeneration(uint64_t generation)
{
    m_generation.store(generation, std::memory_order_release);
}

void LaxoriWorkPacket::SetPreferredWorker(size_t workerIndex)
{
    m_preferred_worker = workerIndex;
}

size_t LaxoriWorkPacket::GetPreferredWorker() const
{
    return m_preferred_worker;
}

void LaxoriWorkPacket::SetDeferred(bool deferred)
{
    m_deferred = deferred;
}

bool LaxoriWorkPacket::IsDeferred() const
{
    return m_deferred;
}

void LaxoriWorkPacket::Execute(size_t workerIndex)
{
    if (m_callback != nullptr)
    {
        m_callback(this, workerIndex, m_context);
    }
}

#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
bool LaxoriWorkPacket::AttachCompletionRegistration(
    LaxoriLazyCompletionRegistration&& registration)
{
    if (!registration.IsActive() || m_completion_registration.IsActive())
    {
        return false;
    }

    m_completion_registration = std::move(registration);
    return true;
}

LaxoriCompletionResult LaxoriWorkPacket::CompleteRegistration()
{
    return m_completion_registration.IsActive()
        ? m_completion_registration.Complete()
        : LaxoriCompletionResult::Success;
}

LaxoriLazyCompletionRegistration LaxoriWorkPacket::DetachCompletionRegistration()
{
    return std::move(m_completion_registration);
}

const LaxoriLazyCompletionRegistration&
LaxoriWorkPacket::GetCompletionRegistration() const
{
    return m_completion_registration;
}

bool LaxoriWorkPacket::HasCompletionRegistration() const
{
    return m_completion_registration.IsActive();
}

LaxoriCompletionResult LaxoriWorkPacket::CreateBlockReleaseGuard(
    LaxoriBlockReleaseGuard* guard) const
{
    return m_completion_registration.CreateReleaseGuard(guard);
}
#endif
