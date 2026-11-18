// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Laxori: the private tier. Thread-owned regions with per-object escape tracking and
// a thread-local mark, plan, update and compaction (the algorithms of Satori's SatoriRegion, on Immix blocks).

#include "common.h"
#include "gcenv.h"
#include "laxori_gc.h"
#include "gcscan.h"
#include "gcref.h"
#include "laxori_write_barrier.h"
#include "laxori_platform.h"

#include <algorithm>
#include <vector>

namespace
{
    constexpr size_t MinUsefulGap = 1024;                     // a smaller gap is not opened for allocation
    constexpr unsigned IndexShift = 11;                       // one index entry for each 2 KiB
    constexpr uint32_t SblkIsHashOrSyncBlockIndex = 0x08000000;
    constexpr uint32_t SblkIsHashCode = 0x04000000;

    inline uintptr_t CurrentThreadTag()
    {
        return LaxoriCurrentThreadTag();
    }

    inline size_t LocalSize(Object* object)
    {
        return (object->GetSize() + sizeof(uintptr_t) - 1) & ~(sizeof(uintptr_t) - 1);
    }

    // The "reserved bits" word of the object header carries the mark-stack link and the relocation distance. The low two
    // bits belong to the engine (the header bits of the GC interface), so the value is kept in the other thirty.
    inline uint32_t& PadWord(Object* object)
    {
        return *(reinterpret_cast<uint32_t*>(object) - 2);
    }
    inline int32_t GetPad(Object* object)
    {
        return static_cast<int32_t>(PadWord(object) >> 2);
    }
    inline void SetPad(Object* object, int32_t value)
    {
        PadWord(object) = (PadWord(object) & 3u) | (static_cast<uint32_t>(value) << 2);
    }

    inline bool HasSyncBlock(Object* object)
    {
        uint32_t bits = object->GetHeader()->GetBits();
        return (bits & SblkIsHashOrSyncBlockIndex) != 0 && (bits & SblkIsHashCode) == 0;
    }

    // Appends the dead range [start, end) (header addresses) to a gap list. A range is cut at the block boundaries: a gap lies
    // inside one block. An empty range adds nothing. Returns false when the list cannot grow.
    struct GapList
    {
        LaxoriPrivateGap* items = nullptr;
        uint32_t count = 0;
        uint32_t capacity = 0;
    };

    inline bool AppendGaps(GapList& list, uintptr_t start, uintptr_t end)
    {
        while (start < end)
        {
            uintptr_t boundary = (start | ImmixBlockGeometry::BlockMask) + 1;
            uintptr_t stop = end < boundary ? end : boundary;
            if (list.count == list.capacity)
            {
                uint32_t capacity = list.capacity == 0 ? 16 : list.capacity * 2;
                LaxoriPrivateGap* grown = static_cast<LaxoriPrivateGap*>(realloc(list.items, capacity * sizeof(LaxoriPrivateGap)));
                if (grown == nullptr)
                {
                    return false;
                }
                list.items = grown;
                list.capacity = capacity;
            }
            list.items[list.count++] = {start, stop};
            start = stop;
        }
        return true;
    }

    // Calls fn(object, size) for every object of the live stretches of a region, dead objects included, in address order.
    // The region must have no open buffer.
    template <typename Fn>
    inline void WalkRegionObjects(const LaxoriPrivateRegion& region, Fn&& fn)
    {
        uintptr_t cursor = region.start + sizeof(uintptr_t);
        uint32_t gap = 0;
        while (true)
        {
            while (gap < region.gapCount && region.gaps[gap].end + sizeof(uintptr_t) <= cursor)
            {
                gap++;
            }
            if (gap < region.gapCount && region.gaps[gap].start + sizeof(uintptr_t) <= cursor)
            {
                cursor = region.gaps[gap].end + sizeof(uintptr_t);
                gap++;
                continue;
            }
            if (cursor - sizeof(uintptr_t) >= region.parseEnd)
            {
                return;
            }
            Object* object = reinterpret_cast<Object*>(cursor);
            size_t size = LocalSize(object);
            fn(object, size);
            cursor += size;
        }
    }
}

// Diagnostics of the thread-local collector (DOTNET_LAXORITlTimers=1). One accumulator for each collection attempt; it is added to
// the engine metrics at the end of LocalCollect. A null pointer means "off".
struct TlAcc
{
    uint64_t cyc[10] = {};   // Total, Esc, StackMark, Sync, Prop, Plan, StackUpd, ObjUpd, Compact, Failed
    uint64_t cnt[10] = {};   // StackScans, Roots, RootsIn, EscObjs, EscBytes, SyncObjs, PropObjs, PlanObjs, MovedBytes, UpdObjs
};
enum { C_Total, C_Esc, C_StackMark, C_Sync, C_Prop, C_Plan, C_StackUpd, C_ObjUpd, C_Compact, C_Failed };
enum { N_StackScans, N_Roots, N_RootsIn, N_EscObjs, N_EscBytes, N_SyncObjs, N_PropObjs, N_PlanObjs, N_MovedBytes, N_UpdObjs };
thread_local TlAcc* t_acc = nullptr;
inline uint64_t TlNow() { return t_acc != nullptr ? LaxoriReadTimestampCounter() : 0; }
inline void TlAdd(int cyc, uint64_t since) { if (t_acc != nullptr) { t_acc->cyc[cyc] += LaxoriReadTimestampCounter() - since; } }
inline void TlCount(int n, uint64_t amount = 1) { if (t_acc != nullptr) { t_acc->cnt[n] += amount; } }

struct LaxoriGcHeap::LocalGcContext
{
    LaxoriGcHeap* heap;
    LaxoriPrivateRegion* region;
    size_t maxSurvivors;
    bool aborted;
    uint64_t roots = 0;
    uint64_t rootsIn = 0;
};

// ---- bitmap helpers ----

bool LaxoriGcHeap::BitTest(uintptr_t address) const
{
    size_t index = (address - m_arena) >> 3;
    return ((m_private_bits[index >> 6] >> (index & 63)) & 1) != 0;
}

void LaxoriGcHeap::BitSet(uintptr_t address)
{
    size_t index = (address - m_arena) >> 3;
    m_private_bits[index >> 6] |= static_cast<uint64_t>(1) << (index & 63);
}

void LaxoriGcHeap::BitClear(uintptr_t address)
{
    size_t index = (address - m_arena) >> 3;
    m_private_bits[index >> 6] &= ~(static_cast<uint64_t>(1) << (index & 63));
}

bool LaxoriGcHeap::AnyExposed(uintptr_t first, size_t length) const
{
    for (uintptr_t address = first & ~(sizeof(uintptr_t) - 1); address < first + length; address += sizeof(uintptr_t))
    {
        if (BitTest(address))
        {
            return true;
        }
    }
    return false;
}

// The next object whose Marked bit is set at an object pointer in [from, limit). Bits inside an object that the caller has
// passed (the Escaped bit, exposed slots, the Pinned bit) are skipped by starting the search after the object.
Object* LaxoriGcHeap::NextMarked(uintptr_t from, uintptr_t limit) const
{
    size_t index = (from - m_arena) >> 3;
    size_t end = (limit - m_arena) >> 3;
    while (index < end)
    {
        uint64_t word = m_private_bits[index >> 6] >> (index & 63);
        if (word == 0)
        {
            index = ((index >> 6) + 1) << 6;
            continue;
        }
        index += LaxoriCountTrailingZeros64(word);
        if (index >= end)
        {
            return nullptr;
        }
        return reinterpret_cast<Object*>(m_arena + (index << 3));
    }
    return nullptr;
}

bool LaxoriGcHeap::InRegion(const LaxoriPrivateRegion& region, uintptr_t pointer) const
{
    return region.start != 0 && pointer - (region.start + sizeof(uintptr_t)) < (region.parseEnd - region.start - sizeof(uintptr_t));
}

// An interior pointer can point into the last word of the last object of the region.
bool LaxoriGcHeap::InRegionInterior(const LaxoriPrivateRegion& region, uintptr_t pointer) const
{
    return region.start != 0 && pointer - (region.start + sizeof(uintptr_t)) < (region.parseEnd - region.start);
}

// ---- first-object index ----

// The object that contains `location`, or null when the location is in a gap. The objects of a region are contiguous
// between gaps, so the walk starts at the object of the index (or at the first object of the region) and steps over the gaps.
// While the runtime buffer of the thread is open, the part of its gap that the thread has not used yet starts at `liveCursor`.
Object* LaxoriGcHeap::FindObject(const LaxoriPrivateRegion& region, uintptr_t location, uintptr_t liveCursor)
{
    const uintptr_t first = region.start + sizeof(uintptr_t);
    const uintptr_t limit = region.parseEnd + sizeof(uintptr_t);
    if (region.start == 0 || location < first || location >= limit)
    {
        return nullptr;
    }
    auto gapStart = [&](uint32_t i) -> uintptr_t
    {
        return (region.buffer && i + 1 == region.gapCurrent) ? liveCursor : region.gaps[i].start;
    };
    // Gaps are in pointer space [start + 8, end + 8). Moves a pointer that is in a gap to the first pointer after it.
    uint32_t gap = 0;
    auto skipGaps = [&](uintptr_t pointer) -> uintptr_t
    {
        while (true)
        {
            while (gap < region.gapCount && region.gaps[gap].end + sizeof(uintptr_t) <= pointer)
            {
                gap++;
            }
            if (gap < region.gapCount && gapStart(gap) + sizeof(uintptr_t) <= pointer)
            {
                pointer = region.gaps[gap].end + sizeof(uintptr_t);
                gap++;
                continue;
            }
            return pointer;
        }
    };
    if (skipGaps(location) != location)
    {
        return nullptr;
    }
    gap = 0;
    uint32_t* index = m_private_index + ((region.start - m_arena) >> IndexShift);
    size_t granule = (location - region.start) >> IndexShift;
    uintptr_t current = first;
    for (size_t i = granule; i > 0; i--)
    {
        uint32_t offset = index[i];
        if (offset != 0)
        {
            uintptr_t candidate = region.start + offset;
            if (candidate >= first && candidate <= location)
            {
                current = candidate;
            }
            break;
        }
    }
    size_t granules = (region.end - region.start) >> IndexShift;
    while (true)
    {
        current = skipGaps(current);
        if (current >= limit || current > location)
        {
            return nullptr;
        }
        Object* object = reinterpret_cast<Object*>(current);
        size_t size = LocalSize(object);
        uintptr_t next = current + size;
        if (location < next)
        {
            return object;
        }
        size_t firstGranule = (current - region.start) >> IndexShift;
        size_t lastGranule = (next - region.start) >> IndexShift;
        for (size_t i = firstGranule + 1; i <= lastGranule && i < granules; i++)
        {
            index[i] = static_cast<uint32_t>(current - region.start);
        }
        current = next;
    }
}

// ---- escape ----

void LaxoriGcHeap::PushMarkStack(LaxoriPrivateRegion& region, Object* object)
{
    if (!object->GetGCSafeMethodTable()->ContainsGCPointers())
    {
        return;
    }
    SetPad(object, region.markStack);
    region.markStack = static_cast<int32_t>(reinterpret_cast<uintptr_t>(object) - region.start);
}

Object* LaxoriGcHeap::PopMarkStack(LaxoriPrivateRegion& region)
{
    if (region.markStack == 0)
    {
        return nullptr;
    }
    Object* object = reinterpret_cast<Object*>(region.start + region.markStack);
    region.markStack = GetPad(object);
    SetPad(object, 0);
    return object;
}

void LaxoriGcHeap::EscapeRecursively(LaxoriPrivateRegion& region, Object* object)
{
    uintptr_t address = reinterpret_cast<uintptr_t>(object);
    if (BitTest(address + sizeof(uintptr_t)))
    {
        return;
    }
    BitSet(address + sizeof(uintptr_t));
    size_t escaped = LocalSize(object);
    uint64_t objects = 1;
    do
    {
        GCReferenceRangeIterator iterator(object);
        GCReferenceRange range;
        while (iterator.Next(&range))
        {
            for (size_t i = 0; i < range.count; i++)
            {
                Object** slot = range.start + i;
                BitSet(reinterpret_cast<uintptr_t>(slot));
                Object* child = *slot;
                uintptr_t childAddress = reinterpret_cast<uintptr_t>(child);
                if (InRegion(region, childAddress) && !BitTest(childAddress + sizeof(uintptr_t)) && m_test_control != 3)
                {
                    BitSet(childAddress + sizeof(uintptr_t));
                    escaped += LocalSize(child);
                    objects++;
                    PushMarkStack(region, child);
                }
            }
        }
        object = PopMarkStack(region);
    } while (object != nullptr);
    region.escapedSize += escaped;
    m_engine.CountRegionMetric(LaxoriReferenceCounting::EscapedObjects, objects);
    m_engine.CountRegionMetric(LaxoriReferenceCounting::EscapedBytes, escaped);
}

// The object may be in the region of the current thread. The caller is in cooperative mode.
void LaxoriGcHeap::EscapeInCurrentRegion(Object** destination, Object* reference)
{
    gc_alloc_context* context = GCToEEInterface::GetAllocContext();
    LaxoriRcMutator* mutator =
        context == nullptr ? nullptr : m_engine.FindCachedMutator(context->gc_reserved_1, context);
    m_engine.CountRegionMetric(LaxoriReferenceCounting::EscapeSlowCalls);
    if (mutator == nullptr)
    {
        m_engine.CountRegionMetric(LaxoriReferenceCounting::ForeignEscapes);
        return;
    }
    LaxoriPrivateRegion& region = mutator->Region();
    uintptr_t address = reinterpret_cast<uintptr_t>(reference);
    if (!InRegion(region, address))
    {
        m_engine.CountRegionMetric(LaxoriReferenceCounting::ForeignEscapes);
        return;
    }
    (void)destination;
    EscapeRecursively(region, reference);
    if (region.escapedSize > m_region_bytes / 4)
    {
        DissolveCurrentRegion(context, mutator, LaxoriReferenceCounting::RegionsDissolvedEscape);
    }
}

void LaxoriGcHeap::EscapeSlowPath(Object** slot, Object* reference)
{
    s_heap->EscapeInCurrentRegion(slot, reference);
}

extern "C" void LaxoriEscapeSlowPath(Object** slot, Object* reference)
{
    LaxoriGcHeap::EscapeSlowPath(slot, reference);
}

// C++ version of the barrier test, for stores of the runtime (pre_store). Does the same as the escape part of the assembly
// helper, and enters cooperative mode when the caller is not in it.
void LaxoriGcHeap::EscapeCheck(Object** destination, Object* reference)
{
    uintptr_t offset = reinterpret_cast<uintptr_t>(reference) - m_arena;
    if (offset >= LaxoriLoadAcquire(&m_private_heap_bytes) ||
        LaxoriLoadAcquire(&m_region_owner[offset >> ImmixBlockGeometry::BlockLogBytes]) !=
        CurrentThreadTag())
    {
        return;
    }
    bool wasPreemptive = !GCToEEInterface::IsPreemptiveGCDisabled();
    if (wasPreemptive)
    {
        GCToEEInterface::DisablePreemptiveGC();
    }
    // A pause may have dissolved the region while the thread was preemptive: look again.
    if (LaxoriLoadAcquire(&m_region_owner[offset >> ImmixBlockGeometry::BlockLogBytes]) == CurrentThreadTag())
    {
        uintptr_t dst = reinterpret_cast<uintptr_t>(destination);
        uintptr_t src = reinterpret_cast<uintptr_t>(reference);
        if (((dst ^ src) >> m_region_shift) != 0 || BitTest(dst))
        {
            EscapeInCurrentRegion(destination, reference);
        }
    }
    if (wasPreemptive)
    {
        GCToEEInterface::EnablePreemptiveGC();
    }
}

void LaxoriGcHeap::EscapeObject(Object* object)
{
    LaxoriGcHeap* heap = s_heap;
    if (heap == nullptr || object == nullptr)
    {
        return;
    }
    uintptr_t offset = reinterpret_cast<uintptr_t>(object) - heap->m_arena;
    if (offset >= LaxoriLoadAcquire(&heap->m_private_heap_bytes) ||
        LaxoriLoadAcquire(&heap->m_region_owner[offset >> ImmixBlockGeometry::BlockLogBytes]) !=
            CurrentThreadTag())
    {
        return;
    }
    bool wasPreemptive = !GCToEEInterface::IsPreemptiveGCDisabled();
    if (wasPreemptive)
    {
        GCToEEInterface::DisablePreemptiveGC();
    }
    if (LaxoriLoadAcquire(&heap->m_region_owner[offset >> ImmixBlockGeometry::BlockLogBytes]) ==
        CurrentThreadTag())
    {
        heap->EscapeInCurrentRegion(nullptr, object);
    }
    if (wasPreemptive)
    {
        GCToEEInterface::EnablePreemptiveGC();
    }
}

void LaxoriEscapeObject(Object* object)
{
    LaxoriGcHeap::EscapeObject(object);
}

// A bulk copy of references into `destination`: every reference of the source that is an object of the private region of
// the current thread escapes, unless the destination is private to that region and not exposed. The source words need not be
// references (a struct array), so a word counts only when it is exactly the start of an object.
void LaxoriGcHeap::EscapeBulk(void* destination, const void* source, size_t length)
{
    uintptr_t dst = reinterpret_cast<uintptr_t>(destination);
    if (dst - m_arena >= m_arena_bytes)
    {
        return;
    }
    gc_alloc_context* context = GCToEEInterface::GetAllocContext();
    LaxoriRcMutator* mutator =
        context == nullptr ? nullptr : m_engine.FindCachedMutator(context->gc_reserved_1, context);
    if (mutator == nullptr || mutator->Region().start == 0)
    {
        return;
    }
    LaxoriPrivateRegion& region = mutator->Region();
    bool destinationInRegion = (dst - region.start) < (region.end - region.start);
    if (destinationInRegion && !AnyExposed(dst, length))
    {
        return;
    }
    uintptr_t first = (reinterpret_cast<uintptr_t>(source) + sizeof(uintptr_t) - 1) & ~(sizeof(uintptr_t) - 1);
    uintptr_t last = (reinterpret_cast<uintptr_t>(source) + length) & ~(sizeof(uintptr_t) - 1);
    uintptr_t liveCursor = region.buffer ? reinterpret_cast<uintptr_t>(context->alloc_ptr) - sizeof(uintptr_t) : region.parseEnd;
    for (uintptr_t cursor = first; cursor < last; cursor += sizeof(uintptr_t))
    {
        uintptr_t word = *reinterpret_cast<const uintptr_t*>(cursor);
        if (!InRegion(region, word) || BitTest(word + sizeof(uintptr_t)))
        {
            continue;
        }
        Object* found = FindObject(region, word, liveCursor);
        if (found == reinterpret_cast<Object*>(word))
        {
            m_engine.CountRegionMetric(LaxoriReferenceCounting::BulkEscapes);
            EscapeRecursively(region, found);
            if (region.escapedSize > m_region_bytes / 4)
            {
                DissolveCurrentRegion(context, mutator, LaxoriReferenceCounting::RegionsDissolvedEscape);
                return;
            }
        }
    }
}

// ---- allocation ----

void LaxoriGcHeap::SetBufferLimits(gc_alloc_context* context, uintptr_t cursor, uintptr_t limit)
{
    context->alloc_ptr = reinterpret_cast<uint8_t*>(cursor + sizeof(ObjHeader));
    context->alloc_limit = reinterpret_cast<uint8_t*>(limit + sizeof(ObjHeader));
    SetCombinedLimit(context, context->alloc_limit);
    context->alloc_bytes += static_cast<int64_t>(limit - cursor);
    m_context_allocated_bytes.fetch_add(limit - cursor, std::memory_order_relaxed);
}

// Opens gap `gapIndex` as the runtime buffer. A gap lies inside one block, so no object that the thread allocates in the buffer
// crosses a block boundary. The memory of a gap holds the rubbish of a collection and is cleared here (the gaps of a new region
// are zero already).
void LaxoriGcHeap::OpenGap(gc_alloc_context* context, LaxoriRcMutator* mutator, uint32_t gapIndex)
{
    LaxoriPrivateRegion& region = mutator->Region();
    const LaxoriPrivateGap gap = region.gaps[gapIndex];
    if (!region.freshGaps && m_test_control != 4)
    {
        uint64_t zeroStart = m_tl_timers ? LaxoriReadTimestampCounter() : 0;
        memset(reinterpret_cast<void*>(gap.start), 0, gap.end - gap.start);
        if (m_tl_timers)
        {
            m_engine.CountRegionMetric(LaxoriReferenceCounting::TlGapZeroCyc, LaxoriReadTimestampCounter() - zeroStart);
            m_engine.CountRegionMetric(LaxoriReferenceCounting::TlGapZeroBytes, gap.end - gap.start);
        }
    }
    uint32_t* index = m_private_index + ((region.start - m_arena) >> IndexShift);
    size_t granules = (region.end - region.start) >> IndexShift;
    for (size_t i = ((gap.start - region.start) >> IndexShift) + 1; i <= ((gap.end - region.start) >> IndexShift) && i < granules; i++)
    {
        index[i] = 0;
    }
    region.gapCurrent = gapIndex + 1;
    if (m_engine.OpenRegionBuffer(mutator, gap.start, gap.end) != LaxoriRcResult::Success)
    {
        Fatal("Laxori could not open a gap of a private region.");
    }
    SetBufferLimits(context, gap.start, gap.end);
}

void LaxoriGcHeap::DissolveCurrentRegion(gc_alloc_context* context, LaxoriRcMutator* mutator, size_t reason)
{
    LaxoriPrivateRegion& region = mutator->Region();
    if (region.start == 0)
    {
        return;
    }
    if (region.buffer)
    {
        RetireAllocationSpan(context, true);
    }
    m_engine.CountRegionMetric(reason);
    if (m_engine.DissolveRegion(mutator, region.parseEnd) != LaxoriRcResult::Success)
    {
        Fatal("Laxori could not dissolve a private region.");
    }
}

Object* LaxoriGcHeap::AllocPrivateSlow(gc_alloc_context* context, LaxoriRcMutator* mutator, size_t size)
{
    LaxoriPrivateRegion& region = mutator->Region();
    for (int attempt = 0; attempt < 8; attempt++)
    {
        if (region.start != 0)
        {
            if (region.buffer)
            {
                if (static_cast<size_t>(context->alloc_limit - context->alloc_ptr) >= size)
                {
                    Object* object = reinterpret_cast<Object*>(context->alloc_ptr);
                    context->alloc_ptr += size;
                    return object;
                }
                RetireAllocationSpan(context, true);
            }
            while (region.gapNext < region.gapCount)
            {
                uint32_t gapIndex = region.gapNext++;
                size_t length = region.gaps[gapIndex].end - region.gaps[gapIndex].start;
                if (length >= MinUsefulGap && length >= size)
                {
                    OpenGap(context, mutator, gapIndex);
                    Object* object = reinterpret_cast<Object*>(context->alloc_ptr);
                    context->alloc_ptr += size;
                    return object;
                }
            }
            if (LocalCollect(mutator))
            {
                continue;
            }
            DissolveCurrentRegion(context, mutator, LaxoriReferenceCounting::RegionsDissolvedFull);
        }
        if (m_shutdown.load(std::memory_order_acquire))
        {
            return nullptr;
        }
        // The thread has no region. A buffer that it still holds is a buffer of the shared engine: close it (a region can be
        // taken only when no buffer is published). After a failure the thread allocates in the shared engine, with the fast
        // buffer of LXR, until a pause has freed blocks or a number of slow allocations have passed.
        if (context->alloc_ptr != nullptr)
        {
            RetireAllocationSpan(context, true);
        }
        if (mutator->AcquireEpoch() == m_collections.load(std::memory_order_relaxed) && --mutator->AcquireSkip() > 0)
        {
            return nullptr;
        }
        if (GetCollectionCause(false) != LaxoriCollectionCause::None)
        {
            if (CollectForPressure(false, false) != LaxoriRcResult::Success)
            {
                Fatal("LAXORI allocation-pressure collection failed.");
            }
        }
        bool taken = false;
        if (m_engine.AcquireRegion(mutator, m_region_blocks, &taken) != LaxoriRcResult::Success)
        {
            Fatal("Laxori could not take a private region.");
        }
        if (!taken)
        {
            mutator->SetAcquireFailure(m_collections.load(std::memory_order_relaxed), 512);
            return nullptr;
        }
        size_t firstBit = (region.start - m_arena) >> 9;
        memset(&m_private_bits[firstBit], 0, m_region_bytes >> 6);
        memset(m_private_index + ((region.start - m_arena) >> IndexShift), 0, (m_region_bytes >> IndexShift) * sizeof(uint32_t));
        region.parseEnd = region.end - sizeof(uintptr_t);
        {
            GapList list;
            if (!AppendGaps(list, region.start, region.parseEnd))
            {
                Fatal("Laxori could not allocate the gap list of a private region.");
            }
            region.gaps = list.items;
            region.gapCount = list.count;
            region.gapCapacity = list.capacity;
            region.gapNext = 0;
            region.gapCurrent = 0;
            region.freshGaps = true;
        }
        if (m_verify_private)
        {
            VerifyRegion(region, "acquire");
        }
        OpenGap(context, mutator, region.gapNext++);
        Object* object = reinterpret_cast<Object*>(context->alloc_ptr);
        context->alloc_ptr += size;
        return object;
    }
    return nullptr;
}

// ---- thread-local collection ----

void LaxoriGcHeap::MarkFn(Object** slot, ScanContext* scan, uint32_t flags)
{
    LocalGcContext* local = static_cast<LocalGcContext*>(scan->_unused1);
    LaxoriGcHeap* heap = local->heap;
    LaxoriPrivateRegion& region = *local->region;
    uintptr_t location = reinterpret_cast<uintptr_t>(*slot);
    bool interior = (flags & GC_CALL_INTERIOR) != 0;
    local->roots++;
    if (!(interior ? heap->InRegionInterior(region, location) : heap->InRegion(region, location)))
    {
        return;
    }
    local->rootsIn++;
    Object* object = reinterpret_cast<Object*>(location);
    if (interior)
    {
        object = heap->FindObject(region, location, region.parseEnd);
        if (object == nullptr)
        {
            return;
        }
    }
    uintptr_t address = reinterpret_cast<uintptr_t>(object);
    if (!heap->BitTest(address))
    {
        heap->BitSet(address);
        region.survivors += LocalSize(object);
        if (HasSyncBlock(object) && !heap->BitTest(address + sizeof(uintptr_t)))
        {
            heap->BitSet(address + 2 * sizeof(uintptr_t));
        }
        heap->PushMarkStack(region, object);
    }
    if ((flags & GC_CALL_PINNED) != 0 && !heap->BitTest(address + sizeof(uintptr_t)))
    {
        heap->BitSet(address + 2 * sizeof(uintptr_t));
    }
}

void LaxoriGcHeap::UpdateFn(Object** slot, ScanContext* scan, uint32_t flags)
{
    LocalGcContext* local = static_cast<LocalGcContext*>(scan->_unused1);
    LaxoriGcHeap* heap = local->heap;
    if (heap->m_test_control == 1)
    {
        return;
    }
    LaxoriPrivateRegion& region = *local->region;
    uintptr_t location = reinterpret_cast<uintptr_t>(*slot);
    bool interior = (flags & GC_CALL_INTERIOR) != 0;
    local->roots++;
    if (!(interior ? heap->InRegionInterior(region, location) : heap->InRegion(region, location)))
    {
        return;
    }
    local->rootsIn++;
    Object* object = reinterpret_cast<Object*>(location);
    if (interior)
    {
        object = heap->FindObject(region, location, region.parseEnd);
        if (object == nullptr)
        {
            // A byref one past the end of a live object (the end pointer of an array loop) lies in a gap or at the end of the
            // region. It did not keep the object alive, but it moves with the object, as in LaxoriReferenceCounting::RelocateRoot.
            Object* previous = location % sizeof(uintptr_t) == 0 ?
                heap->FindObject(region, location - sizeof(uintptr_t), region.parseEnd) : nullptr;
            if (previous != nullptr && heap->BitTest(reinterpret_cast<uintptr_t>(previous)) &&
                reinterpret_cast<uintptr_t>(previous) + LocalSize(previous) == location)
            {
                int32_t move = GetPad(previous);
                if (move != 0)
                {
                    *slot = reinterpret_cast<Object*>(location - static_cast<uintptr_t>(move));
                }
            }
            return;
        }
    }
    int32_t relocation = GetPad(object);
    if (relocation != 0)
    {
        *slot = reinterpret_cast<Object*>(location - static_cast<uintptr_t>(relocation));
    }
}

void LaxoriGcHeap::ClearPads(LaxoriPrivateRegion& region)
{
    for (Object* object = NextMarked(region.start + sizeof(uintptr_t), region.parseEnd); object != nullptr;
         object = NextMarked(reinterpret_cast<uintptr_t>(object) + LocalSize(object), region.parseEnd))
    {
        SetPad(object, 0);
    }
}

bool LaxoriGcHeap::LocalMark(LaxoriPrivateRegion& region)
{
    const uintptr_t first = region.start + sizeof(uintptr_t);
    const uintptr_t limit = region.parseEnd;
    // Escaped objects are roots. At this point only Escaped bits and exposed slots are set, so the first set bit after an
    // object is the Escaped bit of the next escaped object (at its pointer plus one word).
    uint64_t t0 = TlNow();
    size_t index = (first - m_arena) >> 3;
    size_t end = (limit - m_arena) >> 3;
    uint64_t escObjects = 0;
    uint64_t escBytes = 0;
    while (index < end)
    {
        uint64_t word = m_private_bits[index >> 6] >> (index & 63);
        if (word == 0)
        {
            index = ((index >> 6) + 1) << 6;
            continue;
        }
        index += LaxoriCountTrailingZeros64(word);
        if (index >= end)
        {
            break;
        }
        Object* object = reinterpret_cast<Object*>(m_arena + ((index - 1) << 3));
        BitSet(reinterpret_cast<uintptr_t>(object));
        size_t escSize = LocalSize(object);
        escObjects++;
        escBytes += escSize;
        index = (reinterpret_cast<uintptr_t>(object) + escSize - m_arena) >> 3;
    }
    TlAdd(C_Esc, t0);
    TlCount(N_EscObjs, escObjects);
    TlCount(N_EscBytes, escBytes);

    LocalGcContext local{this, &region, m_region_bytes / 8, false};
    ScanContext scan;
    scan.promotion = TRUE;
    scan._unused1 = &local;
    t0 = TlNow();
    GCToEEInterface::GcScanCurrentStackRoots(reinterpret_cast<promote_func*>(MarkFn), &scan);
    TlAdd(C_StackMark, t0);
    TlCount(N_StackScans);
    TlCount(N_Roots, local.roots);
    TlCount(N_RootsIn, local.rootsIn);

    // The sync-block table of the runtime holds raw pointers to the objects that have a sync block, dead or alive, and it is
    // updated only by a pause, so an object with a sync block must stay where it is. The runtime creates a sync block through
    // pre_store (SyncBlockCache::NewSyncBlockSlot), which escapes the object (PreStore), and an escaped object is a root that does not
    // move. The walk of the whole region (every object, dead ones too) that found such objects before is kept for A/B tests only
    // (DOTNET_LAXORISyncWalk=1); it was 40 to 85 percent of the cost of a collection.
    t0 = TlNow();
    uint64_t walked = 0;
    if (m_sync_walk)
    {
        WalkRegionObjects(region, [&](Object* walkedObject, size_t size)
        {
            if (m_test_control == 5)
            {
                return;
            }
            walked++;
            uintptr_t address = reinterpret_cast<uintptr_t>(walkedObject);
            if (!BitTest(address) && HasSyncBlock(walkedObject))
            {
                BitSet(address);
                BitSet(address + 2 * sizeof(uintptr_t));
                region.survivors += size;
                PushMarkStack(region, walkedObject);
            }
        });
    }
    TlAdd(C_Sync, t0);
    TlCount(N_SyncObjs, walked);

    t0 = TlNow();
    uint64_t propagated = 0;
    Object* object = PopMarkStack(region);
    while (object != nullptr)
    {
        if (region.survivors >= local.maxSurvivors)
        {
            local.aborted = true;
            while (PopMarkStack(region) != nullptr)
            {
            }
            TlAdd(C_Prop, t0);
            TlCount(N_PropObjs, propagated);
            return false;
        }
        propagated++;
        GCReferenceRangeIterator iterator(object);
        GCReferenceRange range;
        while (iterator.Next(&range))
        {
            for (size_t i = 0; i < range.count; i++)
            {
                Object* child = range.start[i];
                uintptr_t address = reinterpret_cast<uintptr_t>(child);
                if (InRegion(region, address) && !BitTest(address))
                {
                    BitSet(address);
                    region.survivors += LocalSize(child);
                    if (HasSyncBlock(child) && !BitTest(address + sizeof(uintptr_t)))
                    {
                        BitSet(address + 2 * sizeof(uintptr_t));
                    }
                    PushMarkStack(region, child);
                }
            }
        }
        object = PopMarkStack(region);
    }
    TlAdd(C_Prop, t0);
    TlCount(N_PropObjs, propagated);
    return region.survivors < local.maxSurvivors;
}

// Gives every movable marked object the distance that it slides (stored in its header) and plans the gaps that the collection
// leaves (region.nextGaps). Movable objects slide towards the start of the region, one after another, in address order, so the
// destination of an object is never above its source. An unmovable object (escaped, pinned, or with a sync block) stays. An
// object never lands across a block boundary: it goes to the start of the next block, or stays where it is when that is not
// below its source. The space between two objects is a gap. Returns the bytes of the gaps that are worth opening.
bool LaxoriGcHeap::LocalPlan(LaxoriPrivateRegion& region, size_t* freeBytes)
{
    const uintptr_t first = region.start + sizeof(uintptr_t);
    const uintptr_t limit = region.parseEnd;
    GapList list;
    list.items = region.nextGaps;
    list.capacity = region.nextGapCapacity;
    list.count = 0;
    auto store = [&]()
    {
        region.nextGaps = list.items;
        region.nextGapCapacity = list.capacity;
        region.nextGapCount = list.count;
    };
    uintptr_t destination = region.start;   // header address where the next object lands
    uint64_t planObjects = 0;
    uint64_t movedBytes = 0;
    for (Object* object = NextMarked(first, limit); object != nullptr;)
    {
        planObjects++;
        uintptr_t address = reinterpret_cast<uintptr_t>(object);
        uintptr_t header = address - sizeof(uintptr_t);
        size_t size = LocalSize(object);
        bool unmovable = BitTest(address + sizeof(uintptr_t)) || BitTest(address + 2 * sizeof(uintptr_t));
        uintptr_t target = header;
        if (!unmovable)
        {
            target = destination;
            if (((target ^ (target + size - 1)) >> ImmixBlockGeometry::BlockLogBytes) != 0)
            {
                target = (target | ImmixBlockGeometry::BlockMask) + 1;
            }
            if (target > header)
            {
                target = header;
            }
        }
        if (target > destination && !AppendGaps(list, destination, target))
        {
            store();
            return false;
        }
        if (target != header)
        {
            SetPad(object, static_cast<int32_t>(header - target));
            movedBytes += size;
        }
        destination = target + size;
        object = NextMarked(address + size, limit);
    }
    TlCount(N_PlanObjs, planObjects);
    TlCount(N_MovedBytes, movedBytes);
    if (limit > destination && !AppendGaps(list, destination, limit))
    {
        store();
        return false;
    }
    store();
    size_t usable = 0;
    for (uint32_t i = 0; i < list.count; i++)
    {
        size_t length = list.items[i].end - list.items[i].start;
        if (length >= MinUsefulGap)
        {
            usable += length;
        }
    }
    *freeBytes = usable;
    return true;
}

void LaxoriGcHeap::LocalUpdate(LaxoriPrivateRegion& region)
{
    LocalGcContext local{this, &region, 0, false};
    ScanContext scan;
    scan.promotion = FALSE;
    scan._unused1 = &local;
    uint64_t t0 = TlNow();
    GCToEEInterface::GcScanCurrentStackRoots(reinterpret_cast<promote_func*>(UpdateFn), &scan);
    TlAdd(C_StackUpd, t0);
    TlCount(N_StackScans);
    TlCount(N_Roots, local.roots);
    TlCount(N_RootsIn, local.rootsIn);

    t0 = TlNow();
    uint64_t updated = 0;
    const uintptr_t first = region.start + sizeof(uintptr_t);
    for (Object* object = NextMarked(first, region.parseEnd); object != nullptr;)
    {
        updated++;
        uintptr_t address = reinterpret_cast<uintptr_t>(object);
        size_t size = LocalSize(object);
        // An escaped object refers only to escaped objects (and to objects outside the region): nothing to update.
        if (!BitTest(address + sizeof(uintptr_t)) && m_test_control != 2)
        {
            GCReferenceRangeIterator iterator(object);
            GCReferenceRange range;
            while (iterator.Next(&range))
            {
                for (size_t i = 0; i < range.count; i++)
                {
                    Object* child = range.start[i];
                    uintptr_t childAddress = reinterpret_cast<uintptr_t>(child);
                    if (InRegion(region, childAddress))
                    {
                        int32_t relocation = GetPad(child);
                        if (relocation != 0)
                        {
                            range.start[i] = reinterpret_cast<Object*>(childAddress - static_cast<uintptr_t>(relocation));
                        }
                    }
                }
            }
        }
        object = NextMarked(address + size, region.parseEnd);
    }
    TlAdd(C_ObjUpd, t0);
    TlCount(N_UpdObjs, updated);
}

void LaxoriGcHeap::LocalCompact(LaxoriPrivateRegion& region)
{
    uint64_t t0 = TlNow();
    const uintptr_t first = region.start + sizeof(uintptr_t);
    for (Object* object = NextMarked(first, region.parseEnd); object != nullptr;)
    {
        uintptr_t address = reinterpret_cast<uintptr_t>(object);
        size_t size = LocalSize(object);
        uintptr_t next = address + size;
        int32_t relocation = GetPad(object);
        bool escaped = BitTest(address + sizeof(uintptr_t));
        BitClear(address);
        if (!escaped)
        {
            BitClear(address + 2 * sizeof(uintptr_t));
        }
        if (relocation != 0)
        {
            memmove(reinterpret_cast<void*>(address - sizeof(uintptr_t) - static_cast<uintptr_t>(relocation)),
                reinterpret_cast<void*>(address - sizeof(uintptr_t)), size);
            SetPad(reinterpret_cast<Object*>(address - static_cast<uintptr_t>(relocation)), 0);
        }
        object = NextMarked(next, region.parseEnd);
    }
    TlAdd(C_Compact, t0);
}

bool LaxoriGcHeap::LocalCollect(LaxoriRcMutator* mutator)
{
    TlAcc acc;
    t_acc = m_tl_timers ? &acc : nullptr;
    uint64_t start = TlNow();
    bool result = LocalCollectCore(mutator);
    if (t_acc != nullptr)
    {
        acc.cyc[C_Total] = LaxoriReadTimestampCounter() - start;
        if (!result)
        {
            acc.cyc[C_Failed] = acc.cyc[C_Total];
        }
        m_engine.CountRegionMetric(LaxoriReferenceCounting::TlCalls);
        for (int i = 0; i < 10; i++)
        {
            m_engine.CountRegionMetric(LaxoriReferenceCounting::TlCycTotal + i, acc.cyc[i]);
            m_engine.CountRegionMetric(LaxoriReferenceCounting::TlStackScans + i, acc.cnt[i]);
        }
        t_acc = nullptr;
    }
    return result;
}

bool LaxoriGcHeap::LocalCollectCore(LaxoriRcMutator* mutator)
{
    LaxoriPrivateRegion& region = mutator->Region();
    if (m_verify_private)
    {
        VerifyRegion(region, "before-collect");
    }
    if (region.escapedSize > m_region_bytes / 4 || (region.escapedSize != 0 && region.escapedSize > m_escaped_collect_limit))
    {
        m_engine.CountRegionMetric(LaxoriReferenceCounting::LocalSkipped);
        return false;
    }
    if (mutator->LocalSkip() != 0)
    {
        mutator->LocalSkip()--;
        m_engine.CountRegionMetric(LaxoriReferenceCounting::LocalSkipped);
        return false;
    }
    // A collection that does not pay makes the thread skip the next regions (1, 3, 7, 15), so that a thread whose regions are
    // full of live data does not mark them again and again.
    auto notPaid = [&]()
    {
        uint32_t& streak = mutator->LocalStreak();
        if (streak < 4)
        {
            streak++;
        }
        mutator->LocalSkip() = (1u << streak) - 1;
    };
    const size_t before = region.parseEnd - region.start;
    region.survivors = 0;
    region.markStack = 0;
    if (!LocalMark(region))
    {
        notPaid();
        m_engine.CountRegionMetric(LaxoriReferenceCounting::LocalMarkAborted);
        return false;
    }
    size_t freeBytes = 0;
    uint64_t planStart = TlNow();
    bool planned = LocalPlan(region, &freeBytes);
    TlAdd(C_Plan, planStart);
    if (!planned || freeBytes < m_region_bytes / 4)
    {
        notPaid();
        ClearPads(region);
        free(region.nextGaps);
        region.nextGaps = nullptr;
        region.nextGapCount = 0;
        region.nextGapCapacity = 0;
        m_engine.CountRegionMetric(LaxoriReferenceCounting::LocalNotWorth);
        return false;
    }
    mutator->LocalStreak() = 0;
    LocalUpdate(region);
    LocalCompact(region);
    memset(m_private_index + ((region.start - m_arena) >> IndexShift), 0, (m_region_bytes >> IndexShift) * sizeof(uint32_t));
    region.collections++;
    // The planned gaps replace the old ones. They are opened one after another; a gap that is too small stays dead space.
    free(region.gaps);
    region.gaps = region.nextGaps;
    region.gapCount = region.nextGapCount;
    region.gapCapacity = region.nextGapCapacity;
    region.nextGaps = nullptr;
    region.nextGapCount = 0;
    region.nextGapCapacity = 0;
    region.gapNext = 0;
    region.gapCurrent = 0;
    region.freshGaps = false;
    if (m_verify_private)
    {
        VerifyRegion(region, "after-compact");
    }
    m_engine.CountRegionMetric(LaxoriReferenceCounting::LocalCollections);
    m_engine.CountRegionMetric(LaxoriReferenceCounting::LocalSurvivorBytes, region.survivors);
    m_engine.CountRegionMetric(LaxoriReferenceCounting::LocalReclaimedBytes, before > freeBytes ? freeBytes : before);
    return true;
}

// ---- initialization of the tier ----

// Debug aid (DOTNET_LAXORIVerifyPrivate=1): walks the live objects of a region (it steps over the gaps) and checks the invariants:
// an object lies inside one block and outside the gaps, the gaps are sorted and inside one block, the unlog bits are clear, and
// every reference that points into the region points to the start of a live object.
void LaxoriGcHeap::VerifyRegion(LaxoriPrivateRegion& region, const char* where)
{
    // The unlog bits of a private region must all be clear: its objects are young.
    {
        const uint8_t* metadata = reinterpret_cast<const uint8_t*>(g_laxori_write_barrier_globals.metadata_base);
        for (uintptr_t address = region.start; address < region.end; address += 64)
        {
            if (metadata[address >> 6] != 0)
            {
                fprintf(stderr, "LAXORI verify(%s): unlog byte %02x at %p inside private region %p (end %p)\n", where,
                    metadata[address >> 6], (void*)address, (void*)region.start, (void*)region.end);
                abort();
            }
        }
    }
    if (strcmp(where, "acquire") == 0)
    {
        return;
    }
    uintptr_t previousEnd = region.start;
    for (uint32_t i = 0; i < region.gapCount; i++)
    {
        const LaxoriPrivateGap& gap = region.gaps[i];
        if (gap.start < previousEnd || gap.end < gap.start || gap.end > region.parseEnd ||
            (gap.end > gap.start && ((gap.start ^ (gap.end - 1)) >> ImmixBlockGeometry::BlockLogBytes) != 0))
        {
            fprintf(stderr, "LAXORI verify(%s): bad gap %u [%p, %p) after %p in region %p\n", where, i, (void*)gap.start,
                (void*)gap.end, (void*)previousEnd, (void*)region.start);
            abort();
        }
        previousEnd = gap.end;
    }
    std::vector<uintptr_t> starts;
    uintptr_t cursor = region.start + sizeof(uintptr_t);
    uint32_t gapIndex = 0;
    while (true)
    {
        while (gapIndex < region.gapCount && region.gaps[gapIndex].end + sizeof(uintptr_t) <= cursor)
        {
            gapIndex++;
        }
        if (gapIndex < region.gapCount && region.gaps[gapIndex].start + sizeof(uintptr_t) <= cursor)
        {
            cursor = region.gaps[gapIndex].end + sizeof(uintptr_t);
            gapIndex++;
            continue;
        }
        if (cursor - sizeof(uintptr_t) >= region.parseEnd)
        {
            break;
        }
        Object* object = reinterpret_cast<Object*>(cursor);
        if (object->RawGetMethodTable() == nullptr)
        {
            fprintf(stderr, "LAXORI verify(%s): null method table at %p (region %p, previous %p)\n", where,
                (void*)cursor, (void*)region.start, starts.empty() ? nullptr : (void*)starts.back());
            abort();
        }
        size_t size = LocalSize(object);
        uintptr_t header = cursor - sizeof(uintptr_t);
        uintptr_t nextGap = gapIndex < region.gapCount ? region.gaps[gapIndex].start : region.parseEnd;
        if (size < MIN_OBJECT_SIZE || header + size > nextGap ||
            ((header ^ (header + size - 1)) >> ImmixBlockGeometry::BlockLogBytes) != 0)
        {
            fprintf(stderr, "LAXORI verify(%s): bad object %p size %zu (region %p, next gap or end %p, crosses a block %d)\n", where,
                (void*)cursor, size, (void*)region.start, (void*)nextGap,
                (int)(((header ^ (header + size - 1)) >> ImmixBlockGeometry::BlockLogBytes) != 0));
            abort();
        }
        starts.push_back(cursor);
        cursor += size;
    }
    for (uintptr_t start : starts)
    {
        Object* object = reinterpret_cast<Object*>(start);
        GCReferenceRangeIterator iterator(object);
        GCReferenceRange range;
        while (iterator.Next(&range))
        {
            for (size_t i = 0; i < range.count; i++)
            {
                uintptr_t child = reinterpret_cast<uintptr_t>(range.start[i]);
                bool bad = InRegion(region, child) && !std::binary_search(starts.begin(), starts.end(), child);
                // A value that cannot be a reference: a small number or a value that is not aligned.
                if (!bad && child != 0 && (child < 0x10000 || (child & 7) != 0))
                {
                    bad = true;
                }
                if (bad)
                {
                    fprintf(stderr, "LAXORI verify(%s): object %p slot %p holds %p, which is not a live object start (region %p)\n",
                        where, (void*)start, (void*)(range.start + i), (void*)child, (void*)region.start);
                    abort();
                }
            }
        }
    }
}

uint64_t LaxoriGcHeap::TscKHz() const
{
    // TSC cycles per millisecond since the start of the tier (minipal_hires_ticks is in the unit of minipal_hires_tick_frequency).
    int64_t ticks = static_cast<int64_t>(minipal_hires_ticks()) - m_tsc_ns_start;
    uint64_t cycles = LaxoriReadTimestampCounter() - m_tsc_start;
    uint64_t frequency = static_cast<uint64_t>(minipal_hires_tick_frequency());
    return ticks > 0 ? static_cast<uint64_t>((static_cast<double>(cycles) / static_cast<double>(ticks)) * static_cast<double>(frequency) / 1000.0) : 0;
}

bool LaxoriGcHeap::InitializePrivateTier(size_t regionKB, bool enabled, bool collectEscaped, size_t escapedCollectDiv)
{
    m_private_enabled = enabled;
    m_verify_private = getenv("DOTNET_LAXORIVerifyPrivate") != nullptr;
    m_tl_timers = getenv("DOTNET_LAXORITlTimers") != nullptr;
    m_sync_walk = getenv("DOTNET_LAXORISyncWalk") != nullptr;
    m_tsc_start = LaxoriReadTimestampCounter();
    m_tsc_ns_start = static_cast<int64_t>(minipal_hires_ticks());
    {
        const char* control = getenv("DOTNET_LAXORITestControl");
        m_test_control = control != nullptr ? atoi(control) : 0;
    }
    m_collect_escaped = collectEscaped;
    m_arena = m_engine.GetReservationStart();
    m_arena_bytes = m_engine.GetReservationBytes();
    if (!enabled)
    {
        regionKB = 256;
    }
    if (regionKB < 32 || regionKB > 4096 || (regionKB & (regionKB - 1)) != 0)
    {
        return false;
    }
    m_region_bytes = regionKB * 1024;
    m_escaped_collect_limit = collectEscaped ? m_region_bytes / (escapedCollectDiv == 0 ? 16 : escapedCollectDiv) : 0;
    m_region_blocks = m_region_bytes / ImmixBlockGeometry::BlockBytes;
    m_region_shift = 0;
    while ((static_cast<size_t>(1) << m_region_shift) < m_region_bytes)
    {
        m_region_shift++;
    }
    m_max_private_object = m_region_bytes / 8 < 8192 ? m_region_bytes / 8 : 8192;
    size_t blocks = (m_arena_bytes >> ImmixBlockGeometry::BlockLogBytes) + 1;
    if (!m_region_owner_storage.Reserve(blocks) ||
        !m_private_bits_storage.Reserve((m_arena_bytes >> 9) + 2) ||
        !m_private_index_storage.Reserve((m_arena_bytes >> IndexShift) + 2))
    {
        return false;
    }
    m_region_owner = m_region_owner_storage.Get();
    m_private_bits = m_private_bits_storage.Get();
    m_private_index = m_private_index_storage.Get();
    size_t immix = m_engine.GetImmixCapacityBytes();
    size_t large = m_engine.GetAdmittedBytes() - immix;
    if (!CommitHeapTables(0, immix / sizeof(uintptr_t)) ||
        !CommitHeapTables(m_engine.GetImmixReservationBytes() / sizeof(uintptr_t), large / sizeof(uintptr_t)))
    {
        return false;
    }
    m_private_bits_base = reinterpret_cast<uintptr_t>(m_private_bits) - (m_arena >> 6);
    m_engine.SetRegionOwnerTable(m_region_owner);
    return true;
}

bool LaxoriGcHeap::AdmitHeapTables(size_t firstSlot, size_t slotCount, bool, void* context)
{
    return static_cast<LaxoriGcHeap*>(context)->CommitHeapTables(firstSlot, slotCount);
}

bool LaxoriGcHeap::CommitHeapTables(size_t firstSlot, size_t slotCount)
{
    if (slotCount == 0)
    {
        return true;
    }
    size_t first = firstSlot * sizeof(uintptr_t);
    size_t end = (firstSlot + slotCount) * sizeof(uintptr_t);
    size_t firstBlock = first >> ImmixBlockGeometry::BlockLogBytes;
    size_t endBlock = (end + ImmixBlockGeometry::BlockBytes - 1) >> ImmixBlockGeometry::BlockLogBytes;
    if (!m_card_storage.Commit(first >> 11, ((end + 2047) >> 11) - (first >> 11), false) ||
        !m_bundle_storage.Commit(first >> 21, ((end + (size_t{1} << 21) - 1) >> 21) - (first >> 21), false) ||
        !m_region_owner_storage.Commit(firstBlock, endBlock - firstBlock, false))
    {
        return false;
    }
    if (first < m_engine.GetImmixReservationBytes())
    {
        if (!m_private_bits_storage.Commit(first >> 9, ((end + 511) >> 9) - (first >> 9), false) ||
            !m_private_index_storage.Commit(first >> IndexShift,
                ((end + (size_t{1} << IndexShift) - 1) >> IndexShift) - (first >> IndexShift), false))
        {
            return false;
        }
        // Tables stay committed when borrowed capacity is returned; no live metadata is reset on re-admission.
        if (end > LaxoriLoadAcquire(&m_private_heap_bytes))
        {
            LaxoriStoreRelease(&m_private_heap_bytes, static_cast<uint64_t>(end));
        }
    }
    return true;
}
