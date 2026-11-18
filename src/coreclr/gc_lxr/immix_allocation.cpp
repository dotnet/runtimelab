// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"
#include "gcenv.h"
#include "immix_allocation.h"

namespace
{
    constexpr SideMetadataMemoryOrder RcReadOrder =
        SideMetadataMemoryOrder::Relaxed;

    bool TryAdd(uintptr_t start, size_t size, uintptr_t* end)
    {
        if ((end == nullptr) || (size > (UINTPTR_MAX - start)))
        {
            return false;
        }

        *end = start + size;
        return true;
    }

    // A relaxed load of one word of the count plane. The compiler may schedule these loads freely, which a volatile read
    // does not allow.
    inline uintptr_t LoadCountWord(const volatile uintptr_t* word)
    {
#if defined(__GNUC__) || defined(__clang__)
        return __atomic_load_n(const_cast<const uintptr_t*>(word), __ATOMIC_RELAXED);
#else
        return VolatileLoadWithoutBarrier(word);
#endif
    }

    // The index of the lowest set bit. The value is not zero.
    inline size_t LowestSetBit(uint64_t value)
    {
#if defined(_MSC_VER) && !defined(__clang__)
        unsigned long index;
        _BitScanForward64(&index, value);
        return static_cast<size_t>(index);
#else
        return static_cast<size_t>(__builtin_ctzll(value));
#endif
    }
}

ImmixAllocationManager::ImmixAllocationManager()
    : m_metadata(nullptr)
    , m_blocks(nullptr)
    , m_arena_start(0)
    , m_arena_size(0)
    , m_block_count(0)
    , m_minimum_reuse_lines(0)
    , m_next_fresh_block(0)
    , m_next_recycled_block(0)
    , m_fresh_blocks_acquired(0)
    , m_recycled_blocks_acquired(0)
    , m_recycled_lines_consumed(0)
{
}

SideMetadataResult ImmixAllocationManager::Initialize(
    SideMetadataManager* metadata,
    ImmixBlockManager* blocks,
    uintptr_t arenaStart,
    size_t arenaSize,
    size_t minimumReuseLines)
{
    if ((metadata == nullptr) ||
        (blocks == nullptr) ||
        (m_metadata != nullptr) ||
        !ImmixBlockGeometry::IsBlockAligned(arenaStart) ||
        (arenaSize == 0) ||
        ((arenaSize & ImmixBlockGeometry::BlockMask) != 0) ||
        (minimumReuseLines == 0) ||
        (minimumReuseLines > ImmixBlockGeometry::LinesPerBlock))
    {
        return SideMetadataResult::InvalidArgument;
    }

    uintptr_t arenaEnd;
    if (!TryAdd(arenaStart, arenaSize, &arenaEnd) ||
        (static_cast<uint64_t>(arenaEnd) > ImmixBlockGeometry::AddressLimit))
    {
        return SideMetadataResult::AddressOverflow;
    }

    constexpr LxrSideMetadataKind RequiredKinds[] = {
        LxrSideMetadataKind::ReferenceCount,
        LxrSideMetadataKind::FieldUnlogged,
        LxrSideMetadataKind::ObjectMark,
        LxrSideMetadataKind::LineReuse,
    };
    for (LxrSideMetadataKind kind : RequiredKinds)
    {
        if (!metadata->IsSpecEnabled(kind))
        {
            return SideMetadataResult::InvalidArgument;
        }
    }
    if (!metadata->IsDataRangeOwned(arenaStart, arenaSize))
    {
        return SideMetadataResult::AddressNotOwned;
    }

    m_metadata = metadata;
    m_reference_count = metadata->GetAccessor(LxrSideMetadataKind::ReferenceCount);
#ifndef LXR_OBJECT_START_WALK
    m_valid_object = metadata->GetAccessor(LxrSideMetadataKind::ValidObject);
#endif // !LXR_OBJECT_START_WALK
    m_field_unlogged = metadata->GetAccessor(LxrSideMetadataKind::FieldUnlogged);
    m_object_mark = metadata->GetAccessor(LxrSideMetadataKind::ObjectMark);
    m_line_reuse = metadata->GetAccessor(LxrSideMetadataKind::LineReuse);
    if (!m_reference_count.IsValid() ||
        (m_reference_count.GetLogBytesPerValue() != 3) ||
        (m_reference_count.GetLogBitsPerValue() < 1) ||
        (m_reference_count.GetLogBitsPerValue() > 3))
    {
        m_metadata = nullptr;
        return SideMetadataResult::InvalidLayout;
    }
    m_rc_words_per_line = static_cast<size_t>(1) << (m_reference_count.GetLogBitsPerValue() - 1);
    m_blocks = blocks;
    m_arena_start = arenaStart;
    m_arena_size = arenaSize;
    m_block_count = arenaSize / ImmixBlockGeometry::BlockBytes;
    m_minimum_reuse_lines = minimumReuseLines;
    ResetCursors();
    m_fresh_blocks_acquired.store(0, std::memory_order_relaxed);
    m_recycled_blocks_acquired.store(0, std::memory_order_relaxed);
    m_recycled_lines_consumed.store(0, std::memory_order_relaxed);
    m_blocks_in_use.store(0, std::memory_order_relaxed);
    return SideMetadataResult::Success;
}

void ImmixAllocationManager::Shutdown()
{
    m_metadata = nullptr;
    m_reference_count = SideMetadataAccessor();
    m_valid_object = SideMetadataAccessor();
    m_field_unlogged = SideMetadataAccessor();
    m_object_mark = SideMetadataAccessor();
    m_line_reuse = SideMetadataAccessor();
    m_rc_words_per_line = 0;
    m_arena_start = 0;
    m_arena_size = 0;
    m_block_count = 0;
    m_minimum_reuse_lines = 0;
    ResetCursors();
    m_fresh_blocks_acquired.store(0, std::memory_order_relaxed);
    m_recycled_blocks_acquired.store(0, std::memory_order_relaxed);
    m_recycled_lines_consumed.store(0, std::memory_order_relaxed);
    m_blocks_in_use.store(0, std::memory_order_relaxed);
    m_notes = AcquireNotes{};
    m_placement = false;
    m_mutator_fresh_blocks = 0;
}

SideMetadataResult ImmixAllocationManager::GrowQuiescent(size_t arenaSize)
{
    if (m_metadata == nullptr || arenaSize <= m_arena_size ||
        arenaSize % ImmixBlockGeometry::BlockBytes != 0 ||
        !m_metadata->IsDataRangeOwned(m_arena_start, arenaSize))
    {
        return SideMetadataResult::InvalidArgument;
    }
    size_t previousBlocks = m_block_count;
    m_arena_size = arenaSize;
    m_block_count = arenaSize / ImmixBlockGeometry::BlockBytes;
    ResetCursors();
    InitializeZeroStates(previousBlocks, m_block_count);
    return SideMetadataResult::Success;
}

SideMetadataResult ImmixAllocationManager::ShrinkQuiescent(size_t arenaSize)
{
    if (m_metadata == nullptr || arenaSize == 0 || arenaSize >= m_arena_size ||
        arenaSize % ImmixBlockGeometry::BlockBytes != 0)
    {
        return SideMetadataResult::InvalidArgument;
    }
    m_arena_size = arenaSize;
    m_block_count = arenaSize / ImmixBlockGeometry::BlockBytes;
    ResetCursors();
    return SideMetadataResult::Success;
}

void ImmixAllocationManager::EnablePlacementQuiescent(size_t mutatorFreshBlocks)
{
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_S5B_NO_PLACEMENT_CONTROL)
    m_placement = true;
    m_mutator_fresh_blocks = mutatorFreshBlocks;
    m_next_copy_block.store(0, std::memory_order_relaxed);
#else
    (void)mutatorFreshBlocks;
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_S5B_NO_PLACEMENT_CONTROL
}

void ImmixAllocationManager::SetMutatorFreshBlocksQuiescent(size_t mutatorFreshBlocks)
{
    m_mutator_fresh_blocks = mutatorFreshBlocks;
}

void ImmixAllocationManager::ResetCopyCursorQuiescent()
{
    m_next_copy_block.store(0, std::memory_order_relaxed);
}

void ImmixAllocationManager::ResetCursors()
{
#if defined(LXR_F9_TEST_HOOKS) && defined(LXR_F9_NO_CURSOR_RESET_CONTROL)
    // Control: the cursors are never set back.
    return;
#endif // LXR_F9_TEST_HOOKS && LXR_F9_NO_CURSOR_RESET_CONTROL
    m_next_fresh_block.store(0, std::memory_order_relaxed);
    m_next_recycled_block.store(0, std::memory_order_relaxed);
    m_next_pinned_block.store(0, std::memory_order_relaxed);
    m_next_copy_block.store(0, std::memory_order_relaxed);
}

SideMetadataResult ImmixAllocationManager::TryFindNextHole(
    uintptr_t block,
    size_t searchLine,
    ImmixHole* hole,
    size_t* nextSearchLine,
    ImmixAllocationStatus* status) const
{
    if ((hole == nullptr) ||
        (nextSearchLine == nullptr) ||
        (status == nullptr) ||
        (m_metadata == nullptr) ||
        (searchLine > ImmixBlockGeometry::LinesPerBlock))
    {
        return SideMetadataResult::InvalidArgument;
    }

    hole->start = block;
    hole->end = block;
    hole->line_count = 0;
    *nextSearchLine = searchLine;
    *status = ImmixAllocationStatus::Exhausted;
    if (!ImmixBlockGeometry::IsBlockAligned(block) ||
        (block < m_arena_start) ||
        (block >= (m_arena_start + m_arena_size)))
    {
        return SideMetadataResult::AddressNotOwned;
    }

    // The count plane of the block is read in place, one line at a time, as the Java LXR does (RCArray::is_dead): the
    // first word of the plane is found once, and a line is a fixed number of words from it.
    const volatile uintptr_t* blockWords = m_reference_count.Locate(block).word;
    auto isDead = [&](size_t lineIndex) -> bool
    {
        bool dead = IsLineDeadFast(blockWords, lineIndex);
#ifdef _DEBUG
        for (int attempt = 0; attempt < 16; attempt++)
        {
            bool checkedDead = false;
            if (IsLineDead(block, lineIndex, &checkedDead) != SideMetadataResult::Success || checkedDead == dead)
            {
                return dead;
            }
            // Another thread may have changed a count between the two reads; look again.
            dead = IsLineDeadFast(blockWords, lineIndex);
        }
        _ASSERTE(!"The line scan of the count plane and the checked scan disagree.");
#endif // _DEBUG
        return dead;
    };

#if defined(LXR_F9_TEST_HOOKS) && defined(LXR_F9_EAGER_SCAN_CONTROL)
    // Control: every line of the block is read before the first hole is returned.
    for (size_t eager = 0; eager < ImmixBlockGeometry::LinesPerBlock; eager++)
    {
        (void)isDead(eager);
    }
#endif // LXR_F9_TEST_HOOKS && LXR_F9_EAGER_SCAN_CONTROL

    size_t line = searchLine;
    while (line < ImmixBlockGeometry::LinesPerBlock)
    {
        bool previousDead = false;
        size_t firstReusableLine = ImmixBlockGeometry::LinesPerBlock;
        while (line < ImmixBlockGeometry::LinesPerBlock)
        {
            bool dead = isDead(line);
            if (dead && ((line == 0) || previousDead))
            {
                firstReusableLine = line;
                break;
            }

            previousDead = dead;
            line++;
        }
        if (firstReusableLine == ImmixBlockGeometry::LinesPerBlock)
        {
            break;
        }

        line = firstReusableLine + 1;
        while (line < ImmixBlockGeometry::LinesPerBlock)
        {
            if (!isDead(line))
            {
                break;
            }
            line++;
        }

        size_t reusableLines = line - firstReusableLine;
        *nextSearchLine = line;
        if (reusableLines >= m_minimum_reuse_lines)
        {
            hole->start =
                block + (firstReusableLine * ImmixBlockGeometry::LineBytes);
            hole->end = block + (line * ImmixBlockGeometry::LineBytes);
            hole->line_count = reusableLines;
            *status = ImmixAllocationStatus::Ready;
            return SideMetadataResult::Success;
        }
    }

    *nextSearchLine = ImmixBlockGeometry::LinesPerBlock;
    return SideMetadataResult::Success;
}

SideMetadataResult ImmixAllocationManager::LocateBlockForScan(
    uintptr_t block,
    ImmixHole* hole,
    size_t* nextSearchLine,
    ImmixAllocationStatus* status,
    const volatile uintptr_t** blockWords) const
{
    if ((hole == nullptr) ||
        (nextSearchLine == nullptr) ||
        (status == nullptr) ||
        (m_metadata == nullptr))
    {
        return SideMetadataResult::InvalidArgument;
    }

    hole->start = block;
    hole->end = block;
    hole->line_count = 0;
    *status = ImmixAllocationStatus::Exhausted;
    if (!ImmixBlockGeometry::IsBlockAligned(block) ||
        (block < m_arena_start) ||
        (block >= (m_arena_start + m_arena_size)))
    {
        return SideMetadataResult::AddressNotOwned;
    }

    *blockWords = m_reference_count.Locate(block).word;
    return SideMetadataResult::Success;
}

uint64_t ImmixAllocationManager::GetDeadMask(
    uintptr_t block,
    const volatile uintptr_t* blockWords,
    size_t half,
    DeadLineCache* cache) const
{
    if (cache->loaded[half])
    {
        return cache->mask[half];
    }

    // A line is dead when all of its count words are zero (the same rule as IsLineDeadFast). The loop has no branch that
    // depends on the counts, and each word is read once, with a relaxed load.
    const volatile uintptr_t* words = blockWords + (half * 64 * m_rc_words_per_line);
    uint64_t mask = 0;
    if (m_rc_words_per_line == 1)
    {
        for (size_t group = 0; group < 8; group++)
        {
            uint64_t bits = 0;
            for (size_t line = 0; line < 8; line++)
            {
                bits |= static_cast<uint64_t>(LoadCountWord(words + (group * 8) + line) == 0) << line;
            }
            mask |= bits << (group * 8);
        }
    }
    else
    {
        for (size_t line = 0; line < 64; line++)
        {
            uintptr_t any = 0;
            for (size_t word = 0; word < m_rc_words_per_line; word++)
            {
                any |= LoadCountWord(words + (line * m_rc_words_per_line) + word);
            }
            mask |= static_cast<uint64_t>(any == 0) << line;
        }
    }
#ifdef LXR_F9_TEST_HOOKS
    m_line_probes.fetch_add(64, std::memory_order_relaxed);
#endif // LXR_F9_TEST_HOOKS
#ifdef _DEBUG
    for (size_t line = 0; line < 64; line++)
    {
        size_t lineIndex = (half * 64) + line;
        bool dead = ((mask >> line) & 1) != 0;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            bool checkedDead = false;
            if (IsLineDead(block, lineIndex, &checkedDead) != SideMetadataResult::Success || checkedDead == dead)
            {
                break;
            }
            // Another thread may have changed a count between the two reads; look again.
            dead = IsLineDeadFast(blockWords, lineIndex);
            _ASSERTE(attempt != 15 && "The line mask of the count plane and the checked scan disagree.");
        }
        mask = (mask & ~(static_cast<uint64_t>(1) << line)) | (static_cast<uint64_t>(dead) << line);
    }
#else
    (void)block;
#endif // _DEBUG
    cache->mask[half] = mask;
    cache->loaded[half] = true;
    return mask;
}

void ImmixAllocationManager::FindHoleByMask(
    uintptr_t block,
    const volatile uintptr_t* blockWords,
    DeadLineCache* cache,
    size_t searchLine,
    size_t minimumLines,
    ImmixHole* hole,
    size_t* nextSearchLine,
    ImmixAllocationStatus* status,
    size_t* skipped) const
{
    static_assert(ImmixBlockGeometry::LinesPerBlock == 128, "The line masks have two halves.");
    // The first line at or after `from` that is dead (wantDead) or live, or the end of the block.
    auto nextLine = [&](size_t from, bool wantDead) -> size_t
    {
        for (size_t half = from >> 6; half < 2; half++)
        {
            uint64_t mask = GetDeadMask(block, blockWords, half, cache);
            if (!wantDead)
            {
                mask = ~mask;
            }
            if (half == (from >> 6))
            {
                mask &= ~static_cast<uint64_t>(0) << (from & 63);
            }
            if (mask != 0)
            {
                return (half * 64) + LowestSetBit(mask);
            }
        }
        return ImmixBlockGeometry::LinesPerBlock;
    };

    size_t position = searchLine;
    while (position < ImmixBlockGeometry::LinesPerBlock)
    {
        size_t runStart = nextLine(position, true);
        if (runStart >= ImmixBlockGeometry::LinesPerBlock)
        {
            break;
        }
        size_t runEnd = nextLine(runStart, false);
        position = runEnd;
        // The first dead line after a live line is not reusable (an object of the live line may overflow into it), except
        // line 0. The line at the search line has no known predecessor and counts as such a line, as in TryFindNextHole.
        size_t first = (runStart == 0) ? 0 : runStart + 1;
        if (first >= runEnd)
        {
            continue;
        }

        size_t reusableLines = runEnd - first;
        *nextSearchLine = runEnd;
        if (reusableLines >= m_minimum_reuse_lines)
        {
            if (reusableLines >= minimumLines)
            {
                hole->start = block + (first * ImmixBlockGeometry::LineBytes);
                hole->end = block + (runEnd * ImmixBlockGeometry::LineBytes);
                hole->line_count = reusableLines;
                *status = ImmixAllocationStatus::Ready;
                return;
            }
            (*skipped)++;
        }
    }

    *nextSearchLine = ImmixBlockGeometry::LinesPerBlock;
}

SideMetadataResult ImmixAllocationManager::TryFindFittingHole(
    uintptr_t block,
    size_t searchLine,
    size_t minimumLines,
    ImmixHole* hole,
    size_t* nextSearchLine,
    ImmixAllocationStatus* status,
    size_t* skipped) const
{
    if ((nextSearchLine == nullptr) ||
        (skipped == nullptr) ||
        (searchLine > ImmixBlockGeometry::LinesPerBlock))
    {
        return SideMetadataResult::InvalidArgument;
    }

    *nextSearchLine = searchLine;
    *skipped = 0;
    const volatile uintptr_t* blockWords = nullptr;
    SideMetadataResult result = LocateBlockForScan(block, hole, nextSearchLine, status, &blockWords);
    if (result != SideMetadataResult::Success)
    {
        return result;
    }

    DeadLineCache cache = {};
    FindHoleByMask(block, blockWords, &cache, searchLine, minimumLines, hole, nextSearchLine, status, skipped);
    return SideMetadataResult::Success;
}

SideMetadataResult ImmixAllocationManager::CountHoleLines(uintptr_t block, size_t* lines) const
{
    if (lines == nullptr)
    {
        return SideMetadataResult::InvalidArgument;
    }

    ImmixHole hole;
    size_t next = 0;
    ImmixAllocationStatus status;
    const volatile uintptr_t* blockWords = nullptr;
    *lines = 0;
    SideMetadataResult result = LocateBlockForScan(block, &hole, &next, &status, &blockWords);
    if (result != SideMetadataResult::Success)
    {
        return result;
    }

    DeadLineCache cache = {};
    size_t skipped = 0;
    size_t total = 0;
    while (next < ImmixBlockGeometry::LinesPerBlock)
    {
        status = ImmixAllocationStatus::Exhausted;
        FindHoleByMask(block, blockWords, &cache, next, 0, &hole, &next, &status, &skipped);
        if (status == ImmixAllocationStatus::Ready)
        {
            total += hole.line_count;
        }
    }
    *lines = total;
    return SideMetadataResult::Success;
}

size_t ImmixAllocationManager::GetFreshBlocksAcquired() const
{
    return m_fresh_blocks_acquired.load(std::memory_order_relaxed);
}

size_t ImmixAllocationManager::GetRecycledBlocksAcquired() const
{
    return m_recycled_blocks_acquired.load(std::memory_order_relaxed);
}

size_t ImmixAllocationManager::GetRecycledLinesConsumed() const
{
    return m_recycled_lines_consumed.load(std::memory_order_relaxed);
}

size_t ImmixAllocationManager::GetHolesSkipped() const
{
    return m_holes_skipped.load(std::memory_order_relaxed);
}

size_t ImmixAllocationManager::GetBlocksInUse() const
{
    return m_blocks_in_use.load(std::memory_order_acquire);
}

SideMetadataResult ImmixAllocationManager::NotifyBlockReleased()
{
    size_t current = m_blocks_in_use.load(std::memory_order_relaxed);
    while (current != 0)
    {
        if (m_blocks_in_use.compare_exchange_weak(current, current - 1, std::memory_order_acq_rel))
        {
            return SideMetadataResult::Success;
        }
    }
    return SideMetadataResult::InconsistentState;
}

SideMetadataResult ImmixAllocationManager::NotifyBlocksReleased(size_t count)
{
    size_t current = m_blocks_in_use.load(std::memory_order_relaxed);
    while (current >= count)
    {
        if (m_blocks_in_use.compare_exchange_weak(current, current - count, std::memory_order_acq_rel))
        {
            return SideMetadataResult::Success;
        }
    }
    return SideMetadataResult::InconsistentState;
}

void ImmixAllocationManager::SetAcquireNotes(const AcquireNotes& notes)
{
    m_notes = notes;
    InitializeZeroStates(0, m_block_count);
}

void ImmixAllocationManager::NoteAcquired(size_t index, bool fresh, bool mutator)
{
    if (m_notes.refresh_flags == nullptr)
    {
        return;
    }
    if (mutator)
    {
        // The flag makes the entry unique, so the list never needs more entries than there are blocks.
        if (m_notes.allocated_flags[index].exchange(1, std::memory_order_acq_rel) == 0)
        {
            m_notes.allocated_list[m_notes.allocated_count->fetch_add(1, std::memory_order_acq_rel)] =
                static_cast<uint32_t>(index);
        }
    }
    if (!(fresh && mutator))
    {
        m_notes.refresh_flags[index].store(1, std::memory_order_release);
    }
}

bool ImmixAllocationManager::IsCopySource(ImmixAllocationSource source)
{
    return (source == ImmixAllocationSource::GcCopyFresh) ||
        (source == ImmixAllocationSource::GcCopyRecycled);
}

bool ImmixAllocationManager::IsFreshSource(ImmixAllocationSource source)
{
    return (source == ImmixAllocationSource::MutatorFresh) ||
        (source == ImmixAllocationSource::MutatorPinnedFresh) ||
        (source == ImmixAllocationSource::GcCopyFresh);
}

ImmixBlockAcquireKind ImmixAllocationManager::GetAcquireKind(
    ImmixAllocationSource source)
{
    switch (source)
    {
        case ImmixAllocationSource::MutatorFresh:
            return ImmixBlockAcquireKind::MutatorFresh;
        case ImmixAllocationSource::MutatorRecycled:
        case ImmixAllocationSource::MutatorPinnedRecycled:
            return ImmixBlockAcquireKind::MutatorReusable;
        case ImmixAllocationSource::GcCopyFresh:
            return ImmixBlockAcquireKind::GcCopyFresh;
        case ImmixAllocationSource::GcCopyRecycled:
            return ImmixBlockAcquireKind::GcCopyReusable;
        default:
            return ImmixBlockAcquireKind::MutatorFresh;
    }
}

ImmixAllocationStatus ImmixAllocationManager::GetAllocationStatus(
    ImmixBlockOperationStatus status)
{
    switch (status)
    {
        case ImmixBlockOperationStatus::Updated:
        case ImmixBlockOperationStatus::Unchanged:
            return ImmixAllocationStatus::Ready;
        case ImmixBlockOperationStatus::Contended:
            return ImmixAllocationStatus::Contended;
        case ImmixBlockOperationStatus::Stale:
            return ImmixAllocationStatus::Stale;
        case ImmixBlockOperationStatus::InvalidTransition:
            return ImmixAllocationStatus::InvalidPhase;
        default:
            return ImmixAllocationStatus::InvalidRequest;
    }
}

SideMetadataResult ImmixAllocationManager::IsLineDead(
    uintptr_t block,
    size_t lineIndex,
    bool* dead) const
{
    if ((dead == nullptr) ||
        (lineIndex >= ImmixBlockGeometry::LinesPerBlock))
    {
        return SideMetadataResult::InvalidArgument;
    }

    // TryFindNextHole has checked that the block lies in the arena, and the arena is an owned data range.
    uintptr_t line = block + (lineIndex * ImmixBlockGeometry::LineBytes);
    *dead = m_reference_count.IsRangeZero(line, ImmixBlockGeometry::LineBytes, RcReadOrder);
    return SideMetadataResult::Success;
}

SideMetadataResult ImmixAllocationManager::AcquireBlock(
    uintptr_t owner,
    ImmixAllocationSource source,
    uintptr_t* block,
    ImmixAllocationStatus* status,
    size_t minimumFreeLines)
{
    if ((block == nullptr) ||
        (status == nullptr) ||
        (owner == ImmixBlockManager::NoOwner) ||
        (source > ImmixAllocationSource::MutatorPinnedRecycled))
    {
        return SideMetadataResult::InvalidArgument;
    }

    *block = 0;
    *status = ImmixAllocationStatus::Exhausted;
    bool fresh = IsFreshSource(source);
    bool pinnedSource =
        (source == ImmixAllocationSource::MutatorPinnedFresh) ||
        (source == ImmixAllocationSource::MutatorPinnedRecycled);
    bool mutator =
        (source == ImmixAllocationSource::MutatorFresh) ||
        (source == ImmixAllocationSource::MutatorRecycled) ||
        pinnedSource;
    uint8_t globalEpoch = m_blocks->GetGlobalPhaseEpoch();
    if (mutator ? ((globalEpoch & 1) == 0) : ((globalEpoch & 1) != 0))
    {
        *status = ImmixAllocationStatus::InvalidPhase;
        return SideMetadataResult::Success;
    }

    // P28 (F9). The search for a block is the one of the Java LXR (blockpageresource_nosweep.rs: acquire_clean_blocks_fast,
    // acquire_reusable_blocks_fast). A cursor moves forward only; a caller reads the state of the blocks from the cursor with
    // plain loads and claims the range that it has looked at with one compare-exchange of the cursor. When the cursor has
    // reached the end there is no block for this kind of request until the cursors are reset (at the pause, or when the arena
    // changes). A recycled block is any block that is not free (the Java LXR scans the counts of its lines when it takes it).
    bool placed = fresh && m_placement;
    size_t limit = placed && mutator && m_mutator_fresh_blocks < m_block_count ? m_mutator_fresh_blocks : m_block_count;
    if (limit > m_block_count)
    {
        limit = m_block_count;
    }
    std::atomic<size_t>& cursor = fresh
        ? ((placed && !mutator) ? m_next_copy_block : m_next_fresh_block)
        : (pinnedSource ? m_next_pinned_block : m_next_recycled_block);
    SideMetadataResult failure = SideMetadataResult::Success;
    auto isCandidate = [&](size_t index) -> bool
    {
        uintptr_t address = m_arena_start + (index * ImmixBlockGeometry::BlockBytes);
        ImmixBlockState state;
        uint8_t unavailableLines;
        failure = m_blocks->GetStateFast(address, &state, &unavailableLines);
        if (failure != SideMetadataResult::Success)
        {
            return true;
        }
        if (fresh)
        {
            if (state != ImmixBlockState::Unallocated)
            {
                return false;
            }
            bool nursery;
            failure = m_blocks->IsNurseryFast(address, &nursery);
            // Block state remains Unallocated while a fresh block is a current-epoch nursery; the cursor must not make it
            // available again merely because its owner was returned.
            return failure != SideMetadataResult::Success || !nursery;
        }
        if (state == ImmixBlockState::Unallocated)
        {
            return false;
        }
        // The free lines of a Reusable block were counted when it was made Reusable, and lines only become free after that,
        // so a block with fewer free lines than the pending request needs has no hole that fits it.
        if (minimumFreeLines != 0 && state == ImmixBlockState::Reusable &&
            ImmixBlockGeometry::LinesPerBlock - static_cast<size_t>(unavailableLines) < minimumFreeLines &&
            unavailableLines <= ImmixBlockGeometry::LinesPerBlock)
        {
            return false;
        }
#if defined(LXR_F9_TEST_HOOKS) && defined(LXR_F9_PREFILTER_CONTROL)
        // Control: only a Reusable block with enough free lines is a candidate (the rule before F9).
        if (state != ImmixBlockState::Reusable || unavailableLines > ImmixBlockGeometry::LinesPerBlock ||
            ImmixBlockGeometry::LinesPerBlock - static_cast<size_t>(unavailableLines) < m_minimum_reuse_lines)
        {
            return false;
        }
#endif // LXR_F9_TEST_HOOKS && LXR_F9_PREFILTER_CONTROL
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_S7_POH_RECYCLE_CONTROL)
        // A pinned block holds pinned-object-heap objects only: pinned recycling takes these blocks and no other
        // recycling does.
        bool pinnedBlock = m_notes.pinned_flags != nullptr &&
            (m_notes.pinned_flags[index].load(std::memory_order_acquire) & PinnedBlockFlag) != 0;
        if (pinnedBlock != pinnedSource)
        {
            return false;
        }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !LXR_S7_POH_RECYCLE_CONTROL
        // A block that an allocator took in this epoch has young objects without counts.
        if (m_blocks->IsReusingFast(address))
        {
            return false;
        }
        if (minimumFreeLines > 1 && minimumFreeLines > m_minimum_reuse_lines)
        {
            // A request of more than one line needs a hole of that length. Taking a block, searching it and giving it back
            // changes the metadata of the block and costs more than the search, and most blocks have no such hole, so the
            // block is looked at first. The search that follows the acquire reads the same lines again.
            const volatile uintptr_t* blockWords = m_reference_count.Locate(address).word;
            DeadLineCache cache = {};
            ImmixHole fit = {};
            size_t fitNext = 0;
            size_t fitSkipped = 0;
            ImmixAllocationStatus fitStatus = ImmixAllocationStatus::Exhausted;
            FindHoleByMask(address, blockWords, &cache, 0, minimumFreeLines, &fit, &fitNext, &fitStatus, &fitSkipped);
            if (fitStatus != ImmixAllocationStatus::Ready)
            {
                // The holes that were passed over are counted as a refill that searched the block would count them.
                m_holes_skipped.fetch_add(fitSkipped, std::memory_order_relaxed);
                return false;
            }
            return true;
        }
        return true;
    };

    while (true)
    {
        size_t first = cursor.load(std::memory_order_relaxed);
        if (first >= limit)
        {
            return SideMetadataResult::Success;
        }
        size_t next = first;
        size_t index = SIZE_MAX;
        while (next < limit)
        {
            size_t examined = next++;
            if (isCandidate(examined))
            {
                index = examined;
                break;
            }
        }
        if (failure != SideMetadataResult::Success)
        {
            return failure;
        }
        if (!cursor.compare_exchange_weak(first, next, std::memory_order_relaxed, std::memory_order_relaxed))
        {
            continue;
        }
        if (index == SIZE_MAX)
        {
            return SideMetadataResult::Success;
        }

        uintptr_t candidate = m_arena_start + (index * ImmixBlockGeometry::BlockBytes);
        ImmixBlockOperationStatus blockStatus;
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        if (m_after_precheck_hook != nullptr)
        {
            m_after_precheck_hook(m_after_precheck_context, candidate);
        }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM
        SideMetadataResult result = m_blocks->TryAcquire(
            candidate,
            owner,
            GetAcquireKind(source),
            &blockStatus);
        if (result != SideMetadataResult::Success)
        {
            return result;
        }

        ImmixAllocationStatus allocationStatus =
            GetAllocationStatus(blockStatus);
        if (allocationStatus == ImmixAllocationStatus::Ready)
        {
            *block = candidate;
            *status = allocationStatus;
            if (fresh)
            {
                m_blocks_in_use.fetch_add(1, std::memory_order_release);
                m_fresh_blocks_acquired.fetch_add(
                    1,
                    std::memory_order_relaxed);
            }
            else
            {
                m_recycled_blocks_acquired.fetch_add(
                    1,
                    std::memory_order_relaxed);
            }
            NoteAcquired(index, fresh, mutator);
            if (m_notes.pinned_flags != nullptr)
            {
                if (pinnedSource)
                {
                    m_notes.pinned_flags[index].fetch_or(PinnedBlockFlag, std::memory_order_acq_rel);
                }
                else
                {
                    m_notes.pinned_flags[index].fetch_and(
                        static_cast<uint8_t>(~PinnedBlockFlag), std::memory_order_acq_rel);
                }
            }
            return SideMetadataResult::Success;
        }
        if (allocationStatus == ImmixAllocationStatus::InvalidPhase)
        {
            uint8_t currentEpoch = m_blocks->GetGlobalPhaseEpoch();
            bool validPhase =
                mutator ? ((currentEpoch & 1) != 0) : ((currentEpoch & 1) == 0);
            if (!validPhase)
            {
                *status = allocationStatus;
                return SideMetadataResult::Success;
            }
        }
    }
}

SideMetadataResult ImmixAllocationManager::ReturnBlock(
    uintptr_t block,
    uintptr_t owner,
    ImmixAllocationStatus* status)
{
    if (status == nullptr)
    {
        return SideMetadataResult::InvalidArgument;
    }

    ImmixBlockOperationStatus blockStatus;
    SideMetadataResult result =
        m_blocks->TryReturn(block, owner, &blockStatus);
    if (result == SideMetadataResult::Success)
    {
        *status = GetAllocationStatus(blockStatus);
    }
    return result;
}

SideMetadataResult ImmixAllocationManager::PrepareFreshBlock(
    uintptr_t block,
    ImmixAllocationSource source,
    ImmixMarkingContext markingContext)
{
    // A clean block that the background clearer has cleared (or that was never used) is zero already.
    bool alreadyZero = m_notes.zero_state != nullptr && TakeBlockForUse(block);
    if (!IsCopySource(source))
    {
        if (alreadyZero)
        {
            m_zero_stats[3].fetch_add(1, std::memory_order_relaxed);
#if defined(_DEBUG) || defined(LXR_F9_TEST_HOOKS)
            VerifyBlockZero(block);
#endif // _DEBUG || LXR_F9_TEST_HOOKS
        }
        else
        {
            memset(
                reinterpret_cast<void*>(block),
                0,
                ImmixBlockGeometry::BlockBytes);
            m_zero_stats[0].fetch_add(1, std::memory_order_relaxed);
        }
    }

    SideMetadataResult result = InitializeFieldLogRange(
        block,
        ImmixBlockGeometry::BlockBytes,
        IsCopySource(source));
    if (result != SideMetadataResult::Success)
    {
        return result;
    }

    uintptr_t objectMark =
        markingContext == ImmixMarkingContext::ConcurrentOrFinalMark
            ? 1
            : 0;
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_D2_NO_MARK_CONTROL)
    // Control: a buffer that is handed out during a trace is not marked in bulk.
    if (!IsCopySource(source))
    {
        objectMark = 0;
    }
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_D2_NO_MARK_CONTROL
    result = InitializeObjectMarkRange(
        block,
        ImmixBlockGeometry::BlockBytes,
        objectMark);
    if (result == SideMetadataResult::Success &&
        markingContext == ImmixMarkingContext::ConcurrentOrFinalMark)
    {
        // Port safety delta: a fresh block may replace a remembered source during the same SATB.
        result = IncrementLineReuseRange(block, ImmixBlockGeometry::BlockBytes);
    }
    return result;
}

SideMetadataResult ImmixAllocationManager::PrepareRecycledHole(
    const ImmixHole& hole,
    ImmixAllocationSource source,
    ImmixMarkingContext markingContext)
{
    size_t size = static_cast<size_t>(hole.end - hole.start);
    if (!IsCopySource(source))
    {
        memset(reinterpret_cast<void*>(hole.start), 0, size);
        m_zero_stats[1].fetch_add(1, std::memory_order_relaxed);
        m_zero_stats[2].fetch_add(size, std::memory_order_relaxed);
    }

    SideMetadataResult result = InitializeFieldLogRange(
        hole.start,
        size,
        IsCopySource(source));
    if (result != SideMetadataResult::Success)
    {
        return result;
    }
    if (markingContext == ImmixMarkingContext::ConcurrentOrFinalMark
#if defined(LXR_SIDE_METADATA_TEST_PLATFORM) && defined(LXR_D2_NO_MARK_CONTROL)
        && IsCopySource(source)
#endif // LXR_SIDE_METADATA_TEST_PLATFORM && LXR_D2_NO_MARK_CONTROL
        )
    {
        result = InitializeObjectMarkRange(hole.start, size, 1);
        if (result == SideMetadataResult::Success)
        {
            result = IncrementLineReuseRange(hole.start, size);
        }
    }
    if ((result == SideMetadataResult::Success) && !IsCopySource(source))
    {
        m_recycled_lines_consumed.fetch_add(
            hole.line_count,
            std::memory_order_relaxed);
    }
    return result;
}

SideMetadataResult ImmixAllocationManager::InitializeFieldLogRange(
    uintptr_t start,
    size_t size,
    bool copy)
{
#if !BIGENDIAN && !defined(_DEBUG) && !defined(LXR_SIDE_METADATA_TEST_PLATFORM) && !defined(LXR_F9_TEST_HOOKS)
    uint32_t shift;
#ifndef LXR_OBJECT_START_WALK
    if (m_valid_object.IsValid())
    {
        memset(const_cast<uint8_t*>(m_valid_object.LocateByte(start, &shift)), 0, size >> 6);
    }
#endif // !LXR_OBJECT_START_WALK
    memset(const_cast<uint8_t*>(m_field_unlogged.LocateByte(start, &shift)), copy ? 0xff : 0, size >> 6);
    return SideMetadataResult::Success;
#else
    // The ValidObject plane exists because .NET needs the start of the object that contains an interior pointer; the Java
    // LXR has no such plane. A stale bit of a dead object must not lie in a range that new objects use, so the plane is
    // cleared here, on the same schedule as the unlog and mark tables of the Java LXR (at the refill of the range).
#if !defined(LXR_OBJECT_START_WALK) || defined(LXR_WALK_WRITE_VALID_CONTROL)
#if !defined(LXR_SIDE_METADATA_TEST_PLATFORM) || !defined(LXR_VALID_CLEAR_CONTROL)
    if (m_metadata->IsSpecEnabled(LxrSideMetadataKind::ValidObject))
    {
        SideMetadataResult result = m_metadata->FillRangeBytes(LxrSideMetadataKind::ValidObject, start, size, 0);
        if (result != SideMetadataResult::Success)
        {
            return result;
        }
    }
#endif // !LXR_SIDE_METADATA_TEST_PLATFORM || !defined(LXR_VALID_CLEAR_CONTROL)
#endif // !LXR_OBJECT_START_WALK || LXR_WALK_WRITE_VALID_CONTROL
    // P28 (F9). The Java LXR does not clear the count plane and the straddle plane at a refill: the counts and the straddle
    // marks of a dead object are cleared when the object dies (rc.rs, rc_work.rs: unmark_straddle_object), and the tables of a
    // block are cleared only when a defrag source is released. So a free block and a hole have zero counts and no straddle
    // mark, and the refill only has to unlog (the Java LXR: Block::clear_field_unlog_table, Line::clear_field_unlog_table).
    // Debug builds and the test platform check this invariant.
#if defined(LXR_F9_TEST_HOOKS) && defined(LXR_F9_CLEAR_AT_REFILL_CONTROL)
    // Control: the refill clears the count and straddle planes (the behaviour before F9).
    if (!copy)
    {
        SideMetadataResult result = SideMetadataResult::Success;
        if (m_metadata->IsSpecEnabled(LxrSideMetadataKind::ReferenceCountStraddle))
        {
            result = m_metadata->ResetRangeQuiescent(LxrSideMetadataKind::ReferenceCountStraddle, start, size, 0);
        }
        if (result == SideMetadataResult::Success)
        {
            result = m_metadata->ResetRangeQuiescent(LxrSideMetadataKind::ReferenceCount, start, size, 0);
        }
        if (result != SideMetadataResult::Success)
        {
            return result;
        }
    }
#endif // LXR_F9_TEST_HOOKS && LXR_F9_CLEAR_AT_REFILL_CONTROL
#if (defined(_DEBUG) || defined(LXR_F9_TEST_HOOKS)) && !defined(LXR_F9_NO_VERIFY_CONTROL)
    // A range that the collector takes for copies has the same invariant (F10: the copy of a nursery object does not clear it).
    {
        bool zero = false;
        SideMetadataResult result = m_metadata->IsRangeZero(
            LxrSideMetadataKind::ReferenceCount, start, size, SideMetadataMemoryOrder::Relaxed, &zero);
        bool noStraddle = true;
        if ((result == SideMetadataResult::Success) &&
            m_metadata->IsSpecEnabled(LxrSideMetadataKind::ReferenceCountStraddle))
        {
            result = m_metadata->IsRangeZero(
                LxrSideMetadataKind::ReferenceCountStraddle, start, size, SideMetadataMemoryOrder::Relaxed, &noStraddle);
        }
        if (result != SideMetadataResult::Success)
        {
            return result;
        }
        if (!zero || !noStraddle)
        {
#if !defined(LXR_F9_TEST_HOOKS)
            _ASSERTE(!"A range that a mutator takes has counts or straddle marks.");
#endif // !LXR_F9_TEST_HOOKS
            return SideMetadataResult::InconsistentState;
        }
    }
#endif // (_DEBUG || LXR_F9_TEST_HOOKS) && !LXR_F9_NO_VERIFY_CONTROL
    return m_metadata->FillRangeBytes(LxrSideMetadataKind::FieldUnlogged, start, size, copy ? 0xff : 0);
#endif
}

SideMetadataResult ImmixAllocationManager::InitializeObjectMarkRange(
    uintptr_t start,
    size_t size,
    uintptr_t value)
{
#if !BIGENDIAN && !defined(_DEBUG) && !defined(LXR_SIDE_METADATA_TEST_PLATFORM)
    uint32_t shift;
    memset(const_cast<uint8_t*>(m_object_mark.LocateByte(start, &shift)), value != 0 ? 0xff : 0, size >> 6);
    return SideMetadataResult::Success;
#else
    return m_metadata->FillRangeBytes(
        LxrSideMetadataKind::ObjectMark,
        start,
        size,
        value != 0 ? 0xff : 0);
#endif
}

SideMetadataResult ImmixAllocationManager::IncrementLineReuseRange(
    uintptr_t start,
    size_t size)
{
#if !BIGENDIAN && !defined(_DEBUG) && !defined(LXR_SIDE_METADATA_TEST_PLATFORM)
    uint32_t shift;
    uint8_t* counts = const_cast<uint8_t*>(m_line_reuse.LocateByte(start, &shift));
#else
    // Line::inc_reuse_counts of the Java LXR: one byte per line, a plain load and store (no read-modify-write), wrapping at
    // 255. Only the thread that took the range changes these bytes while it is in the mutator phase.
    SideMetadataLocation first = {};
    SideMetadataResult result = m_metadata->GetLocation(LxrSideMetadataKind::LineReuse, start, &first);
    if (result != SideMetadataResult::Success)
    {
        return result;
    }
    uint8_t* counts = reinterpret_cast<uint8_t*>(const_cast<uintptr_t*>(first.word)) + (first.shift >> 3);
#endif
    for (size_t line = 0; line < size / ImmixBlockGeometry::LineBytes; line++)
    {
        uint8_t count = VolatileLoadWithoutBarrier(counts + line);
        VolatileStore(counts + line, static_cast<uint8_t>(count == UINT8_MAX ? 0 : count + 1));
    }

    return SideMetadataResult::Success;
}

ImmixBumpAllocator::ImmixBumpAllocator()
    : m_manager(nullptr)
    , m_owner(ImmixBlockManager::NoOwner)
    , m_block(0)
    , m_cursor(0)
    , m_limit(0)
    , m_next_search_line(0)
    , m_source(ImmixAllocationSource::MutatorFresh)
{
}

SideMetadataResult ImmixBumpAllocator::Initialize(
    ImmixAllocationManager* manager,
    uintptr_t owner)
{
    if ((manager == nullptr) ||
        (owner == ImmixBlockManager::NoOwner) ||
        (m_manager != nullptr))
    {
        return SideMetadataResult::InvalidArgument;
    }

    m_manager = manager;
    m_owner = owner;
    return SideMetadataResult::Success;
}

SideMetadataResult ImmixBumpAllocator::TryRefill(
    ImmixAllocationSource source,
    ImmixMarkingContext markingContext,
    ImmixAllocationStatus* status,
    size_t minimumHoleBytes)
{
    if ((status == nullptr) ||
        (m_manager == nullptr) ||
        (source > ImmixAllocationSource::MutatorPinnedRecycled) ||
        (markingContext > ImmixMarkingContext::ConcurrentOrFinalMark))
    {
        return SideMetadataResult::InvalidArgument;
    }

    *status = ImmixAllocationStatus::Exhausted;
    if ((m_block != 0) &&
        (source == m_source) &&
        !ImmixAllocationManager::IsFreshSource(m_source))
    {
        ImmixHole hole;
        SideMetadataResult result = FindHole(
            m_block,
            m_next_search_line,
            minimumHoleBytes,
            &hole,
            status);
        if (result != SideMetadataResult::Success)
        {
            return ReleaseAfterFailure(result, status);
        }
        if (*status == ImmixAllocationStatus::Ready)
        {
            result = m_manager->PrepareRecycledHole(
                hole,
                m_source,
                markingContext);
            if (result == SideMetadataResult::Success)
            {
                m_cursor = hole.start;
                m_limit = hole.end;
            }
            if (result != SideMetadataResult::Success)
            {
                return ReleaseAfterFailure(result, status);
            }
            return SideMetadataResult::Success;
        }
    }

    if (m_block != 0)
    {
        SideMetadataResult result = ReturnCurrentBlock(status);
        if ((result != SideMetadataResult::Success) ||
            (*status != ImmixAllocationStatus::Ready))
        {
            return result;
        }
    }

    for (size_t attempt = 0;
         attempt < m_manager->m_block_count;
         attempt++)
    {
        uintptr_t block;
        SideMetadataResult result = m_manager->AcquireBlock(
            m_owner,
            source,
            &block,
            status,
            (minimumHoleBytes + ImmixBlockGeometry::LineBytes - 1) / ImmixBlockGeometry::LineBytes);
        if ((result != SideMetadataResult::Success) ||
            (*status != ImmixAllocationStatus::Ready))
        {
            return result;
        }

        m_block = block;
        m_source = source;
        m_next_search_line = 0;
        if (ImmixAllocationManager::IsFreshSource(source))
        {
            result = m_manager->PrepareFreshBlock(
                block,
                source,
                markingContext);
            if (result == SideMetadataResult::Success)
            {
                m_cursor = block;
                m_limit = block + ImmixBlockGeometry::BlockBytes;
            }
            if (result != SideMetadataResult::Success)
            {
                return ReleaseAfterFailure(result, status);
            }
            return SideMetadataResult::Success;
        }

        ImmixHole hole;
        result = FindHole(
            block,
            0,
            minimumHoleBytes,
            &hole,
            status);
        if (result != SideMetadataResult::Success)
        {
            return ReleaseAfterFailure(result, status);
        }
        if (*status == ImmixAllocationStatus::Ready)
        {
            result = m_manager->PrepareRecycledHole(
                hole,
                source,
                markingContext);
            if (result == SideMetadataResult::Success)
            {
                m_cursor = hole.start;
                m_limit = hole.end;
            }
            if (result != SideMetadataResult::Success)
            {
                return ReleaseAfterFailure(result, status);
            }
            return SideMetadataResult::Success;
        }

        result = ReturnCurrentBlock(status);
        if ((result != SideMetadataResult::Success) ||
            (*status != ImmixAllocationStatus::Ready))
        {
            return result;
        }
    }

    *status = ImmixAllocationStatus::Exhausted;
    return SideMetadataResult::Success;
}

SideMetadataResult ImmixBumpAllocator::TryAllocate(
    const ImmixAllocationRequest& request,
    uintptr_t* address,
    ImmixAllocationStatus* status)
{
    if ((address == nullptr) ||
        (status == nullptr) ||
        (m_manager == nullptr))
    {
        return SideMetadataResult::InvalidArgument;
    }

    *address = 0;
    *status = ImmixAllocationStatus::InvalidRequest;
    if ((request.size < MIN_OBJECT_SIZE) ||
        (request.size >= ImmixBlockGeometry::BlockBytes) ||
        (request.alignment < DATA_ALIGNMENT) ||
        ((request.alignment & (request.alignment - 1)) != 0) ||
        (request.alignment_offset >= request.alignment))
    {
        return SideMetadataResult::Success;
    }

    uintptr_t start;
    if (!TryAlignWithOffset(
            m_cursor,
            request.alignment,
            request.alignment_offset,
            &start))
    {
        return SideMetadataResult::Success;
    }

    uintptr_t end;
    if (!TryAdd(start, request.size, &end) ||
        (start < m_cursor) ||
        (end > m_limit))
    {
        *status = ImmixAllocationStatus::Exhausted;
        return SideMetadataResult::Success;
    }

    m_cursor = end;
    *address = start;
    *status = ImmixAllocationStatus::Allocated;
    return SideMetadataResult::Success;
}

SideMetadataResult ImmixBumpAllocator::Reset(
    ImmixAllocationStatus* status)
{
    if ((status == nullptr) || (m_manager == nullptr))
    {
        return SideMetadataResult::InvalidArgument;
    }
    if (m_block == 0)
    {
        *status = ImmixAllocationStatus::Ready;
        return SideMetadataResult::Success;
    }

    return ReturnCurrentBlock(status);
}

uintptr_t ImmixBumpAllocator::GetBlock() const
{
    return m_block;
}

bool ImmixBumpAllocator::HoldsFreshBlock() const
{
    return (m_block != 0) && ImmixAllocationManager::IsFreshSource(m_source);
}

uintptr_t ImmixBumpAllocator::GetCursor() const
{
    return m_cursor;
}

uintptr_t ImmixBumpAllocator::GetLimit() const
{
    return m_limit;
}

SideMetadataResult ImmixBumpAllocator::SetCursor(uintptr_t cursor)
{
    if (m_block == 0 || cursor < m_cursor || cursor > m_limit ||
        cursor % DATA_ALIGNMENT != 0)
    {
        return SideMetadataResult::InvalidArgument;
    }
    m_cursor = cursor;
    return SideMetadataResult::Success;
}

bool ImmixBumpAllocator::TryAlignWithOffset(
    uintptr_t cursor,
    size_t alignment,
    size_t alignmentOffset,
    uintptr_t* aligned)
{
    if ((aligned == nullptr) ||
        (alignment == 0) ||
        ((alignment & (alignment - 1)) != 0) ||
        (alignmentOffset >= alignment) ||
        (alignmentOffset > (UINTPTR_MAX - cursor)))
    {
        return false;
    }

    uintptr_t adjusted = cursor + alignmentOffset;
    uintptr_t mask = static_cast<uintptr_t>(alignment - 1);
    if (adjusted > (UINTPTR_MAX - mask))
    {
        return false;
    }

    uintptr_t rounded = (adjusted + mask) & ~mask;
    if (rounded < alignmentOffset)
    {
        return false;
    }

    *aligned = rounded - alignmentOffset;
    return true;
}

SideMetadataResult ImmixBumpAllocator::FindHole(
    uintptr_t block,
    size_t searchLine,
    size_t minimumHoleBytes,
    ImmixHole* hole,
    ImmixAllocationStatus* status)
{
    // A request of more than one line looks for a long hole through the whole block, so the lines are read as masks. A
    // request that one line holds takes the first hole, and that search reads only the lines up to the end of the hole.
    size_t minimumLines =
        (minimumHoleBytes + ImmixBlockGeometry::LineBytes - 1) / ImmixBlockGeometry::LineBytes;
    if (minimumLines > 1 && minimumLines > m_manager->m_minimum_reuse_lines)
    {
        size_t skipped = 0;
        SideMetadataResult result = m_manager->TryFindFittingHole(
            block,
            searchLine,
            minimumLines,
            hole,
            &m_next_search_line,
            status,
            &skipped);
        if (skipped != 0)
        {
            m_manager->m_holes_skipped.fetch_add(skipped, std::memory_order_relaxed);
        }
        return result;
    }

    for (;;)
    {
        SideMetadataResult result = m_manager->TryFindNextHole(
            block,
            searchLine,
            hole,
            &m_next_search_line,
            status);
        if ((result != SideMetadataResult::Success) ||
            (*status != ImmixAllocationStatus::Ready) ||
            (static_cast<size_t>(hole->end - hole->start) >= minimumHoleBytes))
        {
            return result;
        }
        // The hole cannot hold the pending request. It stays as it is, with its lines free, for a smaller request.
        m_manager->m_holes_skipped.fetch_add(1, std::memory_order_relaxed);
        searchLine = m_next_search_line;
    }
}

SideMetadataResult ImmixBumpAllocator::ReleaseAfterFailure(
    SideMetadataResult failure,
    ImmixAllocationStatus* status)
{
    SideMetadataResult releaseResult = ReturnCurrentBlock(status);
    return releaseResult == SideMetadataResult::Success
        ? failure
        : releaseResult;
}

SideMetadataResult ImmixBumpAllocator::ReturnCurrentBlock(
    ImmixAllocationStatus* status)
{
    SideMetadataResult result = m_manager->ReturnBlock(
        m_block,
        m_owner,
        status);
    if ((result == SideMetadataResult::Success) &&
        (*status == ImmixAllocationStatus::Ready))
    {
        m_block = 0;
        m_cursor = 0;
        m_limit = 0;
        m_next_search_line = 0;
    }
    return result;
}
