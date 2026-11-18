// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef IMMIX_BLOCK_H
#define IMMIX_BLOCK_H

#include "laxori_side_metadata.h"

#include <atomic>

#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
class LaxoriBlockReleaseGuard;
#endif

enum class ImmixGeometryResult : uint8_t
{
    Success,
    InvalidArgument,
    AddressOverflow,
    CrossesBlock,
};

struct ImmixGeometryRange
{
    uintptr_t first;
    uintptr_t end;
    size_t count;
};

class ImmixBlockGeometry final
{
public:
    static constexpr uint8_t BlockLogBytes = 15;
    static constexpr size_t BlockBytes = static_cast<size_t>(1) << BlockLogBytes;
    static constexpr uintptr_t BlockMask = BlockBytes - 1;
    static constexpr uint8_t LineLogBytes = 8;
    static constexpr size_t LineBytes = static_cast<size_t>(1) << LineLogBytes;
    static constexpr uintptr_t LineMask = LineBytes - 1;
    static constexpr uint8_t BlockLogLines = BlockLogBytes - LineLogBytes;
    static constexpr size_t LinesPerBlock = static_cast<size_t>(1) << BlockLogLines;
    static constexpr uint64_t AddressLimit = LaxoriSideMetadataLayout::AddressLimit;

    static bool IsBlockAligned(uintptr_t address);
    static bool IsLineAligned(uintptr_t address);

    static ImmixGeometryResult GetBlockStart(uintptr_t address, uintptr_t* blockStart);
    static ImmixGeometryResult GetLineStart(uintptr_t address, uintptr_t* lineStart);
    static ImmixGeometryResult AlignBlockUp(uintptr_t address, uintptr_t* blockEnd);
    static ImmixGeometryResult AlignLineUp(uintptr_t address, uintptr_t* lineEnd);

    static ImmixGeometryResult GetBlockIndex(uintptr_t address, size_t* blockIndex);
    static ImmixGeometryResult GetLineIndex(uintptr_t address, size_t* lineIndex);
    static ImmixGeometryResult GetLineIndexInBlock(uintptr_t address, size_t* lineIndex);

    static ImmixGeometryResult GetBlockRange(
        uintptr_t start,
        size_t size,
        ImmixGeometryRange* range);
    static ImmixGeometryResult GetLineRange(
        uintptr_t start,
        size_t size,
        ImmixGeometryRange* range);
    static ImmixGeometryResult GetObjectLineRange(
        uintptr_t start,
        size_t size,
        ImmixGeometryRange* range);

private:
    static ImmixGeometryResult AlignUp(
        uintptr_t address,
        size_t alignment,
        uintptr_t* result);
    static ImmixGeometryResult GetRange(
        uintptr_t start,
        size_t size,
        uint8_t logBytes,
        uintptr_t mask,
        ImmixGeometryRange* range);
};

enum class ImmixBlockState : uint8_t
{
    Unallocated,
    Unmarked,
    Marked,
    Reusable,
};

enum class ImmixBlockAcquireKind : uint8_t
{
    MutatorFresh,
    MutatorReusable,
    GcCopyFresh,
    GcCopyReusable,
    GcMatureEvacuationFresh,
};

enum class ImmixBlockOperationStatus : uint8_t
{
    Updated,
    Unchanged,
    Contended,
    Stale,
    InvalidTransition,
};

class ImmixBlockManager final
{
public:
    static constexpr uint8_t InitialPhaseEpoch = 1;
    static constexpr uint8_t LastPhaseEpoch = 254;
    static constexpr uintptr_t NoOwner = 0;

    ImmixBlockManager();
    ImmixBlockManager(const ImmixBlockManager&) = delete;
    ImmixBlockManager& operator=(const ImmixBlockManager&) = delete;

    SideMetadataResult Initialize(SideMetadataManager* metadata);
    void Shutdown();
    SideMetadataResult RegisterBlockRange(uintptr_t start, size_t size);

    uint8_t GetGlobalPhaseEpoch() const;
    // Increases at every epoch flip and at every block that is made Reusable, and never wraps. Equal tokens mean that
    // no block can have become a candidate for recycled allocation in between.
    uint64_t GetStateToken() const
    {
#ifdef LAXORI_S4_OLD_TOKEN_CONTROL
        return m_flip_serial.load(std::memory_order_acquire);
#else
        return m_flip_serial.load(std::memory_order_acquire) + m_reusable_serial.load(std::memory_order_acquire);
#endif // LAXORI_S4_OLD_TOKEN_CONTROL
    }
    // Number of blocks whose state is not Unallocated (blocks that hold, or may hold, mature data). It changes only at
    // block state transitions, so no pause has to walk the blocks to learn it.
    size_t GetNonFreeBlocks() const
    {
        return m_non_free_blocks.load(std::memory_order_acquire);
    }
    // Free lines of all Reusable blocks, as counted when each block was last made Reusable.
    size_t GetReusableFreeLines() const
    {
        return m_reusable_free_lines.load(std::memory_order_acquire);
    }
    SideMetadataResult StartGcPause(ImmixBlockOperationStatus* status);
    SideMetadataResult ReleaseGcPause(ImmixBlockOperationStatus* status);

    // Process-wide counts of block operations that met an epoch flip: operations in flight when the
    // flip began, and operations that started during it and had to wait. Both are zero when the flip
    // is quiescent, which is what the epoch protocol assumes. Debug builds assert on either. Read-only
    // queries (IsNursery, IsReusing, IsGcReusing) take no gate; Debug builds still count them here.
    static uint64_t GetFlipOverlaps();
    static uint64_t GetFlipBlockedOperations();

    SideMetadataResult GetState(
        uintptr_t block,
        ImmixBlockState* state,
        uint8_t* unavailableLines) const;
    SideMetadataResult GetBlockPhaseEpoch(uintptr_t block, uint8_t* epoch) const;

    // Fast form of GetState; the same rules as IsNurseryFast apply.
    SideMetadataResult GetStateFast(
        uintptr_t block,
        ImmixBlockState* state,
        uint8_t* unavailableLines) const
    {
#ifdef _DEBUG
        _ASSERTE(ValidateBlock(block) == SideMetadataResult::Success);
#endif // _DEBUG
        DecodeState(
            static_cast<uint8_t>(Accessor(LaxoriSideMetadataKind::BlockMark).Load(
                block, SideMetadataMemoryOrder::SequentiallyConsistent)),
            state,
            unavailableLines);
        return SideMetadataResult::Success;
    }
    SideMetadataResult IsNursery(uintptr_t block, bool* result) const;
    // Whether the block is Unallocated and no allocator owns it. For a quiescent caller (a pause that has released its
    // nursery blocks) this means that the block holds no object and that nothing can touch it before the next epoch.
    SideMetadataResult IsFreeBlock(uintptr_t block, bool* result) const;

    // Fast form of IsNursery for the pause loops. It does the same two block-metadata loads and the
    // same epoch load, with the same memory orders, but does not validate the block. The caller
    // must pass the aligned start of a block that was registered with RegisterBlockRange (for
    // example, the block of an object that the engine allocated). Debug builds assert that the block
    // is valid and, in the accessors, that each load gives the same location and value as the checked
    // load.
    SideMetadataResult IsNurseryFast(uintptr_t block, bool* result) const
    {
#ifdef _DEBUG
        ReaderScope reader(this);
        _ASSERTE(ValidateBlock(block) == SideMetadataResult::Success);
#endif // _DEBUG
        uint8_t rawState = static_cast<uint8_t>(Accessor(LaxoriSideMetadataKind::BlockMark).Load(
            block, SideMetadataMemoryOrder::SequentiallyConsistent));
        uint8_t blockEpoch = static_cast<uint8_t>(Accessor(LaxoriSideMetadataKind::PhaseEpoch).Load(
            block, SideMetadataMemoryOrder::SequentiallyConsistent));
        uint8_t globalEpoch = GetGlobalPhaseEpoch();
        bool nurseryOrReusing = (globalEpoch & 1) != 0
            ? blockEpoch == globalEpoch
            : blockEpoch == static_cast<uint8_t>(globalEpoch - 1);
        *result = (rawState == RawUnallocated) && nurseryOrReusing;
        return SideMetadataResult::Success;
    }
    SideMetadataResult IsReusing(uintptr_t block, bool* result) const;
    // Fast form of IsReusing (the same rules as IsNurseryFast): the block is not Unallocated and an allocator took it in this
    // epoch.
    bool IsReusingFast(uintptr_t block) const
    {
        uint8_t rawState = static_cast<uint8_t>(Accessor(LaxoriSideMetadataKind::BlockMark).Load(
            block, SideMetadataMemoryOrder::SequentiallyConsistent));
        uint8_t blockEpoch = static_cast<uint8_t>(Accessor(LaxoriSideMetadataKind::PhaseEpoch).Load(
            block, SideMetadataMemoryOrder::SequentiallyConsistent));
        uint8_t globalEpoch = GetGlobalPhaseEpoch();
        bool nurseryOrReusing = (globalEpoch & 1) != 0
            ? blockEpoch == globalEpoch
            : blockEpoch == static_cast<uint8_t>(globalEpoch - 1);
        return (rawState != RawUnallocated) && nurseryOrReusing;
    }
    SideMetadataResult IsGcReusing(uintptr_t block, bool* result) const;

    SideMetadataResult TryAcquire(
        uintptr_t block,
        uintptr_t owner,
        ImmixBlockAcquireKind kind,
        ImmixBlockOperationStatus* status);
    SideMetadataResult TryReturn(
        uintptr_t block,
        uintptr_t owner,
        ImmixBlockOperationStatus* status);
    SideMetadataResult TryRelease(
        uintptr_t block,
        uintptr_t owner,
        ImmixBlockOperationStatus* status);
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    SideMetadataResult TryReleaseIfRcDead(
        uintptr_t block,
        const LaxoriBlockReleaseGuard* guard,
        ImmixBlockOperationStatus* status);
#endif
    // Pause only. Releases a block that a mutator took as a clean nursery block and that no object has promoted. No
    // object in such a block has a reference count, so the block is empty by definition and the range is not read.
    // Status Unchanged: the block is not such a block (it was promoted, it is a recycled block, or it is free).
    SideMetadataResult TryReleaseNursery(
        uintptr_t block,
        ImmixBlockOperationStatus* status);
    // Pause only, and only while no other thread changes the state of these blocks (the release stage of a pause). The
    // blocks are given by their index from `arena`. It does for a batch what TryReleaseNursery does for one block, but it
    // takes no block lock: a young block that is still Unallocated has no count, no straddle mark and no unlog bit, so
    // only its ValidObject plane is cleared (one fill), and nothing else is written. A block that is not clean (a byte or,
    // in Debug and test builds, a plane has a value) goes through TryReleaseNursery. `released` is the number of blocks
    // for which TryReleaseNursery would have returned Updated.
    SideMetadataResult ReleaseNurseryBatchQuiescent(
        uintptr_t arena,
        const uint32_t* blocks,
        size_t count,
        size_t* released);
    // Pause only. Whether ReleaseNurseryBatchQuiescent releases the block, and so clears its ValidObject plane: the block
    // has no owner, is Unallocated and is a nursery block of this epoch. Nothing changes this answer inside the stage.
    SideMetadataResult IsReleasableNurseryQuiescent(uintptr_t block, bool* releasable) const;
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    // Mutator phase, inside a lazy sweep. Makes a block Reusable with the given number of unavailable lines. A block
    // that an allocator holds, or that an allocator took in this mutator epoch, has young objects without counts, so
    // its lines are not known. Status Contended or Stale: the caller must look at the block again after the next pause.
    SideMetadataResult TrySetReusableLazily(
        uintptr_t block,
        const LaxoriBlockReleaseGuard* guard,
        uint8_t unavailableLines,
        ImmixBlockOperationStatus* status);
#endif
    SideMetadataResult PrepareForTrace(
        uintptr_t block,
        ImmixBlockOperationStatus* status);
    SideMetadataResult Mark(
        uintptr_t block,
        ImmixBlockOperationStatus* status);
    SideMetadataResult SetReusable(
        uintptr_t block,
        uint8_t unavailableLines,
        ImmixBlockOperationStatus* status);
    SideMetadataResult TryPromoteInPlace(
        uintptr_t block,
        ImmixBlockOperationStatus* status);

    SideMetadataResult TryLog(uintptr_t block, bool* logged);
    SideMetadataResult Unlog(uintptr_t block);
#ifdef LAXORI_BLOCK_OPERATION_TEST_HOOK
    // Test only. The hook runs in every block operation, after the operation is counted as in flight and before it reads or
    // changes any block. A test uses it to hold operations open while an epoch flip is tried.
    using InOperationHook = void (*)(void* context);
    void SetInOperationHookForTest(InOperationHook hook, void* context)
    {
        m_in_operation_context = context;
        m_in_operation_hook = hook;
    }
#endif // LAXORI_BLOCK_OPERATION_TEST_HOOK

private:
    class BlockOperationScope final
    {
    public:
        explicit BlockOperationScope(const ImmixBlockManager* manager);
        ~BlockOperationScope();

    private:
        const ImmixBlockManager* m_manager;
        size_t m_stripe;
    };

#ifdef _DEBUG
    // Read-only queries do not use the epoch gate. In Debug builds they are counted so that the
    // flip assertion can prove that none of them runs during a flip.
    class ReaderScope final
    {
    public:
        explicit ReaderScope(const ImmixBlockManager* manager) : m_manager(manager)
        {
            m_manager->m_active_readers.fetch_add(1, std::memory_order_seq_cst);
        }

        ~ReaderScope()
        {
            m_manager->m_active_readers.fetch_sub(1, std::memory_order_seq_cst);
        }

        ReaderScope(const ReaderScope&) = delete;
        ReaderScope& operator=(const ReaderScope&) = delete;

    private:
        const ImmixBlockManager* m_manager;
    };
#endif // _DEBUG

    static constexpr uint8_t RawUnallocated = 0;
    static constexpr uint8_t RawMarked = UINT8_MAX - 1;
    static constexpr uint8_t RawUnmarked = UINT8_MAX;

    static uint8_t EncodeState(ImmixBlockState state, uint8_t unavailableLines);
    static void DecodeState(
        uint8_t rawState,
        ImmixBlockState* state,
        uint8_t* unavailableLines);
    static bool IsMutatorAcquire(ImmixBlockAcquireKind kind);
    static bool IsFreshAcquire(ImmixBlockAcquireKind kind);
    static bool IsMatureEvacuationAcquire(ImmixBlockAcquireKind kind);

    const SideMetadataAccessor& Accessor(LaxoriSideMetadataKind kind) const
    {
        return m_accessors[static_cast<size_t>(kind)];
    }

    SideMetadataResult ValidateBlock(uintptr_t block) const;
    SideMetadataResult LoadByte(
        LaxoriSideMetadataKind kind,
        uintptr_t block,
        uint8_t* value) const;
    SideMetadataResult StoreByte(
        LaxoriSideMetadataKind kind,
        uintptr_t block,
        uint8_t value);
    SideMetadataResult CompareExchangeByte(
        LaxoriSideMetadataKind kind,
        uintptr_t block,
        uint8_t value,
        uint8_t comparand,
        uint8_t* observed,
        bool* exchanged);
    SideMetadataResult TryLock(uintptr_t block, bool* acquired);
    SideMetadataResult Unlock(uintptr_t block);
    SideMetadataResult LoadOwner(uintptr_t block, uintptr_t* owner) const;
    SideMetadataResult StoreOwner(uintptr_t block, uintptr_t owner);
    SideMetadataResult IsNurseryOrReusing(uintptr_t block, bool* result) const;
    SideMetadataResult TransitionLocked(
        uintptr_t block,
        ImmixBlockState expected,
        uint8_t expectedUnavailableLines,
        ImmixBlockState desired,
        uint8_t desiredUnavailableLines,
        ImmixBlockOperationStatus* status);
    // Keeps the block counters in step with a change of the raw state byte of one block.
    void NoteRawStateChange(uint8_t oldRaw, uint8_t newRaw);
    size_t BeginBlockOperation() const;
    void EndBlockOperation(size_t stripe) const;
    bool HasActiveOperations() const;
    bool BeginPhaseTransition();
    void EndPhaseTransition();

    SideMetadataManager* m_metadata;
    SideMetadataAccessor m_accessors[LaxoriSideMetadataLayout::SpecCount];
    SideMetadataAccessor m_reference_count;
    // The three groups below are on separate cache lines. The first is read on every block operation and every allocation
    // search and changes only at an epoch flip. The operation counters are changed twice by every block operation, and the
    // block counters at every block state change, by all worker threads: when they shared a line with the epoch, each of
    // those changes made every reader of the epoch miss in its cache.
    alignas(64) std::atomic<uint8_t> m_global_phase_epoch;
    mutable std::atomic<bool> m_phase_transition;
    std::atomic<uint64_t> m_flip_serial{0};
    // Operations in flight, one counter per cache line. An operation counts itself in the stripe that its stack address
    // selects, and a flip waits until every stripe is zero.
    static constexpr size_t OperationStripes = 16;
    struct alignas(64) OperationStripe
    {
        std::atomic<uint32_t> count{0};
    };
    mutable OperationStripe m_operation_stripes[OperationStripes];
    // Block counters. They change at block state transitions only (about once per block and epoch).
    alignas(64) std::atomic<size_t> m_non_free_blocks{0};
    std::atomic<size_t> m_reusable_free_lines{0};
    // Counts the blocks that were made Reusable. A failed search for a recycled block is valid until this changes.
    std::atomic<uint64_t> m_reusable_serial{0};
#ifdef _DEBUG
    alignas(64) mutable std::atomic<uint32_t> m_active_readers;
#endif // _DEBUG
#ifdef LAXORI_BLOCK_OPERATION_TEST_HOOK
    InOperationHook m_in_operation_hook = nullptr;
    void* m_in_operation_context = nullptr;
#endif // LAXORI_BLOCK_OPERATION_TEST_HOOK
};

static_assert(ImmixBlockGeometry::BlockBytes == 32 * 1024);
static_assert(ImmixBlockGeometry::LineBytes == 256);
static_assert(ImmixBlockGeometry::LinesPerBlock == 128);
static_assert((ImmixBlockGeometry::BlockBytes & ImmixBlockGeometry::BlockMask) == 0);
static_assert((ImmixBlockGeometry::LineBytes & ImmixBlockGeometry::LineMask) == 0);

#endif // IMMIX_BLOCK_H
