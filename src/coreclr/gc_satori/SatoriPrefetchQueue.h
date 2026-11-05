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
// SatoriPrefetchQueue.h
//

#ifndef __SATORI_PREFETCH_QUEUE_H__
#define __SATORI_PREFETCH_QUEUE_H__

#include "common.h"
#include "../gc.h"
#include "SatoriUtil.h"

class SatoriObject;

// A small FIFO of discovered objects that are about to be processed.
// Objects are prefetched when added, and handed back for processing only after a few more
// objects are added, by then their memory is likely in cache. This helps graph traversals
// that would otherwise stall on every discovered object when reading its size or type.
//
// NB: This is used on the write barrier path (escaping), thus must not use vector registers.
//     Prefetch instructions do not, and the storage is intentionally not initialized.
template <int SIZE>
class SatoriPrefetchQueue
{
    static_assert(SIZE > 0 && (SIZE & (SIZE - 1)) == 0, "size must be a power of 2");

public:
    SatoriPrefetchQueue()
        : m_head(0), m_count(0)
    {
    }

    // Prefetches the object and adds it to the queue.
    // If the queue was full, returns the oldest object, which the caller should process now.
    // Otherwise returns nullptr.
    FORCEINLINE SatoriObject* Push(SatoriObject* o)
    {
        SatoriUtil::Prefetch(o);
        if (m_count == SIZE)
        {
            SatoriObject* oldest = m_items[m_head];
            m_items[m_head] = o;
            m_head = (m_head + 1) & (SIZE - 1);
            return oldest;
        }

        m_items[(m_head + m_count) & (SIZE - 1)] = o;
        m_count++;
        return nullptr;
    }

    // Removes and returns the oldest object, or nullptr if the queue is empty.
    FORCEINLINE SatoriObject* Pop()
    {
        if (m_count == 0)
        {
            return nullptr;
        }

        SatoriObject* oldest = m_items[m_head];
        m_head = (m_head + 1) & (SIZE - 1);
        m_count--;
        return oldest;
    }

private:
    SatoriObject* m_items[SIZE];
    int m_head;
    int m_count;
};

// A small FIFO of references to relocated objects whose forwarding pointers are prefetched.
// Each entry records the reference slot and the relocated target object, so flushing or
// evicting the oldest entry reads the prefetched forwarding pointer without re-reading the slot.
template <int SIZE>
class SatoriRelocatedRefQueue
{
    static_assert(SIZE > 0 && (SIZE & (SIZE - 1)) == 0, "size must be a power of 2");

public:
    SatoriRelocatedRefQueue()
        : m_head(0), m_count(0)
    {
    }

    FORCEINLINE void Push(SatoriObject** ppObject, SatoriObject* child)
    {
        SatoriUtil::Prefetch((SatoriObject**)child - 1);
        if (m_count == SIZE)
        {
            VolatileStoreWithoutBarrier(m_slots[m_head], m_children[m_head]->RelocatedToUnchecked());
            m_slots[m_head] = ppObject;
            m_children[m_head] = child;
            m_head = (m_head + 1) & (SIZE - 1);
            return;
        }

        int index = (m_head + m_count) & (SIZE - 1);
        m_slots[index] = ppObject;
        m_children[index] = child;
        m_count++;
    }

    FORCEINLINE void Flush()
    {
        while (m_count > 0)
        {
            VolatileStoreWithoutBarrier(m_slots[m_head], m_children[m_head]->RelocatedToUnchecked());
            m_head = (m_head + 1) & (SIZE - 1);
            m_count--;
        }
    }

private:
    SatoriObject** m_slots[SIZE];
    SatoriObject* m_children[SIZE];
    int m_head;
    int m_count;
};

#endif
