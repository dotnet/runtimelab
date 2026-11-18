// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef LAXORI_COMPLETION_COUNTER_H
#define LAXORI_COMPLETION_COUNTER_H

#include <atomic>
#include <cstdint>

enum class LaxoriCompletionResult : uint8_t
{
    Success,
    InvalidArgument,
    InvalidState,
    StaleGeneration,
    Overflow,
    Underflow,
    AlreadyCompleted,
    Cancelled,
    CallbackFailed,
};

enum class LaxoriCompletionWorkKind : uint8_t
{
    Overall,
    Decrement,
    MatureSweep,
};

enum class LaxoriCompletionLifecycle : uint8_t
{
    Uninitialized,
    Registering,
    Published,
    Draining,
    DecisionRunning,
    Drained,
    Cancelling,
    Cancelled,
    Failed,
};

enum class LaxoriCompletionCallbackResult : uint8_t
{
    Success,
    Failure,
};

class LaxoriLazyCompletionGenerationState;
class LaxoriMatureSweepContinuation;
class LaxoriBlockReleaseGuard;

class LaxoriLazyCompletionRegistration final
{
public:
    LaxoriLazyCompletionRegistration();
    ~LaxoriLazyCompletionRegistration();
    LaxoriLazyCompletionRegistration(const LaxoriLazyCompletionRegistration&) = delete;
    LaxoriLazyCompletionRegistration& operator=(const LaxoriLazyCompletionRegistration&) = delete;
    LaxoriLazyCompletionRegistration(LaxoriLazyCompletionRegistration&& other) noexcept;
    LaxoriLazyCompletionRegistration& operator=(LaxoriLazyCompletionRegistration&& other) noexcept;

    LaxoriCompletionResult Complete();
    LaxoriCompletionResult RegisterChild(
        LaxoriCompletionWorkKind kind,
        LaxoriLazyCompletionRegistration* registration) const;
    LaxoriCompletionResult CreateReleaseGuard(LaxoriBlockReleaseGuard* guard) const;
    bool IsActive() const;
    uint64_t GetGeneration() const;
    LaxoriCompletionWorkKind GetKind() const;

private:
    friend class LaxoriLazyCompletionGenerationState;
    friend class LaxoriMatureSweepContinuation;

    LaxoriLazyCompletionRegistration(
        LaxoriLazyCompletionGenerationState* state,
        uint64_t generation,
        LaxoriCompletionWorkKind kind);
    void MoveFrom(LaxoriLazyCompletionRegistration&& other);

    LaxoriLazyCompletionGenerationState* m_state;
    uint64_t m_generation;
    LaxoriCompletionWorkKind m_kind;
    bool m_active;
};

class LaxoriBlockReleaseGuard final
{
public:
    LaxoriBlockReleaseGuard();
    bool IsValid() const;
    uint64_t GetGeneration() const;

private:
    friend class LaxoriLazyCompletionGenerationState;

    LaxoriBlockReleaseGuard(
        const LaxoriLazyCompletionGenerationState* state,
        uint64_t generation);

    const LaxoriLazyCompletionGenerationState* m_state;
    uint64_t m_generation;
};

class LaxoriMatureSweepContinuation final
{
public:
    LaxoriMatureSweepContinuation(const LaxoriMatureSweepContinuation&) = delete;
    LaxoriMatureSweepContinuation& operator=(const LaxoriMatureSweepContinuation&) = delete;
    LaxoriMatureSweepContinuation(LaxoriMatureSweepContinuation&& other) noexcept;
    LaxoriMatureSweepContinuation& operator=(LaxoriMatureSweepContinuation&& other) noexcept;

    LaxoriCompletionResult RegisterSweep(LaxoriLazyCompletionRegistration* registration);
    uint64_t GetGeneration() const;

private:
    friend class LaxoriLazyCompletionGenerationState;

    explicit LaxoriMatureSweepContinuation(
        LaxoriLazyCompletionRegistration&& registration);

    LaxoriLazyCompletionRegistration m_registration;
};

using LaxoriDecrementsDrainedCallback = LaxoriCompletionCallbackResult (*)(
    LaxoriMatureSweepContinuation* continuation,
    void* context);
using LaxoriOverallDrainedCallback = LaxoriCompletionCallbackResult (*)(
    uint64_t generation,
    void* context);

struct LaxoriCompletionSnapshot
{
    uint64_t generation;
    uint64_t overallOutstanding;
    uint64_t decrementOutstanding;
    uint64_t registrations;
    uint64_t completions;
    uint64_t decrementZeroTransitions;
    uint64_t overallZeroTransitions;
    uint64_t decrementCallbacks;
    uint64_t overallCallbacks;
    uint64_t ownershipViolations;
    LaxoriCompletionLifecycle lifecycle;
    LaxoriCompletionCallbackResult decrementCallbackResult;
    LaxoriCompletionCallbackResult overallCallbackResult;
    bool callbackRunning;
};

class LaxoriLazyCompletionGenerationState final
{
public:
    LaxoriLazyCompletionGenerationState();
    LaxoriLazyCompletionGenerationState(const LaxoriLazyCompletionGenerationState&) = delete;
    LaxoriLazyCompletionGenerationState& operator=(const LaxoriLazyCompletionGenerationState&) = delete;

    LaxoriCompletionResult Initialize(
        uint64_t generation,
        LaxoriDecrementsDrainedCallback decrementsDrained,
        LaxoriOverallDrainedCallback overallDrained,
        void* callbackContext,
        LaxoriLazyCompletionRegistration* seed,
        uint64_t maximumOutstanding = UINT64_MAX);
    LaxoriCompletionResult Seal(uint64_t generation);
    LaxoriCompletionResult MarkPublished(uint64_t generation);
    LaxoriCompletionResult RegisterRoot(
        uint64_t generation,
        LaxoriCompletionWorkKind kind,
        LaxoriLazyCompletionRegistration* registration);
    LaxoriCompletionResult RegisterChild(
        const LaxoriLazyCompletionRegistration& parent,
        LaxoriCompletionWorkKind kind,
        LaxoriLazyCompletionRegistration* registration);
    LaxoriCompletionResult Complete(LaxoriLazyCompletionRegistration* registration);
    LaxoriCompletionResult CreateReleaseGuard(
        const LaxoriLazyCompletionRegistration& registration,
        LaxoriBlockReleaseGuard* guard) const;
    LaxoriCompletionResult Cancel(uint64_t generation);
    LaxoriCompletionResult FinalizeCancellation(uint64_t generation);
    LaxoriCompletionResult PrepareForReuse(uint64_t generation);
    LaxoriCompletionSnapshot GetSnapshot() const;
    bool IsReleaseAllowed(uint64_t generation) const;

private:
    LaxoriCompletionResult RegisterInternal(
        uint64_t generation,
        LaxoriCompletionWorkKind kind,
        bool root,
        LaxoriLazyCompletionRegistration* registration);
    LaxoriCompletionResult IncrementChecked(std::atomic<uint64_t>* value);
    LaxoriCompletionResult DecrementChecked(
        std::atomic<uint64_t>* value,
        uint64_t* previous);
    LaxoriCompletionResult CompleteOverall(uint64_t generation);
    bool IsRegistrationLifecycle(LaxoriCompletionLifecycle lifecycle) const;
    void RecordOwnershipViolation();

    std::atomic<uint64_t> m_generation;
    std::atomic<uint64_t> m_overall_outstanding;
    std::atomic<uint64_t> m_decrement_outstanding;
    std::atomic<uint64_t> m_registrations;
    std::atomic<uint64_t> m_completions;
    std::atomic<uint64_t> m_decrement_zero_transitions;
    std::atomic<uint64_t> m_overall_zero_transitions;
    std::atomic<uint64_t> m_decrement_callbacks;
    std::atomic<uint64_t> m_overall_callbacks;
    std::atomic<uint64_t> m_ownership_violations;
    std::atomic<LaxoriCompletionLifecycle> m_lifecycle;
    std::atomic<LaxoriCompletionCallbackResult> m_decrement_callback_result;
    std::atomic<LaxoriCompletionCallbackResult> m_overall_callback_result;
    std::atomic<bool> m_callback_running;
    LaxoriDecrementsDrainedCallback m_decrements_drained;
    LaxoriOverallDrainedCallback m_overall_drained;
    void* m_callback_context;
    uint64_t m_maximum_outstanding;
};

#endif // LAXORI_COMPLETION_COUNTER_H
