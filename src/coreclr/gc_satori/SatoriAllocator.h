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
// SatoriAllocator.h
//

#ifndef __SATORI_ALLOCATOR_H__
#define __SATORI_ALLOCATOR_H__

#include "common.h"
#include "../gc.h"
#include "SatoriUtil.h"

#include "SatoriRegionQueue.h"

class SatoriHeap;
class SatoriPage;
class SatoriRegion;
class SatoriObject;
class SatoriAllocationContext;
class SatoriWorkChunk;
class SatoriShardedWorkList;

enum AllocationTickKind
{
    Small = 0,
    Large = 1,
    Pinned = 2,
};

class SatoriAllocator
{
public:
    void Initialize(SatoriHeap* heap);
    Object* Alloc(SatoriAllocationContext* context, size_t size, uint32_t flags);

    // A region with at least commitSize bytes from its start committed, (a whole number of watermark units) for the memory that
    // its first use needs. The claim commits that before it publishes the region, and a failed commit gives the claim back,
    // so a region that a caller gets cannot fail its first use for want of memory and never has to be returned unused.
    SatoriRegion* GetRegion(size_t minSize, size_t commitSize = 0);
    SatoriRegion* GetRegionLongLived(size_t minSize, size_t commitSize = 0);
    void ReturnRegion(SatoriRegion* region);
    // Gives the regions from the cache back to the free granules of their pages, where the trimmer can see them.
    // Only where no thread scans a stack: in the trimmer, or in a trim on request after the GC has marked.
    void DrainRegionCache();

    size_t GetTotalAllocatedBytes();
    size_t GetTotalAllocatedBytesPrecise();
    void PublishAllocatedBytesOnThreadExit(SatoriAllocationContext* context);

    SatoriWorkChunk* TryGetWorkChunk();
    SatoriWorkChunk* GetWorkChunk();
    void ReturnWorkChunk(SatoriWorkChunk* chunk);
    void ReturnWorkChunks(SatoriWorkChunk* first, SatoriWorkChunk* last);
    void ReturnWorkChunks(SatoriWorkChunk* first);

    void DeactivateSharedRegion(SatoriRegion* region, bool promoteAllRegions);
    void DeactivateSharedRegions(bool promoteAllRegions);

private:
    // Bytes allocated by threads, as published by the threads. (see PublishAllocatedBytes)
    // Each counter is on its own cache line.
    struct DECLSPEC_ALIGN(Satori::WIDE_CACHE_GRANULARITY) AllocatedBytesCounter
    {
        size_t bytes;
        // the value of "bytes" at the last allocation tick. (see MaybeFireAllocationTick)
        size_t bytesAtLastTick;
    };

    SatoriHeap* m_heap;
    SatoriShardedWorkList* m_workChunks;

    SatoriRegion* m_regularRegion;
    SatoriRegion* m_largeRegion;
    SatoriRegion* m_pinnedRegion;
    SatoriRegion* m_immortalRegion;

    int m_smallAllocatedBytesCounterCount;

    // the allocation tick event was enabled when last checked. (see IsAllocationTickEnabled)
    volatile bool m_allocationTickWasEnabled;

    DECLSPEC_ALIGN(64)
    SatoriLock m_regularAllocLock;

    DECLSPEC_ALIGN(64)
    SatoriLock m_immortalAllocLock;

    DECLSPEC_ALIGN(64)
    SatoriLock m_largeAllocLock;

    DECLSPEC_ALIGN(64)
    SatoriLock m_pinnedAllocLock;

    // Work chunks are added by one thread at a time (see AddMoreWorkChunks), the others wait for it by polling.
    // Thus nobody may ask for a work chunk while holding what the refilling thread needs to get a region.
    // Lock order: the allocation locks, this lock, m_pageAddLock, m_runClaimLock.
    DECLSPEC_ALIGN(64)
    SatoriLock m_workChunkRefillLock;

    // Serializes the threads that claim runs of granules for big regions. (see SatoriPage::TryClaimRun)
    // Claims of single granules do not take it. Nothing else is locked while holding it.
    DECLSPEC_ALIGN(64)
    SatoriLock m_runClaimLock;

    // the shard to publish the next chain of fresh work chunks to. Protected by m_workChunkRefillLock.
    size_t m_workChunkRefillShard;

    // Small objects are allocated most often, thus their counter is sharded,
    // one counter per ALLOCATED_BYTES_COUNTER_PROCESSORS processors.
    // Large and pinned objects are allocated less often and have a single counter each.
    // TODO: Ideally the sharding would consider NUMA nodes. We may also want to shard
    //       the large and pinned counters per NUMA node. Not yet.
    AllocatedBytesCounter m_smallAllocatedBytes[Satori::MAX_ALLOCATED_BYTES_COUNTERS];
    AllocatedBytesCounter m_largeAllocatedBytes;
    AllocatedBytesCounter m_pinnedAllocatedBytes;

    // What terminated threads have allocated, precisely. (see GetTotalAllocatedBytesPrecise)
    size_t m_deadThreadsAllocatedBytes;

    // A floor for approximate reads after a precise total has been observed.
    size_t m_lastPreciseAllocatedBytes;

private:
    // A small cache of free 2 MB regions in front of the free granules. Regular requests take from the cache first,
    // and the regions that are freed go there, while the cache is below its cap. That keeps the reuse of the regions
    // that were used recently, and the allocation of a region a pop from a queue.
    // The cached regions are formatted, have valid headers and are members of the queue, so a header may be read
    // only under the lock of the queue. (the queue is of the Allocator kind)
    SatoriShardedRegionQueue* m_regionCache;

    // How many regions the cache may have: a couple for every CPU that this process can use. The racing threads can get the count a bit over it.
    size_t m_regionCacheCap;

    DECLSPEC_ALIGN(64)
    volatile size_t m_regionCacheCount;

    // Regular pages are added by one thread at a time, see GetRegionCore. A thread that waits for this lock longer than
    // PAGE_ADD_LOCK_TIMEOUT_MS may add its page without it, one such thread at a time (m_pageAddEscapee).
    // Lock order: the allocation locks and m_workChunkRefillLock, this lock, then m_runClaimLock (the thread that adds a page claims
    // a run in it under this lock, see TryClaimInPage), the locks of the queues and the map lock of the heap (SatoriHeap::CommitMoreMap).
    // Nothing takes this lock while holding any of the locks that come after it.
    DECLSPEC_ALIGN(64)
    SatoriLock m_pageAddLock;

    // The number of the regular page adds that have completed, with the lock or without it (interlocked),
    // after the added page can be claimed from.
    volatile int32_t m_pageAddsCompleted;

    // 1 while a thread adds a regular page without the page-add lock, after its wait for the lock has timed out.
    // A slow add makes all its waiters time out at about the same time. Only the one that sets this adds a page;
    // the others wait again, and the page of either add will do for them.
    volatile int32_t m_pageAddEscapee;

    int SmallAllocatedBytesCounterIndex();
    void PublishAllocatedBytes(SatoriAllocationContext* context, AllocationTickKind kind, SatoriObject* obj, size_t size);
    void PublishAllocatedBytesIfNeeded(SatoriAllocationContext* context, AllocationTickKind kind, SatoriObject* obj, size_t size);
    void OnLargeOrPinnedAllocated(SatoriAllocationContext* context, uint32_t flags, SatoriObject* obj, size_t size);
    bool IsAllocationTickEnabled();
    static void CatchUpAllocationTick(AllocatedBytesCounter* counter);
    static void MaybeFireAllocationTick(AllocatedBytesCounter* counter, size_t total, AllocationTickKind kind, SatoriObject* obj, size_t size);

    void UpdateAllocStatsAndHelpIfNeeded(SatoriAllocationContext * context);
    SatoriObject* AllocRegular(SatoriAllocationContext* context, size_t size, uint32_t flags);
    SatoriObject* AllocRegularShared(SatoriAllocationContext* context, size_t size, uint32_t flags);
    SatoriObject* AllocLarge(SatoriAllocationContext* context, size_t size, uint32_t flags);
    SatoriObject* AllocLargeShared(SatoriAllocationContext* context, size_t size, uint32_t flags);
    SatoriObject* AllocHuge(SatoriAllocationContext* context, size_t size, uint32_t flags);
    SatoriObject* AllocPinned(SatoriAllocationContext* context, size_t size, uint32_t flags);
    SatoriObject* AllocImmortal(SatoriAllocationContext* context, size_t size, uint32_t flags);

    void TryGetRegularRegion(SatoriRegion*& region, size_t size);
    SatoriRegion* GetRegionCore(size_t regionSize, bool longLived, size_t commitSize);
    void LeavePageAdd(bool locked, bool escaped);
    SatoriRegion* TryClaimRegion(size_t granules, bool longLived, size_t commitSize, bool& outOfMemory);
    size_t TryClaimInPage(SatoriPage* page, size_t granules, bool longLived);
    SatoriRegion* TryClaimWarmRegion(size_t granules, bool longLived, size_t commitSize);
    SatoriRegion* TryGetCachedRegion();
    bool TryCacheRegion(SatoriRegion* region);
    void FreeToBitmap(SatoriRegion* region);
    bool AddMoreWorkChunks();
};

#endif
