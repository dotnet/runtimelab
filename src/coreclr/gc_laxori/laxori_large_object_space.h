// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef LARGE_OBJECT_SPACE_H
#define LARGE_OBJECT_SPACE_H

#include "laxori_immix_block.h"

#include <atomic>

enum class LaxoriLargeObjectResult : uint8_t
{
    Success,
    InvalidArgument,
    NotInitialized,
    UnsupportedAddressSpace,
    UnsupportedPageSize,
    BelowThreshold,
    AddressOverflow,
    ReservationFailed,
    CommitFailed,
    DecommitFailed,
    OutOfMemory,
    NotFound,
    NotOwner,
    InvalidPhase,
    MetadataFailure,
};

enum class LaxoriLargeObjectPhase : uint8_t
{
    Mutator,
    ConcurrentMark,
    Release,
    Shutdown,
};

struct LaxoriLargeObjectAllocation
{
    uintptr_t object;
    uintptr_t page_start;
    size_t object_bytes;
    size_t page_count;
    size_t padding_bytes;
    uintptr_t owner;
};

class LaxoriLargeObjectSpace final
{
public:
    static constexpr size_t PageBytes = 4 * 1024;
    static constexpr size_t MinimumObjectBytes = ImmixBlockGeometry::BlockBytes;
    static constexpr size_t FieldMetadataGranularity = 8;
    static constexpr uint8_t MarkBit = 0x1;
    static constexpr uint8_t NurseryBit = 0x2;
    static constexpr uint8_t StateMask = MarkBit | NurseryBit;

    LaxoriLargeObjectSpace();
    ~LaxoriLargeObjectSpace();
    LaxoriLargeObjectSpace(const LaxoriLargeObjectSpace&) = delete;
    LaxoriLargeObjectSpace& operator=(const LaxoriLargeObjectSpace&) = delete;

    static bool ShouldUseLargeObjectSpace(size_t objectBytes);
    static LaxoriLargeObjectResult ComputeAllocationGeometry(
        uintptr_t pageStart,
        size_t objectBytes,
        size_t alignment,
        size_t alignmentOffset,
        LaxoriLargeObjectAllocation* allocation);

    LaxoriLargeObjectResult Initialize(
        SideMetadataManager* metadata, size_t reservationBytes, void* reservation = nullptr,
        size_t admittedBytes = 0);
    LaxoriLargeObjectResult GrowQuiescent(size_t admittedBytes);
    size_t GetAdmittedBytes() const;
    size_t GetRequiredGrowthBytes(size_t objectBytes) const;
    void Shutdown();

    LaxoriLargeObjectPhase GetPhase() const;
    uint8_t GetMarkState() const;
    LaxoriLargeObjectResult BeginCollection(bool concurrent, bool fullTrace);
    LaxoriLargeObjectResult BeginRelease();
    LaxoriLargeObjectResult EndCollection();

    LaxoriLargeObjectResult Allocate(
        size_t objectBytes,
        size_t alignment,
        size_t alignmentOffset,
        uintptr_t owner,
        LaxoriLargeObjectAllocation* allocation);
    // unlogFields is false for an object that has no reference fields (a primitive array): nothing can log such an object,
    // so its unlog bits stay clear, as in the Java LAXORI (rc.rs: scan_nursery_object skips value arrays).
    LaxoriLargeObjectResult TryMarkAndPromote(
        uintptr_t object,
        uintptr_t owner,
        bool* updated,
        bool unlogFields = true);
    LaxoriLargeObjectResult TryRelease(uintptr_t object, uintptr_t owner);
    LaxoriLargeObjectResult GetAllocation(
        uintptr_t object,
        LaxoriLargeObjectAllocation* allocation) const;
    LaxoriLargeObjectResult GetContainingAllocation(
        uintptr_t address,
        LaxoriLargeObjectAllocation* allocation) const;
#ifdef LAXORI_ENABLE_LAZY_COMPLETION_COUNTER
    LaxoriLargeObjectResult ReleaseAfterDecrements(
        uintptr_t object, uintptr_t owner, const LaxoriBlockReleaseGuard& guard);
#endif
    // Pause only. Releases the young large objects that are dead: no reference count and still in the nursery state. It
    // visits the young spans only (the spans that were allocated since they were last found young), not all spans.
    LaxoriLargeObjectResult SweepNursery(size_t* releasedBytes);
    uintptr_t GetReservationStart() const;
    size_t GetReservationBytes() const;
    size_t GetCommittedBytes() const;
    LaxoriLargeObjectResult GetMatureCommittedBytes(size_t* bytes) const;
    // Page bytes of the large objects that are not nursery, and the part of those pages that no object byte uses. The answer
    // comes from running totals: the promotion of a span adds to them, and the release of a mature span subtracts from them.
    LaxoriLargeObjectResult GetMatureStatistics(size_t* bytes, size_t* slackBytes) const;
    // The same figures by a walk of all spans under the lock: the reference of the totals, and the answer when
    // LAXORI_LOS_LEGACY_WALKS=1 is set. For tests.
    LaxoriLargeObjectResult GetMatureStatisticsByWalk(size_t* bytes, size_t* slackBytes) const;
    // Copies the objects of the spans in the young list (the entries that are still current) into `objects`, and returns in
    // `entries` the number of entries of the list including the stale ones. For tests.
    LaxoriLargeObjectResult GetYoungObjects(
        uintptr_t* objects, size_t capacity, size_t* count, size_t* entries) const;
    // The number of differences that the validation mode found. For tests.
    size_t GetValidationFailures() const;
    // Checks the free-space tree, the page map and the active list against each other. For tests.
    LaxoriLargeObjectResult ValidateStructure(size_t* activeSpans, size_t* freePages) const;

private:
    // The life of a Span: Pooled -> Allocating -> Active -> Releasing -> Pooled, or Quarantined when the metadata of the
    // pages cannot be reset. An Allocating span is already in the active list, but it is not in the page map yet.
    enum class SpanState : uint8_t
    {
        Pooled,
        Allocating,
        Active,
        Releasing,
        Quarantined,
    };

    // The memory of a Span is never freed before Shutdown, so a reader that holds a stale pointer from the page map reads
    // valid memory. The fields are guarded by the sequence number "version" (odd while a writer changes them).
    struct Span
    {
        std::atomic<uint32_t> version;
        std::atomic<SpanState> state;
        std::atomic<uintptr_t> page_start;
        std::atomic<size_t> page_count;
        std::atomic<uintptr_t> object;
        std::atomic<size_t> object_bytes;
        std::atomic<size_t> padding_bytes;
        std::atomic<uintptr_t> owner;
        // The members below are guarded by the lock.
        Span* next;
        Span* previous;
        bool linked;
    };

    struct SpanSnapshot
    {
        uintptr_t page_start;
        size_t page_count;
        uintptr_t object;
        size_t object_bytes;
        size_t padding_bytes;
        uintptr_t owner;
        SpanState state;
    };

    struct SpanChunk
    {
        SpanChunk* next;
        Span spans[64];
    };

    struct SideState;
    struct SweepExpectation;

    class LockScope final
    {
    public:
        explicit LockScope(const LaxoriLargeObjectSpace* space);
        ~LockScope();

    private:
        const LaxoriLargeObjectSpace* m_space;
    };

    // Takes the lock when no allocation is between TryCarve and its end (and, when asked, no release is in progress). The
    // phase changes and Shutdown use it, so no allocation or release sees a phase that changed in the middle of it.
    class QuiescentLockScope final
    {
    public:
        QuiescentLockScope(const LaxoriLargeObjectSpace* space, bool includeReleases);
        ~QuiescentLockScope();

    private:
        const LaxoriLargeObjectSpace* m_space;
    };

    static bool IsPowerOfTwo(size_t value);
    static bool TryAdd(uintptr_t left, size_t right, uintptr_t* result);
    static bool TryAlignUp(size_t value, size_t alignment, size_t* result);
    static LaxoriLargeObjectResult FromMetadataResult(SideMetadataResult result);

    void EnterLock() const;
    void ExitLock() const;
    static void Backoff(uint32_t* attempt);

    // Lock-free lookup through the page map. The result is the span that covered the page at some moment, copied as a whole.
    bool FindSpan(uintptr_t address, SpanSnapshot* snapshot, Span** span) const;
    static void ReadSpan(const Span* span, SpanSnapshot* snapshot);
    static void WriteSpan(
        Span* span, SpanState state, const LaxoriLargeObjectAllocation& allocation);

    // The calls below need the lock.
    Span* PopSpan();
    void PushSpan(Span* span);
    void LinkActive(Span* span);
    void UnlinkActive(Span* span);
    size_t FindFirstFit(size_t pageCount) const;
    void SetFreeLeaf(size_t page, size_t pageCount);
    bool ResizeFreeTree(size_t pages);
    size_t FreeLeaf(size_t page) const;
    void CarveFree(size_t page, size_t pageCount);
    void InsertFreeAndCoalesce(size_t page, size_t pageCount);
    static uintptr_t FreeTag(size_t page);
    static bool IsFreeTag(uintptr_t entry);
    size_t PageIndex(uintptr_t address) const;

    LaxoriLargeObjectResult TryCarve(
        size_t objectBytes, size_t alignment, size_t alignmentOffset, uintptr_t owner,
        Span** span, LaxoriLargeObjectAllocation* candidate, LaxoriLargeObjectPhase* phase, uint8_t* markState,
        bool* retry);
    void AbandonAllocation(Span* span, const LaxoriLargeObjectAllocation& candidate);
    void PublishSpan(Span* span, const LaxoriLargeObjectAllocation& allocation);
    // knownYoung: the caller found the nursery bit of the object set, so the span is not in the mature totals.
    LaxoriLargeObjectResult BeginSpanRelease(Span* span, uintptr_t object, uintptr_t owner, bool knownYoung = false);
    LaxoriLargeObjectResult FinishSpanRelease(Span* span);
    void AbortSpanRelease(Span* span);
    // The young list. These calls need the lock, except RequeueYoung, which takes it.
    void AppendYoung(Span* span);
    void RequeueYoung(Span* span);
    LaxoriLargeObjectResult SweepYoungList(Span** detachedHead);
    LaxoriLargeObjectResult SweepAllSpans(Span** detachedHead, bool rebuildYoungList);
    void CaptureSweepExpectation(SweepExpectation* expectation) const;
    void CheckSweepExpectation(const SweepExpectation& expectation, const Span* detachedHead) const;
    void ReportValidationFailure(const char* what) const;
    LaxoriLargeObjectResult ReleaseObject(uintptr_t object, uintptr_t owner);
    LaxoriLargeObjectResult ResetAllocationMetadata(
        uintptr_t pageStart,
        size_t pageBytes);
    LaxoriLargeObjectResult InitializeAllocationMetadata(
        const LaxoriLargeObjectAllocation& allocation, LaxoriLargeObjectPhase phase, uint8_t markState);

    SideMetadataManager* m_metadata;
    void* m_reservation;
    size_t m_reservation_bytes;
    std::atomic<size_t> m_admitted_bytes{0};
    std::atomic<size_t> m_span_bytes{0};
    bool m_owns_reservation;
    Span* m_active_spans;
    Span* m_quarantined_spans;
    Span* m_span_pool;
    SpanChunk* m_span_chunks;
    // One entry per page of the reservation. An entry is null, or the Span of an active allocation (the same Span in
    // every page of it), or, in the last page of a free span, a tag (FreeTag) with the first page of that free span.
    std::atomic<uintptr_t>* m_page_map;
    // The free spans by address: a segment tree over the pages. The leaf of the first page of a free span holds the page
    // count of the span, the other leaves are zero, and an inner node holds the maximum of its children. The first fit by
    // address is a descent of the tree.
    uint32_t* m_free_tree;
    size_t m_tree_leaves;
    // Allocations between TryCarve and PublishSpan or AbandonAllocation, and releases between BeginSpanRelease and the
    // return of the pages. The phase changes and Shutdown wait until the first count is zero; an allocation that finds
    // no free span waits until the second count is zero.
    std::atomic<size_t> m_inflight_allocations{0};
    std::atomic<size_t> m_pending_releases{0};
    std::atomic<LaxoriLargeObjectPhase> m_phase;
    std::atomic<uint8_t> m_mark_state;
    mutable std::atomic_flag m_lock = ATOMIC_FLAG_INIT;

    // Everything that was added after the first version of the space is here, behind one pointer that is the last member.
    // The lock above is contended, and the layout of Span and of the members before this one decides how it is shared: a
    // change of that layout alone made a LOS workload use 25 % more CPU (P44). Do not add a member to Span or in front of
    // this pointer.
    SideState* m_side;
};

static_assert(LaxoriLargeObjectSpace::MinimumObjectBytes == 32 * 1024);
static_assert((LaxoriLargeObjectSpace::PageBytes & (LaxoriLargeObjectSpace::PageBytes - 1)) == 0);

#endif // LARGE_OBJECT_SPACE_H
