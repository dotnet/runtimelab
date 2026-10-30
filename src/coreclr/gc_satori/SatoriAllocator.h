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

    SatoriRegion* GetRegion(size_t minSize);
    void AddRegion(SatoriRegion* region);
    void ReturnRegion(SatoriRegion* region);

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
    struct DECLSPEC_ALIGN(Satori::CACHE_LINE_GRANULARITY) AllocatedBytesCounter
    {
        size_t bytes;
        // the value of "bytes" at the last allocation tick. (see MaybeFireAllocationTick)
        size_t bytesAtLastTick;
    };

    SatoriHeap* m_heap;
    SatoriShardedRegionQueue* m_queues[Satori::ALLOCATOR_BUCKET_COUNT];
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
    DECLSPEC_ALIGN(64)
    volatile int32_t m_singePageAdders;

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
    bool AddMoreWorkChunks();

    static int SizeToBucket(size_t size)
    {
        _ASSERTE(size >= Satori::REGION_SIZE_GRANULARITY);

        DWORD highestBit;
        BitScanReverse64(&highestBit, size);
        return min((int)highestBit - Satori::REGION_BITS, Satori::ALLOCATOR_BUCKET_COUNT - 1);
    }
};

#endif
