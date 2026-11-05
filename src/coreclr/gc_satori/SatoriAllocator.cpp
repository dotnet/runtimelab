// Copyright (c) 2025 Vladimir Sadov
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
// SatoriAllocator.cpp
//

#include "common.h"
#include "gcenv.h"
#include "../env/gcenv.os.h"
#include "../gceventstatus.h"

#include "SatoriUtil.h"
#include "SatoriHeap.h"
#include "SatoriObject.h"
#include "SatoriObject.inl"
#include "SatoriPage.h"
#include "SatoriAllocator.h"
#include "SatoriRegion.h"
#include "SatoriRegion.inl"
#include "SatoriRegionQueue.h"
#include "SatoriAllocationContext.h"
#include "SatoriWorkChunk.h"
#include "SatoriWorkList.h"

// Zeroes [start + sizeof(size_t), end - sizeof(size_t)), but only the part of it in [dirtyStart, dirtyEnd).
// The rest is known to be zeroed already. (see SatoriRegion::AllocateDirty)
// NB: the first word is the MethodTable and the last one is the syncblock of whatever follows. Neither is zeroed here.
static void ZeroInitDirtyBody(size_t start, size_t end, size_t dirtyStart, size_t dirtyEnd)
{
    size_t zeroFrom = max(start + sizeof(size_t), dirtyStart);
    size_t zeroUpTo = min(end - sizeof(size_t), dirtyEnd);
    if (zeroUpTo > zeroFrom)
    {
        memset((void*)zeroFrom, 0, zeroUpTo - zeroFrom);
    }
}

void SatoriAllocator::Initialize(SatoriHeap* heap)
{
    m_heap = heap;

    int processorCount = (int)GCToOSInterface::GetTotalProcessorCount();
    for (int i = 0; i < Satori::ALLOCATOR_BUCKET_COUNT; i++)
    {
        // Bucket 0 (regular 2MB regions) carries nearly all allocator traffic.
        // Bucket 1 (4-6MB) sees moderate traffic; larger buckets are rarely accessed.
        size_t shardCount = (i == 0) ? SatoriShardedRegionQueue::ShardCountForProcessors(4) :
                            (i == 1) ? SatoriShardedRegionQueue::ShardCountForProcessors(16) : 1;
        m_queues[i] = SatoriShardedRegionQueue::AllocAligned(QueueKind::Allocator, shardCount);
    }

    m_workChunks = SatoriShardedWorkList::AllocAlignedForProcessors(4);

    m_immortalRegion = nullptr;
    m_immortalAllocLock.Initialize();

    m_pinnedRegion = nullptr;
    m_pinnedAllocLock.Initialize();

    m_largeRegion = nullptr;
    m_largeAllocLock.Initialize();

    m_regularRegion = nullptr;
    m_regularAllocLock.Initialize();

    m_singePageAdders = 0;

    // As if the event was on before. If it is on from the start, there is nothing to catch up.
    // If it is off, the first check will notice that.
    m_allocationTickWasEnabled = true;

    int counterCount = (processorCount + Satori::ALLOCATED_BYTES_COUNTER_PROCESSORS - 1) / Satori::ALLOCATED_BYTES_COUNTER_PROCESSORS;
    m_smallAllocatedBytesCounterCount = max(1, min(counterCount, Satori::MAX_ALLOCATED_BYTES_COUNTERS));

    for (int i = 0; i < Satori::MAX_ALLOCATED_BYTES_COUNTERS; i++)
    {
        m_smallAllocatedBytes[i] = {};
    }

    m_largeAllocatedBytes = {};
    m_pinnedAllocatedBytes = {};
    m_deadThreadsAllocatedBytes = 0;
    m_lastPreciseAllocatedBytes = 0;
}

SatoriRegion* SatoriAllocator::GetRegion(size_t regionSize)
{
    _ASSERTE(regionSize % Satori::REGION_SIZE_GRANULARITY == 0);
    int64_t newPageDeadline = 0;
    int newPageSpin = 0;

tryAgain:
    SatoriRegion* putBack = nullptr;

    int bucket = SizeToBucket(regionSize);

    SatoriRegion* region = (bucket == 0) ?
                            m_queues[bucket]->TryPop():
                            m_queues[bucket]->TryRemoveWithSize(regionSize, putBack);

    // Return putBack even if the split failed to avoid losing the original region.
    if (putBack)
    {
        AddRegion(putBack);
        putBack = nullptr;
    }

    if (region)
    {
        return region;
    }

    while (++bucket < Satori::ALLOCATOR_BUCKET_COUNT)
    {
        region = m_queues[bucket]->TryPopWithSize(regionSize, putBack);
        if (putBack)
        {
            // Split the remainder so two threads can allocate from it concurrently.
            if (region && putBack->Size() > region->Size() * 2)
            {
                SatoriRegion* half = putBack->TrySplit(ALIGN_DOWN((putBack->Size() / 2), Satori::REGION_SIZE_GRANULARITY));
                if (half)
                {
                    AddRegion(half);
                }
            }

            AddRegion(putBack);
            putBack = nullptr;
        }

        if (region)
        {
            return region;
        }
    }

    // for regions < 1/2 page granularity, just allocate a regular page
    // this is the most common case.
    // for bigger ones, use more complex helper that may allocate a larger
    // page, if needed.
    if (regionSize < Satori::PAGE_SIZE_GRANULARITY / 2)
    {
        // Reserving a regular-sized Page.
        // We will often come here on multiple threads and we do not want all threads to reserve a page.
        // If someone else is reserving, we will allow 1 msec of retrying before reserving a page eagerly.
        if (newPageDeadline == 0)
        {
            newPageDeadline = SatoriUtil::GetTimeStamp() + SatoriUtil::GetTimeStampFrequency() / 1000;
        }

        if (m_singePageAdders != 0 ||
            Interlocked::CompareExchange(&m_singePageAdders, 1, 0) != 0)
        {
            // someone is adding.
            if (SatoriUtil::GetTimeStamp() - newPageDeadline > 0)
            {
                // timed out
                Interlocked::Increment(&m_singePageAdders);
            }
            else
            {
                newPageSpin = newPageSpin * 2 + 1;
                for (int i = 0; i < newPageSpin; i++)
                {
                    YieldProcessor();
                }

                goto tryAgain;
            }
        }

        SatoriPage* page = nullptr;
        if (m_heap->TryAddRegularPage(page))
        {
            if (page)
            {
                putBack = page->InitialRegion();
                region = putBack->TrySplit(regionSize);
                AddRegion(putBack);
                Interlocked::Decrement(&m_singePageAdders);
                if (region)
                {
                    return region;
                }

                goto allocationFailed;
            }

            // Someone added a page, but not us. There could be more regions.
            Interlocked::Decrement(&m_singePageAdders);
            goto tryAgain;
        }

        Interlocked::Decrement(&m_singePageAdders);
    }
    else
    {
        SatoriPage* page = m_heap->AddLargePage(regionSize);
        if (page)
        {
            region = page->InitialRegion();
            _ASSERTE(region->Size() >= regionSize);
            if (region->Size() > regionSize)
            {
                putBack = region->TrySplit(region->Size() - regionSize);
                if (putBack)
                {
                    AddRegion(putBack);
                }
                else
                {
                    // OOM
                    AddRegion(region);
                    region = nullptr;
                }
            }

            if (region)
            {
                return region;
            }
        }
    }

allocationFailed:
    // Prefer a region that can be split without another commit attempt.
    bucket = SizeToBucket(regionSize);
    if (bucket != 0)
    {
        putBack = nullptr;
        region = m_queues[bucket]->TryRemoveWithSize(regionSize, putBack, SIZE_MAX, /*preferCommitted*/ true);
        if (putBack)
        {
            AddRegion(putBack);
        }

        return region;
    }

    // most likely OOM
    return nullptr;
}

void SatoriAllocator::AddRegion(SatoriRegion* region)
{
    _ASSERTE(region->Generation() == -1);
    m_queues[SizeToBucket(region->Size())]->Enqueue(region);
}

// same as AddRegion, just puts it in front of the queue
// this is for recently used regions
void SatoriAllocator::ReturnRegion(SatoriRegion* region)
{
    _ASSERTE(region->IsAttachedToAllocatingOwner() == false);
    _ASSERTE(region->Generation() == -1);
    m_queues[SizeToBucket(region->Size())]->Push(region);
}

// Allocated bytes accounting.
//
// Threads count their allocations in their allocation contexts and publish the counts to the shared
// counters in batches, once per PACE_BUDGET or so - the same granularity as pacing.
// That happens after allocations that are not satisfied from the allocation context, and when a thread
// terminates. Thus the shared counters are kept current without relying on GCs, and the cost of
// updating them is amortized over many allocations.
//
// Each kind of allocations (small, large, pinned) is published on its own, by allocations of that kind.
// Allocation ticks are fired when publishing and must report an object of the published kind. If one kind
// published for another, a kind that is always published along with another one would never tick.
//
// The approximate total is the sum of the shared counters, or the last precise total, whichever is greater.
// It only grows, but can lag behind live allocations by up to PACE_BUDGET or so per thread and kind.
// NB: the precise total does not use the shared counters. With EE suspended, it sums up the counts in allocation
//     contexts of live threads and adds what terminated threads have allocated. (see GetTotalAllocatedBytesPrecise)
//
// Allocation tick events are driven by the same counters. (see MaybeFireAllocationTick)

// What the current thread has allocated, but not yet published.
thread_local
struct UnpublishedAllocatedBytes
{
    // Small objects are counted in the allocation context. This is how much of that is already published.
    int64_t smallPublished;

    // Large and pinned objects that are not published yet.
    // NB: the allocation context counts them together in alloc_bytes_uoh, but we want them apart.
    size_t large;
    size_t pinned;
} t_unpublishedAllocatedBytes;

// Bytes taken by small objects allocated in the context.
// NB: alloc_bytes also includes the unused remainder of the current allocation buffer, which is not counted here.
static int64_t SmallAllocatedBytes(SatoriAllocationContext* context)
{
    return context->alloc_bytes - (context->alloc_limit - context->alloc_ptr);
}

// All bytes allocated in the context, precisely. (same as GC.GetAllocatedBytesForCurrentThread)
static int64_t AllocatedBytes(SatoriAllocationContext* context)
{
    return SmallAllocatedBytes(context) + context->alloc_bytes_uoh;
}

// The counter for the small objects allocated on the current processor.
int SatoriAllocator::SmallAllocatedBytesCounterIndex()
{
    int counterCount = m_smallAllocatedBytesCounterCount;
    if (counterCount == 1)
    {
        return 0;
    }

    uint32_t processor;
    if (GCToOSInterface::CanGetCurrentProcessorNumber())
    {
        processor = GCToOSInterface::GetCurrentProcessorNumber();
    }
    else
    {
        // no processor number, just spread the threads
        size_t thread = SatoriUtil::GetCurrentThreadTag();
        processor = (uint32_t)((thread * 11400714819323198485llu) >> 32);
    }

    return (int)((processor / Satori::ALLOCATED_BYTES_COUNTER_PROCESSORS) % (uint32_t)counterCount);
}

size_t SatoriAllocator::GetTotalAllocatedBytes()
{
    size_t total =
        VolatileLoadWithoutBarrier(&m_largeAllocatedBytes.bytes) +
        VolatileLoadWithoutBarrier(&m_pinnedAllocatedBytes.bytes);

    for (int i = 0; i < m_smallAllocatedBytesCounterCount; i++)
    {
        total += VolatileLoadWithoutBarrier(&m_smallAllocatedBytes[i].bytes);
    }

    return max(total, VolatileLoadWithoutBarrier(&m_lastPreciseAllocatedBytes));
}

static void AddAllocatedBytesFn(gc_alloc_context* gcContext, void* param)
{
    *(size_t*)param += (size_t)AllocatedBytes((SatoriAllocationContext*)gcContext);
}

// The precise total. EE must be suspended, so that the counts in allocation contexts do not change.
// What terminated threads have allocated is not in any context. (see PublishAllocatedBytesOnThreadExit)
size_t SatoriAllocator::GetTotalAllocatedBytesPrecise()
{
    size_t total = m_deadThreadsAllocatedBytes;
    GCToEEInterface::GcEnumAllocContexts(AddAllocatedBytesFn, &total);
    VolatileStore(&m_lastPreciseAllocatedBytes, total);
    return total;
}

// Moves the last tick of the counter up to ALLOCATION_TICK_BYTES behind the counter, if it is further behind.
// Ticks may happen concurrently, so this only moves the last tick forward.
/* static */
void SatoriAllocator::CatchUpAllocationTick(AllocatedBytesCounter* counter)
{
    size_t bytes = VolatileLoadWithoutBarrier(&counter->bytes);
    size_t target = bytes > Satori::ALLOCATION_TICK_BYTES ? bytes - Satori::ALLOCATION_TICK_BYTES : 0;
    size_t atLastTick = VolatileLoadWithoutBarrier(&counter->bytesAtLastTick);
    while ((ptrdiff_t)(target - atLastTick) > 0)
    {
        size_t observed = Interlocked::CompareExchange(&counter->bytesAtLastTick, target, atLastTick);
        if (observed == atLastTick)
        {
            break;
        }

        atLastTick = observed;
    }
}

// Tells if the allocation tick event is enabled.
// While the event is off, the last ticks of the counters are not maintained, so when we see that it was
// turned on, we bring them up to date - as if the event was on for the last ALLOCATION_TICK_BYTES or so.
// NB: this must be called after publishing. Then, if a thread sees the event on, what it has just published
//     is either from the time the event was on, or the last ticks are caught up with it.
//     Checking before publishing could report bytes allocated while the event was off.
bool SatoriAllocator::IsAllocationTickEnabled()
{
#ifdef FEATURE_EVENT_TRACE
    bool isEnabled = EVENT_ENABLED(GCAllocationTick_V4);
    if (isEnabled != m_allocationTickWasEnabled)
    {
        if (isEnabled)
        {
            for (int i = 0; i < m_smallAllocatedBytesCounterCount; i++)
            {
                CatchUpAllocationTick(&m_smallAllocatedBytes[i]);
            }

            CatchUpAllocationTick(&m_largeAllocatedBytes);
            CatchUpAllocationTick(&m_pinnedAllocatedBytes);
        }

        m_allocationTickWasEnabled = isEnabled;
    }

    return isEnabled;
#else
    return false;
#endif
}

// Fires an allocation tick event if ALLOCATION_TICK_BYTES or more were published to the counter since the last tick.
// The event reports the bytes published to the counter since the last tick and the object that was just allocated.
// "total" is the value of the counter right after the current thread published to it. That includes the object.
/* static */
void SatoriAllocator::MaybeFireAllocationTick(AllocatedBytesCounter* counter, size_t total, AllocationTickKind kind, SatoriObject* obj, size_t size)
{
    size_t atLastTick = VolatileLoadWithoutBarrier(&counter->bytesAtLastTick);

    // Other threads may publish and tick after we have published, then atLastTick is ahead of our total and
    // what we have published is already reported. The comparison is signed, so that such case does not look
    // like a large amount.
    ptrdiff_t sinceLastTick = (ptrdiff_t)(total - atLastTick);
    if (sinceLastTick <= 0)
    {
        return;
    }

    // What we have just published includes the object, so the bytes since the last tick are normally at least
    // the size of the object. They can be less only if catching up after the event was turned on moved the last
    // tick past some of our bytes. (see IsAllocationTickEnabled) That should not discard the object that is
    // being reported, thus we report at least its size. Nothing is reported twice - catching up reports nothing.
    size_t amount = max((size_t)sinceLastTick, size);
    if (amount >= Satori::ALLOCATION_TICK_BYTES &&
        Interlocked::CompareExchange(&counter->bytesAtLastTick, total, atLastTick) == atLastTick)
    {
        FIRE_EVENT(GCAllocationTick_V4,
            amount,
            /*kind*/ (uint32_t)kind,
            /*heap_number*/ 0,
            (void*)obj,
            size);
    }
}

// What the current thread has allocated of the given kind, but has not published yet.
static size_t UnpublishedBytes(SatoriAllocationContext* context, AllocationTickKind kind)
{
    UnpublishedAllocatedBytes& unpublished = t_unpublishedAllocatedBytes;
    switch (kind)
    {
    case AllocationTickKind::Small:
    {
        int64_t smallAllocated = SmallAllocatedBytes(context);
        _ASSERTE(smallAllocated >= unpublished.smallPublished);
        return (size_t)(smallAllocated - unpublished.smallPublished);
    }
    case AllocationTickKind::Large:
        return unpublished.large;
    default:
        return unpublished.pinned;
    }
}

// Publishes what the current thread has allocated of the given kind since the last time.
// "obj" is the object of that kind that was just allocated. It is reported if an allocation tick is due.
// If "obj" is null, no ticks are fired.
void SatoriAllocator::PublishAllocatedBytes(SatoriAllocationContext* context, AllocationTickKind kind, SatoriObject* obj, size_t size)
{
    size_t bytes = UnpublishedBytes(context, kind);
    if (bytes == 0)
    {
        return;
    }

    UnpublishedAllocatedBytes& unpublished = t_unpublishedAllocatedBytes;
    AllocatedBytesCounter* counter;
    switch (kind)
    {
    case AllocationTickKind::Small:
        unpublished.smallPublished += (int64_t)bytes;
        counter = &m_smallAllocatedBytes[SmallAllocatedBytesCounterIndex()];
        break;
    case AllocationTickKind::Large:
        unpublished.large = 0;
        counter = &m_largeAllocatedBytes;
        break;
    default:
        unpublished.pinned = 0;
        counter = &m_pinnedAllocatedBytes;
        break;
    }

    size_t total = Interlocked::ExchangeAdd64(&counter->bytes, bytes) + bytes;

    // NB: the event is checked after publishing. (see IsAllocationTickEnabled)
    if (IsAllocationTickEnabled() && obj != nullptr)
    {
        MaybeFireAllocationTick(counter, total, kind, obj, size);
    }
}

// Publishes what the current thread has allocated of the given kind, if enough has accumulated since the last time.
// Called after allocations that are not satisfied from the allocation context.
// "obj" is the object of the given kind that was just allocated. It is reported if an allocation tick is due.
void SatoriAllocator::PublishAllocatedBytesIfNeeded(SatoriAllocationContext* context, AllocationTickKind kind, SatoriObject* obj, size_t size)
{
    // same granularity as pacing.
    if (UnpublishedBytes(context, kind) >= Satori::PACE_BUDGET)
    {
        PublishAllocatedBytes(context, kind, obj, size);
    }
}

// Accounts for a large or pinned object that the current thread has just allocated.
void SatoriAllocator::OnLargeOrPinnedAllocated(SatoriAllocationContext* context, uint32_t flags, SatoriObject* obj, size_t size)
{
    context->alloc_bytes_uoh += size;

    AllocationTickKind kind;
    // immortal objects do not move either, we count them as pinned.
    if (flags & (GC_ALLOC_PINNED_OBJECT_HEAP | GC_ALLOC_IMMORTAL))
    {
        kind = AllocationTickKind::Pinned;
        t_unpublishedAllocatedBytes.pinned += size;
    }
    else
    {
        kind = AllocationTickKind::Large;
        t_unpublishedAllocatedBytes.large += size;
    }

    PublishAllocatedBytesIfNeeded(context, kind, obj, size);
}

// The current thread is terminating and its allocation context is about to be cleared.
// Publishes all that is not published yet, moves the precise count from the context to the total
// for terminated threads, and resets the accounting to match the cleared context.
// NB: this runs on the terminating thread itself, after its allocation context is deactivated.
void SatoriAllocator::PublishAllocatedBytesOnThreadExit(SatoriAllocationContext* context)
{
    PublishAllocatedBytes(context, AllocationTickKind::Small, /*obj*/ nullptr, /*size*/ 0);
    PublishAllocatedBytes(context, AllocationTickKind::Large, /*obj*/ nullptr, /*size*/ 0);
    PublishAllocatedBytes(context, AllocationTickKind::Pinned, /*obj*/ nullptr, /*size*/ 0);
    t_unpublishedAllocatedBytes = {};

    // The runtime calls this when EE cannot be suspended (in cooperative mode or holding the thread store lock),
    // so GetTotalAllocatedBytesPrecise sees the count either in the context or in the total, never in both.
    // Other threads may be terminating concurrently though.
    Interlocked::ExchangeAdd64(&m_deadThreadsAllocatedBytes, (size_t)AllocatedBytes(context));
    context->alloc_bytes = 0;
    context->alloc_bytes_uoh = 0;
}

Object* SatoriAllocator::Alloc(SatoriAllocationContext* context, size_t size, uint32_t flags)
{
    size = ALIGN_UP(size, Satori::OBJECT_ALIGNMENT);

    if ((flags & (GC_ALLOC_IMMORTAL | GC_ALLOC_PINNED_OBJECT_HEAP | GC_ALLOC_LARGE_OBJECT_HEAP)) == 0)
    {
        if (context->alloc_ptr + size <= context->alloc_limit)
        {
            SatoriObject* result = (SatoriObject*)context->alloc_ptr;
            if ((flags & GC_ALLOC_FINALIZE) && !result->ContainingRegion()->RegisterForFinalization(result))
            {
                return nullptr;
            }

            context->alloc_ptr += size;
            return result;
        }

        if (size < Satori::LARGE_OBJECT_THRESHOLD)
        {
            return AllocRegular(context, size, flags);
        }

        flags |= GC_ALLOC_LARGE_OBJECT_HEAP;
    }

    if (flags & GC_ALLOC_PINNED_OBJECT_HEAP)
    {
        SatoriObject* result = AllocPinned(context, size, flags);
        if (result != nullptr)
        {
            result->SetUnmovable();
        }

        return result;
    }

    if (flags & GC_ALLOC_LARGE_OBJECT_HEAP)
    {
        return AllocLarge(context, size, flags);
    }

    if (flags & GC_ALLOC_IMMORTAL)
    {
        return AllocImmortal(context, size, flags);
    }

    return nullptr;
}

#ifdef _DEBUG
// allow more frequent allocs from shared in debug
const size_t minSharedAllocDelay = 5;
#else
// This is how often would we want to take a lock in usecs for 2k or more.
// If we come for more sooner, arrange an own region.
// Shared is for slow allocators. This is basically the threshold for slow.
const size_t minSharedAllocDelay = 50;
#endif

thread_local
size_t t_lastAllocBytes;

thread_local
size_t t_lastAllocBytesAtGCcheck;

thread_local
struct AllocStats
{
    size_t lastRegularAllocBytes;
    size_t lastAllocRecordUsec;
    // in byte/microsecond, same as MB/sec
    size_t regularAllocRate;
} t_allocStats;

void SatoriAllocator::UpdateAllocStatsAndHelpIfNeeded(SatoriAllocationContext* context)
{
    size_t curAllocBytes = context->alloc_bytes + context->alloc_bytes_uoh;
    if (curAllocBytes == 0)
    {
        m_heap->Recycler()->MaybeTriggerGC(gc_reason::reason_alloc_soh);
        return;
    }

    size_t lastAllocBytes = t_lastAllocBytes;
    // sometimes we release bytes back, wait until we go forward.
    if (curAllocBytes < lastAllocBytes)
        return;

    size_t change = curAllocBytes - lastAllocBytes;
    if (change < Satori::PACE_BUDGET)
    {
        // too soon. 
        return;
    }

    if (curAllocBytes - t_lastAllocBytesAtGCcheck >= Satori::REGION_SIZE_GRANULARITY)
    {
        m_heap->Recycler()->MaybeTriggerGC(gc_reason::reason_alloc_soh);
        t_lastAllocBytesAtGCcheck = curAllocBytes;
    }
    else
    {
        m_heap->Recycler()->HelpOnce();
    }

    t_lastAllocBytes = curAllocBytes;

    // This is after possible helping or doing GC, but since this is sampling
    // of overal alloc rate it does not matter if we take time before or after.
    size_t curUsec = m_heap->Recycler()->GetNowUsecs();
    size_t curRegularAllocBytes = context->alloc_bytes;
    if (curRegularAllocBytes > t_allocStats.lastRegularAllocBytes &&
        curUsec > t_allocStats.lastAllocRecordUsec)
    {
        t_allocStats.regularAllocRate =
            (curRegularAllocBytes - t_allocStats.lastRegularAllocBytes) /
            (curUsec - t_allocStats.lastAllocRecordUsec);
        t_allocStats.lastRegularAllocBytes = curRegularAllocBytes;
        t_allocStats.lastAllocRecordUsec = curUsec; 
    }
}

SatoriObject* SatoriAllocator::AllocRegular(SatoriAllocationContext* context, size_t size, uint32_t flags)
{
    // when allocations cross certain thresholds, check if GC should start or help is needed.
    UpdateAllocStatsAndHelpIfNeeded(context);

    // The region we have just given up, if any. Used as a hint when getting the next one.
    SatoriRegion* prevRegion = nullptr;

tryAgain: 

    if (!context->RegularRegion())
    {
        // If was allocating from shared, close the alloc.
        // If we continue from the same shared region, we will return the remainder, once we have the lock.
        // If not we will just leave the free obj unreturned. It is probably too small anyways.
        SatoriObject* freeObj = context->alloc_ptr != 0 ?
            context->FinishAllocFromShared() : nullptr;

        // TUNING: may be 10 or 50 ? Is it important?
        // we will consider 100+ MB/s as fast-allocating and try getting a non-shared region
        if (t_allocStats.regularAllocRate < 100)
        {
            //m_regularAllocLock.Enter();
            if (m_regularAllocLock.TryEnter())
            {
                if (freeObj && freeObj->ContainingRegion() == m_regularRegion)
                {
                    size_t freeSize = freeObj->FreeObjSize();
                    m_regularRegion->SetOccupancy(m_regularRegion->Occupancy() - freeSize);
                    m_regularRegion->ReturnFreeSpace(freeObj, freeSize);
                }

                return AllocRegularShared(context, size, flags);
            }
        }
    }

    SatoriRegion* region = context->RegularRegion();
    _ASSERTE(region == nullptr || region->IsAttachedToAllocatingOwner());

    if (region != nullptr)
    {
        // if a GC took the region while it stayed attached, start a new accounting period.
        region->RebaselineUsableIfStale(context->alloc_bytes);

        if (region->ClearStaleEscapeMarks())
        {
            // the barrier stopped tracking because of too many escapes.
            SatoriRegion::OnEscapeTrackingEnded();
        }
    }

    while (true)
    {
        if (region != nullptr)
        {
            _ASSERTE((size_t)context->alloc_limit == region->GetAllocStart());
            size_t moreSpace = context->alloc_ptr + size - context->alloc_limit;

            // try allocate more to form a contiguous chunk to fit size
            size_t allocRemaining = region->GetTlgcAllocRemaining();
            if (moreSpace <= allocRemaining)
            {
                bool zeroInitialize = !(flags & GC_ALLOC_ZEROING_OPTIONAL);
                if (zeroInitialize)
                {
                    // Take more than needed, so that we come here less often, but zero ahead at most MinZeroInitSize,
                    // so that the zeroed memory is still in cache when we use it and not much is wasted if we stop
                    // allocating here before using it all. (the unused part goes back to the free list with a note
                    // that it is zeroed, but that is lost if the region is swept before the space is reused)
                    // With MinZeroInitSize or more remaining, this takes MinZeroInitSize. Closer to the end of the span,
                    // it takes the largest power of two that fits, to use up the rest in a few trips.
                    // These are multiples of INDEX_GRANULARITY, so alloc_limit stays misaligned as described below.
                    if (allocRemaining >= Satori::INDEX_GRANULARITY)
                    {
                        size_t quantum = min(SatoriUtil::MinZeroInitSize(), SatoriUtil::RoundDownPwr2(allocRemaining));
                        moreSpace = max(moreSpace, quantum);
                    }
                    else
                    {
                        // The tail of the span is smaller than that. Taking just what is needed would send every
                        // allocation in the tail here again, so take all of it, as long as the object would fit in it
                        // by itself. When it would not (the object hangs over from the current allocation context),
                        // what is left after it is unlikely to fit the next object of a similar size, and zeroing it
                        // would likely be wasted. (it is also too small for a free list)
                        if (size <= allocRemaining)
                        {
                            moreSpace = allocRemaining;
                        }
                    }

                    // What is already known to be zeroed costs nothing to take.
                    // In fresh memory that can be the rest of the region, so take at most PACE_BUDGET / 2 (64 KB):
                    // the fast-path array allocator assumes the allocation context never exceeds LARGE_OBJECT_SIZE,
                    // and allocation pacing runs when we come here.
                    static_assert(Satori::PACE_BUDGET / 2 + Satori::INDEX_GRANULARITY < LARGE_OBJECT_SIZE);
                    size_t zeroedRemaining = min(region->GetAllocZeroedRemaining(), (size_t)(Satori::PACE_BUDGET / 2));
                    moreSpace = max(moreSpace, min(zeroedRemaining, allocRemaining));

                    // " +/- sizeof(size_t)" here is to intentionally misalign alloc_limit on the index granularity
                    // to improve chances that the object that is allocated here will be indexed
                    size_t misAlignedOnIndexEnd = ALIGN_UP(region->GetAllocStart() + moreSpace + sizeof(size_t), Satori::INDEX_GRANULARITY) - sizeof(size_t);
                    size_t misAlignedMoreSpace = misAlignedOnIndexEnd - region->GetAllocStart();

                    if (misAlignedMoreSpace <= allocRemaining)
                    {
                        moreSpace = misAlignedMoreSpace;
                    }
                }

                if (region->Allocate(moreSpace, zeroInitialize))
                {
                    context->alloc_limit += moreSpace;
                    context->alloc_bytes += moreSpace;

                    SatoriObject* result = (SatoriObject*)(size_t)context->alloc_ptr;
                    if ((flags & GC_ALLOC_FINALIZE) &&
                        !region->RegisterForFinalization(result))
                    {
                        return nullptr;
                    }

                    context->alloc_ptr += size;
                    _ASSERTE((size_t)(context->alloc_limit - context->alloc_ptr) < LARGE_OBJECT_SIZE);
                    result->CleanSyncBlock();
                    region->SetIndicesForObject(result, result->Start() + size);

                    PublishAllocatedBytesIfNeeded(context, AllocationTickKind::Small, result, size);
                    return result;
                }
                else
                {
                    //OOM
                    return nullptr;
                }
            }

            // unclaim unused.
            size_t unused = context->alloc_limit - context->alloc_ptr;
            context->alloc_bytes -= unused;

            bool collectAtTlgcLimit = region->ShouldCollectAtTlgcLimit(context->alloc_bytes);

            if (region->IsAllocating())
            {
                // the unused part of the allocation context is zeroed.
                region->StopAllocating((size_t)context->alloc_ptr, (size_t)context->alloc_limit);
            }
            else
            {
                _ASSERTE(context->alloc_ptr == nullptr);
            }

            if (collectAtTlgcLimit)
            {
                if (!region->ThreadLocalCollect(context->alloc_bytes, /* atTlgcLimit */ true))
                {
                    SatoriRegion::OnEscapeTrackingEnded();
                    region->StopEscapeTracking();
                }
            }

            // try get from the free list
            if (region->StartAllocating(size, context->alloc_bytes))
            {
                // we have enough free space in the region to continue
                context->alloc_ptr = context->alloc_limit = (uint8_t*)region->GetAllocStart();
                continue;
            }

            if (region->IsEscapeTracking())
            {
                // try performing thread local collection and see if we have enough space after that.
                if (region->ThreadLocalCollect(context->alloc_bytes))
                {
                    if (region->StartAllocating(size, context->alloc_bytes))
                    {
                        // we have enough free space in the region to continue
                        context->alloc_ptr = context->alloc_limit = (uint8_t*)region->GetAllocStart();
                        continue;
                    }
                }
                else
                {
                    SatoriRegion::OnEscapeTrackingEnded(/* outOfSpace */ true);
                    region->StopEscapeTracking();
                }
            }

            // A pointer-free medium object that does not fit, while the region still has a lot of space in smaller
            // holes. Retiring the region would waste that space (and charge for it), so serve this object from the
            // large allocator instead and keep the region for the smaller allocations.
            // Pointer-free, so the object does not care about the region and its escape tracking. Finalizable
            // objects are registered with their region, so they stay out of this.
            // Only 8 KB or more: the large allocator serves each object individually, without an allocation context,
            // which is reasonable for objects that are a sizable fraction of LARGE_OBJECT_THRESHOLD, but too costly
            // per byte for smaller ones. Those retire the region as before and continue in another one.
            if (size >= 8 * 1024 &&
                !(flags & (GC_ALLOC_CONTAINS_REF | GC_ALLOC_FINALIZE)) &&
                region->UsableCapacity() > Satori::REGION_SIZE_GRANULARITY / 32)
            {
                // the region stays attached, but not allocating.
                context->alloc_ptr = context->alloc_limit = nullptr;
                return AllocLarge(context, size, flags | GC_ALLOC_LARGE_OBJECT_HEAP);
            }

            context->alloc_ptr = context->alloc_limit = nullptr;
            if (region->IsEscapeTracking())
            {
                // the region ran out of space while tracking.
                SatoriRegion::OnEscapeTrackingEnded(/* outOfSpace */ true);
            }

            region->DetachFromAlocatingOwnerRelease();
            m_heap->Recycler()->AddEphemeralRegion(region);

            // could not allocate, start over with trying to get a region
            prevRegion = region;
            goto tryAgain;
        }

        // Prefer a neighbor of the region we have just given up. (see TryGetRegularRegion)
        // The previous region is detached and owned by the recycler now, but, same as in AllocRegularShared,
        // we only use it to look at its neighbor in the page.
        _ASSERTE(region == nullptr);
        region = prevRegion;
        TryGetRegularRegion(region, size);

        if (region == nullptr)
        {
            //OOM
            return nullptr;
        }

        // the order of assignments:
        // 1) Attach  (optional: set escape tag)
        // 2) Set Generation to 0 or 1
        // 3) Reset ReusableFor
        // <objects allocated>
        // 4) (optional: clear escape tag) Detach       

        region->AttachToAllocatingOwner(&context->RegularRegion());
        if (SatoriUtil::IsGen0Enabled())
        {
            switch (region->ReusableFor())
            {
            case SatoriRegion::ReuseLevel::Gen0:
                if (!SatoriRegion::ShouldStartEscapeTracking())
                {
                    region->SetGenerationRelease(1);
                    break;
                }

                region->EscapeAll();
                goto fallthrough;
            case SatoriRegion::ReuseLevel::None:
                if (!SatoriRegion::ShouldStartEscapeTracking())
                {
                    region->SetGenerationRelease(1);
                    break;
                }

            fallthrough:
                //NB: sets Generation to 0
                region->StartEscapeTrackingRelease(SatoriUtil::GetCurrentThreadTag(), context->alloc_bytes, size);
                break;
            case SatoriRegion::ReuseLevel::Gen1:
                region->SetGenerationRelease(1);
                break;
            }
        }
        else
        {
            region->SetGenerationRelease(1);
        }

        region->ResetReusableForRelease();
        context->alloc_ptr = context->alloc_limit = (uint8_t*)region->GetAllocStart();
    }
}

SatoriObject* SatoriAllocator::AllocRegularShared(SatoriAllocationContext* context, size_t size, uint32_t flags)
{
    SatoriRegion* region = m_regularRegion;
    _ASSERTE(region == nullptr || region->IsAttachedToAllocatingOwner());

    while (true)
    {
        if (region != nullptr)
        {
            // get extra MIN_FREE_SIZE as other threads will allocate after us
            // and when we are done we need a parseability separator.
            size_t moreSpace = size + Satori::MIN_FREE_SIZE;
            size_t allocRemaining = region->GetAllocRemaining();
            if (allocRemaining >= moreSpace)
            {
                // we have enough free space in the region to continue
                bool zeroInitialize = !(flags & GC_ALLOC_ZEROING_OPTIONAL);
                if (zeroInitialize)
                {
                    // We do not want to pre-zero too much in shared case.
                    // Just aligning on index is good enough.
                    // Also we do not want the threads to hold on to unfinished allocs for too long.

                    // " +/- sizeof(size_t)" here is to intentionally misalign alloc_limit on the index granularity
                    // to improve chances that the object that is allocated here will be indexed
                    size_t misAlignedOnIndexEnd = ALIGN_UP(region->GetAllocStart() + moreSpace + sizeof(size_t), Satori::INDEX_GRANULARITY) - sizeof(size_t);
                    size_t misAlignedMoreSpace = misAlignedOnIndexEnd - region->GetAllocStart();

                    if (misAlignedMoreSpace <= allocRemaining)
                    {
                        moreSpace = misAlignedMoreSpace;
                    }
                    else
                    {
                        // In nonshared we can pick the tail with single object allocs.
                        // In shared, just pick the tail. We do not want allocs in shared that are too small.
                        moreSpace = allocRemaining;
                    }
                }

                // do not zero-initialize just yet, we will do that after leaving the lock.
                size_t dirtyStart, dirtyEnd;
                SatoriObject* result = (SatoriObject*)region->AllocateDirty(moreSpace, &dirtyStart, &dirtyEnd);
                if (!result)
                {
                    //OOM, nothing to undo.
                    m_regularAllocLock.Leave();
                    return nullptr;
                }

                if (flags & GC_ALLOC_FINALIZE)
                {
                    if (!region->RegisterForFinalization(result))
                    {
                        // OOM, undo the allocation
                        region->StopAllocating(result->Start());
                        m_regularAllocLock.Leave();
                        return nullptr;
                    }
                }

                // Conservatively assume the region may get finalizable objects.
                // This is for initial placement into queues, which may happen before
                // all objects in the buffer are allocated.
                // It is ok to be conservative, also this will self-correct after sweeping.
                region->SetHasFinalizables();

                // region stays unparsable until "moreSpace" is consumed.
                region->IncrementUnfinishedAlloc();

                // done with region modifications.
                m_regularAllocLock.Leave();

                context->alloc_ptr = (uint8_t*)result + size;
                context->alloc_bytes += moreSpace;
                // leave some space at the end for the free object
                moreSpace -= Satori::MIN_FREE_SIZE;
                context->alloc_limit = (uint8_t*)result + moreSpace;

                result->CleanSyncBlock();
                region->SetIndicesForObject(result, result->Start() + size);
                if (zeroInitialize)
                {
                    ZeroInitDirtyBody(result->Start(), result->Start() + moreSpace, dirtyStart, dirtyEnd);
                }

                PublishAllocatedBytesIfNeeded(context, AllocationTickKind::Small, result, size);
                return result;
            }

            if (region->IsAllocating())
            {
                region->StopAllocating();
            }

            // if a GC took the region while it stayed attached, start a new accounting period.
            region->RebaselineUsableIfStale(0);

            // try get from the free list
            size_t desiredFreeSpace = size + Satori::MIN_FREE_SIZE;
            if (region->StartAllocating(desiredFreeSpace))
            {
                // we have enough free space in the region to continue
                continue;
            }

            region->DetachFromAlocatingOwnerRelease();
            m_heap->Recycler()->AddEphemeralRegion(region);
        }

        TryGetRegularRegion(region, size);
        if (region == nullptr)
        {
            //OOM
            m_regularAllocLock.Leave();
            return nullptr;
        }

        region->TryCommit();

        region->AttachToAllocatingOwner(&m_regularRegion);
        region->SetGenerationRelease(1);
        region->ResetReusableForRelease();
    }
}

// Gets a region for a regular allocation of the given size. Prefers reusable regions, but a reusable region
// that cannot serve the request from its free lists would just be retired on the spot while charging
// the budget for its unused space. Such regions are skipped and left to be used for smaller requests.
void SatoriAllocator::TryGetRegularRegion(SatoriRegion*& region, size_t size)
{
    size_t minFree = size + Satori::MIN_FREE_SIZE;

    // opportunistically try get the next region, if available
    SatoriRegion* next = nullptr;
    SatoriQueue<SatoriRegion>* nextContainingQueue = nullptr;

    if (region)
    {
        next = region->NextInPage();
        if (next)
        {
            nextContainingQueue = next->ContainingQueue();
            if (nextContainingQueue &&
                nextContainingQueue->Kind() == QueueKind::RecyclerReusable &&
                next->CanFitAllocation(minFree) &&
                nextContainingQueue->TryRemove(next))
            {
                region = next;
                _ASSERTE(region->Occupancy() < Satori::REGION_SIZE_GRANULARITY);
                return;
            }
        }
    }

    region = m_heap->Recycler()->TryGetReusable(minFree);

    if (region == nullptr)
    {
        if (nextContainingQueue &&
            nextContainingQueue->Kind() == QueueKind::Allocator &&
            next->Size() == Satori::REGION_SIZE_GRANULARITY &&
            nextContainingQueue->TryRemove(next))
        {
            if (next->Size() != Satori::REGION_SIZE_GRANULARITY)
            {
                ReturnRegion(next);
            }
            else
            {
                region = next;
                return;
            }
        }

        region = GetRegion(Satori::REGION_SIZE_GRANULARITY);
        _ASSERTE(region == nullptr || region->NothingMarked());
    }
}

SatoriObject* SatoriAllocator::AllocLarge(SatoriAllocationContext* context, size_t size, uint32_t flags)
{
    // handle huge allocation. It can't be from the current region.
    if (size > SatoriRegion::MAX_LARGE_OBJ_SIZE)
    {
        return AllocHuge(context, size, flags);
    }

    // when allocations cross certain thresholds, check if GC should start or help is needed.
    UpdateAllocStatsAndHelpIfNeeded(context);

tryAgain:

    if (!context->LargeRegion() &&
        size < Satori::REGION_SIZE_GRANULARITY / 2)
    {
        //m_largeAllocLock.Enter();
        if (m_largeAllocLock.TryEnter())
        {
            return AllocLargeShared(context, size, flags);
        }
    }

    SatoriRegion* region = context->LargeRegion();
    while (true)
    {
        if (region)
        {
            size_t allocRemaining = region->GetAllocRemaining();
            if (allocRemaining >= size)
            {
                bool zeroInitialize = !(flags & GC_ALLOC_ZEROING_OPTIONAL);
                SatoriObject* result = (SatoriObject*)region->Allocate(size, zeroInitialize);
                if (!result)
                {
                    // OOM, nothing to undo
                    return nullptr;
                }

                if (flags & GC_ALLOC_FINALIZE)
                {
                    if (!region->RegisterForFinalization(result))
                    {
                        // OOM, undo allocation
                        region->StopAllocating(result->Start());
                        return nullptr;
                    }

                    region->SetHasFinalizables();
                }

                // success
                result->CleanSyncBlock();
                region->SetIndicesForObject(result, result->Start() + size);

                OnLargeOrPinnedAllocated(context, flags, result, size);
                return result;
            }
        }

        // check if existing region has space or drop it into recycler
        if (region)
        {
            if (region->IsAllocating())
            {
                region->StopAllocating();
            }

            // if a GC took the region while it stayed attached, start a new accounting period.
            region->RebaselineUsableIfStale(0);

            // try get from the free list
            if (region->StartAllocating(size))
            {
                // we have enough free space in the region to continue
                continue;
            }

            region->DetachFromAlocatingOwnerRelease();
            m_heap->Recycler()->AddEphemeralRegion(region);

            // retry
            goto tryAgain;
        }

        // get a new regular region.
        _ASSERTE(SatoriRegion::RegionSizeForAlloc(size) == Satori::REGION_SIZE_GRANULARITY);

        region = nullptr;
        if (size < Satori::REGION_SIZE_GRANULARITY / 2)
        {
            region = m_heap->Recycler()->TryGetReusableForLarge();
        }

        if (!region)
        {
            region = GetRegion(Satori::REGION_SIZE_GRANULARITY);
            if (!region)
            {
                //OOM
                return (SatoriObject*)nullptr;
            }

            _ASSERTE(region->NothingMarked());
        }

        region->AttachToAllocatingOwner(&context->LargeRegion());
        region->SetGenerationRelease(1);
        region->ResetReusableForRelease();
    }
}

SatoriObject* SatoriAllocator::AllocLargeShared(SatoriAllocationContext* context, size_t size, uint32_t flags)
{
    _ASSERTE(flags & (GC_ALLOC_LARGE_OBJECT_HEAP | GC_ALLOC_PINNED_OBJECT_HEAP));

    SatoriRegion* region = m_largeRegion;
    _ASSERTE(region == nullptr || region->IsAttachedToAllocatingOwner());

    while (true)
    {
        if (region != nullptr)
        {
            if (region->GetAllocRemaining() >= size)
            {
                // do not zero-initialize just yet, we will do that after leaving the lock.
                size_t dirtyStart, dirtyEnd;
                SatoriObject* result = (SatoriObject*)region->AllocateDirty(size, &dirtyStart, &dirtyEnd);
                if (!result)
                {
                    //OOM, nothing to undo
                    m_largeAllocLock.Leave();
                    return nullptr;
                }

                if (flags & GC_ALLOC_FINALIZE)
                {
                    if (!region->RegisterForFinalization(result))
                    {
                        // OOM, undo the allocation
                        region->StopAllocating(result->Start());
                        m_largeAllocLock.Leave();
                        return nullptr;
                    }

                    region->SetHasFinalizables();
                }

                // region stays unparsable until allocation is complete.
                region->IncrementUnfinishedAlloc();
                // done with region modifications.
                m_largeAllocLock.Leave();

                result->CleanSyncBlockAndSetUnfinished();
                region->SetIndicesForObject(result, result->Start() + size);
                if (!(flags & GC_ALLOC_ZEROING_OPTIONAL))
                {
                    ZeroInitDirtyBody(result->Start(), result->Start() + size, dirtyStart, dirtyEnd);
                }

                OnLargeOrPinnedAllocated(context, flags, result, size);
                return result;
            }

            if (region->IsAllocating())
            {
                region->StopAllocating();
            }

            // if a GC took the region while it stayed attached, start a new accounting period.
            region->RebaselineUsableIfStale(0);

            // try get from the free list
            if (region->StartAllocating(size))
            {
                // we have enough free space in the region to continue
                continue;
            }

            region->DetachFromAlocatingOwnerRelease();
            m_heap->Recycler()->AddEphemeralRegion(region);
        }

        // get a new regular region.
        _ASSERTE(SatoriRegion::RegionSizeForAlloc(size) == Satori::REGION_SIZE_GRANULARITY);

        region = nullptr;
        if (size < Satori::REGION_SIZE_GRANULARITY / 2)
        {
            region = m_heap->Recycler()->TryGetReusableForLarge();
            _ASSERTE(region == nullptr || !region->IsAllocating());
        }

        if (!region)
        {
            region = GetRegion(Satori::REGION_SIZE_GRANULARITY);
            if (!region)
            {
                //OOM
                m_largeAllocLock.Leave();
                return nullptr;
            }

            _ASSERTE(region->NothingMarked());
        }

        region->TryCommit();

        region->AttachToAllocatingOwner(&m_largeRegion);
        region->SetGenerationRelease(1);
        region->ResetReusableForRelease();
    }
}

SatoriObject* SatoriAllocator::AllocHuge(SatoriAllocationContext* context, size_t size, uint32_t flags)
{
    // when allocations cross certain thresholds, check if GC should start or help is needed.
    UpdateAllocStatsAndHelpIfNeeded(context);

    size_t regionSize = SatoriRegion::RegionSizeForAlloc(size);
    _ASSERTE(regionSize > Satori::REGION_SIZE_GRANULARITY);
    SatoriRegion* hugeRegion = GetRegion(regionSize);
    if (!hugeRegion)
    {
        //OOM
        return nullptr;
    }

    _ASSERTE(hugeRegion->NothingMarked());
    bool zeroInitialize = !(flags & GC_ALLOC_ZEROING_OPTIONAL);
    SatoriObject* result = (SatoriObject*)hugeRegion->AllocateHuge(size, zeroInitialize);
    if (!result)
    {
        ReturnRegion(hugeRegion);
        // OOM
        return nullptr;
    }

    result->CleanSyncBlockAndSetUnfinished();
    hugeRegion->SetIndicesForObject(result, hugeRegion->Start() + Satori::REGION_SIZE_GRANULARITY);

    // huge regions are not attached to contexts and in gen0+ would appear parseable,
    // but this one is not parseable yet since the new object has no MethodTable
    // we will keep the region in gen -1 for now and make it gen1 or gen2 in PublishObject.
    hugeRegion->StopAllocating();

    if (flags & GC_ALLOC_FINALIZE)
    {
        hugeRegion->SetHasFinalizables();
        if (!hugeRegion->RegisterForFinalization(result))
        {
            result->CleanSyncBlock();
            hugeRegion->MakeBlank();
            ReturnRegion(hugeRegion);
            return nullptr;
        }
    }

    OnLargeOrPinnedAllocated(context, flags, result, size);
    return result;
}

SatoriObject* SatoriAllocator::AllocPinned(SatoriAllocationContext* context, size_t size, uint32_t flags)
{
    _ASSERTE(flags & GC_ALLOC_PINNED_OBJECT_HEAP);

    if (size > SatoriRegion::MAX_LARGE_OBJ_SIZE)
    {
        return AllocHuge(context, size, flags);
    }

    // when allocations cross certain thresholds, check if GC should start or help is needed.
    // NB: not while holding the lock. This may trigger a GC or do some GC work and, while the lock is held,
    //     all other pinned allocations would be diverted to AllocLarge.
    UpdateAllocStatsAndHelpIfNeeded(context);

    // if can't get a lock, let AllocLarge handle this.
    if (!m_pinnedAllocLock.TryEnter())
    {
        return AllocLarge(context, size, flags);
    }

    SatoriRegion* region = m_pinnedRegion;
    while (true)
    {
        if (region != nullptr)
        {
            if (region->GetAllocRemaining() >= size)
            {
                // do not zero-initialize just yet, we will do that after leaving the lock.
                size_t dirtyStart, dirtyEnd;
                SatoriObject* result = (SatoriObject*)region->AllocateDirty(size, &dirtyStart, &dirtyEnd);
                if (!result)
                {
                    //OOM, nothing to undo
                    m_pinnedAllocLock.Leave();
                    return nullptr;
                }

                if (flags & GC_ALLOC_FINALIZE)
                {
                    if (!region->RegisterForFinalization(result))
                    {
                        // OOM, undo the allocation
                        region->StopAllocating(result->Start());
                        m_pinnedAllocLock.Leave();
                        return nullptr;
                    }

                    region->SetHasFinalizables();
                }

                // region stays unparsable until allocation is complete.
                region->IncrementUnfinishedAlloc();
                // done with region modifications.
                m_pinnedAllocLock.Leave();

                result->CleanSyncBlockAndSetUnfinished();
                region->SetIndicesForObject(result, result->Start() + size);
                if (!(flags & GC_ALLOC_ZEROING_OPTIONAL))
                {
                    ZeroInitDirtyBody(result->Start(), result->Start() + size, dirtyStart, dirtyEnd);
                }

                OnLargeOrPinnedAllocated(context, flags, result, size);
                return result;
            }

            if (region->IsAllocating())
            {
                region->StopAllocating();
            }

            // if a GC took the region while it stayed attached, start a new accounting period.
            region->RebaselineUsableIfStale(0);

            // try get from the free list
            if (region->StartAllocating(size))
            {
                // we have enough free space in the region to continue
                continue;
            }

            region->DetachFromAlocatingOwnerRelease();
            m_heap->Recycler()->AddEphemeralRegion(region);
        }

        // get a new regular region.
        _ASSERTE(SatoriRegion::RegionSizeForAlloc(size) == Satori::REGION_SIZE_GRANULARITY);

        region = nullptr;
        if (size < Satori::REGION_SIZE_GRANULARITY / 2)
        {
            region = m_heap->Recycler()->TryGetReusableForLarge();
            _ASSERTE(region == nullptr || !region->IsAllocating());
        }

        if (!region)
        {
            region = GetRegion(Satori::REGION_SIZE_GRANULARITY);
            if (!region)
            {
                //OOM
                m_pinnedAllocLock.Leave();
                return nullptr;
            }

            _ASSERTE(region->NothingMarked());
        }

        region->TryCommit();

        region->AttachToAllocatingOwner(&m_pinnedRegion);
        region->SetGenerationRelease(1);
        region->ResetReusableForRelease();
    }
}

SatoriObject* SatoriAllocator::AllocImmortal(SatoriAllocationContext* context, size_t size, uint32_t flags)
{
    // immortal allocs should be way less than region size.
    _ASSERTE(size < Satori::REGION_SIZE_GRANULARITY / 2);

    SatoriLockHolder holder(&m_immortalAllocLock);
    SatoriRegion* region = m_immortalRegion;

    while (true)
    {
        if (region)
        {
            size_t allocRemaining = region->GetAllocRemaining();
            if (allocRemaining >= size)
            {
                bool zeroInitialize = !(flags & GC_ALLOC_ZEROING_OPTIONAL);
                SatoriObject* result = (SatoriObject*)region->Allocate(size, zeroInitialize);
                if (result)
                {
                    region->SetIndicesForObject(result, result->Start() + size);
                    OnLargeOrPinnedAllocated(context, flags, result, size);
                }
                else
                {
                    // OOM
                }

                return result;
            }

            if (region->IsAllocating())
            {
                region->StopAllocating();
            }
            else
            {
                if (region->StartAllocating(size))
                {
                    // we have enough free space in the region to continue
                    continue;
                }
            }
        }

        // get a new regular region.
        region = GetRegion(Satori::REGION_SIZE_GRANULARITY);
        if (!region)
        {
            //OOM
            return nullptr;
        }

        _ASSERTE(region->NothingMarked());
        // Clear the dirty part of the region eagerly to not clear for each allocated (and typically small) object.
        // And, while at that, link the previous one in case we have a reason to iterate old regions.
        region->ZeroInitAndLink(m_immortalRegion);
        if (m_immortalRegion)
        {
            m_immortalRegion->DetachFromAlocatingOwnerRelease();
        }

        region->AttachToAllocatingOwner(&m_immortalRegion);
        region->SetGenerationRelease(3);
    }

    return nullptr;
}

void SatoriAllocator::DeactivateSharedRegion(SatoriRegion* region, bool promoteAllRegions)
{
    if (region)
    {
        if (region->IsAllocating())
        {
            region->StopAllocating();
        }

        if (promoteAllRegions)
        {
            region->DetachFromAlocatingOwnerRelease();
        }

        m_heap->Recycler()->AddEphemeralRegion(region);
    }
}

void SatoriAllocator::DeactivateSharedRegions(bool promoteAllRegions)
{
    SatoriRegion* immortalRegion = m_immortalRegion;
    if (immortalRegion && immortalRegion->IsAllocating())
    {
        immortalRegion->StopAllocating();
    }

    DeactivateSharedRegion(m_pinnedRegion, promoteAllRegions);
    DeactivateSharedRegion(m_largeRegion, promoteAllRegions);
    DeactivateSharedRegion(m_regularRegion, promoteAllRegions);
}

SatoriWorkChunk* SatoriAllocator::TryGetWorkChunk()
{
#if _DEBUG
    static int i = 0;
    // simulate low memory case once in a while
    // This is just to force more overflows. Otherwise they are very rare.
    if (i++ % 2 == 0)
    {
        return nullptr;
    }
#endif
    SatoriWorkChunk* chunk = m_workChunks->TryPop();

    while (!chunk && AddMoreWorkChunks())
    {
        chunk = m_workChunks->TryPop();
    }

    // chunks may be returned not cleared (see ReturnWorkChunks)
    if (chunk)
    {
        chunk->Clear();
    }

    return chunk;
}

// returns NULL only in OOM case
SatoriWorkChunk* SatoriAllocator::GetWorkChunk()
{
    SatoriWorkChunk* chunk = m_workChunks->TryPop();
    while (!chunk && AddMoreWorkChunks())
    {
        chunk = m_workChunks->TryPop();
    }

    // chunks may be returned not cleared (see ReturnWorkChunks)
    if (chunk)
    {
        chunk->Clear();
    }

    return chunk;
}

bool SatoriAllocator::AddMoreWorkChunks()
{
    SatoriRegion* region = GetRegion(Satori::REGION_SIZE_GRANULARITY);
    if (!region)
    {
        return false;
    }

    while (true)
    {
        size_t mem = region->Allocate(Satori::MARK_CHUNK_SIZE, /*zeroInitialize*/ false);
        if (!mem)
        {
            break;
        }

        SatoriWorkChunk* chunk = SatoriWorkChunk::InitializeAt(mem);
        m_workChunks->Push(chunk);
    }

    return true;
}

void SatoriAllocator::ReturnWorkChunk(SatoriWorkChunk* chunk)
{
    _ASSERTE(chunk->Count() == 0);
    m_workChunks->Push(chunk);
}

// Returns a chain of chunks at once, linked through Next() from the first to the last.
// Clearing every chunk here would be a cache miss per chunk. Chunks are cleared when taken instead.
void SatoriAllocator::ReturnWorkChunks(SatoriWorkChunk* first, SatoriWorkChunk* last)
{
    m_workChunks->PushChain(first, last);
}

// Same as above, when the last chunk is not known.
void SatoriAllocator::ReturnWorkChunks(SatoriWorkChunk* first)
{
    SatoriWorkChunk* last = first;
    while (last->Next())
    {
        last = last->Next();
    }

    ReturnWorkChunks(first, last);
}
