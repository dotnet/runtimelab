// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"
#include "gcenv.h"
#include "large_object_space.h"
#ifdef LXR_ENABLE_LAZY_COMPLETION_COUNTER
#include "lxr_completion_counter.h"
#endif

#include <algorithm>
#include <cstdio>
#include <cstdlib>
#include <new>
#include <vector>
#ifndef LXR_SIDE_METADATA_TEST_PLATFORM
#include <minipal/ospagesize.h>
#endif
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
bool LxrTestDecommitShouldFail();
#endif
#if defined(HOST_UNIX) && !defined(LXR_LOS_PORTABLE_COMMIT)
#include <sys/mman.h>
// The admitted range stays committed (read and write) while the space lives. A free page gives its memory back to the
// system with madvise(MADV_DONTNEED), and the next use of the page reads zeros. This takes the address-space lock only for
// reading, and it does not change the mappings. A commit and a decommit of every allocation take it for writing, and with
// many threads that serializes the threads in the kernel. LXR_LOS_PORTABLE_COMMIT selects the portable code, which commits
// the pages of each allocation and decommits the pages of each release, on Unix too.
#define LXR_LOS_PERSISTENT_COMMIT
#endif

namespace
{
    bool ReleasePhysicalPages(uintptr_t pageStart, size_t pageBytes)
    {
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
        if (LxrTestDecommitShouldFail())
        {
            return false;
        }
#endif
#ifdef LXR_LOS_PERSISTENT_COMMIT
        return madvise(reinterpret_cast<void*>(pageStart), pageBytes, MADV_DONTNEED) == 0;
#else
        return GCToOSInterface::VirtualDecommit(reinterpret_cast<void*>(pageStart), pageBytes);
#endif
    }

    bool CommitAdmittedPages(uintptr_t pageStart, size_t pageBytes)
    {
#ifdef LXR_LOS_PERSISTENT_COMMIT
        return GCToOSInterface::VirtualCommit(reinterpret_cast<void*>(pageStart), pageBytes);
#else
        (void)pageStart;
        (void)pageBytes;
        return true;
#endif
    }

    bool ReadFlag(const char* name)
    {
        const char* value = std::getenv(name);
        return value != nullptr && value[0] != '\0' && value[0] != '0';
    }
}

// The state that was added after the first version of the space (see m_side in the header). The lock guards the fields.
struct LxrLargeObjectSpace::SideState
{
    struct Young
    {
        Span* span;
        uint32_t version;
    };

    // The totals of the mature spans (Active, and the nursery bit of the object is clear): their page bytes, and the part of
    // those that no object byte uses. A promotion adds to them, and the start of the release of a mature span subtracts from
    // them (the walk does not count a span in the state Releasing either). Atomics: the promotions run on several threads
    // and take no lock. On a line of their own, as these fields are written by workers while the lock holder writes the rest.
    alignas(64) std::atomic<size_t> matureBytes{0};
    std::atomic<size_t> matureSlack{0};

    // The young list: the spans that were allocated and are still young (the nursery bit of the object is set). SweepNursery
    // visits these spans and no other. An entry is stale when its span has changed since (the version differs, because the
    // span was released and used again) or is out of use; a stale entry is dropped at the next sweep or when the list is full.
    // A span has one entry at most, except for a span that is released between two pauses: its stale entry stays beside the
    // entry of its next use.
    alignas(64) Young* young = nullptr;
    size_t youngCount = 0;
    size_t youngCapacity = 0;
    // False after an entry could not be stored (out of memory). The next sweep walks all spans and builds the list again.
    bool youngValid = true;
    // Read once by Initialize from the environment. LXR_LOS_LEGACY_WALKS=1 turns the young list and the totals off (the sweep
    // and the statistics walk all spans, as before). LXR_LOS_VALIDATE=1 compares every sweep and every statistics call with a
    // walk of all spans and reports a difference.
    bool legacyWalks = false;
    bool validate = false;
    std::atomic<size_t> validationFailures{0};

    ~SideState()
    {
        free(young);
    }

    static bool IsStale(const Young& entry)
    {
        if (entry.span->version.load(std::memory_order_acquire) != entry.version)
        {
            return true;
        }
        SpanState state = entry.span->state.load(std::memory_order_acquire);
        return state == SpanState::Pooled || state == SpanState::Quarantined;
    }
};

// What a sweep has to do, found by a walk of all spans: the spans to release, and the spans that stay young.
struct LxrLargeObjectSpace::SweepExpectation
{
    std::vector<const Span*> release;
    std::vector<const Span*> young;
    bool failed = false;
};

LxrLargeObjectSpace::LxrLargeObjectSpace()
    : m_metadata(nullptr)
    , m_reservation(nullptr)
    , m_reservation_bytes(0)
    , m_owns_reservation(false)
    , m_active_spans(nullptr)
    , m_quarantined_spans(nullptr)
    , m_span_pool(nullptr)
    , m_span_chunks(nullptr)
    , m_page_map(nullptr)
    , m_free_tree(nullptr)
    , m_tree_leaves(0)
    , m_phase(LxrLargeObjectPhase::Shutdown)
    , m_mark_state(0)
    , m_side(nullptr)
{
}

LxrLargeObjectSpace::~LxrLargeObjectSpace()
{
    Shutdown();
    delete m_side;
}

size_t LxrLargeObjectSpace::GetCommittedBytes() const
{
    return m_span_bytes.load(std::memory_order_acquire);
}

bool LxrLargeObjectSpace::ShouldUseLargeObjectSpace(size_t objectBytes)
{
    return objectBytes >= MinimumObjectBytes;
}

bool LxrLargeObjectSpace::IsPowerOfTwo(size_t value)
{
    return (value != 0) && ((value & (value - 1)) == 0);
}

bool LxrLargeObjectSpace::TryAdd(uintptr_t left, size_t right, uintptr_t* result)
{
    if ((result == nullptr) || (right > (UINTPTR_MAX - left)))
    {
        return false;
    }

    *result = left + right;
    return true;
}

bool LxrLargeObjectSpace::TryAlignUp(size_t value, size_t alignment, size_t* result)
{
    if ((result == nullptr) || !IsPowerOfTwo(alignment) || (value > (SIZE_MAX - (alignment - 1))))
    {
        return false;
    }

    *result = (value + (alignment - 1)) & ~(alignment - 1);
    return true;
}

LxrLargeObjectResult LxrLargeObjectSpace::ComputeAllocationGeometry(
    uintptr_t pageStart,
    size_t objectBytes,
    size_t alignment,
    size_t alignmentOffset,
    LxrLargeObjectAllocation* allocation)
{
    if ((allocation == nullptr) ||
        (objectBytes == 0) ||
        !IsPowerOfTwo(alignment) ||
    (alignment > PageBytes) ||
    (alignmentOffset >= alignment) ||
        ((pageStart & (PageBytes - 1)) != 0))
    {
        return LxrLargeObjectResult::InvalidArgument;
    }
    if (!ShouldUseLargeObjectSpace(objectBytes))
    {
        return LxrLargeObjectResult::BelowThreshold;
    }

    uintptr_t adjusted;
    if (!TryAdd(pageStart, alignmentOffset, &adjusted))
    {
        return LxrLargeObjectResult::AddressOverflow;
    }
    size_t misalignment = static_cast<size_t>(adjusted & (alignment - 1));
    size_t padding = misalignment == 0 ? 0 : alignment - misalignment;
    if (objectBytes > (SIZE_MAX - padding))
    {
        return LxrLargeObjectResult::AddressOverflow;
    }

    size_t usedBytes = padding + objectBytes;
    size_t pageBytes;
    if (!TryAlignUp(usedBytes, PageBytes, &pageBytes))
    {
        return LxrLargeObjectResult::AddressOverflow;
    }

    uintptr_t object;
    uintptr_t end;
    if (!TryAdd(pageStart, padding, &object) ||
        !TryAdd(pageStart, pageBytes, &end) ||
        (end > LxrSideMetadataLayout::AddressLimit))
    {
        return LxrLargeObjectResult::AddressOverflow;
    }

    allocation->object = object;
    allocation->page_start = pageStart;
    allocation->object_bytes = objectBytes;
    allocation->page_count = pageBytes / PageBytes;
    allocation->padding_bytes = padding;
    allocation->owner = 0;
    return LxrLargeObjectResult::Success;
}

LxrLargeObjectResult LxrLargeObjectSpace::FromMetadataResult(SideMetadataResult result)
{
    return result == SideMetadataResult::Success ?
        LxrLargeObjectResult::Success :
        LxrLargeObjectResult::MetadataFailure;
}

void LxrLargeObjectSpace::Backoff(uint32_t* attempt)
{
    uint32_t count = (*attempt)++;
    if (count < 128)
    {
        // The critical sections are short, so a waiter first spins on the processor and does not give the processor away.
        for (uint32_t spin = 0; spin <= (count >> 3); spin++)
        {
            YieldProcessor();
        }
    }
    else
    {
        GCToOSInterface::YieldThread(count);
    }
}

void LxrLargeObjectSpace::EnterLock() const
{
    uint32_t attempt = 0;
    while (m_lock.test_and_set(std::memory_order_acquire))
    {
        Backoff(&attempt);
    }
}

void LxrLargeObjectSpace::ExitLock() const
{
    m_lock.clear(std::memory_order_release);
}

LxrLargeObjectSpace::LockScope::LockScope(const LxrLargeObjectSpace* space)
    : m_space(space)
{
    m_space->EnterLock();
}

LxrLargeObjectSpace::LockScope::~LockScope()
{
    m_space->ExitLock();
}

LxrLargeObjectSpace::QuiescentLockScope::QuiescentLockScope(const LxrLargeObjectSpace* space, bool includeReleases)
    : m_space(space)
{
    uint32_t attempt = 0;
    for (;;)
    {
        m_space->EnterLock();
        if (m_space->m_inflight_allocations.load(std::memory_order_acquire) == 0 &&
            (!includeReleases || m_space->m_pending_releases.load(std::memory_order_acquire) == 0))
        {
            return;
        }
        m_space->ExitLock();
        Backoff(&attempt);
    }
}

LxrLargeObjectSpace::QuiescentLockScope::~QuiescentLockScope()
{
    m_space->ExitLock();
}

uintptr_t LxrLargeObjectSpace::FreeTag(size_t page)
{
    return (static_cast<uintptr_t>(page) << 1) | 1;
}

bool LxrLargeObjectSpace::IsFreeTag(uintptr_t entry)
{
    return (entry & 1) != 0;
}

size_t LxrLargeObjectSpace::PageIndex(uintptr_t address) const
{
    return static_cast<size_t>((address - GetReservationStart()) / PageBytes);
}

size_t LxrLargeObjectSpace::FreeLeaf(size_t page) const
{
    return m_free_tree[m_tree_leaves + page];
}

void LxrLargeObjectSpace::SetFreeLeaf(size_t page, size_t pageCount)
{
    size_t node = m_tree_leaves + page;
    m_free_tree[node] = static_cast<uint32_t>(pageCount);
    for (node >>= 1; node >= 1; node >>= 1)
    {
        uint32_t left = m_free_tree[2 * node];
        uint32_t right = m_free_tree[2 * node + 1];
        uint32_t maximum = left > right ? left : right;
        if (m_free_tree[node] == maximum)
        {
            break;
        }
        m_free_tree[node] = maximum;
    }
}

// Makes the tree cover at least the given number of pages. The leaves keep their values and the inner nodes are
// recomputed. The old tree is freed after the new one is complete.
bool LxrLargeObjectSpace::ResizeFreeTree(size_t pages)
{
    if (pages <= m_tree_leaves)
    {
        return true;
    }
    size_t leaves = m_tree_leaves == 0 ? 1 : m_tree_leaves;
    while (leaves < pages)
    {
        leaves <<= 1;
    }
    uint32_t* tree = static_cast<uint32_t*>(calloc(2 * leaves, sizeof(uint32_t)));
    if (tree == nullptr)
    {
        return false;
    }
    if (m_free_tree != nullptr)
    {
        memcpy(tree + leaves, m_free_tree + m_tree_leaves, m_tree_leaves * sizeof(uint32_t));
    }
    for (size_t node = leaves - 1; node >= 1; node--)
    {
        uint32_t left = tree[2 * node];
        uint32_t right = tree[2 * node + 1];
        tree[node] = left > right ? left : right;
    }
    free(m_free_tree);
    m_free_tree = tree;
    m_tree_leaves = leaves;
    return true;
}

size_t LxrLargeObjectSpace::FindFirstFit(size_t pageCount) const
{
    if (m_free_tree == nullptr || pageCount == 0 || pageCount > m_free_tree[1])
    {
        return SIZE_MAX;
    }
    size_t node = 1;
    while (node < m_tree_leaves)
    {
        node <<= 1;
        if (m_free_tree[node] < pageCount)
        {
            node++;
        }
    }
    return node - m_tree_leaves;
}

void LxrLargeObjectSpace::CarveFree(size_t page, size_t pageCount)
{
    size_t total = FreeLeaf(page);
    size_t last = page + total - 1;
    SetFreeLeaf(page, 0);
    if (total > pageCount)
    {
        SetFreeLeaf(page + pageCount, total - pageCount);
        m_page_map[last].store(FreeTag(page + pageCount), std::memory_order_relaxed);
    }
    else
    {
        m_page_map[last].store(0, std::memory_order_relaxed);
    }
}

void LxrLargeObjectSpace::InsertFreeAndCoalesce(size_t page, size_t pageCount)
{
    size_t start = page;
    size_t count = pageCount;
    if (start > 0)
    {
        uintptr_t entry = m_page_map[start - 1].load(std::memory_order_relaxed);
        if (IsFreeTag(entry))
        {
            size_t leftStart = static_cast<size_t>(entry >> 1);
            size_t leftCount = FreeLeaf(leftStart);
            if (leftCount != 0 && leftStart + leftCount == start)
            {
                SetFreeLeaf(leftStart, 0);
                m_page_map[start - 1].store(0, std::memory_order_relaxed);
                start = leftStart;
                count += leftCount;
            }
        }
    }
    size_t end = page + pageCount;
    if (end < m_tree_leaves)
    {
        size_t rightCount = FreeLeaf(end);
        if (rightCount != 0)
        {
            SetFreeLeaf(end, 0);
            count += rightCount;
        }
    }
    SetFreeLeaf(start, count);
    m_page_map[start + count - 1].store(FreeTag(start), std::memory_order_relaxed);
}

LxrLargeObjectSpace::Span* LxrLargeObjectSpace::PopSpan()
{
    if (m_span_pool == nullptr)
    {
        SpanChunk* chunk = new (std::nothrow) SpanChunk();
        if (chunk == nullptr)
        {
            return nullptr;
        }
        chunk->next = m_span_chunks;
        m_span_chunks = chunk;
        for (Span& span : chunk->spans)
        {
            span.next = m_span_pool;
            m_span_pool = &span;
        }
    }
    Span* span = m_span_pool;
    m_span_pool = span->next;
    span->next = nullptr;
    span->previous = nullptr;
    span->linked = false;
    return span;
}

void LxrLargeObjectSpace::PushSpan(Span* span)
{
    span->state.store(SpanState::Pooled, std::memory_order_release);
    span->next = m_span_pool;
    span->previous = nullptr;
    span->linked = false;
    m_span_pool = span;
}

void LxrLargeObjectSpace::LinkActive(Span* span)
{
    span->previous = nullptr;
    span->next = m_active_spans;
    if (m_active_spans != nullptr)
    {
        m_active_spans->previous = span;
    }
    m_active_spans = span;
    span->linked = true;
}

void LxrLargeObjectSpace::UnlinkActive(Span* span)
{
    if (!span->linked)
    {
        return;
    }
    if (span->previous == nullptr)
    {
        m_active_spans = span->next;
    }
    else
    {
        span->previous->next = span->next;
    }
    if (span->next != nullptr)
    {
        span->next->previous = span->previous;
    }
    span->next = nullptr;
    span->previous = nullptr;
    span->linked = false;
}

void LxrLargeObjectSpace::ReadSpan(const Span* span, SpanSnapshot* snapshot)
{
    uint32_t attempt = 0;
    for (;;)
    {
        uint32_t before = span->version.load(std::memory_order_acquire);
        if ((before & 1) == 0)
        {
            snapshot->state = span->state.load(std::memory_order_relaxed);
            snapshot->page_start = span->page_start.load(std::memory_order_relaxed);
            snapshot->page_count = span->page_count.load(std::memory_order_relaxed);
            snapshot->object = span->object.load(std::memory_order_relaxed);
            snapshot->object_bytes = span->object_bytes.load(std::memory_order_relaxed);
            snapshot->padding_bytes = span->padding_bytes.load(std::memory_order_relaxed);
            snapshot->owner = span->owner.load(std::memory_order_relaxed);
            std::atomic_thread_fence(std::memory_order_acquire);
            if (span->version.load(std::memory_order_relaxed) == before)
            {
                return;
            }
        }
        Backoff(&attempt);
    }
}

void LxrLargeObjectSpace::WriteSpan(
    Span* span, SpanState state, const LxrLargeObjectAllocation& allocation)
{
    uint32_t version = span->version.load(std::memory_order_relaxed);
    span->version.store(version + 1, std::memory_order_relaxed);
    std::atomic_thread_fence(std::memory_order_release);
    span->state.store(state, std::memory_order_relaxed);
    span->page_start.store(allocation.page_start, std::memory_order_relaxed);
    span->page_count.store(allocation.page_count, std::memory_order_relaxed);
    span->object.store(allocation.object, std::memory_order_relaxed);
    span->object_bytes.store(allocation.object_bytes, std::memory_order_relaxed);
    span->padding_bytes.store(allocation.padding_bytes, std::memory_order_relaxed);
    span->owner.store(allocation.owner, std::memory_order_relaxed);
    span->version.store(version + 2, std::memory_order_release);
}

bool LxrLargeObjectSpace::FindSpan(uintptr_t address, SpanSnapshot* snapshot, Span** span) const
{
    if (m_page_map == nullptr)
    {
        return false;
    }
    uintptr_t start = GetReservationStart();
    if (address < start || address - start >= m_admitted_bytes.load(std::memory_order_acquire))
    {
        return false;
    }
    uintptr_t entry = m_page_map[(address - start) / PageBytes].load(std::memory_order_acquire);
    if (entry == 0 || IsFreeTag(entry))
    {
        return false;
    }
    Span* found = reinterpret_cast<Span*>(entry);
    ReadSpan(found, snapshot);
    if (span != nullptr)
    {
        *span = found;
    }
    return true;
}

LxrLargeObjectResult LxrLargeObjectSpace::Initialize(
    SideMetadataManager* metadata,
    size_t reservationBytes,
    void* suppliedReservation,
    size_t admittedBytes)
{
    admittedBytes = admittedBytes == 0 ? reservationBytes : admittedBytes;
    if ((metadata == nullptr) ||
        (reservationBytes == 0) ||
        admittedBytes > reservationBytes || admittedBytes % PageBytes != 0 ||
        ((reservationBytes & (PageBytes - 1)) != 0) ||
        ((reinterpret_cast<uintptr_t>(suppliedReservation) & (PageBytes - 1)) != 0))
    {
        return LxrLargeObjectResult::InvalidArgument;
    }
    if (sizeof(uintptr_t) != sizeof(uint64_t))
    {
        return LxrLargeObjectResult::UnsupportedAddressSpace;
    }
#ifdef LXR_SIDE_METADATA_TEST_PLATFORM
    size_t pageSize = GCToOSInterface::GetPageSize();
#else
    size_t pageSize = minipal_getpagesize();
#endif
    if (pageSize != PageBytes)
    {
        return LxrLargeObjectResult::UnsupportedPageSize;
    }

    LockScope lock(this);
    if ((m_reservation != nullptr) || (m_phase.load(std::memory_order_acquire) != LxrLargeObjectPhase::Shutdown))
    {
        return LxrLargeObjectResult::InvalidPhase;
    }
    if (m_side == nullptr)
    {
        m_side = new (std::nothrow) SideState();
        if (m_side == nullptr)
        {
            return LxrLargeObjectResult::OutOfMemory;
        }
    }
    m_side->youngCount = 0;
    m_side->youngValid = true;
    m_side->matureBytes.store(0, std::memory_order_relaxed);
    m_side->matureSlack.store(0, std::memory_order_relaxed);
    m_side->legacyWalks = ReadFlag("LXR_LOS_LEGACY_WALKS");
    m_side->validate = ReadFlag("LXR_LOS_VALIDATE");
    m_side->validationFailures.store(0, std::memory_order_relaxed);

    const LxrSideMetadataKind required[] =
    {
#ifndef LXR_OBJECT_START_WALK
        LxrSideMetadataKind::ValidObject,
#endif // !LXR_OBJECT_START_WALK
        LxrSideMetadataKind::ReferenceCount,
        LxrSideMetadataKind::FieldUnlogged,
        LxrSideMetadataKind::ObjectMark,
        LxrSideMetadataKind::LargeObjectMarkNursery,
        LxrSideMetadataKind::LargeObjectPageReuse,
    };
    for (LxrSideMetadataKind kind : required)
    {
        if (!metadata->IsSpecEnabled(kind))
        {
            return LxrLargeObjectResult::MetadataFailure;
        }
    }

    size_t reservationPages = reservationBytes / PageBytes;
    if (reservationPages > UINT32_MAX)
    {
        return LxrLargeObjectResult::UnsupportedAddressSpace;
    }
    // The page map is reserved for the whole reservation and committed for the admitted pages only; the free tree is
    // allocated for the admitted pages. A table the size of the reservation would raise the commit charge at start-up
    // on Windows, which counts committed pages that nothing has written.
    size_t admittedPages = admittedBytes / PageBytes;
    size_t leaves = 1;
    while (leaves < admittedPages)
    {
        leaves <<= 1;
    }
    uint32_t* tree = static_cast<uint32_t*>(calloc(2 * leaves, sizeof(uint32_t)));
    auto freeTables = [&]()
    {
        free(tree);
        m_page_map_storage.Shutdown();
    };
    if (tree == nullptr || !m_page_map_storage.Reserve(reservationPages) ||
        !m_page_map_storage.Commit(0, admittedPages, false))
    {
        freeTables();
        return LxrLargeObjectResult::OutOfMemory;
    }
    std::atomic<uintptr_t>* map = m_page_map_storage.Get();

    bool ownsReservation = suppliedReservation == nullptr;
    void* reservation = ownsReservation
        ? GCToOSInterface::VirtualReserve(reservationBytes, PageBytes, VirtualReserveFlags::NoReserve)
        : suppliedReservation;
    if (reservation == nullptr)
    {
        freeTables();
        return LxrLargeObjectResult::ReservationFailed;
    }

    uintptr_t start = reinterpret_cast<uintptr_t>(reservation);
    uintptr_t end;
    if (!TryAdd(start, reservationBytes, &end) ||
        (end > LxrSideMetadataLayout::AddressLimit))
    {
        if (ownsReservation)
        {
            GCToOSInterface::VirtualRelease(reservation, reservationBytes);
        }
        freeTables();
        return LxrLargeObjectResult::UnsupportedAddressSpace;
    }

    if (!CommitAdmittedPages(start, admittedBytes))
    {
        if (ownsReservation)
        {
            GCToOSInterface::VirtualRelease(reservation, reservationBytes);
        }
        freeTables();
        return LxrLargeObjectResult::CommitFailed;
    }

    SideMetadataResult metadataResult = metadata->CommitDataRange(start, admittedBytes);
    if (metadataResult != SideMetadataResult::Success)
    {
        if (ownsReservation)
        {
            GCToOSInterface::VirtualRelease(reservation, reservationBytes);
        }
        freeTables();
        return FromMetadataResult(metadataResult);
    }

    m_metadata = metadata;
    m_reservation = reservation;
    m_reservation_bytes = reservationBytes;
    m_admitted_bytes.store(admittedBytes, std::memory_order_release);
    m_owns_reservation = ownsReservation;
    m_active_spans = nullptr;
    m_quarantined_spans = nullptr;
    m_span_pool = nullptr;
    m_span_chunks = nullptr;
    m_free_tree = tree;
    m_page_map = map;
    m_tree_leaves = leaves;
    InsertFreeAndCoalesce(0, admittedBytes / PageBytes);
    m_inflight_allocations.store(0, std::memory_order_release);
    m_pending_releases.store(0, std::memory_order_release);
    m_span_bytes.store(0, std::memory_order_release);
    m_mark_state.store(0, std::memory_order_release);
    m_phase.store(LxrLargeObjectPhase::Mutator, std::memory_order_release);
    return LxrLargeObjectResult::Success;
}

LxrLargeObjectResult LxrLargeObjectSpace::GrowQuiescent(size_t admittedBytes)
{
    LockScope lock(this);
    size_t previous = m_admitted_bytes.load(std::memory_order_relaxed);
    if (m_metadata == nullptr || admittedBytes <= previous || admittedBytes > m_reservation_bytes ||
        admittedBytes % PageBytes != 0 ||
        !m_metadata->IsDataRangeOwned(GetReservationStart(), admittedBytes))
    {
        return LxrLargeObjectResult::InvalidArgument;
    }
    if (!ResizeFreeTree(admittedBytes / PageBytes))
    {
        return LxrLargeObjectResult::OutOfMemory;
    }
    if (!m_page_map_storage.Commit(previous / PageBytes, (admittedBytes - previous) / PageBytes, false))
    {
        return LxrLargeObjectResult::CommitFailed;
    }
    if (!CommitAdmittedPages(GetReservationStart() + previous, admittedBytes - previous))
    {
        return LxrLargeObjectResult::CommitFailed;
    }
    InsertFreeAndCoalesce(previous / PageBytes, (admittedBytes - previous) / PageBytes);
    m_admitted_bytes.store(admittedBytes, std::memory_order_release);
    return LxrLargeObjectResult::Success;
}

size_t LxrLargeObjectSpace::GetAdmittedBytes() const
{
    return m_admitted_bytes.load(std::memory_order_acquire);
}

size_t LxrLargeObjectSpace::GetRequiredGrowthBytes(size_t objectBytes) const
{
    LockScope lock(this);
    LxrLargeObjectAllocation geometry;
    if (ComputeAllocationGeometry(0, objectBytes, sizeof(uintptr_t), 0, &geometry) !=
        LxrLargeObjectResult::Success)
    {
        return SIZE_MAX;
    }
    size_t required = geometry.page_count * PageBytes;
    size_t frontierPages = m_admitted_bytes.load(std::memory_order_relaxed) / PageBytes;
    if (m_page_map != nullptr && frontierPages != 0)
    {
        // The free span that ends at the frontier has a tag in its last page.
        uintptr_t entry = m_page_map[frontierPages - 1].load(std::memory_order_relaxed);
        if (IsFreeTag(entry))
        {
            size_t bytes = (frontierPages - static_cast<size_t>(entry >> 1)) * PageBytes;
            return required > bytes ? required - bytes : 0;
        }
    }
    return required;
}

LxrLargeObjectResult LxrLargeObjectSpace::GetMatureCommittedBytes(size_t* bytes) const
{
    size_t slack;
    return GetMatureStatistics(bytes, &slack);
}

LxrLargeObjectResult LxrLargeObjectSpace::GetMatureStatistics(size_t* bytes, size_t* slackBytes) const
{
    if (bytes == nullptr || slackBytes == nullptr)
    {
        return LxrLargeObjectResult::InvalidArgument;
    }
    SideState* side = m_side;
    if (side == nullptr || side->legacyWalks)
    {
        return GetMatureStatisticsByWalk(bytes, slackBytes);
    }
    *bytes = side->matureBytes.load(std::memory_order_relaxed);
    *slackBytes = side->matureSlack.load(std::memory_order_relaxed);
    if (side->validate)
    {
        size_t walkBytes = 0;
        size_t walkSlack = 0;
        LxrLargeObjectResult walk = GetMatureStatisticsByWalk(&walkBytes, &walkSlack);
        if (walk == LxrLargeObjectResult::Success && (walkBytes != *bytes || walkSlack != *slackBytes))
        {
            ReportValidationFailure("the mature totals differ from a walk of all spans");
        }
    }
    return LxrLargeObjectResult::Success;
}

LxrLargeObjectResult LxrLargeObjectSpace::GetMatureStatisticsByWalk(size_t* bytes, size_t* slackBytes) const
{
    if (bytes == nullptr || slackBytes == nullptr)
    {
        return LxrLargeObjectResult::InvalidArgument;
    }
    LockScope lock(this);
    *bytes = 0;
    *slackBytes = 0;
    for (Span* span = m_active_spans; span != nullptr; span = span->next)
    {
        if (span->state.load(std::memory_order_acquire) != SpanState::Active)
        {
            continue;
        }
        uintptr_t state;
        if (m_metadata->Load(LxrSideMetadataKind::LargeObjectMarkNursery,
                span->object.load(std::memory_order_relaxed), SideMetadataMemoryOrder::Relaxed, &state) !=
            SideMetadataResult::Success)
        {
            return LxrLargeObjectResult::MetadataFailure;
        }
        if ((state & NurseryBit) == 0)
        {
            size_t pageBytes = span->page_count.load(std::memory_order_relaxed) * PageBytes;
            *bytes += pageBytes;
            *slackBytes += pageBytes - span->object_bytes.load(std::memory_order_relaxed);
        }
    }
    return LxrLargeObjectResult::Success;
}

void LxrLargeObjectSpace::Shutdown()
{
    QuiescentLockScope lock(this, true);
    if (m_reservation != nullptr && m_owns_reservation)
    {
        GCToOSInterface::VirtualRelease(m_reservation, m_reservation_bytes);
    }
    while (m_span_chunks != nullptr)
    {
        SpanChunk* next = m_span_chunks->next;
        delete m_span_chunks;
        m_span_chunks = next;
    }
    free(m_free_tree);
    m_page_map_storage.Shutdown();
    m_free_tree = nullptr;
    m_page_map = nullptr;
    m_tree_leaves = 0;
    m_span_pool = nullptr;
    m_metadata = nullptr;
    m_reservation = nullptr;
    m_reservation_bytes = 0;
    m_owns_reservation = false;
    m_active_spans = nullptr;
    m_quarantined_spans = nullptr;
    if (m_side != nullptr)
    {
        m_side->youngCount = 0;
        m_side->youngValid = true;
        m_side->matureBytes.store(0, std::memory_order_relaxed);
        m_side->matureSlack.store(0, std::memory_order_relaxed);
    }
    m_span_bytes.store(0, std::memory_order_release);
    m_mark_state.store(0, std::memory_order_release);
    m_phase.store(LxrLargeObjectPhase::Shutdown, std::memory_order_release);
}

LxrLargeObjectPhase LxrLargeObjectSpace::GetPhase() const
{
    return m_phase.load(std::memory_order_acquire);
}

uint8_t LxrLargeObjectSpace::GetMarkState() const
{
    return m_mark_state.load(std::memory_order_acquire);
}

LxrLargeObjectResult LxrLargeObjectSpace::BeginCollection(bool concurrent, bool fullTrace)
{
    QuiescentLockScope lock(this, false);
    if (m_phase.load(std::memory_order_relaxed) != LxrLargeObjectPhase::Mutator)
    {
        return LxrLargeObjectResult::InvalidPhase;
    }
    if (fullTrace)
    {
        m_mark_state.fetch_xor(MarkBit, std::memory_order_seq_cst);
    }
    m_phase.store(
        concurrent ? LxrLargeObjectPhase::ConcurrentMark : LxrLargeObjectPhase::Release,
        std::memory_order_release);
    return LxrLargeObjectResult::Success;
}

LxrLargeObjectResult LxrLargeObjectSpace::BeginRelease()
{
    QuiescentLockScope lock(this, false);
    if (m_phase.load(std::memory_order_relaxed) != LxrLargeObjectPhase::ConcurrentMark)
    {
        return LxrLargeObjectResult::InvalidPhase;
    }
    m_phase.store(LxrLargeObjectPhase::Release, std::memory_order_release);
    return LxrLargeObjectResult::Success;
}

LxrLargeObjectResult LxrLargeObjectSpace::EndCollection()
{
    QuiescentLockScope lock(this, false);
    if (m_phase.load(std::memory_order_relaxed) != LxrLargeObjectPhase::Release)
    {
        return LxrLargeObjectResult::InvalidPhase;
    }
    m_phase.store(LxrLargeObjectPhase::Mutator, std::memory_order_release);
    return LxrLargeObjectResult::Success;
}

LxrLargeObjectResult LxrLargeObjectSpace::ResetAllocationMetadata(
    uintptr_t pageStart,
    size_t pageBytes)
{
    const LxrSideMetadataKind resetKinds[] =
    {
#ifndef LXR_OBJECT_START_WALK
        LxrSideMetadataKind::ValidObject,
#endif // !LXR_OBJECT_START_WALK
        LxrSideMetadataKind::ReferenceCount,
        LxrSideMetadataKind::FieldUnlogged,
        LxrSideMetadataKind::ObjectMark,
        LxrSideMetadataKind::LargeObjectMarkNursery,
    };
    for (LxrSideMetadataKind kind : resetKinds)
    {
        SideMetadataResult result = m_metadata->ResetRangeQuiescent(
            kind,
            pageStart,
            pageBytes,
            0);
        if (result != SideMetadataResult::Success)
        {
            return FromMetadataResult(result);
        }
    }
    return LxrLargeObjectResult::Success;
}

LxrLargeObjectResult LxrLargeObjectSpace::InitializeAllocationMetadata(
    const LxrLargeObjectAllocation& allocation, LxrLargeObjectPhase phase, uint8_t markState)
{
    size_t pageBytes = allocation.page_count * PageBytes;
    LxrLargeObjectResult result = ResetAllocationMetadata(allocation.page_start, pageBytes);
    if (result != LxrLargeObjectResult::Success)
    {
        return result;
    }

    if (phase == LxrLargeObjectPhase::ConcurrentMark)
    {
        for (size_t page = 0; page < allocation.page_count; page++)
        {
            uintptr_t previous;
            SideMetadataResult metadataResult = m_metadata->FetchAddWrapping(
                LxrSideMetadataKind::LargeObjectPageReuse,
                allocation.page_start + (page * PageBytes),
                1,
                SideMetadataMemoryOrder::SequentiallyConsistent,
                &previous);
            if (metadataResult != SideMetadataResult::Success)
            {
                return FromMetadataResult(metadataResult);
            }
        }
    }

    uint8_t state = static_cast<uint8_t>(markState | NurseryBit);
    SideMetadataResult metadataResult = m_metadata->Store(
        LxrSideMetadataKind::LargeObjectMarkNursery,
        allocation.object,
        state,
        SideMetadataMemoryOrder::SequentiallyConsistent);
#ifndef LXR_OBJECT_START_WALK
    if (metadataResult == SideMetadataResult::Success)
    {
        metadataResult = m_metadata->Store(
            LxrSideMetadataKind::ValidObject,
            allocation.object,
            1,
            SideMetadataMemoryOrder::SequentiallyConsistent);
    }
#endif // !LXR_OBJECT_START_WALK
    return FromMetadataResult(metadataResult);
}

LxrLargeObjectResult LxrLargeObjectSpace::TryCarve(
    size_t objectBytes,
    size_t alignment,
    size_t alignmentOffset,
    uintptr_t owner,
    Span** carvedSpan,
    LxrLargeObjectAllocation* candidate,
    LxrLargeObjectPhase* capturedPhase,
    uint8_t* capturedMarkState,
    bool* retry)
{
    LockScope lock(this);
    LxrLargeObjectPhase phase = m_phase.load(std::memory_order_relaxed);
    if (phase == LxrLargeObjectPhase::Shutdown)
    {
        return LxrLargeObjectResult::NotInitialized;
    }
    if ((phase != LxrLargeObjectPhase::Mutator) &&
        (phase != LxrLargeObjectPhase::ConcurrentMark))
    {
        return LxrLargeObjectResult::InvalidPhase;
    }

    LxrLargeObjectAllocation maximum;
    LxrLargeObjectResult result = ComputeAllocationGeometry(
        0,
        objectBytes,
        alignment,
        alignmentOffset,
        &maximum);
    if (result != LxrLargeObjectResult::Success)
    {
        return result;
    }

    size_t firstPage = FindFirstFit(maximum.page_count);
    if (firstPage == SIZE_MAX)
    {
        // A release that is in progress returns pages soon. The allocation waits for it, as it waited for the lock
        // while the release held it.
        *retry = m_pending_releases.load(std::memory_order_acquire) != 0;
        return LxrLargeObjectResult::OutOfMemory;
    }

    result = ComputeAllocationGeometry(
        GetReservationStart() + (firstPage * PageBytes),
        objectBytes,
        alignment,
        alignmentOffset,
        candidate);
    if (result != LxrLargeObjectResult::Success)
    {
        return result;
    }
    candidate->owner = owner;

    Span* span = PopSpan();
    if (span == nullptr)
    {
        return LxrLargeObjectResult::OutOfMemory;
    }
    CarveFree(firstPage, candidate->page_count);
    WriteSpan(span, SpanState::Allocating, *candidate);
    LinkActive(span);
    AppendYoung(span);
    m_inflight_allocations.fetch_add(1, std::memory_order_acq_rel);
    *carvedSpan = span;
    *capturedPhase = phase;
    *capturedMarkState = m_mark_state.load(std::memory_order_relaxed);
    return LxrLargeObjectResult::Success;
}

void LxrLargeObjectSpace::AbandonAllocation(Span* span, const LxrLargeObjectAllocation& candidate)
{
    {
        LockScope lock(this);
        UnlinkActive(span);
        InsertFreeAndCoalesce(PageIndex(candidate.page_start), candidate.page_count);
        PushSpan(span);
    }
    m_inflight_allocations.fetch_sub(1, std::memory_order_acq_rel);
}

void LxrLargeObjectSpace::PublishSpan(Span* span, const LxrLargeObjectAllocation& allocation)
{
    size_t firstPage = PageIndex(allocation.page_start);
    for (size_t page = 0; page < allocation.page_count; page++)
    {
        m_page_map[firstPage + page].store(reinterpret_cast<uintptr_t>(span), std::memory_order_release);
    }
    span->state.store(SpanState::Active, std::memory_order_release);
    m_span_bytes.fetch_add(allocation.page_count * PageBytes, std::memory_order_release);
    m_inflight_allocations.fetch_sub(1, std::memory_order_acq_rel);
}

LxrLargeObjectResult LxrLargeObjectSpace::Allocate(
    size_t objectBytes,
    size_t alignment,
    size_t alignmentOffset,
    uintptr_t owner,
    LxrLargeObjectAllocation* allocation)
{
    if ((allocation == nullptr) || (owner == 0))
    {
        return LxrLargeObjectResult::InvalidArgument;
    }

    Span* span = nullptr;
    LxrLargeObjectAllocation candidate;
    LxrLargeObjectPhase phase;
    uint8_t markState;
    uint32_t attempt = 0;
    for (;;)
    {
        bool retry = false;
        LxrLargeObjectResult carved = TryCarve(
            objectBytes, alignment, alignmentOffset, owner, &span, &candidate, &phase, &markState, &retry);
        if (carved == LxrLargeObjectResult::Success)
        {
            break;
        }
        if (!retry)
        {
            return carved;
        }
        Backoff(&attempt);
    }

    // The pages belong to this thread now, so the zeroing and the metadata need no lock.
    size_t committedBytes = candidate.page_count * PageBytes;
#ifndef LXR_LOS_PERSISTENT_COMMIT
    if (!GCToOSInterface::VirtualCommit(
            reinterpret_cast<void*>(candidate.page_start),
            committedBytes))
    {
        AbandonAllocation(span, candidate);
        return LxrLargeObjectResult::CommitFailed;
    }
    memset(reinterpret_cast<void*>(candidate.page_start), 0, committedBytes);
#endif

    LxrLargeObjectResult result = InitializeAllocationMetadata(candidate, phase, markState);
    if (result != LxrLargeObjectResult::Success)
    {
        ReleasePhysicalPages(candidate.page_start, committedBytes);
        AbandonAllocation(span, candidate);
        return result;
    }

    PublishSpan(span, candidate);
    *allocation = candidate;
    return LxrLargeObjectResult::Success;
}

LxrLargeObjectResult LxrLargeObjectSpace::TryMarkAndPromote(
    uintptr_t object,
    uintptr_t owner,
    bool* updated,
    bool unlogFields)
{
    if (updated == nullptr)
    {
        return LxrLargeObjectResult::InvalidArgument;
    }
    *updated = false;

    if (m_phase.load(std::memory_order_acquire) != LxrLargeObjectPhase::Release)
    {
        return LxrLargeObjectResult::InvalidPhase;
    }

    SpanSnapshot span;
    if (!FindSpan(object, &span, nullptr) || span.state != SpanState::Active || span.object != object)
    {
        return LxrLargeObjectResult::NotFound;
    }
    if (span.owner != owner)
    {
        return LxrLargeObjectResult::NotOwner;
    }

    size_t fieldMetadataBytes;
    if (!TryAlignUp(
            span.object_bytes,
            FieldMetadataGranularity,
            &fieldMetadataBytes))
    {
        return LxrLargeObjectResult::AddressOverflow;
    }

    size_t committedBytes = span.page_count * PageBytes;
    size_t objectOffset = span.object - span.page_start;
    if ((objectOffset > committedBytes) ||
        (fieldMetadataBytes > (committedBytes - objectOffset)))
    {
        return LxrLargeObjectResult::AddressOverflow;
    }

    if (unlogFields)
    {
        SideMetadataResult metadataResult = m_metadata->ResetRangeQuiescent(
            LxrSideMetadataKind::FieldUnlogged,
            span.object,
            fieldMetadataBytes,
            1);
        if (metadataResult != SideMetadataResult::Success)
        {
            return FromMetadataResult(metadataResult);
        }
    }

    uintptr_t oldState;
    SideMetadataResult metadataResult = m_metadata->Load(
        LxrSideMetadataKind::LargeObjectMarkNursery,
        object,
        SideMetadataMemoryOrder::SequentiallyConsistent,
        &oldState);
    if (metadataResult != SideMetadataResult::Success)
    {
        return FromMetadataResult(metadataResult);
    }
    uintptr_t desired = m_mark_state.load(std::memory_order_relaxed);
    if (oldState == desired)
    {
        return LxrLargeObjectResult::Success;
    }

    uintptr_t observed;
    bool exchanged;
    metadataResult = m_metadata->CompareExchange(
        LxrSideMetadataKind::LargeObjectMarkNursery,
        object,
        desired,
        oldState,
        SideMetadataMemoryOrder::SequentiallyConsistent,
        &observed,
        &exchanged);
    *updated = exchanged;
    if (exchanged && (oldState & NurseryBit) != 0 && m_side != nullptr && !m_side->legacyWalks)
    {
        // The span became mature (the new state has no nursery bit).
#ifndef LXR_LOS_CONTROL_TOTALS_NO_PROMOTE
        size_t spanBytes = span.page_count * PageBytes;
        m_side->matureBytes.fetch_add(spanBytes, std::memory_order_relaxed);
        m_side->matureSlack.fetch_add(spanBytes - span.object_bytes, std::memory_order_relaxed);
#endif
    }
    return FromMetadataResult(metadataResult);
}

LxrLargeObjectResult LxrLargeObjectSpace::TryRelease(
    uintptr_t object,
    uintptr_t owner)
{
    if (m_phase.load(std::memory_order_acquire) != LxrLargeObjectPhase::Release)
    {
        return LxrLargeObjectResult::InvalidPhase;
    }

    return ReleaseObject(object, owner);
}

// The caller owns a span that is in the state Releasing. The pages are decommitted and the metadata is reset outside the
// lock; only the return of the pages to the free spans needs the lock.
LxrLargeObjectResult LxrLargeObjectSpace::FinishSpanRelease(Span* span)
{
    uintptr_t pageStart = span->page_start.load(std::memory_order_relaxed);
    size_t pageCount = span->page_count.load(std::memory_order_relaxed);
    size_t pageBytes = pageCount * PageBytes;
    if (!ReleasePhysicalPages(pageStart, pageBytes))
    {
        AbortSpanRelease(span);
        return LxrLargeObjectResult::DecommitFailed;
    }

    LxrLargeObjectResult result = ResetAllocationMetadata(pageStart, pageBytes);
    size_t firstPage = PageIndex(pageStart);
    for (size_t page = 0; page < pageCount; page++)
    {
        m_page_map[firstPage + page].store(0, std::memory_order_release);
    }
    {
        LockScope lock(this);
        UnlinkActive(span);
        if (result != LxrLargeObjectResult::Success)
        {
            span->state.store(SpanState::Quarantined, std::memory_order_release);
            span->next = m_quarantined_spans;
            m_quarantined_spans = span;
        }
        else
        {
            InsertFreeAndCoalesce(firstPage, pageCount);
            PushSpan(span);
        }
    }
    if (result == LxrLargeObjectResult::Success)
    {
        m_span_bytes.fetch_sub(pageBytes, std::memory_order_release);
    }
    m_pending_releases.fetch_sub(1, std::memory_order_release);
    return result;
}

void LxrLargeObjectSpace::AbortSpanRelease(Span* span)
{
    // A mature span that was taken out of the totals when the release began goes back into them.
    if (m_side != nullptr && !m_side->legacyWalks)
    {
        uintptr_t state = NurseryBit;
        if (m_metadata->Load(LxrSideMetadataKind::LargeObjectMarkNursery, span->object.load(std::memory_order_relaxed),
                SideMetadataMemoryOrder::Relaxed, &state) == SideMetadataResult::Success &&
            (state & NurseryBit) == 0)
        {
            size_t pageBytes = span->page_count.load(std::memory_order_relaxed) * PageBytes;
            m_side->matureBytes.fetch_add(pageBytes, std::memory_order_relaxed);
            m_side->matureSlack.fetch_add(
                pageBytes - span->object_bytes.load(std::memory_order_relaxed), std::memory_order_relaxed);
        }
    }
    {
        LockScope lock(this);
        if (!span->linked)
        {
            LinkActive(span);
        }
        span->state.store(SpanState::Active, std::memory_order_release);
    }
    m_pending_releases.fetch_sub(1, std::memory_order_release);
}

// Takes the span for a release: the state changes from Active to Releasing, which only one thread can do. The span may have
// been reused for another object after the lookup, so the identity is checked again after the change.
LxrLargeObjectResult LxrLargeObjectSpace::BeginSpanRelease(
    Span* span, uintptr_t object, uintptr_t owner, bool knownYoung)
{
    m_pending_releases.fetch_add(1, std::memory_order_acq_rel);
    SpanState expected = SpanState::Active;
    if (!span->state.compare_exchange_strong(expected, SpanState::Releasing, std::memory_order_acq_rel))
    {
        m_pending_releases.fetch_sub(1, std::memory_order_release);
        return LxrLargeObjectResult::NotFound;
    }
    LxrLargeObjectResult result = LxrLargeObjectResult::Success;
    if (span->object.load(std::memory_order_relaxed) != object)
    {
        result = LxrLargeObjectResult::NotFound;
    }
    else if (span->owner.load(std::memory_order_relaxed) != owner)
    {
        result = LxrLargeObjectResult::NotOwner;
    }
    else if (!knownYoung && m_side != nullptr && !m_side->legacyWalks)
    {
        // A span in the state Releasing is not in the totals (the walk skips it), so a mature span leaves them now.
        uintptr_t state = 0;
        if (m_metadata->Load(LxrSideMetadataKind::LargeObjectMarkNursery, object, SideMetadataMemoryOrder::Relaxed,
                &state) != SideMetadataResult::Success)
        {
            result = LxrLargeObjectResult::MetadataFailure;
        }
        else if ((state & NurseryBit) == 0)
        {
#ifndef LXR_LOS_CONTROL_TOTALS_NO_RELEASE
            size_t pageBytes = span->page_count.load(std::memory_order_relaxed) * PageBytes;
            m_side->matureBytes.fetch_sub(pageBytes, std::memory_order_relaxed);
            m_side->matureSlack.fetch_sub(
                pageBytes - span->object_bytes.load(std::memory_order_relaxed), std::memory_order_relaxed);
#endif
        }
    }
    if (result != LxrLargeObjectResult::Success)
    {
        span->state.store(SpanState::Active, std::memory_order_release);
        m_pending_releases.fetch_sub(1, std::memory_order_release);
    }
    return result;
}

LxrLargeObjectResult LxrLargeObjectSpace::ReleaseObject(uintptr_t object, uintptr_t owner)
{
    SpanSnapshot snapshot;
    Span* span;
    if (!FindSpan(object, &snapshot, &span) || snapshot.state != SpanState::Active || snapshot.object != object)
    {
        return LxrLargeObjectResult::NotFound;
    }
    if (snapshot.owner != owner)
    {
        return LxrLargeObjectResult::NotOwner;
    }
    LxrLargeObjectResult result = BeginSpanRelease(span, object, owner);
    if (result != LxrLargeObjectResult::Success)
    {
        return result;
    }
    return FinishSpanRelease(span);
}

#ifdef LXR_ENABLE_LAZY_COMPLETION_COUNTER
LxrLargeObjectResult LxrLargeObjectSpace::ReleaseAfterDecrements(
    uintptr_t object, uintptr_t owner, const LxrBlockReleaseGuard& guard)
{
    if (!guard.IsValid())
    {
        return LxrLargeObjectResult::InvalidArgument;
    }
    LxrLargeObjectPhase phase = m_phase.load(std::memory_order_acquire);
    // The lazy release runs after the pause that killed the object. While a concurrent trace is active the space
    // is in ConcurrentMark between pauses, so that phase must be accepted too. A dying object was marked and scanned before its
    // count reached zero (FinishDyingTraceObject), so the trace holds no reference to it. An object that dies in the first epoch
    // after InitialMark is not marked (P46): it was unreachable at the snapshot, so the trace holds no reference to it either.
    if (phase == LxrLargeObjectPhase::Shutdown)
    {
        return LxrLargeObjectResult::InvalidPhase;
    }
    uintptr_t count = 0;
    uintptr_t state = 0;
    SideMetadataResult result = m_metadata->Load(
        LxrSideMetadataKind::ReferenceCount, object,
        SideMetadataMemoryOrder::SequentiallyConsistent, &count);
    if (result != SideMetadataResult::Success)
    {
        return FromMetadataResult(result);
    }
    result = m_metadata->Load(
        LxrSideMetadataKind::LargeObjectMarkNursery, object,
        SideMetadataMemoryOrder::SequentiallyConsistent, &state);
    if (result != SideMetadataResult::Success)
    {
        return FromMetadataResult(result);
    }
    if (count != 0 || (state & NurseryBit) != 0)
    {
        return LxrLargeObjectResult::InvalidPhase;
    }
    return ReleaseObject(object, owner);
}
#endif

void LxrLargeObjectSpace::AppendYoung(Span* span)
{
    SideState* side = m_side;
    if (side == nullptr || side->legacyWalks || !side->youngValid)
    {
        return;
    }
#ifdef LXR_LOS_CONTROL_YOUNG_NO_RECORD
    (void)span;
    return;
#else
    if (side->youngCount == side->youngCapacity)
    {
        // Entries of spans that were released since are dropped first, so the list stays as long as the spans that are
        // really young and does not grow with the releases that happen between two pauses.
        size_t kept = 0;
        for (size_t index = 0; index < side->youngCount; index++)
        {
            if (!SideState::IsStale(side->young[index]))
            {
                side->young[kept++] = side->young[index];
            }
        }
        side->youngCount = kept;
        if (kept * 2 >= side->youngCapacity)
        {
            size_t capacity = side->youngCapacity == 0 ? 1024 : side->youngCapacity * 2;
            SideState::Young* grown = static_cast<SideState::Young*>(
                realloc(side->young, capacity * sizeof(SideState::Young)));
            if (grown == nullptr)
            {
                side->youngValid = false;
                return;
            }
            side->young = grown;
            side->youngCapacity = capacity;
        }
    }
    side->young[side->youngCount++] = { span, span->version.load(std::memory_order_relaxed) };
#endif
}

// A span that a sweep took for a release and that stayed (the release failed) is young again.
void LxrLargeObjectSpace::RequeueYoung(Span* span)
{
    LockScope lock(this);
    if (span->state.load(std::memory_order_acquire) == SpanState::Active)
    {
        AppendYoung(span);
    }
}

void LxrLargeObjectSpace::ReportValidationFailure(const char* what) const
{
    m_side->validationFailures.fetch_add(1, std::memory_order_relaxed);
    std::fprintf(stderr, "LXR LOS validation: %s\n", what);
}

LxrLargeObjectResult LxrLargeObjectSpace::GetYoungObjects(
    uintptr_t* objects, size_t capacity, size_t* count, size_t* entries) const
{
    if (objects == nullptr || count == nullptr || entries == nullptr)
    {
        return LxrLargeObjectResult::InvalidArgument;
    }
    *count = 0;
    *entries = 0;
    LockScope lock(this);
    if (m_side == nullptr || m_side->legacyWalks)
    {
        return LxrLargeObjectResult::Success;
    }
    *entries = m_side->youngCount;
    for (size_t index = 0; index < m_side->youngCount; index++)
    {
        const SideState::Young& entry = m_side->young[index];
        SpanState state = entry.span->state.load(std::memory_order_acquire);
        if (entry.span->version.load(std::memory_order_acquire) != entry.version ||
            (state != SpanState::Active && state != SpanState::Allocating))
        {
            continue;
        }
        if (*count == capacity)
        {
            return LxrLargeObjectResult::InvalidArgument;
        }
        objects[(*count)++] = entry.span->object.load(std::memory_order_relaxed);
    }
    return LxrLargeObjectResult::Success;
}

size_t LxrLargeObjectSpace::GetValidationFailures() const
{
    return m_side == nullptr ? 0 : m_side->validationFailures.load(std::memory_order_relaxed);
}

// The sweep that visits the young spans only. A span is young while the nursery bit of its object is set, and the bit is
// set when the span is allocated and cleared when the object is promoted (TryMarkAndPromote). So every span that can be dead
// and young is in the list. For each entry:
//  - stale (the span was released and maybe used again, or is out of use): dropped;
//  - promoted (no nursery bit): dropped, it is old now;
//  - young and dead (no reference count): taken out of the active list for the release, and out of the young list;
//  - young and alive but not promoted yet, or still being allocated, or being released by another thread: stays.
LxrLargeObjectResult LxrLargeObjectSpace::SweepYoungList(Span** detachedHead)
{
    SideState* side = m_side;
    Span* tail = nullptr;
    LxrLargeObjectResult walk = LxrLargeObjectResult::Success;
    size_t kept = 0;
    size_t index = 0;
    for (; index < side->youngCount; index++)
    {
        SideState::Young entry = side->young[index];
        Span* span = entry.span;
        if (span->version.load(std::memory_order_acquire) != entry.version)
        {
            continue;
        }
        SpanState spanState = span->state.load(std::memory_order_acquire);
        if (spanState == SpanState::Allocating || spanState == SpanState::Releasing)
        {
            side->young[kept++] = entry;
            continue;
        }
        if (spanState != SpanState::Active)
        {
            continue;
        }
        uintptr_t object = span->object.load(std::memory_order_relaxed);
        uintptr_t state = 0;
        uintptr_t count = 0;
        SideMetadataResult result = m_metadata->Load(
            LxrSideMetadataKind::LargeObjectMarkNursery, object,
            SideMetadataMemoryOrder::SequentiallyConsistent, &state);
        if (result == SideMetadataResult::Success)
        {
            result = m_metadata->Load(
                LxrSideMetadataKind::ReferenceCount, object,
                SideMetadataMemoryOrder::SequentiallyConsistent, &count);
        }
        if (result != SideMetadataResult::Success)
        {
            walk = FromMetadataResult(result);
            break;
        }
#ifndef LXR_LOS_CONTROL_YOUNG_KEEP_PROMOTED
        if ((state & NurseryBit) == 0)
        {
            continue;
        }
#endif
        if (count == 0 &&
            BeginSpanRelease(span, object, span->owner.load(std::memory_order_relaxed), true) ==
                LxrLargeObjectResult::Success)
        {
            UnlinkActive(span);
            span->next = nullptr;
            if (tail == nullptr)
            {
                *detachedHead = span;
            }
            else
            {
                tail->next = span;
            }
            tail = span;
            continue;
        }
        side->young[kept++] = entry;
    }
    // After an error the entries that were not visited stay, the entry of the error included.
    for (; index < side->youngCount; index++)
    {
        side->young[kept++] = side->young[index];
    }
    side->youngCount = kept;
    return walk;
}

// The sweep that visits all spans (the first version of the sweep). With rebuildYoungList it also stores the spans that stay
// young into an empty young list; it is the way back after the list lost an entry.
LxrLargeObjectResult LxrLargeObjectSpace::SweepAllSpans(Span** detachedHead, bool rebuildYoungList)
{
    SideState* side = m_side;
    if (rebuildYoungList)
    {
        side->youngCount = 0;
        side->youngValid = true;
    }
    LxrLargeObjectResult walk = LxrLargeObjectResult::Success;
    Span* detached = *detachedHead;
    Span* span = m_active_spans;
    while (span != nullptr)
    {
        Span* next = span->next;
        SpanState spanState = span->state.load(std::memory_order_acquire);
        if (spanState == SpanState::Active)
        {
            uintptr_t object = span->object.load(std::memory_order_relaxed);
            uintptr_t state = 0;
            uintptr_t count = 0;
            SideMetadataResult result = m_metadata->Load(
                LxrSideMetadataKind::LargeObjectMarkNursery, object,
                SideMetadataMemoryOrder::SequentiallyConsistent, &state);
            if (result == SideMetadataResult::Success)
            {
                result = m_metadata->Load(
                    LxrSideMetadataKind::ReferenceCount, object,
                    SideMetadataMemoryOrder::SequentiallyConsistent, &count);
            }
            if (result != SideMetadataResult::Success)
            {
                walk = FromMetadataResult(result);
                break;
            }
            if ((state & NurseryBit) != 0 && count == 0 &&
                BeginSpanRelease(span, object, span->owner.load(std::memory_order_relaxed), true) ==
                    LxrLargeObjectResult::Success)
            {
                UnlinkActive(span);
                span->next = detached;
                detached = span;
            }
            else if (rebuildYoungList && (state & NurseryBit) != 0)
            {
                AppendYoung(span);
            }
        }
        else if (rebuildYoungList && spanState == SpanState::Allocating)
        {
            AppendYoung(span);
        }
        span = next;
    }
    *detachedHead = detached;
    if (rebuildYoungList && walk != LxrLargeObjectResult::Success)
    {
        // The list is incomplete: it is built again at the next sweep.
        side->youngValid = false;
    }
    return walk;
}

// For the validation mode. Before the sweep: what the old walk would do.
void LxrLargeObjectSpace::CaptureSweepExpectation(SweepExpectation* expectation) const
{
    for (const Span* span = m_active_spans; span != nullptr; span = span->next)
    {
        SpanState spanState = span->state.load(std::memory_order_acquire);
        if (spanState == SpanState::Allocating)
        {
            expectation->young.push_back(span);
            continue;
        }
        if (spanState != SpanState::Active)
        {
            continue;
        }
        uintptr_t object = span->object.load(std::memory_order_relaxed);
        uintptr_t state = 0;
        uintptr_t count = 0;
        if (m_metadata->Load(LxrSideMetadataKind::LargeObjectMarkNursery, object,
                SideMetadataMemoryOrder::SequentiallyConsistent, &state) != SideMetadataResult::Success ||
            m_metadata->Load(LxrSideMetadataKind::ReferenceCount, object,
                SideMetadataMemoryOrder::SequentiallyConsistent, &count) != SideMetadataResult::Success)
        {
            expectation->failed = true;
            return;
        }
        if ((state & NurseryBit) != 0)
        {
            (count == 0 ? expectation->release : expectation->young).push_back(span);
        }
    }
}

// For the validation mode. After the sweep: the spans taken for the release are the spans that the old walk would take, and the
// young list holds the spans that stay young, and no other span.
void LxrLargeObjectSpace::CheckSweepExpectation(const SweepExpectation& expectation, const Span* detachedHead) const
{
    if (expectation.failed)
    {
        return;
    }
    std::vector<const Span*> released;
    for (const Span* span = detachedHead; span != nullptr; span = span->next)
    {
        released.push_back(span);
    }
    std::vector<const Span*> expectedRelease = expectation.release;
    std::sort(released.begin(), released.end());
    std::sort(expectedRelease.begin(), expectedRelease.end());
    if (released != expectedRelease)
    {
        ReportValidationFailure("the young sweep releases other spans than a walk of all spans");
    }
    std::vector<const Span*> listed;
    const SideState* side = m_side;
    for (size_t index = 0; index < side->youngCount; index++)
    {
        const SideState::Young& entry = side->young[index];
        SpanState state = entry.span->state.load(std::memory_order_acquire);
        if (entry.span->version.load(std::memory_order_acquire) == entry.version &&
            (state == SpanState::Active || state == SpanState::Allocating))
        {
            listed.push_back(entry.span);
        }
    }
    std::vector<const Span*> expectedYoung = expectation.young;
    std::sort(listed.begin(), listed.end());
    std::sort(expectedYoung.begin(), expectedYoung.end());
    if (listed != expectedYoung)
    {
        ReportValidationFailure("the young list differs from the young spans of a walk of all spans");
    }
}

LxrLargeObjectResult LxrLargeObjectSpace::SweepNursery(size_t* releasedBytes)
{
    if (releasedBytes == nullptr)
    {
        return LxrLargeObjectResult::InvalidArgument;
    }
    *releasedBytes = 0;
    // The dead nursery spans are taken out of the active list under the lock, and then released outside of it.
    Span* detached = nullptr;
    LxrLargeObjectResult walk = LxrLargeObjectResult::Success;
    {
        LockScope lock(this);
        if (m_phase.load(std::memory_order_relaxed) != LxrLargeObjectPhase::Release)
        {
            return LxrLargeObjectResult::InvalidPhase;
        }
        SideState* side = m_side;
        if (side == nullptr || side->legacyWalks)
        {
            walk = SweepAllSpans(&detached, false);
        }
        else
        {
            SweepExpectation expectation;
            if (side->validate)
            {
                CaptureSweepExpectation(&expectation);
            }
            walk = side->youngValid ? SweepYoungList(&detached) : SweepAllSpans(&detached, true);
            if (side->validate)
            {
                CheckSweepExpectation(expectation, detached);
            }
        }
    }

    LxrLargeObjectResult result = walk;
    while (detached != nullptr)
    {
        Span* span = detached;
        detached = span->next;
        span->next = nullptr;
        if (result != LxrLargeObjectResult::Success)
        {
            AbortSpanRelease(span);
            RequeueYoung(span);
            continue;
        }
        size_t bytes = span->page_count.load(std::memory_order_relaxed) * PageBytes;
        result = FinishSpanRelease(span);
        if (result == LxrLargeObjectResult::Success)
        {
            *releasedBytes += bytes;
        }
        else if (span->state.load(std::memory_order_acquire) == SpanState::Active)
        {
            RequeueYoung(span);
        }
    }
    return result;
}

uintptr_t LxrLargeObjectSpace::GetReservationStart() const
{
    return reinterpret_cast<uintptr_t>(m_reservation);
}

size_t LxrLargeObjectSpace::GetReservationBytes() const
{
    return m_reservation_bytes;
}

LxrLargeObjectResult LxrLargeObjectSpace::GetAllocation(
    uintptr_t object,
    LxrLargeObjectAllocation* allocation) const
{
    if (allocation == nullptr)
    {
        return LxrLargeObjectResult::InvalidArgument;
    }

    if (m_phase.load(std::memory_order_acquire) == LxrLargeObjectPhase::Shutdown)
    {
        return LxrLargeObjectResult::NotInitialized;
    }
    SpanSnapshot span;
    if (!FindSpan(object, &span, nullptr) || span.state != SpanState::Active || span.object != object)
    {
        return LxrLargeObjectResult::NotFound;
    }
    allocation->object = span.object;
    allocation->page_start = span.page_start;
    allocation->object_bytes = span.object_bytes;
    allocation->page_count = span.page_count;
    allocation->padding_bytes = span.padding_bytes;
    allocation->owner = span.owner;
    return LxrLargeObjectResult::Success;
}

LxrLargeObjectResult LxrLargeObjectSpace::GetContainingAllocation(
    uintptr_t address,
    LxrLargeObjectAllocation* allocation) const
{
    if (allocation == nullptr)
    {
        return LxrLargeObjectResult::InvalidArgument;
    }
    if (m_phase.load(std::memory_order_acquire) == LxrLargeObjectPhase::Shutdown)
    {
        return LxrLargeObjectResult::NotInitialized;
    }
    SpanSnapshot span;
    if (!FindSpan(address, &span, nullptr) || span.state != SpanState::Active ||
        address < span.object || address - span.object >= span.object_bytes)
    {
        return LxrLargeObjectResult::NotFound;
    }
    *allocation = {span.object, span.page_start, span.object_bytes,
        span.page_count, span.padding_bytes, span.owner};
    return LxrLargeObjectResult::Success;
}

LxrLargeObjectResult LxrLargeObjectSpace::ValidateStructure(size_t* activeSpans, size_t* freePages) const
{
    if (activeSpans == nullptr || freePages == nullptr)
    {
        return LxrLargeObjectResult::InvalidArgument;
    }
    LockScope lock(this);
    if (m_page_map == nullptr)
    {
        return LxrLargeObjectResult::NotInitialized;
    }
    const LxrLargeObjectResult bad = LxrLargeObjectResult::MetadataFailure;
    bool quiet = m_inflight_allocations.load(std::memory_order_acquire) == 0 &&
        m_pending_releases.load(std::memory_order_acquire) == 0 && m_quarantined_spans == nullptr;

    for (size_t node = 1; node < m_tree_leaves; node++)
    {
        uint32_t left = m_free_tree[2 * node];
        uint32_t right = m_free_tree[2 * node + 1];
        if (m_free_tree[node] != (left > right ? left : right))
        {
            return bad;
        }
    }

    size_t listed = 0;
    for (Span* span = m_active_spans; span != nullptr; span = span->next)
    {
        if (!span->linked || (span->next != nullptr && span->next->previous != span))
        {
            return bad;
        }
        listed++;
    }

    size_t pages = m_admitted_bytes.load(std::memory_order_relaxed) / PageBytes;
    size_t active = 0;
    size_t free = 0;
    bool previousFree = false;
    for (size_t page = 0; page < pages;)
    {
        uintptr_t entry = m_page_map[page].load(std::memory_order_relaxed);
        size_t leaf = FreeLeaf(page);
        if (entry != 0 && !IsFreeTag(entry))
        {
            Span* span = reinterpret_cast<Span*>(entry);
            size_t count = span->page_count.load(std::memory_order_relaxed);
            SpanState state = span->state.load(std::memory_order_relaxed);
            if (leaf != 0 || count == 0 || page + count > pages ||
                PageIndex(span->page_start.load(std::memory_order_relaxed)) != page ||
                state != SpanState::Active || !span->linked)
            {
                return bad;
            }
            for (size_t index = 0; index < count; index++)
            {
                if (m_page_map[page + index].load(std::memory_order_relaxed) != entry)
                {
                    return bad;
                }
            }
            active++;
            previousFree = false;
            page += count;
        }
        else if (leaf != 0)
        {
            if (previousFree || page + leaf > pages ||
                m_page_map[page + leaf - 1].load(std::memory_order_relaxed) != FreeTag(page))
            {
                return bad;
            }
            for (size_t index = 0; index + 1 < leaf; index++)
            {
                if (m_page_map[page + index].load(std::memory_order_relaxed) != 0)
                {
                    return bad;
                }
            }
            free += leaf;
            previousFree = true;
            page += leaf;
        }
        else
        {
            if (quiet)
            {
                return bad;
            }
            previousFree = false;
            page++;
        }
    }
    if (quiet && active != listed)
    {
        return bad;
    }
    *activeSpans = active;
    *freePages = free;
    return LxrLargeObjectResult::Success;
}
