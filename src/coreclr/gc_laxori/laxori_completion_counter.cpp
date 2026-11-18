// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "laxori_completion_counter.h"

#include <utility>

LaxoriLazyCompletionRegistration::LaxoriLazyCompletionRegistration()
    : m_state(nullptr),
      m_generation(0),
      m_kind(LaxoriCompletionWorkKind::Overall),
      m_active(false)
{
}

LaxoriLazyCompletionRegistration::LaxoriLazyCompletionRegistration(
    LaxoriLazyCompletionGenerationState* state,
    uint64_t generation,
    LaxoriCompletionWorkKind kind)
    : m_state(state),
      m_generation(generation),
      m_kind(kind),
      m_active(true)
{
}

LaxoriLazyCompletionRegistration::~LaxoriLazyCompletionRegistration()
{
    Complete();
}

LaxoriLazyCompletionRegistration::LaxoriLazyCompletionRegistration(
    LaxoriLazyCompletionRegistration&& other) noexcept
    : LaxoriLazyCompletionRegistration()
{
    MoveFrom(std::move(other));
}

LaxoriLazyCompletionRegistration& LaxoriLazyCompletionRegistration::operator=(
    LaxoriLazyCompletionRegistration&& other) noexcept
{
    if (this != &other)
    {
        Complete();
        MoveFrom(std::move(other));
    }
    return *this;
}

void LaxoriLazyCompletionRegistration::MoveFrom(
    LaxoriLazyCompletionRegistration&& other)
{
    m_state = other.m_state;
    m_generation = other.m_generation;
    m_kind = other.m_kind;
    m_active = other.m_active;
    other.m_state = nullptr;
    other.m_generation = 0;
    other.m_kind = LaxoriCompletionWorkKind::Overall;
    other.m_active = false;
}

LaxoriCompletionResult LaxoriLazyCompletionRegistration::Complete()
{
    if (!m_active)
    {
        return LaxoriCompletionResult::AlreadyCompleted;
    }
    if (m_state == nullptr)
    {
        m_active = false;
        return LaxoriCompletionResult::InvalidState;
    }

    return m_state->Complete(this);
}

LaxoriCompletionResult LaxoriLazyCompletionRegistration::RegisterChild(
    LaxoriCompletionWorkKind kind,
    LaxoriLazyCompletionRegistration* registration) const
{
    return m_state != nullptr
        ? m_state->RegisterChild(*this, kind, registration)
        : LaxoriCompletionResult::InvalidState;
}

LaxoriCompletionResult LaxoriLazyCompletionRegistration::CreateReleaseGuard(
    LaxoriBlockReleaseGuard* guard) const
{
    return m_state != nullptr
        ? m_state->CreateReleaseGuard(*this, guard)
        : LaxoriCompletionResult::InvalidState;
}

bool LaxoriLazyCompletionRegistration::IsActive() const
{
    return m_active;
}

uint64_t LaxoriLazyCompletionRegistration::GetGeneration() const
{
    return m_generation;
}

LaxoriCompletionWorkKind LaxoriLazyCompletionRegistration::GetKind() const
{
    return m_kind;
}

LaxoriBlockReleaseGuard::LaxoriBlockReleaseGuard()
    : m_state(nullptr),
      m_generation(0)
{
}

LaxoriBlockReleaseGuard::LaxoriBlockReleaseGuard(
    const LaxoriLazyCompletionGenerationState* state,
    uint64_t generation)
    : m_state(state),
      m_generation(generation)
{
}

bool LaxoriBlockReleaseGuard::IsValid() const
{
    return m_state != nullptr && m_state->IsReleaseAllowed(m_generation);
}

uint64_t LaxoriBlockReleaseGuard::GetGeneration() const
{
    return m_generation;
}

LaxoriMatureSweepContinuation::LaxoriMatureSweepContinuation(
    LaxoriLazyCompletionRegistration&& registration)
    : m_registration(std::move(registration))
{
}

LaxoriMatureSweepContinuation::LaxoriMatureSweepContinuation(
    LaxoriMatureSweepContinuation&& other) noexcept
    : m_registration(std::move(other.m_registration))
{
}

LaxoriMatureSweepContinuation& LaxoriMatureSweepContinuation::operator=(
    LaxoriMatureSweepContinuation&& other) noexcept
{
    if (this != &other)
    {
        m_registration = std::move(other.m_registration);
    }
    return *this;
}

LaxoriCompletionResult LaxoriMatureSweepContinuation::RegisterSweep(
    LaxoriLazyCompletionRegistration* registration)
{
    if (!m_registration.IsActive() || m_registration.m_state == nullptr)
    {
        return LaxoriCompletionResult::InvalidState;
    }

    return m_registration.m_state->RegisterChild(
        m_registration,
        LaxoriCompletionWorkKind::MatureSweep,
        registration);
}

uint64_t LaxoriMatureSweepContinuation::GetGeneration() const
{
    return m_registration.GetGeneration();
}

LaxoriLazyCompletionGenerationState::LaxoriLazyCompletionGenerationState()
    : m_generation(0),
      m_overall_outstanding(0),
      m_decrement_outstanding(0),
      m_registrations(0),
      m_completions(0),
      m_decrement_zero_transitions(0),
      m_overall_zero_transitions(0),
      m_decrement_callbacks(0),
      m_overall_callbacks(0),
      m_ownership_violations(0),
      m_lifecycle(LaxoriCompletionLifecycle::Uninitialized),
      m_decrement_callback_result(LaxoriCompletionCallbackResult::Success),
      m_overall_callback_result(LaxoriCompletionCallbackResult::Success),
      m_callback_running(false),
      m_decrements_drained(nullptr),
      m_overall_drained(nullptr),
      m_callback_context(nullptr),
      m_maximum_outstanding(UINT64_MAX)
{
    static_assert(std::atomic<uint64_t>::is_always_lock_free);
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::Initialize(
    uint64_t generation,
    LaxoriDecrementsDrainedCallback decrementsDrained,
    LaxoriOverallDrainedCallback overallDrained,
    void* callbackContext,
    LaxoriLazyCompletionRegistration* seed,
    uint64_t maximumOutstanding)
{
    if (generation == 0 ||
        decrementsDrained == nullptr ||
        overallDrained == nullptr ||
        seed == nullptr ||
        seed->IsActive() ||
        maximumOutstanding == 0)
    {
        return LaxoriCompletionResult::InvalidArgument;
    }
    if (m_lifecycle.load(std::memory_order_seq_cst) !=
            LaxoriCompletionLifecycle::Uninitialized ||
        m_overall_outstanding.load(std::memory_order_seq_cst) != 0 ||
        m_decrement_outstanding.load(std::memory_order_seq_cst) != 0 ||
        m_callback_running.load(std::memory_order_seq_cst))
    {
        return LaxoriCompletionResult::InvalidState;
    }

    m_decrements_drained = decrementsDrained;
    m_overall_drained = overallDrained;
    m_callback_context = callbackContext;
    m_maximum_outstanding = maximumOutstanding;
    m_registrations.store(1, std::memory_order_seq_cst);
    m_completions.store(0, std::memory_order_seq_cst);
    m_decrement_zero_transitions.store(0, std::memory_order_seq_cst);
    m_overall_zero_transitions.store(0, std::memory_order_seq_cst);
    m_decrement_callbacks.store(0, std::memory_order_seq_cst);
    m_overall_callbacks.store(0, std::memory_order_seq_cst);
    m_ownership_violations.store(0, std::memory_order_seq_cst);
    m_decrement_callback_result.store(
        LaxoriCompletionCallbackResult::Success,
        std::memory_order_seq_cst);
    m_overall_callback_result.store(
        LaxoriCompletionCallbackResult::Success,
        std::memory_order_seq_cst);
    m_overall_outstanding.store(1, std::memory_order_seq_cst);
    m_decrement_outstanding.store(1, std::memory_order_seq_cst);
    m_generation.store(generation, std::memory_order_seq_cst);
    m_lifecycle.store(
        LaxoriCompletionLifecycle::Registering,
        std::memory_order_seq_cst);
    *seed = LaxoriLazyCompletionRegistration(
        this,
        generation,
        LaxoriCompletionWorkKind::Decrement);
    return LaxoriCompletionResult::Success;
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::Seal(uint64_t generation)
{
    if (m_generation.load(std::memory_order_seq_cst) != generation)
    {
        return LaxoriCompletionResult::StaleGeneration;
    }

    LaxoriCompletionLifecycle expected = LaxoriCompletionLifecycle::Registering;
    return m_lifecycle.compare_exchange_strong(
        expected,
        LaxoriCompletionLifecycle::Published,
        std::memory_order_seq_cst,
        std::memory_order_seq_cst)
        ? LaxoriCompletionResult::Success
        : LaxoriCompletionResult::InvalidState;
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::MarkPublished(
    uint64_t generation)
{
    if (m_generation.load(std::memory_order_seq_cst) != generation)
    {
        return LaxoriCompletionResult::StaleGeneration;
    }
    return m_lifecycle.load(std::memory_order_seq_cst) ==
        LaxoriCompletionLifecycle::Published
        ? LaxoriCompletionResult::Success
        : LaxoriCompletionResult::InvalidState;
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::RegisterRoot(
    uint64_t generation,
    LaxoriCompletionWorkKind kind,
    LaxoriLazyCompletionRegistration* registration)
{
    if (kind == LaxoriCompletionWorkKind::MatureSweep)
    {
        return LaxoriCompletionResult::InvalidArgument;
    }
    return RegisterInternal(generation, kind, true, registration);
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::RegisterChild(
    const LaxoriLazyCompletionRegistration& parent,
    LaxoriCompletionWorkKind kind,
    LaxoriLazyCompletionRegistration* registration)
{
    if (!parent.m_active ||
        parent.m_state != this ||
        parent.m_generation != m_generation.load(std::memory_order_seq_cst))
    {
        return LaxoriCompletionResult::StaleGeneration;
    }
    if ((kind == LaxoriCompletionWorkKind::Decrement &&
            parent.m_kind != LaxoriCompletionWorkKind::Decrement) ||
        (kind == LaxoriCompletionWorkKind::MatureSweep &&
            parent.m_kind != LaxoriCompletionWorkKind::MatureSweep))
    {
        return LaxoriCompletionResult::InvalidArgument;
    }

    return RegisterInternal(parent.m_generation, kind, false, registration);
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::RegisterInternal(
    uint64_t generation,
    LaxoriCompletionWorkKind kind,
    bool root,
    LaxoriLazyCompletionRegistration* registration)
{
    if (registration == nullptr ||
        registration->IsActive() ||
        kind > LaxoriCompletionWorkKind::MatureSweep)
    {
        return LaxoriCompletionResult::InvalidArgument;
    }
    if (m_generation.load(std::memory_order_seq_cst) != generation)
    {
        return LaxoriCompletionResult::StaleGeneration;
    }

    LaxoriCompletionLifecycle lifecycle =
        m_lifecycle.load(std::memory_order_seq_cst);
    if (root)
    {
        if (lifecycle != LaxoriCompletionLifecycle::Registering)
        {
            return LaxoriCompletionResult::InvalidState;
        }
    }
    else if (!IsRegistrationLifecycle(lifecycle))
    {
        return lifecycle == LaxoriCompletionLifecycle::Cancelling ||
                lifecycle == LaxoriCompletionLifecycle::Cancelled
            ? LaxoriCompletionResult::Cancelled
            : LaxoriCompletionResult::InvalidState;
    }
    if (kind == LaxoriCompletionWorkKind::Decrement &&
        m_decrement_outstanding.load(std::memory_order_seq_cst) == 0)
    {
        return LaxoriCompletionResult::InvalidState;
    }

    if (kind == LaxoriCompletionWorkKind::Decrement)
    {
        LaxoriCompletionResult result = IncrementChecked(&m_decrement_outstanding);
        if (result != LaxoriCompletionResult::Success)
        {
            return result;
        }
    }

    LaxoriCompletionResult result = IncrementChecked(&m_overall_outstanding);
    if (result != LaxoriCompletionResult::Success)
    {
        if (kind == LaxoriCompletionWorkKind::Decrement)
        {
            uint64_t previous = 0;
            DecrementChecked(&m_decrement_outstanding, &previous);
        }
        return result;
    }

    m_registrations.fetch_add(1, std::memory_order_seq_cst);
    *registration = LaxoriLazyCompletionRegistration(this, generation, kind);
    return LaxoriCompletionResult::Success;
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::Complete(
    LaxoriLazyCompletionRegistration* registration)
{
    if (registration == nullptr || !registration->m_active)
    {
        return LaxoriCompletionResult::AlreadyCompleted;
    }
    if (registration->m_state != this ||
        registration->m_generation != m_generation.load(std::memory_order_seq_cst))
    {
        registration->m_active = false;
        RecordOwnershipViolation();
        return LaxoriCompletionResult::StaleGeneration;
    }

    uint64_t generation = registration->m_generation;
    LaxoriCompletionWorkKind kind = registration->m_kind;
    registration->m_active = false;
    registration->m_state = nullptr;
    registration->m_generation = 0;

    LaxoriCompletionResult result = LaxoriCompletionResult::Success;
    if (kind == LaxoriCompletionWorkKind::Decrement)
    {
        uint64_t decrementPrevious = 0;
        result = DecrementChecked(&m_decrement_outstanding, &decrementPrevious);
        if (result != LaxoriCompletionResult::Success)
        {
            RecordOwnershipViolation();
            return result;
        }

        if (decrementPrevious == 1)
        {
            m_decrement_zero_transitions.fetch_add(1, std::memory_order_seq_cst);
            LaxoriLazyCompletionRegistration continuationRegistration;
            result = RegisterInternal(
                generation,
                LaxoriCompletionWorkKind::MatureSweep,
                false,
                &continuationRegistration);
            if (result != LaxoriCompletionResult::Success)
            {
                m_lifecycle.store(
                    LaxoriCompletionLifecycle::Failed,
                    std::memory_order_seq_cst);
            }
            else
            {
                m_lifecycle.store(
                    LaxoriCompletionLifecycle::Draining,
                    std::memory_order_seq_cst);
                LaxoriMatureSweepContinuation continuation(
                    std::move(continuationRegistration));
                m_callback_running.store(true, std::memory_order_seq_cst);
                m_decrement_callbacks.fetch_add(1, std::memory_order_seq_cst);
                LaxoriCompletionCallbackResult callbackResult =
                    m_decrements_drained(&continuation, m_callback_context);
                m_decrement_callback_result.store(
                    callbackResult,
                    std::memory_order_seq_cst);
                if (callbackResult != LaxoriCompletionCallbackResult::Success)
                {
                    m_lifecycle.store(
                        LaxoriCompletionLifecycle::Failed,
                        std::memory_order_seq_cst);
                    result = LaxoriCompletionResult::CallbackFailed;
                }
                m_callback_running.store(false, std::memory_order_seq_cst);
            }
        }
    }

    LaxoriCompletionResult overallResult = CompleteOverall(generation);
    m_completions.fetch_add(1, std::memory_order_seq_cst);
    if (result != LaxoriCompletionResult::Success)
    {
        return result;
    }
    return overallResult;
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::CompleteOverall(
    uint64_t generation)
{
    uint64_t previous = 0;
    LaxoriCompletionResult result =
        DecrementChecked(&m_overall_outstanding, &previous);
    if (result != LaxoriCompletionResult::Success)
    {
        RecordOwnershipViolation();
        return result;
    }
    if (previous != 1)
    {
        return LaxoriCompletionResult::Success;
    }

    m_overall_zero_transitions.fetch_add(1, std::memory_order_seq_cst);
    LaxoriCompletionLifecycle lifecycle =
        m_lifecycle.load(std::memory_order_seq_cst);
    if (lifecycle == LaxoriCompletionLifecycle::Cancelling ||
        lifecycle == LaxoriCompletionLifecycle::Cancelled)
    {
        m_lifecycle.store(
            LaxoriCompletionLifecycle::Cancelled,
            std::memory_order_seq_cst);
        return LaxoriCompletionResult::Cancelled;
    }
    if (lifecycle == LaxoriCompletionLifecycle::Failed)
    {
        return LaxoriCompletionResult::CallbackFailed;
    }

    m_lifecycle.store(
        LaxoriCompletionLifecycle::DecisionRunning,
        std::memory_order_seq_cst);
    m_callback_running.store(true, std::memory_order_seq_cst);
    m_overall_callbacks.fetch_add(1, std::memory_order_seq_cst);
    LaxoriCompletionCallbackResult callbackResult =
        m_overall_drained(generation, m_callback_context);
    m_overall_callback_result.store(callbackResult, std::memory_order_seq_cst);
    m_callback_running.store(false, std::memory_order_seq_cst);
    m_lifecycle.store(
        callbackResult == LaxoriCompletionCallbackResult::Success
            ? LaxoriCompletionLifecycle::Drained
            : LaxoriCompletionLifecycle::Failed,
        std::memory_order_seq_cst);
    return callbackResult == LaxoriCompletionCallbackResult::Success
        ? LaxoriCompletionResult::Success
        : LaxoriCompletionResult::CallbackFailed;
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::CreateReleaseGuard(
    const LaxoriLazyCompletionRegistration& registration,
    LaxoriBlockReleaseGuard* guard) const
{
    if (guard == nullptr)
    {
        return LaxoriCompletionResult::InvalidArgument;
    }
    if (!registration.m_active ||
        registration.m_state != this ||
        registration.m_kind != LaxoriCompletionWorkKind::MatureSweep)
    {
        return LaxoriCompletionResult::InvalidState;
    }
    if (!IsReleaseAllowed(registration.m_generation))
    {
        return LaxoriCompletionResult::InvalidState;
    }

    *guard = LaxoriBlockReleaseGuard(this, registration.m_generation);
    return LaxoriCompletionResult::Success;
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::Cancel(uint64_t generation)
{
    if (m_generation.load(std::memory_order_seq_cst) != generation)
    {
        return LaxoriCompletionResult::StaleGeneration;
    }

    LaxoriCompletionLifecycle lifecycle =
        m_lifecycle.load(std::memory_order_seq_cst);
    if (lifecycle == LaxoriCompletionLifecycle::Uninitialized)
    {
        return LaxoriCompletionResult::InvalidState;
    }
    if (lifecycle == LaxoriCompletionLifecycle::Drained ||
        lifecycle == LaxoriCompletionLifecycle::Cancelled)
    {
        return LaxoriCompletionResult::Success;
    }

    m_lifecycle.store(
        LaxoriCompletionLifecycle::Cancelling,
        std::memory_order_seq_cst);
    if (m_overall_outstanding.load(std::memory_order_seq_cst) == 0)
    {
        m_lifecycle.store(
            LaxoriCompletionLifecycle::Cancelled,
            std::memory_order_seq_cst);
    }
    return LaxoriCompletionResult::Success;
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::FinalizeCancellation(
    uint64_t generation)
{
    if (m_generation.load(std::memory_order_seq_cst) != generation)
    {
        return LaxoriCompletionResult::StaleGeneration;
    }
    if (m_overall_outstanding.load(std::memory_order_seq_cst) != 0 ||
        m_decrement_outstanding.load(std::memory_order_seq_cst) != 0 ||
        m_callback_running.load(std::memory_order_seq_cst))
    {
        RecordOwnershipViolation();
        return LaxoriCompletionResult::InvalidState;
    }

    m_lifecycle.store(
        LaxoriCompletionLifecycle::Cancelled,
        std::memory_order_seq_cst);
    return LaxoriCompletionResult::Success;
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::PrepareForReuse(
    uint64_t generation)
{
    if (m_generation.load(std::memory_order_seq_cst) != generation)
    {
        return LaxoriCompletionResult::StaleGeneration;
    }
    if (m_lifecycle.load(std::memory_order_seq_cst) !=
            LaxoriCompletionLifecycle::Drained ||
        m_overall_outstanding.load(std::memory_order_seq_cst) != 0 ||
        m_decrement_outstanding.load(std::memory_order_seq_cst) != 0 ||
        m_callback_running.load(std::memory_order_seq_cst))
    {
        return LaxoriCompletionResult::InvalidState;
    }

    m_decrements_drained = nullptr;
    m_overall_drained = nullptr;
    m_callback_context = nullptr;
    m_lifecycle.store(
        LaxoriCompletionLifecycle::Uninitialized,
        std::memory_order_seq_cst);
    return LaxoriCompletionResult::Success;
}

LaxoriCompletionSnapshot LaxoriLazyCompletionGenerationState::GetSnapshot() const
{
    LaxoriCompletionSnapshot snapshot{};
    snapshot.generation = m_generation.load(std::memory_order_seq_cst);
    snapshot.overallOutstanding =
        m_overall_outstanding.load(std::memory_order_seq_cst);
    snapshot.decrementOutstanding =
        m_decrement_outstanding.load(std::memory_order_seq_cst);
    snapshot.registrations = m_registrations.load(std::memory_order_seq_cst);
    snapshot.completions = m_completions.load(std::memory_order_seq_cst);
    snapshot.decrementZeroTransitions =
        m_decrement_zero_transitions.load(std::memory_order_seq_cst);
    snapshot.overallZeroTransitions =
        m_overall_zero_transitions.load(std::memory_order_seq_cst);
    snapshot.decrementCallbacks =
        m_decrement_callbacks.load(std::memory_order_seq_cst);
    snapshot.overallCallbacks =
        m_overall_callbacks.load(std::memory_order_seq_cst);
    snapshot.ownershipViolations =
        m_ownership_violations.load(std::memory_order_seq_cst);
    snapshot.lifecycle = m_lifecycle.load(std::memory_order_seq_cst);
    snapshot.decrementCallbackResult =
        m_decrement_callback_result.load(std::memory_order_seq_cst);
    snapshot.overallCallbackResult =
        m_overall_callback_result.load(std::memory_order_seq_cst);
    snapshot.callbackRunning = m_callback_running.load(std::memory_order_seq_cst);
    return snapshot;
}

bool LaxoriLazyCompletionGenerationState::IsReleaseAllowed(
    uint64_t generation) const
{
    if (m_generation.load(std::memory_order_seq_cst) != generation ||
        m_decrement_outstanding.load(std::memory_order_seq_cst) != 0)
    {
        return false;
    }

    LaxoriCompletionLifecycle lifecycle =
        m_lifecycle.load(std::memory_order_seq_cst);
    return lifecycle == LaxoriCompletionLifecycle::Draining ||
        lifecycle == LaxoriCompletionLifecycle::DecisionRunning;
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::IncrementChecked(
    std::atomic<uint64_t>* value)
{
    uint64_t current = value->load(std::memory_order_seq_cst);
    while (true)
    {
        if (current >= m_maximum_outstanding)
        {
            return LaxoriCompletionResult::Overflow;
        }
        if (value->compare_exchange_weak(
                current,
                current + 1,
                std::memory_order_seq_cst,
                std::memory_order_seq_cst))
        {
            return LaxoriCompletionResult::Success;
        }
    }
}

LaxoriCompletionResult LaxoriLazyCompletionGenerationState::DecrementChecked(
    std::atomic<uint64_t>* value,
    uint64_t* previous)
{
    uint64_t current = value->load(std::memory_order_seq_cst);
    while (true)
    {
        if (current == 0)
        {
            return LaxoriCompletionResult::Underflow;
        }
        if (value->compare_exchange_weak(
                current,
                current - 1,
                std::memory_order_seq_cst,
                std::memory_order_seq_cst))
        {
            *previous = current;
            return LaxoriCompletionResult::Success;
        }
    }
}

bool LaxoriLazyCompletionGenerationState::IsRegistrationLifecycle(
    LaxoriCompletionLifecycle lifecycle) const
{
    return lifecycle == LaxoriCompletionLifecycle::Registering ||
        lifecycle == LaxoriCompletionLifecycle::Published ||
        lifecycle == LaxoriCompletionLifecycle::Draining;
}

void LaxoriLazyCompletionGenerationState::RecordOwnershipViolation()
{
    m_ownership_violations.fetch_add(1, std::memory_order_seq_cst);
}
