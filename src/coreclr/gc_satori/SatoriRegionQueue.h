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
// SatoriRegionQueue.h
//

#ifndef __SATORI_REGION_QUEUE_H__
#define __SATORI_REGION_QUEUE_H__

#include "common.h"
#include "gcenv.h"
#include "../env/gcenv.os.h"
#include "../gc.h"
#include "SatoriUtil.h"
#include "SatoriQueue.h"

class SatoriRegion;

class SatoriRegionQueue : public SatoriQueue<SatoriRegion>
{
public:
    SatoriRegionQueue(QueueKind kind) :
        SatoriQueue<SatoriRegion>(kind) {}

    SatoriRegion* TryPopWithSize(size_t regionSize, SatoriRegion* &putBack);
    SatoriRegion* TryRemoveWithSize(
        size_t regionSize,
        SatoriRegion*& putBack,
        size_t maxRegions = 64,
        bool preferCommitted = false,
        size_t maxRegionSize = SIZE_MAX,
        bool requireCommitted = false,
        size_t* outBestCandidateSize = nullptr,
        bool* outBestCandidateNeedsCommit = nullptr);
    SatoriRegion* TryDequeueIfHasFreeSpaceInTopBucket();
    // Takes the first of the first maxScan regions that can serve an allocation of minFree bytes, if any.
    SatoriRegion* TryPopFitting(size_t minFree, size_t maxScan);

    static SatoriRegionQueue* AllocAligned(QueueKind kind);
};

// A region queue partitioned into N (power of 2) SatoriRegionQueue shards to reduce contention.
// Operations first try the shard for the current processor (or thread), and on failure visit all
// other shards in random order and stride so that any region in the queue remains visible to the
// caller. Each shard is the m_containingQueue of the regions it holds and carries the QueueKind.
class DECLSPEC_ALIGN(Satori::CACHE_LINE_GRANULARITY) SatoriShardedRegionQueue
{
public:
    class Batch : public SatoriRegionQueue::Batch
    {
        static const size_t ROTATE_SHARD_COUNT = 4;

    public:
        Batch(SatoriShardedRegionQueue* eventualQueue, bool prepend = false) :
            SatoriRegionQueue::Batch(eventualQueue->CurrentShard()),
            m_shardedQueue(eventualQueue),
            m_shardIndex(eventualQueue->CurrentShardIndex()),
            m_prepend(prepend)
        {}

        void Push(SatoriRegion* item)
        {
            RotateIfNeeded();
            SatoriRegionQueue::Batch::Push(item);
        }

        void Enqueue(SatoriRegion* item)
        {
            RotateIfNeeded();
            SatoriRegionQueue::Batch::Enqueue(item);
        }

    private:
        SatoriShardedRegionQueue* m_shardedQueue;
        size_t m_shardIndex;
        bool m_prepend;

        void RotateIfNeeded()
        {
            if (m_count >= ROTATE_SHARD_COUNT && m_shardedQueue->m_shardMask != 0)
            {
                if (m_prepend)
                {
                    m_queue->Prepend(this);
                }
                else
                {
                    m_queue->Append(this);
                }

                m_shardIndex = (m_shardIndex + 1) & m_shardedQueue->m_shardMask;
                m_queue = m_shardedQueue->Shard(m_shardIndex);
            }
        }
    };

    SatoriShardedRegionQueue(QueueKind kind, size_t shardCount) :
        m_shardMask(shardCount - 1),
        m_shards{ kind }
    {
        _ASSERTE(shardCount > 0 && (shardCount & (shardCount - 1)) == 0);
        for (size_t i = 1; i < shardCount; i++)
        {
            new (Shard(i)) SatoriRegionQueue(kind);
        }
    }

    static SatoriShardedRegionQueue* AllocAligned(QueueKind kind, size_t shardCount);

    static size_t ShardCountForProcessors(int processorsPerShard)
    {
        _ASSERTE(processorsPerShard > 0);
        int processorCount = (int)GCToOSInterface::GetTotalProcessorCount();
        return SatoriUtil::RoundUpPwr2((processorCount + processorsPerShard - 1) / processorsPerShard);
    }

    static SatoriShardedRegionQueue* AllocAlignedForProcessors(QueueKind kind, int processorsPerShard)
    {
        return AllocAligned(kind, ShardCountForProcessors(processorsPerShard));
    }

    void Push(SatoriRegion* item)
    {
        CurrentShard()->Push(item);
    }

    void Enqueue(SatoriRegion* item)
    {
        CurrentShard()->Enqueue(item);
    }

    void Append(SatoriRegionQueue::Batch* other)
    {
        other->Queue()->Append(other);
    }

    void Prepend(SatoriRegionQueue::Batch* other)
    {
        other->Queue()->Prepend(other);
    }

    void AppendUnsafe(SatoriShardedRegionQueue* other)
    {
        size_t otherShardCount = other->ShardCount();
        for (size_t i = 0; i < otherShardCount; i++)
        {
            Shard(i & m_shardMask)->AppendUnsafe(other->Shard(i));
        }
    }

    SatoriRegion* TryPop()
    {
        return TryOnShards(
            [](SatoriRegionQueue* shard)
            {
                return shard->TryPop();
            }
        );
    }

    SatoriRegion* PopOrPush(SatoriRegion* item)
    {
        SatoriRegion* result = TryPop();
        if (result != nullptr)
        {
            return result;
        }

        // Concurrent callers that all observe an empty queue in TryPop() may each push
        // into their own shard instead of pairing with one another. This is deliberate:
        // PopOrPush is used to seed relocation target queues when empty, where multiple
        // worker threads need targets concurrently and subsequent relocations consume them
        // from their local shards; at the tail of relocation at most one region per racing
        // shard can remain unpaired.
        return CurrentShard()->PopOrPush(item);
    }

    SatoriRegion* TryPopWithTryEnter()
    {
        return TryOnShards(
            [](SatoriRegionQueue* shard)
            {
                return shard->TryPopWithTryEnter();
            }
        );
    }

    SatoriRegion* TryPopDrainOnly()
    {
        size_t ownedShard = CurrentShardIndex();
        SatoriRegion* result = Shard(ownedShard)->TryPopDrainOnly</* prefetch */ true>();
        if (result != nullptr || m_shardMask == 0)
        {
            return result;
        }

        return StealFromOtherShards(
            ownedShard,
            [](SatoriRegionQueue* shard)
            {
                return shard->TryPopDrainOnly</* prefetch */ false>();
            }
        );
    }

    SatoriRegion* TryPopUnsafe()
    {
        size_t shardCount = ShardCount();
        for (size_t i = 0; i < shardCount; i++)
        {
            SatoriRegion* result = Shard(i)->TryPopUnsafe();
            if (result != nullptr)
            {
                return result;
            }
        }

        return nullptr;
    }

    void ResetAfterUnsafeDrain()
    {
        size_t shardCount = ShardCount();
        for (size_t i = 0; i < shardCount; i++)
        {
            Shard(i)->ResetAfterUnsafeDrain();
        }
    }

    SatoriRegion* TryPopWithSize(size_t regionSize, SatoriRegion*& putBack)
    {
        _ASSERTE(putBack == nullptr);
        return TryOnShards(
            [&](SatoriRegionQueue* shard)
            {
                return shard->TryPopWithSize(regionSize, putBack);
            }
        );
    }

    SatoriRegion* TryRemoveWithSize(
        size_t regionSize,
        SatoriRegion*& putBack,
        size_t maxRegions = 64,
        bool preferCommitted = false,
        bool requireCommitted = false)
    {
        _ASSERTE(putBack == nullptr);
        size_t shardCount = ShardCount();
        if (shardCount == 1)
        {
            return Shard(0)->TryRemoveWithSize(
                regionSize,
                putBack,
                maxRegions,
                preferCommitted,
                /* maxRegionSize */ SIZE_MAX,
                requireCommitted);
        }

        SatoriRegionQueue* bestShard = nullptr;
        size_t bestSize = SIZE_MAX;
        bool bestNeedsCommit = true;

        SatoriRegion* result = TryOnShards(
            [&](SatoriRegionQueue* shard) -> SatoriRegion*
            {
                size_t shardBestSize = SIZE_MAX;
                bool shardBestNeedsCommit = true;
                SatoriRegion* exact = shard->TryRemoveWithSize(
                    regionSize,
                    putBack,
                    maxRegions,
                    preferCommitted,
                    /* maxRegionSize */ regionSize,
                    requireCommitted,
                    &shardBestSize,
                    &shardBestNeedsCommit);
                if (exact != nullptr)
                {
                    return exact;
                }

                if (shardBestSize < SIZE_MAX)
                {
                    if ((bestNeedsCommit && !shardBestNeedsCommit) ||
                        (bestNeedsCommit == shardBestNeedsCommit && shardBestSize < bestSize))
                    {
                        bestShard = shard;
                        bestSize = shardBestSize;
                        bestNeedsCommit = shardBestNeedsCommit;
                    }
                }

                return nullptr;
            }
        );
        if (result != nullptr || bestShard == nullptr)
        {
            return result;
        }

        result = bestShard->TryRemoveWithSize(
            regionSize,
            putBack,
            maxRegions,
            preferCommitted,
            /* maxRegionSize */ bestSize,
            /* requireCommitted */ requireCommitted || (preferCommitted && !bestNeedsCommit));
        if (result != nullptr)
        {
            return result;
        }

        if (!requireCommitted)
        {
            result = TryRemoveWithSize(
                regionSize,
                putBack,
                maxRegions,
                /* preferCommitted */ true,
                /* requireCommitted */ true);
            if (result != nullptr)
            {
                return result;
            }
        }

        return TryOnShards(
            [&](SatoriRegionQueue* shard)
            {
                return shard->TryRemoveWithSize(
                    regionSize,
                    putBack,
                    maxRegions,
                    preferCommitted,
                    /* maxRegionSize */ SIZE_MAX,
                    requireCommitted);
            }
        );
    }

    SatoriRegion* TryDequeueIfHasFreeSpaceInTopBucket()
    {
        return TryOnShards(
            [](SatoriRegionQueue* shard)
            {
                return shard->TryDequeueIfHasFreeSpaceInTopBucket();
            }
        );
    }

    SatoriRegion* TryPopFitting(size_t minFree, size_t maxScan)
    {
        return TryOnShards(
            [=](SatoriRegionQueue* shard)
            {
                return shard->TryPopFitting(minFree, maxScan);
            }
        );
    }

    template <typename F>
    void ForEachRegion(F lambda)
    {
        size_t shardCount = ShardCount();
        for (size_t i = 0; i < shardCount; i++)
        {
            Shard(i)->ForEachRegion(lambda);
        }
    }

    size_t Count()
    {
        size_t count = 0;
        size_t shardCount = ShardCount();
        for (size_t i = 0; i < shardCount; i++)
        {
            count += Shard(i)->Count();
        }

        return count;
    }

    bool IsEmpty()
    {
        size_t shardCount = ShardCount();
        for (size_t i = 0; i < shardCount; i++)
        {
            if (!Shard(i)->IsEmpty())
            {
                return false;
            }
        }

        return true;
    }

    QueueKind Kind()
    {
        return Shard(0)->Kind();
    }

    size_t ShardCount()
    {
        return m_shardMask + 1;
    }

private:
    size_t m_shardMask;

    DECLSPEC_ALIGN(Satori::CACHE_LINE_GRANULARITY)
    SatoriRegionQueue m_shards[1];

    SatoriRegionQueue* Shard(size_t index)
    {
        _ASSERTE(index <= m_shardMask);
        return (SatoriRegionQueue*)((size_t)m_shards + index * sizeof(SatoriRegionQueue));
    }

    size_t CurrentShardIndex()
    {
        if (m_shardMask == 0)
        {
            return 0;
        }

        uint32_t value;
        if (GCToOSInterface::CanGetCurrentProcessorNumber())
        {
            value = GCToOSInterface::GetCurrentProcessorNumber();
            // On SMT systems the OS schedules threads onto primary (even-numbered) logical
            // processors first. Mixing with (value >> 1) keeps both consecutive logical
            // processors and consecutive physical cores (2*c) bijective modulo any power of 2.
            value ^= value >> 1;
        }
        else
        {
            size_t thread = SatoriUtil::GetCurrentThreadTag();
            value = (uint32_t)((thread * 11400714819323198485llu) >> 32);
        }

        return value & m_shardMask;
    }

    SatoriRegionQueue* CurrentShard()
    {
        return Shard(CurrentShardIndex());
    }

    template <typename F>
    SatoriRegion* StealFromOtherShards(size_t ownedShard, F fn)
    {
        // Visit all other shards in random order and odd stride (coprime to power-of-2 shard count).
        size_t rnd = (((size_t)&rnd ^ (size_t)SatoriUtil::GetTimeStamp()) * 11400714819323198485llu) >> 32;
        size_t index = rnd & m_shardMask;
        size_t stride = (rnd >> 16) | 1;
        size_t shardCount = ShardCount();

        for (size_t i = 0; i < shardCount; i++, index = (index + stride) & m_shardMask)
        {
            if (index != ownedShard)
            {
                SatoriRegion* result = fn(Shard(index));
                if (result != nullptr)
                {
                    return result;
                }
            }
        }

        return nullptr;
    }

    template <typename F>
    SatoriRegion* TryOnShards(F fn)
    {
        size_t ownedShard = CurrentShardIndex();
        SatoriRegion* result = fn(Shard(ownedShard));
        if (result != nullptr || m_shardMask == 0)
        {
            return result;
        }

        return StealFromOtherShards(ownedShard, fn);
    }
};

#endif
