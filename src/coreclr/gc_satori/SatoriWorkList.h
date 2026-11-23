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
// SatoriWorkList.h
//

#ifndef __SATORI_WORK_LIST_H__
#define __SATORI_WORK_LIST_H__

#include "common.h"
#include "../gc.h"
#include "SatoriWorkChunk.h"

#if _DEBUG
#define COUNT_CHUNKS
#endif

class DECLSPEC_ALIGN(128) SatoriWorkList
{
public:
    SatoriWorkList() :
        SatoriWorkList(nullptr, 0)
    {}

    SatoriWorkList(SatoriWorkChunk* head, size_t aba)
    {
        m_head = head;
        m_aba = aba;
#ifdef COUNT_CHUNKS
        m_count = 0;
#endif
    }

    static SatoriWorkList* AllocAligned()
    {
        const size_t align =
#if defined(TARGET_AMD64)
            64;
#else
            128;
#endif

#ifdef _MSC_VER
        void* buffer = _aligned_malloc(sizeof(SatoriWorkList), align);
#else
        void* buffer = malloc(sizeof(SatoriWorkList) + align);
        buffer = (void*)ALIGN_UP((size_t)buffer, align);
#endif
        return new(buffer)SatoriWorkList();
    }

    bool IsEmpty()
    {
        return m_head == nullptr;
    }

    FORCEINLINE
    void Push(SatoriWorkChunk* item)
    {
        _ASSERTE(item->m_next == nullptr);

        SatoriWorkChunk* head = this->m_head;
        size_t aba = this->m_aba;

        item->m_next = head;

        SatoriWorkList orig(head, aba);
        if (Interlocked::CompareExchange128((int64_t*)this, aba + 1, (int64_t)item, (int64_t*)&orig))
        {
#ifdef COUNT_CHUNKS
            Interlocked::Increment(&m_count);
#endif
            return;
        }

        PushSlow(item);
    }

    FORCEINLINE
    SatoriWorkChunk* TryPop()
    {
        // read the version first and with acquire, see TryPopSlow.
        size_t aba = VolatileLoad(&this->m_aba);
        SatoriWorkChunk* head  = this->m_head;

        if (head == nullptr)
        {
            return nullptr;
        }

        SatoriWorkList orig(head, aba);
        if (Interlocked::CompareExchange128((int64_t*)this, aba + 1, (int64_t)head->m_next, (int64_t*)&orig))
        {
#ifdef COUNT_CHUNKS
            Interlocked::Decrement(&m_count);
#endif
            head->m_next = nullptr;
            return head;
        }

        return TryPopSlow();
    }

#ifdef COUNT_CHUNKS
    size_t Count()
    {
        return m_count;
    }
#endif

    void PushChain(SatoriWorkChunk* first, SatoriWorkChunk* last);

private:
    struct
    {
        SatoriWorkChunk* volatile m_head;
        volatile size_t m_aba;
    };
#ifdef COUNT_CHUNKS
    size_t m_count;
#endif

    NOINLINE
    void PushSlow(SatoriWorkChunk* item);
    NOINLINE
    SatoriWorkChunk* TryPopSlow();
};

class DECLSPEC_ALIGN(128) SatoriShardedWorkList
{
public:
    SatoriShardedWorkList(size_t shardCount) :
        m_shardMask(shardCount - 1)
    {
        _ASSERTE(shardCount > 0);
        _ASSERTE((shardCount & (shardCount - 1)) == 0);
        for (size_t i = 0; i < shardCount; i++)
        {
            new (Shard(i)) SatoriWorkList();
        }
    }

    static size_t ShardCountForProcessors(int processorsPerShard)
    {
        _ASSERTE(processorsPerShard > 0);
        int processorCount = (int)GCToOSInterface::GetTotalProcessorCount();
        int desiredShards = (processorCount + processorsPerShard - 1) / processorsPerShard;
        return SatoriUtil::RoundUpPwr2((size_t)max(1, desiredShards));
    }

    static SatoriShardedWorkList* AllocAligned(size_t shardCount);

    static SatoriShardedWorkList* AllocAlignedForProcessors(int processorsPerShard)
    {
        return AllocAligned(ShardCountForProcessors(processorsPerShard));
    }

    void Push(SatoriWorkChunk* item)
    {
        CurrentShard()->Push(item);
    }

    void PushChain(SatoriWorkChunk* first, SatoriWorkChunk* last)
    {
        CurrentShard()->PushChain(first, last);
    }

    // Same as PushChain, to a chosen shard. (the index wraps around)
    // This is for spreading chunks that are added in bulk, so that every shard gets some.
    void PushChainToShard(size_t shard, SatoriWorkChunk* first, SatoriWorkChunk* last)
    {
        Shard(shard & m_shardMask)->PushChain(first, last);
    }

    SatoriWorkChunk* TryPop()
    {
        size_t ownedShard = CurrentShardIndex();
        SatoriWorkChunk* result = Shard(ownedShard)->TryPop();
        if (result != nullptr || m_shardMask == 0)
        {
            return result;
        }

        // Visit all other shards in random order and odd stride (coprime to power-of-2 shard count).
        size_t rnd = (((size_t)&rnd ^ (size_t)SatoriUtil::GetTimeStamp()) * 11400714819323198485llu) >> 32;
        size_t index = rnd & m_shardMask;
        size_t stride = (rnd >> 16) | 1;
        size_t shardCount = ShardCount();

        for (size_t i = 0; i < shardCount; i++, index = (index + stride) & m_shardMask)
        {
            if (index != ownedShard)
            {
                result = Shard(index)->TryPop();
                if (result != nullptr)
                {
                    return result;
                }
            }
        }

        return nullptr;
    }

    bool IsCurrentShardEmpty()
    {
        return CurrentShard()->IsEmpty();
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

#ifdef COUNT_CHUNKS
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
#endif

    size_t ShardCount()
    {
        return m_shardMask + 1;
    }

private:
    size_t m_shardMask;

    DECLSPEC_ALIGN(128)
    SatoriWorkList m_shards[1];

    SatoriWorkList* Shard(size_t index)
    {
        _ASSERTE(index <= m_shardMask);
        return (SatoriWorkList*)((size_t)m_shards + index * sizeof(SatoriWorkList));
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
            // Where the two logical processors of a core are numbered 2c and 2c+1 (Windows, some Linux systems), threads
            // that are spread one per core can all have even numbers. Mixing with (value >> 1) keeps both consecutive
            // logical processors and consecutive physical cores (2*c) bijective modulo any power of 2.
            value ^= value >> 1;
        }
        else
        {
            size_t thread = SatoriUtil::GetCurrentThreadTag();
            value = (uint32_t)((thread * 11400714819323198485llu) >> 32);
        }

        return value & m_shardMask;
    }

    SatoriWorkList* CurrentShard()
    {
        return Shard(CurrentShardIndex());
    }
};

#endif
