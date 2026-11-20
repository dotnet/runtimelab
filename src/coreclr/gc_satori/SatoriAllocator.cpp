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
#include "SatoriPage.inl"
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

// Where threads on different processors start their search for free granules.
static size_t ProcessorHint()
{
    if (GCToOSInterface::CanGetCurrentProcessorNumber())
    {
        // Mixes in the upper bits (a Gray code), as the sharded queues do. (see CurrentShardIndex in SatoriRegionQueue.h)
        // Where the two logical processors of a core are numbered 2c and 2c+1 (Windows, some Linux systems), threads
        // that are spread one per core can all have even numbers, and the mix spreads those too.
        // Where they are numbered half the processor count apart (other Linux systems), the mix is not needed.
        // ForEachPageFrom takes the hint modulo the number of page map entries in use, so the spread is even only when that is a power of 2.
        uint32_t processor = GCToOSInterface::GetCurrentProcessorNumber();
        return processor ^ (processor >> 1);
    }

    return (size_t)((SatoriUtil::GetCurrentThreadTag() * 11400714819323198485llu) >> 32);
}

void SatoriAllocator::Initialize(SatoriHeap* heap)
{
    m_heap = heap;

    int processorCount = (int)GCToOSInterface::GetTotalProcessorCount();

    m_workChunks = SatoriShardedWorkList::AllocAlignedForProcessors(4);

    m_immortalRegion = nullptr;
    m_immortalAllocLock.Initialize();

    m_pinnedRegion = nullptr;
    m_pinnedAllocLock.Initialize();

    m_workChunkRefillLock.Initialize();
    m_workChunkRefillShard = 0;

    m_runClaimLock.Initialize();

    m_regionCache = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::Allocator, 4);
    m_regionCacheCount = 0;
    // The cap is a couple of regions for every CPU that this process can use.
    m_regionCacheCap = max((size_t)4, 2 * (size_t)GCToEEInterface::GetCurrentProcessCpuCount());

    m_largeRegion = nullptr;
    m_largeAllocLock.Initialize();

    m_regularRegion = nullptr;
    m_regularAllocLock.Initialize();

    m_pageAddLock.Initialize();
    m_pageAddsCompleted = 0;
    m_pageAddEscapee = 0;

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

SatoriRegion* SatoriAllocator::GetRegion(size_t regionSize, size_t commitSize)
{
    return GetRegionCore(regionSize, /* longLived */ false, commitSize);
}

// Same as GetRegion, but for the regions that are long-lived and rarely freed, like the ones for immortal allocations,
// work chunks or the finalization queue. Such regions are placed at the opposite end of a page from the regular ones,
// at the highest free granules, so that they do not fragment the space that the regular regions fill.
SatoriRegion* SatoriAllocator::GetRegionLongLived(size_t regionSize, size_t commitSize)
{
    return GetRegionCore(regionSize, /* longLived */ true, commitSize);
}

// Gets a region of the given size from the free granules of the pages, and adds a page if there are none.
// A regular request first takes a region from the cache, and then claims the lowest free granules,
// the ones that are committed in full first. A request for a long-lived region takes the highest ones.
// A request that needs memory committed (commitSize) does not take a region from the cache, which has whatever its last owner committed.
SatoriRegion* SatoriAllocator::GetRegionCore(size_t regionSize, bool longLived, size_t commitSize)
{
    _ASSERTE(regionSize % Satori::REGION_SIZE_GRANULARITY == 0);
    size_t granules = regionSize >> Satori::REGION_BITS;

    SatoriRegion* region;
    bool outOfMemory;

tryAgain:
    // The regular page adds that complete after this are not seen by the search for regions below. (see the add of a page)
    int32_t pageAddsCompleted = VolatileLoad(&m_pageAddsCompleted);
    if (granules == 1 && !longLived && commitSize == 0)
    {
        region = m_regionCache->TryPop();
        if (region)
        {
            Interlocked::Decrement(&m_regionCacheCount);
            return region;
        }
    }

    outOfMemory = false;
    region = TryClaimRegion(granules, longLived, commitSize, outOfMemory);
    if (region)
    {
        return region;
    }

    if (outOfMemory)
    {
        goto failed;
    }

    // for regions < 1/2 page granularity, just allocate a regular page
    // this is the most common case.
    // for bigger ones, use more complex helper that may allocate a larger
    // page, if needed.
    if (regionSize < Satori::PAGE_SIZE_GRANULARITY / 2)
    {
        // Adding a regular-sized Page.
        // We will often come here on multiple threads and we do not want all threads to add a page.
        // A page has regions for all of them, and an add takes as long for one thread as it does for many.
        // A thread that adds a page while another add is in flight gets a region no sooner than by waiting,
        // and the page that it adds is not needed. So the adds are one at a time, under a lock.
        // A thread that gets the lock after another one has added a page finds that the count of completed adds
        // is not the one that it saw before it looked for regions, and looks again.
        // The wait for the lock is bounded (PAGE_ADD_LOCK_TIMEOUT_MS; on Windows the wait ends on a tick of the system timer): a thread
        // that waits that long, behind an add that takes unusually long or an adder that is not running, may add a page without the lock.
        // Only one thread at a time may (m_pageAddEscapee). The waiters of a slow add time out at about the same time, and if each
        // of them added a page, one slow add would bring as many pages as it has waiters. The others wait again.
        // A thread still looks again first if an add has completed meanwhile. The page-add lock is taken before m_mapLock (CommitMoreMap),
        // never after it, and the add without the lock takes m_mapLock as usual.
        bool locked = m_pageAddLock.TryEnterFor(Satori::PAGE_ADD_LOCK_TIMEOUT_MS);
        bool escaped = false;
        while (!locked && m_pageAddsCompleted == pageAddsCompleted)
        {
            if (Interlocked::CompareExchange(&m_pageAddEscapee, 1, 0) == 0)
            {
                escaped = true;
                break;
            }

            locked = m_pageAddLock.TryEnterFor(Satori::PAGE_ADD_LOCK_TIMEOUT_MS);
        }

        if (m_pageAddsCompleted != pageAddsCompleted)
        {
            LeavePageAdd(locked, escaped);
            goto tryAgain;
        }

        _ASSERTE(locked != escaped);
        SatoriPage* page = nullptr;
        if (m_heap->TryAddRegularPage(page))
        {
            if (page)
            {
                // The page is ours. Claim from it right away, before the others take what we need.
                // Only the claim is done under the lock, it is a few bit operations. The format commits what the region needs,
                // which can be a huge object of hundreds of MB, and the waiters do not wait for that: a waiter that timed out
                // could add a page of its own while this one has room.
                size_t first = TryClaimInPage(page, granules, longLived);

                // The free granules of the page can be claimed, and we have ours. The waiters look for theirs when they see the change.
                // An add without the lock counts as well: the count is interlocked, and it changes before the escapee's mark is cleared.
                Interlocked::Increment(&m_pageAddsCompleted);
                LeavePageAdd(locked, escaped);

                if (first == SIZE_MAX)
                {
                    goto tryAgain;
                }

                region = page->FormatClaimed(first, granules, commitSize);
                if (region)
                {
                    return region;
                }

                // Out of memory. The granules went back to the page.
                goto failed;
            }

            // Someone added a page, but not us. There could be more regions.
            Interlocked::Increment(&m_pageAddsCompleted);
            LeavePageAdd(locked, escaped);
            goto tryAgain;
        }

        LeavePageAdd(locked, escaped);
    }
    else
    {
        // A region of half a page or more may need a larger page. It is claimed in the page before the page is published.
        SatoriPage* page = m_heap->AddLargePage(regionSize, longLived, region, commitSize);
        if (page)
        {
            _ASSERTE(region);
            return region;
        }
    }

    // Most likely out of memory, and the page-add lock is not held here. A request for one granule can still get a region
    // from the cache: it is formatted, and needs no commit. A long-lived region has not tried the cache before, and the regular one only did at the start.
    // Not a request that needs memory committed.
failed:
    if (granules == 1 && commitSize == 0)
    {
        region = TryGetCachedRegion();
        if (region)
        {
            return region;
        }
    }

    return nullptr;
}

// Ends a regular page add: leaves the page-add lock, or lets another thread whose wait times out add without it.
void SatoriAllocator::LeavePageAdd(bool locked, bool escaped)
{
    if (locked)
    {
        m_pageAddLock.Leave();
    }
    else if (escaped)
    {
        Interlocked::Exchange(&m_pageAddEscapee, 0);
    }
}

// A region from the cache. It is formatted, so it needs no commit.
SatoriRegion* SatoriAllocator::TryGetCachedRegion()
{
    SatoriRegion* region = m_regionCache->TryPop();
    if (region)
    {
        Interlocked::Decrement(&m_regionCacheCount);
    }

    return region;
}

// The region that a claim got could not be committed, but that does not mean that nothing can be had: a region that is committed in full
// needs no commit. Looks for one in the order of the claims. The candidate that failed went back to its page as it was and is not warm.
// A granule or a run that is not committed in full after its claim goes back, and the search goes on: the trimmer can decommit a granule between
// the look at its warm bit and the claim, and then the trimmer has cleared the bit, so the granule is not found again.
// What is committed in full has what any commitSize asks for.
SatoriRegion* SatoriAllocator::TryClaimWarmRegion(size_t granules, bool longLived, size_t commitSize)
{
    SatoriRegion* result = nullptr;
    if (granules > 1)
    {
        SatoriPage* claimedPage = nullptr;
        size_t first = SIZE_MAX;

        m_runClaimLock.Enter();
        m_heap->ForEachPageFrom(0,
            [&](SatoriPage* page)
            {
                while ((first = page->TryClaimRun(granules, /* fromTop */ longLived, /* warmOnly */ true)) != SIZE_MAX)
                {
                    if (page->IsCommittedInFull(first, granules))
                    {
                        claimedPage = page;
                        return true;
                    }

                    page->PublishFreeGranules(first, granules);
                }

                return false;
            }
        );
        m_runClaimLock.Leave();

        if (claimedPage)
        {
            result = claimedPage->FormatClaimed(first, granules, commitSize);
        }
    }
    else
    {
        size_t startHint = longLived ? 0 : ProcessorHint();
        m_heap->ForEachPageFrom(startHint,
            [&](SatoriPage* page)
            {
                size_t granule;
                while ((granule = page->TryClaimGranule(/* warmOnly */ true, /* fromTop */ longLived)) != SIZE_MAX)
                {
                    if (page->IsCommittedInFull(granule, 1))
                    {
                        result = page->FormatClaimed(granule, 1, commitSize);
                        return result != nullptr;
                    }

                    page->PublishFreeGranules(granule, 1);
                }

                return false;
            }
        );
    }

    return result;
}

// Claims free granules for a region of the given number of granules, in the pages that we have.
// Returns nullptr if there is nothing free, or if what we claimed cannot be committed. (outOfMemory)
SatoriRegion* SatoriAllocator::TryClaimRegion(size_t granules, bool longLived, size_t commitSize, bool& outOfMemory)
{
    SatoriRegion* result = nullptr;

    if (granules > 1)
    {
        // Runs are rare, all of them are for huge objects. The first page that has room is as good as any.
        SatoriPage* claimedPage = nullptr;
        size_t first = SIZE_MAX;

        m_runClaimLock.Enter();
        m_heap->ForEachPageFrom(0,
            [&](SatoriPage* page)
            {
                first = page->TryClaimRun(granules, /* fromTop */ longLived);
                if (first == SIZE_MAX)
                {
                    return false;
                }

                claimedPage = page;
                return true;
            }
        );
        m_runClaimLock.Leave();

        if (claimedPage)
        {
            result = claimedPage->FormatClaimed(first, granules, commitSize);
            outOfMemory = (result == nullptr);
        }
    }
    else
    {
        // Threads on different processors start with different pages.
        // Requests for long-lived regions start with the first page, to keep those regions together.
        size_t startHint = longLived ? 0 : ProcessorHint();

        // Regular requests take a region that is committed in full, if there is one. Others take any.
        for (int pass = longLived ? 1 : 0; pass < 2 && !result && !outOfMemory; pass++)
        {
            bool warmOnly = pass == 0;
            m_heap->ForEachPageFrom(startHint,
                [&](SatoriPage* page)
                {
                    size_t granule = page->TryClaimGranule(warmOnly, /* fromTop */ longLived);
                    if (granule == SIZE_MAX)
                    {
                        return false;
                    }

                    result = page->FormatClaimed(granule, 1, commitSize);
                    outOfMemory = (result == nullptr);
                    return true;
                }
            );
        }
    }

    if (outOfMemory)
    {
        // What was claimed first could not be committed, and a claim that fails to format ends the search above.
        // A region that is committed in full needs no commit, and there may be one.
        result = TryClaimWarmRegion(granules, longLived, commitSize);
        outOfMemory = (result == nullptr);
    }

    return result;
}

// Claims free granules for a region in one page, and nothing more: the caller formats them. (see the add of a page in GetRegionCore)
size_t SatoriAllocator::TryClaimInPage(SatoriPage* page, size_t granules, bool longLived)
{
    if (granules > 1)
    {
        m_runClaimLock.Enter();
        size_t first = page->TryClaimRun(granules, /* fromTop */ longLived);
        m_runClaimLock.Leave();
        return first;
    }

    return page->TryClaimGranule(/* warmOnly */ false, /* fromTop */ longLived);
}

// Returns a region that its owner no longer uses: to the front of the cache, as the most recently used one, or to the free granules.
// In the conservative mode not while a thread scans a stack. (see FreeToBitmap)
void SatoriAllocator::ReturnRegion(SatoriRegion* region)
{
    _ASSERTE(region->IsAttachedToAllocatingOwner() == false);
    _ASSERTE(region->Generation() == -1);
    if (!TryCacheRegion(region))
    {
        FreeToBitmap(region);
    }
}

// Puts a 2 MB region that its owner no longer uses at the front of the cache, if the cache is not full.
bool SatoriAllocator::TryCacheRegion(SatoriRegion* region)
{
    if (region->Size() != Satori::REGION_SIZE_GRANULARITY)
    {
        return false;
    }

    if (m_regionCacheCount >= m_regionCacheCap)
    {
        return false;
    }

    // The count is ahead of the queue, so a pop never takes the count below 0.
    Interlocked::Increment(&m_regionCacheCount);
    m_regionCache->Push(region);
    return true;
}

// Frees the region into the free granules of its page.
//
// The conservative mode. A thread that scans a stack finds the region of a candidate pointer in the region map, and then reads its header.
// If the region were freed in between, its granules could be claimed and formatted again at the same address, or become a part of a bigger
// run, whose owner writes over the old header, (a huge object, the array of the finalization queue) while the scan is still reading.
// So no region is freed into the free granules while a thread may scan a stack. The frees are ordered outside of the scans:
//  - The sweep after a GC is done before the next GC scans a stack. (see DrainDeferredSweepQueueConcurrent)
//  - A GC frees regions in the phases that scan no stacks, with all the helpers out of the phase in between. (see RunWithHelp)
//  - The trimmer frees the regions from the cache only while no GC marks.
// What can run while a thread scans a stack does not free a region. A claim commits what the first use of its region needs before it
// publishes the region, and a claim that fails gives back granules that no thread has seen as a region. (see SatoriPage::FormatClaimed)
// Checked builds assert that no region is freed into the free granules while a thread scans a stack. (see SatoriPage::FreeRegion)
void SatoriAllocator::FreeToBitmap(SatoriRegion* region)
{
    region->ContainingPage()->FreeRegion(region);
}

void SatoriAllocator::DrainRegionCache()
{
    while (SatoriRegion* region = m_regionCache->TryPop())
    {
        Interlocked::Decrement(&m_regionCacheCount);
        FreeToBitmap(region);
    }
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

// The region that follows `region` in its page, if it is in a reusable queue and fits the request, taken out of that queue.
//
// The next region is not ours to look at. It can be freed meanwhile, and the granule of a free region is decommitted by the trimmer.
// This is the only place where a thread reads the header of a region that it does not own, and it is safe for these reasons:
//  - The thread registers with the page (EnterUnownedHeaderRead) before it reads the region map, and leaves after its last read
//    of the header. The trimmer does not decommit while a thread is registered with the page.
//  - A region is removed from the map before its granules are marked free, and the trimmer claims only free granules, and looks
//    at the registrations after it claims. So if the map has a region at `next` after we registered, then either the region is
//    still there, or it was freed and the trimmer sees us and leaves the memory committed. Our registration is ordered before our read
//    of the map (see EnterUnownedHeaderRead). The claim of the trimmer is an acquire, so its look at the registrations
//    is ordered after the free bit that it took (see TryTrimRun). The map is read with an acquire (see NextInPage).
//  - The header of a region that was freed holds what it held, and FreeRegion asserts that its queue is null, so the read finds no queue.
//    A region that was freed and is in use again by somebody else is a region, whatever it is used for, and its queue is what it is.
//    The removal checks the queue under its lock.
//  - The granule of a freed region can also be claimed as a part of a run, for a bigger region (a huge object, the finalization queue).
//    The old header is then the body of that region, and it can hold anything. It stays committed (a region in use decommits only above the start
//    of its last granule, see SatoriRegion::TryDecommitAbove), and the queue that we read from it is used only if it is one of the reusable queues.
// The free granule after the region, which the caller claims, has no header. It is a bit operation, and reads no memory of the granule.
// Other readers get to headers through the region map too (conservative marking, interior pointers, the card groups).
// They run only on regions that are live: in the precise mode a reference is to a live object, and a card group is set only for a live region.
// In the conservative mode a marker can follow a candidate pointer to a region that is not live any more, so no region is freed into the
// free granules while a thread may scan a stack. (see FreeToBitmap)
static SatoriRegion* TryTakeReusableNext(SatoriPage* page, SatoriRegion* region, size_t minFree)
{
    SatoriRegion* result = nullptr;

    page->EnterUnownedHeaderRead();
    SatoriRegion* next = region->NextInPage();
    if (next)
    {
#if _DEBUG
        page->AssertHeaderIsReadable((next->Start() - page->Start()) >> Satori::REGION_BITS);
#endif
        SatoriQueue<SatoriRegion>* nextContainingQueue = next->ContainingQueue();
        if (nextContainingQueue &&
            page->Heap()->Recycler()->IsReusableQueue(nextContainingQueue) &&
            next->CanFitAllocation(minFree) &&
            nextContainingQueue->TryRemove(next))
        {
            result = next;
        }
    }

    page->LeaveUnownedHeaderRead();
    return result;
}

// Gets a region for a regular allocation of the given size. Prefers reusable regions, but a reusable region
// that cannot serve the request from its free lists would just be retired on the spot while charging
// the budget for its unused space. Such regions are skipped and left to be used for smaller requests.
void SatoriAllocator::TryGetRegularRegion(SatoriRegion*& region, size_t size)
{
    size_t minFree = size + Satori::MIN_FREE_SIZE;

    // opportunistically try get the next region, if available
    SatoriPage* page = nullptr;
    size_t nextStart = 0;

    if (region)
    {
        page = region->ContainingPage();
        nextStart = region->End();

        SatoriRegion* next = TryTakeReusableNext(page, region, minFree);
        if (next)
        {
            region = next;
            _ASSERTE(region->Occupancy() < Satori::REGION_SIZE_GRANULARITY);
            return;
        }
    }

    region = m_heap->Recycler()->TryGetReusable(minFree);

    if (region == nullptr)
    {
        // The granule right after the region, if it is free. That keeps the regions of a thread together.
        // Free memory has no header to read, so this takes a claim and nothing else.
        if (page && nextStart < page->End())
        {
            size_t granule = (nextStart - page->Start()) >> Satori::REGION_BITS;
            if (page->TryClaimGranuleAt(granule))
            {
                region = page->FormatClaimed(granule, 1, /* commitSize */ 0);
                if (region)
                {
                    _ASSERTE(region->NothingMarked());
                    return;
                }
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

    // Nothing may fail once the region is published: in the conservative mode it could not be freed then, since this can run
    // while a thread scans a stack. (see SatoriPage::FreeRegion) So the work chunk that tracks a finalizable object is taken first,
    // and the claim commits the memory for the object before it publishes the region. (see SatoriPage::FormatClaimed)
    SatoriWorkChunk* tracker = nullptr;
    if (flags & GC_ALLOC_FINALIZE)
    {
        tracker = GetWorkChunk();
        if (!tracker)
        {
            // OOM
            return nullptr;
        }
    }

    SatoriRegion* hugeRegion = GetRegion(regionSize, SatoriRegion::HugeCommitSize(regionSize, size));
    if (!hugeRegion)
    {
        if (tracker)
        {
            ReturnWorkChunk(tracker);
        }

        //OOM
        return nullptr;
    }

    _ASSERTE(hugeRegion->NothingMarked());
    bool zeroInitialize = !(flags & GC_ALLOC_ZEROING_OPTIONAL);
    SatoriObject* result = (SatoriObject*)hugeRegion->AllocateHuge(size, zeroInitialize);
    _ASSERTE(result);

    result->CleanSyncBlockAndSetUnfinished();
    hugeRegion->SetIndicesForObject(result, hugeRegion->Start() + Satori::REGION_SIZE_GRANULARITY);

    // huge regions are not attached to contexts and in gen0+ would appear parseable,
    // but this one is not parseable yet since the new object has no MethodTable
    // we will keep the region in gen -1 for now and make it gen1 or gen2 in PublishObject.
    hugeRegion->StopAllocating();

    if (flags & GC_ALLOC_FINALIZE)
    {
        hugeRegion->SetHasFinalizables();
        hugeRegion->RegisterForFinalization(result, tracker);
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

        // get a new regular region. It will stay forever, so place it at the highest free granules of a page.
        region = GetRegionLongLived(Satori::REGION_SIZE_GRANULARITY);
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

// Refills the pool of work chunks, one thread at a time.
//
// Many threads run out of chunks together, e.g. when marking starts. If each of them took a region for chunks,
// we would have as many regions as there were waiters and most of the chunks would sit unused.
// Instead the one that gets the lock refills and the others poll for chunks, which they do as long as we return true.
// The chunks are published in chains as they are made, so the waiters do not wait for the whole region to be formatted,
// and the chains go to different shards, so that every shard has some.
//
// Returns false if there is no memory to get chunks.
bool SatoriAllocator::AddMoreWorkChunks()
{
    // One shot - TryEnter would spin in the lock, and the waiters need to be looking for the chunks as they are published.
    if (!m_workChunkRefillLock.TryEnterOneShot())
    {
        // Someone else is refilling and publishes chunks as it goes. Look for them or for the end of the refill.
        for (int i = 0; i < 32; i++)
        {
            if (!m_workChunks->IsEmpty() || !m_workChunkRefillLock.IsLocked())
            {
                return true;
            }

            YieldProcessor();
        }

        // Still nothing. Let the refilling thread run, in case it was preempted.
        GCToOSInterface::YieldThread(0);
        return true;
    }

    SatoriLockHolder holder(&m_workChunkRefillLock, /* isLocked */ true);

    // The pool could have been refilled, or chunks returned, since the caller looked.
    if (!m_workChunks->IsEmpty())
    {
        return true;
    }

    // The chunks stay forever, so place the region at the highest free granules of a page.
    SatoriRegion* region = GetRegionLongLived(Satori::REGION_SIZE_GRANULARITY);
    if (!region)
    {
        return false;
    }

    // Commit all at once, not in small steps as chunks are allocated.
    region->TryCommit();

    SatoriWorkChunk* first = nullptr;
    SatoriWorkChunk* last = nullptr;
    size_t chainLength = 0;
    size_t totalLength = 0;

    while (true)
    {
        size_t mem = region->Allocate(Satori::MARK_CHUNK_SIZE, /*zeroInitialize*/ false);
        if (!mem)
        {
            break;
        }

        SatoriWorkChunk* chunk = SatoriWorkChunk::InitializeAt(mem);
        if (last)
        {
            last->SetNext(chunk);
        }
        else
        {
            first = chunk;
        }

        last = chunk;
        totalLength++;
        if (++chainLength == Satori::WORK_CHUNK_REFILL_CHAIN)
        {
            m_workChunks->PushChainToShard(m_workChunkRefillShard++, first, last);
            first = last = nullptr;
            chainLength = 0;
        }
    }

    if (first)
    {
        m_workChunks->PushChainToShard(m_workChunkRefillShard++, first, last);
    }

    // The commit above can fail, but every region has the start of its body committed, which holds several chunks.
    // (see SatoriRegion::MinCommitSize) So there is no region to give back for want of memory, which in the conservative mode
    // could not be freed here: this can run while a thread scans a stack. Were there no chunk, the caller would be told that
    // there is no memory, rather than to look again and take another region.
    _ASSERTE(totalLength > 0);
    return totalLength > 0;
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
