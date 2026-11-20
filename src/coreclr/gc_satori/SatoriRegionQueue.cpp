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
// SatoriRegionQueue.cpp
//

#include "common.h"

#include "gcenv.h"
#include "../env/gcenv.os.h"
#include "SatoriRegionQueue.h"
#include "SatoriLock.h"
#include "SatoriUtil.h"
#include "SatoriRegion.h"
#include "SatoriRegion.inl"
#include "SatoriPage.inl"

SatoriRegion* SatoriRegionQueue::TryDequeueIfHasFreeSpaceInTopBucket()
{
    if (!m_tail)
    {
        return nullptr;
    }

    m_lock.Enter();

    SatoriRegion* result = m_tail;
    if (result == nullptr || !result->HasFreeSpaceInTopBucket())
    {
        m_lock.Leave();
        return nullptr;
    }

    // take out the result
    m_count--;
    result->m_containingQueue = nullptr;
    m_tail = result->m_prev;
    if (m_tail == nullptr)
    {
        m_head = nullptr;
    }
    else
    {
        m_tail->m_next = nullptr;
    }

    m_lock.Leave();

    _ASSERTE(result->m_next == nullptr);
    result->m_prev = nullptr;
    return result;
}

SatoriRegion* SatoriRegionQueue::TryPopFitting(size_t minFree, size_t maxScan)
{
    if (IsEmpty())
    {
        return nullptr;
    }

    m_lock.Enter();

    SatoriRegion* result = m_head;
    for (size_t i = 0; result != nullptr; i++)
    {
        if (i == maxScan)
        {
            result = nullptr;
            break;
        }

        if (result->CanFitAllocation(minFree))
        {
            break;
        }

        result = result->m_next;
    }

    if (result == nullptr)
    {
        m_lock.Leave();
        return nullptr;
    }

    SatoriRegion* prev = result->m_prev;
    SatoriRegion* next = result->m_next;
    if (prev != nullptr)
    {
        prev->m_next = next;
    }
    else
    {
        m_head = next;
    }

    if (next != nullptr)
    {
        next->m_prev = prev;
    }
    else
    {
        m_tail = prev;
    }

    m_count--;
    result->m_containingQueue = nullptr;

    m_lock.Leave();

    result->m_prev = nullptr;
    result->m_next = nullptr;
    return result;
}

SatoriRegionQueue* SatoriRegionQueue::AllocAligned(QueueKind kind)
{
    const size_t align = alignof(SatoriRegionQueue);

#ifdef _MSC_VER
    void* buffer = _aligned_malloc(sizeof(SatoriRegionQueue), align);
#else
    void* buffer = malloc(sizeof(SatoriRegionQueue) + align);
    buffer = (void*)ALIGN_UP((size_t)buffer, align);
#endif
    return new(buffer)SatoriRegionQueue(kind);
}

SatoriShardedRegionQueue* SatoriShardedRegionQueue::AllocAligned(QueueKind kind, size_t shardCount)
{
    _ASSERTE(shardCount > 0 && (shardCount & (shardCount - 1)) == 0);
    const size_t align = alignof(SatoriShardedRegionQueue);
    const size_t size = offsetof(SatoriShardedRegionQueue, m_shards) + shardCount * sizeof(SatoriRegionQueue);

#ifdef _MSC_VER
    void* buffer = _aligned_malloc(size, align);
#else
    void* buffer = malloc(size + align);
    buffer = (void*)ALIGN_UP((size_t)buffer, align);
#endif
    return new(buffer)SatoriShardedRegionQueue(kind, shardCount);
}

