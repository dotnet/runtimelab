// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Pre-zeroing of clean Immix blocks.
//
// An allocator that takes a clean block clears all of it (ImmixAllocationManager::PrepareFreshBlock), on the allocating
// thread. A per-block byte records which blocks are known to be zero, so that the clear can be left out:
//   - A block that was never used is zero (committed memory is zero). This is on by default.
//   - A block that a background thread (LaxoriBlockZeroer, off by default) has cleared in front of the allocators.
//
// Measured on the WSL2 host (see the commit message): the background thread takes the clear off the program thread (the
// memset of the program thread of `requests` falls from 108 ms to 8 ms), but the program thread then pays the same cost as
// cache misses of the first write to each line, so its CPU time does not fall and the total CPU time rises. The memory has
// to come into the cache of the program thread once, from the clearer thread or from memory. This is why the thread is
// an option.
//
// The protocol for one block:
//
//   ZeroStateDirty / ZeroStateTaken  not known to be zero. Dirty: not used since the last time it was written to. Taken:
//                                    an allocator took the block. The clearer treats them alike.
//   ZeroStateActive                  the clearer is clearing the block. Nobody else touches the memory.
//   ZeroStateClean                   every byte of the block is zero and no allocator holds it.
//
// The clearer claims a block with a compare-exchange from Dirty or Taken to Active and only then checks that the block is
// free (state Unallocated, no owner, not a nursery block of this epoch). An allocator that takes a block (always through
// PrepareFreshBlock, after the owner of the block is set) makes the block Taken with a compare-exchange, waiting while the
// block is Active. Both sides write first and read next with full barriers, so one of them sees the other: either the
// clearer sees the owner and gives up, or the allocator sees Active and waits. A block that was Clean when the allocator
// took it needs no clear. The clearer does not run during a pause (SuspendPreZero), so the epoch flip, the release of the
// nursery blocks, the copy of survivors and the change of the arena size never overlap with it.
//
// Only whole clean blocks are handled. The holes of recycled blocks stay cleared by the allocator (PrepareRecycledHole): a
// dead line is known to be dead only by its counts, and the memory of a dead object that a concurrent trace may still
// visit is protected by its mark bit, which a clear ahead of the allocation would not set.

#include "common.h"
#include "gcenv.h"
#include "laxori_immix_allocation.h"

#include <chrono>
#include <thread>

namespace
{
    void ClearBlockMemory(uintptr_t block)
    {
        memset(reinterpret_cast<void*>(block), 0, ImmixBlockGeometry::BlockBytes);
    }
}

void ImmixAllocationManager::InitializeZeroStates(size_t first, size_t end)
{
    if (m_notes.zero_state == nullptr)
    {
        return;
    }
    for (size_t index = first; index < end; index++)
    {
        m_notes.zero_state[index].store(ZeroStateClean, std::memory_order_relaxed);
    }
}

bool ImmixAllocationManager::TakeBlockForUse(uintptr_t block)
{
    size_t index = (block - m_arena_start) / ImmixBlockGeometry::BlockBytes;
    std::atomic<uint8_t>& state = m_notes.zero_state[index];
    uint8_t observed = state.load(std::memory_order_acquire);
#if !defined(LAXORI_F9_TEST_HOOKS) || !defined(LAXORI_PREZERO_NO_WAIT_CONTROL)
    uint32_t spins = 0;
#endif // !LAXORI_F9_TEST_HOOKS || !LAXORI_PREZERO_NO_WAIT_CONTROL
    while (true)
    {
#if !defined(LAXORI_F9_TEST_HOOKS) || !defined(LAXORI_PREZERO_NO_WAIT_CONTROL)
        // Control: an allocator does not wait for the claim of the clearer.
        if (observed == ZeroStateActive)
        {
            if (spins++ == 0)
            {
                m_zero_stats[6].fetch_add(1, std::memory_order_relaxed);
            }
            if (spins < 256)
            {
                YieldProcessor();
            }
            else
            {
                std::this_thread::yield();
            }
            observed = state.load(std::memory_order_acquire);
            continue;
        }
#endif // !LAXORI_F9_TEST_HOOKS || !LAXORI_PREZERO_NO_WAIT_CONTROL
        if (state.compare_exchange_weak(observed, ZeroStateTaken, std::memory_order_seq_cst, std::memory_order_acquire))
        {
            if (index >= m_prezero_wake_index.load(std::memory_order_relaxed) &&
#if !defined(LAXORI_F9_TEST_HOOKS) || !defined(LAXORI_PREZERO_NO_WAKE_CONTROL)
                m_prezero_wake_index.exchange(SIZE_MAX, std::memory_order_relaxed) != SIZE_MAX &&
#endif // !LAXORI_F9_TEST_HOOKS || !LAXORI_PREZERO_NO_WAKE_CONTROL
                m_prezero_wake != nullptr)
            {
#if defined(LAXORI_F9_TEST_HOOKS) && defined(LAXORI_PREZERO_NO_WAKE_CONTROL)
                // Control: the allocator never wakes the clearer.
                return observed == ZeroStateClean;
#endif // LAXORI_F9_TEST_HOOKS && LAXORI_PREZERO_NO_WAKE_CONTROL
                m_prezero_wake(m_prezero_wake_context);
            }
#if defined(LAXORI_F9_TEST_HOOKS) && defined(LAXORI_PREZERO_TAKE_AS_CLEAN_CONTROL)
            // Control: every block is taken as zero.
            return true;
#endif // LAXORI_F9_TEST_HOOKS && LAXORI_PREZERO_TAKE_AS_CLEAN_CONTROL
            return observed == ZeroStateClean;
        }
    }
}

#if defined(_DEBUG) || defined(LAXORI_F9_TEST_HOOKS)
void ImmixAllocationManager::VerifyBlockZero(uintptr_t block)
{
    const uintptr_t* words = reinterpret_cast<const uintptr_t*>(block);
    for (size_t word = 0; word < ImmixBlockGeometry::BlockBytes / sizeof(uintptr_t); word++)
    {
        if (words[word] != 0)
        {
            m_zero_stats[9].fetch_add(1, std::memory_order_relaxed);
            _ASSERTE(!"A block that was taken as zero is not zero.");
            return;
        }
    }
}
#endif // _DEBUG || LAXORI_F9_TEST_HOOKS

void ImmixAllocationManager::SuspendPreZero()
{
    if (m_notes.zero_state == nullptr)
    {
        return;
    }
    m_prezero_gate.store(true, std::memory_order_seq_cst);
    uint32_t spins = 0;
    while (m_prezero_inside.load(std::memory_order_seq_cst))
    {
        if (spins++ < 256)
        {
            YieldProcessor();
        }
        else
        {
            std::this_thread::yield();
        }
    }
}

void ImmixAllocationManager::ResumePreZero()
{
    m_prezero_resumes.fetch_add(1, std::memory_order_release);
    m_prezero_gate.store(false, std::memory_order_seq_cst);
}

bool ImmixAllocationManager::ArmPreZeroWake(size_t wakeIndex)
{
    m_prezero_wake_index.store(wakeIndex, std::memory_order_seq_cst);
    return m_next_fresh_block.load(std::memory_order_seq_cst) < wakeIndex;
}

bool ImmixAllocationManager::TryPreZeroBlock(size_t index, uint8_t epochParity)
{
    std::atomic<uint8_t>& state = m_notes.zero_state[index];
    uint8_t previous = state.load(std::memory_order_relaxed);
    if (previous != ZeroStateDirty && previous != ZeroStateTaken)
    {
        return false;
    }
    uintptr_t block = m_arena_start + index * ImmixBlockGeometry::BlockBytes;
    // Cheap look before the claim: only a block that a fresh acquire could take is of use.
    ImmixBlockState blockState;
    uint8_t unavailableLines;
    bool nursery;
#if !defined(LAXORI_F9_TEST_HOOKS) || !defined(LAXORI_PREZERO_NO_STATE_CHECK_CONTROL)
    // Control: the clearer does not look at the state of the block.
    if (m_blocks->GetStateFast(block, &blockState, &unavailableLines) != SideMetadataResult::Success ||
        blockState != ImmixBlockState::Unallocated ||
        m_blocks->IsNurseryFast(block, &nursery) != SideMetadataResult::Success || nursery)
    {
        return false;
    }
#else
    (void)blockState;
    (void)unavailableLines;
    (void)nursery;
#endif // !LAXORI_F9_TEST_HOOKS || !LAXORI_PREZERO_NO_STATE_CHECK_CONTROL
    if (!state.compare_exchange_strong(previous, ZeroStateActive, std::memory_order_seq_cst, std::memory_order_relaxed))
    {
        return false;
    }
#if !defined(LAXORI_F9_TEST_HOOKS) || !defined(LAXORI_PREZERO_NO_STATE_CHECK_CONTROL)
    bool free = false;
    bool ok = (m_blocks->GetGlobalPhaseEpoch() & 1) == epochParity &&
        m_blocks->IsFreeBlock(block, &free) == SideMetadataResult::Success && free &&
        m_blocks->IsNurseryFast(block, &nursery) == SideMetadataResult::Success && !nursery;
#else
    bool ok = true;
    (void)epochParity;
#endif // !LAXORI_F9_TEST_HOOKS || !LAXORI_PREZERO_NO_STATE_CHECK_CONTROL
    if (!ok)
    {
        state.store(previous, std::memory_order_release);
        m_zero_stats[5].fetch_add(1, std::memory_order_relaxed);
        return false;
    }
#if defined(LAXORI_F9_TEST_HOOKS) && defined(LAXORI_PREZERO_NO_WAIT_CONTROL)
    // Control: the claim lasts long enough for an allocator that does not wait to write into the block.
    std::this_thread::sleep_for(std::chrono::milliseconds(1));
#endif // LAXORI_F9_TEST_HOOKS && LAXORI_PREZERO_NO_WAIT_CONTROL
    ClearBlockMemory(block);
    state.store(ZeroStateClean, std::memory_order_release);
    m_zero_stats[4].fetch_add(1, std::memory_order_relaxed);
    return true;
}

size_t ImmixAllocationManager::PreZeroStep(size_t marginBlocks, size_t windowBlocks, size_t budget, PreZeroPass* pass)
{
    pass->cleared = 0;
    pass->caughtUp = true;
    pass->gated = false;
    pass->cursor = 0;
    pass->wakeIndex = SIZE_MAX;
    if (m_notes.zero_state == nullptr)
    {
        return 0;
    }
#if !defined(LAXORI_F9_TEST_HOOKS) || !defined(LAXORI_PREZERO_NO_GATE_CONTROL)
    // Control: the clearer ignores the gate of a pause.
    if (m_prezero_gate.load(std::memory_order_seq_cst))
    {
        pass->gated = true;
        return 0;
    }
#endif // !LAXORI_F9_TEST_HOOKS || !LAXORI_PREZERO_NO_GATE_CONTROL
    m_prezero_inside.store(true, std::memory_order_seq_cst);
#if !defined(LAXORI_F9_TEST_HOOKS) || !defined(LAXORI_PREZERO_NO_GATE_CONTROL)
    if (m_prezero_gate.load(std::memory_order_seq_cst))
    {
        m_prezero_inside.store(false, std::memory_order_release);
        pass->gated = true;
        return 0;
    }
#endif // !LAXORI_F9_TEST_HOOKS || !LAXORI_PREZERO_NO_GATE_CONTROL
    m_zero_stats[7].fetch_add(1, std::memory_order_relaxed);
    size_t cursor = m_next_fresh_block.load(std::memory_order_relaxed);
    pass->cursor = cursor;
    uint32_t resumes = m_prezero_resumes.load(std::memory_order_acquire);
    if (resumes != m_prezero_seen_resumes)
    {
        // A pause has set the cursor back: the blocks that the clearer passed over may be free now.
        m_prezero_seen_resumes = resumes;
        m_prezero_frontier = 0;
    }
    // Mutators take blocks below m_mutator_fresh_blocks only (placement); the blocks above are for the collector.
    size_t limit = m_placement && m_mutator_fresh_blocks < m_block_count ? m_mutator_fresh_blocks : m_block_count;
    size_t first = cursor + marginBlocks;
    size_t end = cursor + marginBlocks + windowBlocks;
    if (end > limit)
    {
        end = limit;
    }
    size_t index = m_prezero_frontier > first ? m_prezero_frontier : first;
    while (index < end && pass->cleared < budget)
    {
        if (TryPreZeroBlock(index, 1))
        {
            pass->cleared++;
        }
        index++;
    }
    m_prezero_frontier = index;
    pass->caughtUp = index >= end;
    // The allocators take blocks at the cursor, so a take at (end - margin - window / 2) leaves half a window to clear.
    pass->wakeIndex = end >= limit ? SIZE_MAX : end - marginBlocks - windowBlocks / 2;
    m_prezero_inside.store(false, std::memory_order_release);
    return pass->cleared;
}
