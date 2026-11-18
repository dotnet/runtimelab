// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef LAXORI_WORK_PACKET_H
#define LAXORI_WORK_PACKET_H

#ifdef byte
#pragma push_macro("byte")
#define LAXORI_WORK_PACKET_RESTORE_BYTE
#undef byte
#endif
#ifdef __valid
#pragma push_macro("__valid")
#define LAXORI_WORK_PACKET_RESTORE_VALID
#undef __valid
#endif

#include <atomic>
#include <cstddef>
#include <cstdint>

#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
#include "laxori_completion_counter.h"
#endif

enum class LaxoriWorkBucketStage : uint8_t
{
    Unconstrained,
    Concurrent,
    ConcurrentResumable,
    FinishConcurrentWork,
    Initial,
    Prepare,
    Closure,
    Release,
    STWRCDecsAndSweep,
    Final,
    Count,
};

constexpr LaxoriWorkBucketStage LaxoriRCProcessIncs = LaxoriWorkBucketStage::Initial;
constexpr LaxoriWorkBucketStage LaxoriRCProcessDecs = LaxoriWorkBucketStage::STWRCDecsAndSweep;
constexpr LaxoriWorkBucketStage LaxoriFirstStwStage = LaxoriWorkBucketStage::FinishConcurrentWork;
constexpr size_t LaxoriLocalPacketCacheCapacity = 16;
constexpr size_t LaxoriDefaultRcPacketReferenceCapacity = 1024;
constexpr size_t LaxoriEdgePacketReferenceCapacity = 4096;

static_assert(static_cast<uint8_t>(LaxoriWorkBucketStage::Unconstrained) == 0);
static_assert(static_cast<uint8_t>(LaxoriWorkBucketStage::Concurrent) == 1);
static_assert(static_cast<uint8_t>(LaxoriWorkBucketStage::ConcurrentResumable) == 2);
static_assert(static_cast<uint8_t>(LaxoriWorkBucketStage::FinishConcurrentWork) == 3);
static_assert(static_cast<uint8_t>(LaxoriWorkBucketStage::Initial) == 4);
static_assert(static_cast<uint8_t>(LaxoriWorkBucketStage::Prepare) == 5);
static_assert(static_cast<uint8_t>(LaxoriWorkBucketStage::Closure) == 6);
static_assert(static_cast<uint8_t>(LaxoriWorkBucketStage::Release) == 7);
static_assert(static_cast<uint8_t>(LaxoriWorkBucketStage::STWRCDecsAndSweep) == 8);
static_assert(static_cast<uint8_t>(LaxoriWorkBucketStage::Final) == 9);
static_assert(static_cast<uint8_t>(LaxoriWorkBucketStage::Count) == 10);
static_assert(LaxoriRCProcessIncs == LaxoriWorkBucketStage::Initial);
static_assert(LaxoriRCProcessDecs == LaxoriWorkBucketStage::STWRCDecsAndSweep);

const char* LaxoriGetWorkBucketStageName(LaxoriWorkBucketStage stage);
bool LaxoriIsConcurrentStage(LaxoriWorkBucketStage stage);
bool LaxoriIsStwStage(LaxoriWorkBucketStage stage);
bool LaxoriIsSupportedReferencePacketCapacity(size_t capacity);
bool LaxoriTryGetReferencePacketCount(size_t referenceCount, size_t capacity, size_t* packetCount);

enum class LaxoriWorkPacketState : uint8_t
{
    CallerOwned,
    Queued,
    Executing,
    Completed,
};

class LaxoriWorkPacket final
{
public:
    using ExecuteCallback = void (*)(
        LaxoriWorkPacket* packet,
        size_t workerIndex,
        void* context);

    LaxoriWorkPacket(
        uint64_t id,
        LaxoriWorkBucketStage stage,
        const uintptr_t* references,
        size_t referenceCount,
        ExecuteCallback callback,
        void* context);
    LaxoriWorkPacket(const LaxoriWorkPacket&) = delete;
    LaxoriWorkPacket& operator=(const LaxoriWorkPacket&) = delete;

    uint64_t GetId() const;
    LaxoriWorkBucketStage GetStage() const;
    uint64_t GetGeneration() const;
    const uintptr_t* GetReferences() const;
    size_t GetReferenceCount() const;
    LaxoriWorkPacketState GetState() const;
    // P26 (F2). The context pointer that the owner gave to the packet.
    void* GetContext() const { return m_context; }
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    LaxoriCompletionResult CreateBlockReleaseGuard(
        LaxoriBlockReleaseGuard* guard) const;
#endif

private:
    friend class LaxoriWorkScheduler;

    bool TryTransition(LaxoriWorkPacketState expected, LaxoriWorkPacketState desired);
    void SetGeneration(uint64_t generation);
    void SetPreferredWorker(size_t workerIndex);
    size_t GetPreferredWorker() const;
    void SetDeferred(bool deferred);
    bool IsDeferred() const;
    void Execute(size_t workerIndex);
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    bool AttachCompletionRegistration(
        LaxoriLazyCompletionRegistration&& registration);
    LaxoriCompletionResult CompleteRegistration();
    LaxoriLazyCompletionRegistration DetachCompletionRegistration();
    const LaxoriLazyCompletionRegistration& GetCompletionRegistration() const;
    bool HasCompletionRegistration() const;
#endif

    const uint64_t m_id;
    const LaxoriWorkBucketStage m_stage;
    const uintptr_t* const m_references;
    const size_t m_reference_count;
    const ExecuteCallback m_callback;
    void* const m_context;
    std::atomic<uint64_t> m_generation;
    std::atomic<LaxoriWorkPacketState> m_state;
    size_t m_preferred_worker;
    bool m_deferred;
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    LaxoriLazyCompletionRegistration m_completion_registration;
#endif
};

#ifdef LAXORI_WORK_PACKET_RESTORE_BYTE
#pragma pop_macro("byte")
#undef LAXORI_WORK_PACKET_RESTORE_BYTE
#endif
#ifdef LAXORI_WORK_PACKET_RESTORE_VALID
#pragma pop_macro("__valid")
#undef LAXORI_WORK_PACKET_RESTORE_VALID
#endif

#endif // LAXORI_WORK_PACKET_H
