// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef IMMIX_ALLOCATION_H
#define IMMIX_ALLOCATION_H

#include "immix_block.h"

#include <atomic>

// Test hooks of F9 (the probe counter, the check of the clean-range invariant and the controls). The native test platform
// of the engine tests enables them; the validation program of P2.4 enables them with -DLXR_F9_TEST_HOOKS.
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && !defined(LXR_F9_TEST_HOOKS)
#define LXR_F9_TEST_HOOKS
#endif

enum class ImmixAllocationSource : uint8_t
{
    MutatorFresh,
    MutatorRecycled,
    GcCopyFresh,
    GcCopyRecycled,
    // P17 S7: a mutator takes a clean block for pinned-object-heap objects. The block is marked "pinned" until it is
    // acquired again for another purpose. A pinned object goes only into a pinned block, and no other allocation goes
    // into one: MutatorPinnedRecycled takes a Reusable pinned block, and every other recycled source skips them.
    MutatorPinnedFresh,
    MutatorPinnedRecycled,
};

enum class ImmixMarkingContext : uint8_t
{
    Inactive,
    ConcurrentOrFinalMark,
};

enum class ImmixAllocationStatus : uint8_t
{
    Allocated,
    Ready,
    Exhausted,
    Contended,
    Stale,
    InvalidPhase,
    InvalidRequest,
};

struct ImmixAllocationRequest
{
    size_t size;
    size_t alignment;
    size_t alignment_offset;
};

struct ImmixHole
{
    uintptr_t start;
    uintptr_t end;
    size_t line_count;
};

class ImmixAllocationManager final
{
public:
    ImmixAllocationManager();
    ImmixAllocationManager(const ImmixAllocationManager&) = delete;
    ImmixAllocationManager& operator=(const ImmixAllocationManager&) = delete;

    SideMetadataResult Initialize(
        SideMetadataManager* metadata,
        ImmixBlockManager* blocks,
        uintptr_t arenaStart,
        size_t arenaSize,
        size_t minimumReuseLines);
    // Requires all allocation workers to be quiescent.
    void Shutdown();
    SideMetadataResult GrowQuiescent(size_t arenaSize);
    // Lowers the arena to arenaSize. Requires all allocation workers to be quiescent and no allocator to hold a
    // block at or above the new end. The cursors wrap on the new block count.
    SideMetadataResult ShrinkQuiescent(size_t arenaSize);
    // P17 S5b placement. With it, mutators take fresh blocks only below mutatorFreshBlocks (the kept capacity), and the
    // collector takes fresh blocks for copying from the lowest free block upwards, restarting at block 0 in every pause
    // (ResetCopyCursorQuiescent). Survivors then fill free blocks low in the arena and the borrowed range above them is
    // free at the end of the pause. Without it both use one shared round-robin cursor over the whole arena.
    void EnablePlacementQuiescent(size_t mutatorFreshBlocks);
    void SetMutatorFreshBlocksQuiescent(size_t mutatorFreshBlocks);
    void ResetCopyCursorQuiescent();
    // The Java LXR starts every pause, and ends it, with the block cursors at 0 (BlockPageResource::prepare_gc, reset).
    void ResetCursors();

    SideMetadataResult TryFindNextHole(
        uintptr_t block,
        size_t searchLine,
        ImmixHole* hole,
        size_t* nextSearchLine,
        ImmixAllocationStatus* status) const;
    // The result of calling TryFindNextHole from searchLine until it returns a hole of at least minimumLines lines (the
    // holes that it passes over, which have the minimum reuse lines but fewer than minimumLines, are added to *skipped).
    // The count words of the block are read in halves of 64 lines, with no more than one pass over each, and the runs of
    // dead lines are found with bit operations. TryFindNextHole is better for a request that fits the first hole.
    SideMetadataResult TryFindFittingHole(
        uintptr_t block,
        size_t searchLine,
        size_t minimumLines,
        ImmixHole* hole,
        size_t* nextSearchLine,
        ImmixAllocationStatus* status,
        size_t* skipped) const;
    // The sum of the lines of all holes that TryFindNextHole returns for the block, from line 0.
    SideMetadataResult CountHoleLines(uintptr_t block, size_t* lines) const;

    size_t GetFreshBlocksAcquired() const;
    size_t GetRecycledBlocksAcquired() const;
    size_t GetRecycledLinesConsumed() const;
    // Holes that a refill left alone because they were smaller than the pending request (see TryRefill).
    size_t GetHolesSkipped() const;
    size_t GetBlocksInUse() const;
    // Zeroing metrics (the clears of memory for new objects). 0: clean blocks cleared by the allocating thread, 1: holes
    // cleared by it, 2: bytes of those holes, 3: clean blocks that an allocator took already zero, 4: blocks cleared by the
    // background clearer, 5: blocks that the clearer claimed and gave up, 6: times an allocator waited for a claim,
    // 7: passes of the clearer, 9: blocks taken as zero that were not (a defect; Debug and test builds only).
    uint64_t GetZeroingMetric(size_t metric) const
    {
        return metric < 10 ? m_zero_stats[metric].load(std::memory_order_relaxed) : UINT64_MAX;
    }
#ifdef LXR_F9_TEST_HOOKS
    // Test only. The number of lines whose counts the hole search has read since the manager was initialized.
    uint64_t GetLineProbesForTest() const
    {
        return m_line_probes.load(std::memory_order_relaxed);
    }
#endif // LXR_F9_TEST_HOOKS
    SideMetadataResult NotifyBlockReleased();
    // The same for `count` blocks. It fails, and changes nothing, when fewer than `count` blocks are in use.
    SideMetadataResult NotifyBlocksReleased(size_t count);

    // Storage that the owner of the manager provides, one byte per block unless noted. Every successful block
    // acquisition is noted here, so that no pause has to walk all blocks to find the blocks that allocators touched.
    //   allocated_flags / allocated_list / allocated_count: the blocks that mutators took since the last pause, each
    //     once. The pause releases those that are clean nursery blocks and that no object promoted.
    //   refresh_flags: a block that a mutator took as a recycled block, or that a collector thread took for copying.
    //     Its free lines have to be counted again after the next pause.
    //   pinned_flags (P17 S7): the owner's byte for each block. The manager sets PinnedBlockFlag in it when a mutator
    //     takes the block as a pinned block, and clears the flag when the block is taken for any other purpose. The
    //     owner may keep other bits in the same byte.
    struct AcquireNotes
    {
        std::atomic<uint8_t>* allocated_flags;
        uint32_t* allocated_list;
        std::atomic<size_t>* allocated_count;
        std::atomic<uint8_t>* refresh_flags;
        std::atomic<uint8_t>* pinned_flags = nullptr;
        // Pre-zeroing of clean blocks (immix_zeroing.cpp). One byte per block, values ZeroState*. Null: every clean block
        // is cleared when an allocator takes it. The blocks of the arena at the time of SetAcquireNotes (and the blocks
        // that GrowQuiescent adds) are taken as zero, because committed memory is zero.
        std::atomic<uint8_t>* zero_state = nullptr;
    };
    static constexpr uint8_t PinnedBlockFlag = 1;
    void SetAcquireNotes(const AcquireNotes& notes);

    // The state of the memory of a block that no allocator holds. A block that an allocator took is ZeroStateTaken (the
    // memory is in use and not zero) until it is cleared again.
    static constexpr uint8_t ZeroStateDirty = 0;
    static constexpr uint8_t ZeroStateActive = 1;
    static constexpr uint8_t ZeroStateClean = 2;
    static constexpr uint8_t ZeroStateTaken = 3;
    struct PreZeroPass
    {
        size_t cleared;
        // The window of blocks in front of the allocation cursor is clean (or cannot be cleaned now).
        bool caughtUp;
        // The pass did nothing because a pause (or a change of the arena) holds the clearer back.
        bool gated;
        size_t cursor;
        // The index of the block that an allocator takes when the clearer has to wake up again (SIZE_MAX: never; the end of a
        // pause wakes it).
        size_t wakeIndex;
    };
    // The clearer calls ArmPreZeroWake before it sleeps; an allocator that takes a block at wakeIndex or above calls the
    // callback (once). The callback must be cheap and must not block.
    using PreZeroWakeCallback = void (*)(void* context);
    void SetPreZeroWake(PreZeroWakeCallback callback, void* context)
    {
        m_prezero_wake = callback;
        m_prezero_wake_context = context;
    }
    // Returns true when the clearer may sleep (the allocators have not reached wakeIndex yet).
    bool ArmPreZeroWake(size_t wakeIndex);
    void DisarmPreZeroWake()
    {
        m_prezero_wake_index.store(SIZE_MAX, std::memory_order_relaxed);
    }
    // One pass of the background clearer (the thread of LxrBlockZeroer). It clears at most `budget` free blocks, in the
    // window that starts `marginBlocks` blocks after the cursor of the clean-block search and has `windowBlocks` blocks.
    // A block that is free and not clean is claimed (ZeroStateActive), cleared and published as ZeroStateClean; an
    // allocator that takes a claimed block waits for the claim to end. The clearer works only while the program runs.
    size_t PreZeroStep(size_t marginBlocks, size_t windowBlocks, size_t budget, PreZeroPass* pass);
    // The clearer is out of the arena and stays out until ResumePreZero. SuspendPreZero returns when no pass is running.
    void SuspendPreZero();
    void ResumePreZero();
    bool IsPreZeroEnabled() const
    {
        return m_notes.zero_state != nullptr;
    }
    // Whether a pause holds the clearer back. For tests.
    bool IsPreZeroGated() const
    {
        return m_prezero_gate.load(std::memory_order_acquire);
    }
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    // Test only. The hook runs in AcquireBlock after a candidate has passed the unlocked checks and before the locked
    // acquire. A test uses it to run the work of another thread inside that window.
    using AfterPrecheckHook = void (*)(void* context, uintptr_t candidate);
    void SetAfterPrecheckHookForTest(AfterPrecheckHook hook, void* context)
    {
        m_after_precheck_hook = hook;
        m_after_precheck_context = context;
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM

private:
    friend class ImmixBumpAllocator;

    static bool IsCopySource(ImmixAllocationSource source);
    static bool IsFreshSource(ImmixAllocationSource source);
    static ImmixBlockAcquireKind GetAcquireKind(ImmixAllocationSource source);
    static ImmixAllocationStatus GetAllocationStatus(
        ImmixBlockOperationStatus status);

    SideMetadataResult IsLineDead(
        uintptr_t block,
        size_t lineIndex,
        bool* dead) const;
    // Dead lines of one block as one bit per line (bit i of mask[h] is line 64 * h + i), read from the count plane when a
    // half is first needed.
    struct DeadLineCache
    {
        uint64_t mask[2];
        bool loaded[2];
    };
    // Checks the arguments and the block as TryFindNextHole does, and gives the first word of the count plane of the block.
    SideMetadataResult LocateBlockForScan(
        uintptr_t block,
        ImmixHole* hole,
        size_t* nextSearchLine,
        ImmixAllocationStatus* status,
        const volatile uintptr_t** blockWords) const;
    uint64_t GetDeadMask(
        uintptr_t block,
        const volatile uintptr_t* blockWords,
        size_t half,
        DeadLineCache* cache) const;
    // One search of TryFindFittingHole over the lines that the cache covers.
    void FindHoleByMask(
        uintptr_t block,
        const volatile uintptr_t* blockWords,
        DeadLineCache* cache,
        size_t searchLine,
        size_t minimumLines,
        ImmixHole* hole,
        size_t* nextSearchLine,
        ImmixAllocationStatus* status,
        size_t* skipped) const;
    // The line scan of the Java LXR (RCArray::is_dead): the counts of one line are a few whole words of the count plane, and
    // the line is dead when all of them are zero. blockWords is the first word of the count plane of the block.
    bool IsLineDeadFast(const volatile uintptr_t* blockWords, size_t lineIndex) const
    {
#ifdef LXR_F9_TEST_HOOKS
        m_line_probes.fetch_add(1, std::memory_order_relaxed);
#endif // LXR_F9_TEST_HOOKS
        const volatile uintptr_t* words = blockWords + lineIndex * m_rc_words_per_line;
#if defined(LXR_F9_TEST_HOOKS) && defined(LXR_F9_SCAN_ONE_WORD_CONTROL)
        // Control: only the first word of the counts of a line is read.
        return VolatileLoadWithoutBarrier(words) == 0;
#else
        for (size_t word = 0; word < m_rc_words_per_line; word++)
        {
            if (VolatileLoadWithoutBarrier(words + word) != 0)
            {
                return false;
            }
        }
        return true;
#endif // LXR_F9_TEST_HOOKS && LXR_F9_SCAN_ONE_WORD_CONTROL
    }
    SideMetadataResult AcquireBlock(
        uintptr_t owner,
        ImmixAllocationSource source,
        uintptr_t* block,
        ImmixAllocationStatus* status,
        size_t minimumFreeLines = 0);
    SideMetadataResult ReturnBlock(
        uintptr_t block,
        uintptr_t owner,
        ImmixAllocationStatus* status);
    SideMetadataResult PrepareFreshBlock(
        uintptr_t block,
        ImmixAllocationSource source,
        ImmixMarkingContext markingContext);
    SideMetadataResult PrepareRecycledHole(
        const ImmixHole& hole,
        ImmixAllocationSource source,
        ImmixMarkingContext markingContext);
    SideMetadataResult InitializeFieldLogRange(
        uintptr_t start,
        size_t size,
        bool copy);
    SideMetadataResult InitializeObjectMarkRange(
        uintptr_t start,
        size_t size,
        uintptr_t value);
    SideMetadataResult IncrementLineReuseRange(
        uintptr_t start,
        size_t size);
    void NoteAcquired(size_t index, bool fresh, bool mutator);
    // Pre-zeroing (immix_zeroing.cpp). TakeBlockForUse waits for a claim of the clearer, marks the block as in use and tells
    // whether its memory is known to be zero.
    bool TakeBlockForUse(uintptr_t block);
    // Marks the zero state of the blocks [first, end) as clean: memory that is committed is zero.
    void InitializeZeroStates(size_t first, size_t end);
    bool TryPreZeroBlock(size_t index, uint8_t epochParity);
#if defined(_DEBUG) || defined(LXR_F9_TEST_HOOKS)
    // Counts (metric 9) the blocks that were taken as zero and were not.
    void VerifyBlockZero(uintptr_t block);
#endif // _DEBUG || LXR_F9_TEST_HOOKS

    SideMetadataManager* m_metadata;
    // Inline accessor for the reference-count plane (line scan).
    // The arena that this manager serves is always inside a data range that the metadata manager owns.
    SideMetadataAccessor m_reference_count;
    SideMetadataAccessor m_valid_object;
    SideMetadataAccessor m_field_unlogged;
    SideMetadataAccessor m_object_mark;
    SideMetadataAccessor m_line_reuse;
    // Words of the count plane for one line: 1, 2 or 4 for counts of 2, 4 or 8 bits.
    size_t m_rc_words_per_line = 0;
    ImmixBlockManager* m_blocks;
    uintptr_t m_arena_start;
    size_t m_arena_size;
    size_t m_block_count;
    size_t m_minimum_reuse_lines;
    // The cursors of the block search (the Java LXR: blockpageresource_nosweep.rs, clean_block_cursor and reuse_block_cursor).
    // A cursor only moves forward through the arena, and the search for a block stops at the end. The cursors go back to 0
    // at the start and at the end of every pause, and when the arena changes size (ResetCursorsQuiescent).
    std::atomic<size_t> m_next_fresh_block;
    std::atomic<size_t> m_next_recycled_block;
    std::atomic<size_t> m_next_pinned_block{0};
    bool m_placement = false;
    size_t m_mutator_fresh_blocks = 0;
    std::atomic<size_t> m_next_copy_block{0};
    AcquireNotes m_notes{};
    std::atomic<size_t> m_fresh_blocks_acquired;
    std::atomic<size_t> m_recycled_blocks_acquired;
    std::atomic<size_t> m_recycled_lines_consumed;
    std::atomic<size_t> m_holes_skipped{0};
    std::atomic<size_t> m_blocks_in_use{0};
    std::atomic<uint64_t> m_zero_stats[10]{};
    // Pre-zeroing. The gate keeps the clearer out of the arena during a pause; the clearer sets m_prezero_inside while it
    // looks at blocks. The frontier and the last cursor belong to the clearer thread.
    std::atomic<bool> m_prezero_gate{false};
    std::atomic<bool> m_prezero_inside{false};
    size_t m_prezero_frontier = 0;
    std::atomic<uint32_t> m_prezero_resumes{0};
    uint32_t m_prezero_seen_resumes = 0;
    std::atomic<size_t> m_prezero_wake_index{SIZE_MAX};
    PreZeroWakeCallback m_prezero_wake = nullptr;
    void* m_prezero_wake_context = nullptr;
#ifdef LXR_F9_TEST_HOOKS
    mutable std::atomic<uint64_t> m_line_probes{0};
#endif // LXR_F9_TEST_HOOKS
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    AfterPrecheckHook m_after_precheck_hook = nullptr;
    void* m_after_precheck_context = nullptr;
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
};

class ImmixBumpAllocator final
{
public:
    ImmixBumpAllocator();
    ImmixBumpAllocator(const ImmixBumpAllocator&) = delete;
    ImmixBumpAllocator& operator=(const ImmixBumpAllocator&) = delete;

    SideMetadataResult Initialize(
        ImmixAllocationManager* manager,
        uintptr_t owner);
    // minimumHoleBytes: a recycled refill passes over holes smaller than this without preparing them (no zeroing, no
    // count of consumed lines), so a request that does not fit them does not burn them. Zero takes every hole.
    SideMetadataResult TryRefill(
        ImmixAllocationSource source,
        ImmixMarkingContext markingContext,
        ImmixAllocationStatus* status,
        size_t minimumHoleBytes = 0);
    SideMetadataResult TryAllocate(
        const ImmixAllocationRequest& request,
        uintptr_t* address,
        ImmixAllocationStatus* status);
    SideMetadataResult Reset(ImmixAllocationStatus* status);

    uintptr_t GetBlock() const;
    // Whether the block that the allocator holds was taken as a clean block.
    bool HoldsFreshBlock() const;
    uintptr_t GetCursor() const;
    uintptr_t GetLimit() const;
    SideMetadataResult SetCursor(uintptr_t cursor);

private:
    static bool TryAlignWithOffset(
        uintptr_t cursor,
        size_t alignment,
        size_t alignmentOffset,
        uintptr_t* aligned);
    SideMetadataResult FindHole(
        uintptr_t block,
        size_t searchLine,
        size_t minimumHoleBytes,
        ImmixHole* hole,
        ImmixAllocationStatus* status);
    SideMetadataResult ReleaseAfterFailure(
        SideMetadataResult failure,
        ImmixAllocationStatus* status);
    SideMetadataResult ReturnCurrentBlock(ImmixAllocationStatus* status);

    ImmixAllocationManager* m_manager;
    uintptr_t m_owner;
    uintptr_t m_block;
    uintptr_t m_cursor;
    uintptr_t m_limit;
    size_t m_next_search_line;
    ImmixAllocationSource m_source;
};

#endif // IMMIX_ALLOCATION_H
