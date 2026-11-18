// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// P22 (D2): the spans of memory that mutators filled since the last pause.
//
// The stock allocation helpers bump a pointer inside a buffer that the collector gave to a thread, and never tell the
// collector about the objects. The collector learns what a thread allocated at two points: when the buffer is handed
// out (bulk preparation of the whole range) and when the buffer is retired (the used part is known exactly). A span is
// the used part of one buffer: [start, end), where both addresses are object starts (the address of the object header,
// not of the MethodTable pointer) and the memory between them is a sequence of complete objects with no gaps.
//
// A thread cannot be suspended inside a bump-pointer helper, so every object in a span is complete when a pause
// retires the buffers. The spans are the only record of young objects: no per-object bit exists for them.

#ifndef LAXORI_YOUNG_SPANS_H
#define LAXORI_YOUNG_SPANS_H

#include <algorithm>
#include <atomic>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <new>

struct LaxoriSpan
{
    uintptr_t start;
    uintptr_t end;
};

// Growable list of spans. One thread appends. A pause merges the lists of all mutators and sorts the result by start
// address on demand; after that, Find is a binary search that any number of threads may run at the same time.
class LaxoriSpanList final
{
public:
    LaxoriSpanList() : m_data(nullptr), m_size(0), m_capacity(0), m_sort_state(2) {}
    ~LaxoriSpanList() { free(m_data); }
    LaxoriSpanList(const LaxoriSpanList&) = delete;
    LaxoriSpanList& operator=(const LaxoriSpanList&) = delete;

    bool Push(uintptr_t start, uintptr_t end)
    {
        if (m_size == m_capacity)
        {
            size_t capacity = m_capacity == 0 ? 64 : m_capacity * 2;
            LaxoriSpan* data = static_cast<LaxoriSpan*>(realloc(m_data, capacity * sizeof(LaxoriSpan)));
            if (data == nullptr)
            {
                return false;
            }
            m_data = data;
            m_capacity = capacity;
        }
        m_data[m_size++] = {start, end};
        m_sort_state.store(0, std::memory_order_relaxed);
        return true;
    }

    // Moves every span of other to the end of this list. Returns false when memory is exhausted.
    bool Take(LaxoriSpanList& other)
    {
        if (other.m_size == 0)
        {
            return true;
        }
        size_t needed = m_size + other.m_size;
        if (needed > m_capacity)
        {
            size_t capacity = m_capacity == 0 ? 64 : m_capacity;
            while (capacity < needed)
            {
                capacity *= 2;
            }
            LaxoriSpan* data = static_cast<LaxoriSpan*>(realloc(m_data, capacity * sizeof(LaxoriSpan)));
            if (data == nullptr)
            {
                return false;
            }
            m_data = data;
            m_capacity = capacity;
        }
        std::memcpy(m_data + m_size, other.m_data, other.m_size * sizeof(LaxoriSpan));
        m_size = needed;
        other.m_size = 0;
        other.m_sort_state.store(2, std::memory_order_relaxed);
        m_sort_state.store(0, std::memory_order_relaxed);
        return true;
    }

    void Sort() const
    {
        uint8_t state = m_sort_state.load(std::memory_order_acquire);
        if (state == 2)
        {
            return;
        }
        uint8_t expected = 0;
        if (m_sort_state.compare_exchange_strong(expected, 1, std::memory_order_acq_rel))
        {
            std::sort(m_data, m_data + m_size, [](const LaxoriSpan& left, const LaxoriSpan& right)
            {
                return left.start < right.start;
            });
            m_sort_state.store(2, std::memory_order_release);
            return;
        }
        while (m_sort_state.load(std::memory_order_acquire) != 2)
        {
        }
    }

    // The span that contains the address (start <= address < end), or nullptr.
    const LaxoriSpan* Find(uintptr_t address) const
    {
        Sort();
        size_t low = 0;
        size_t high = m_size;
        while (low < high)
        {
            size_t middle = low + (high - low) / 2;
            if (m_data[middle].end <= address)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }
        return low < m_size && m_data[low].start <= address ? &m_data[low] : nullptr;
    }

    void Clear()
    {
        m_size = 0;
        m_sort_state.store(2, std::memory_order_relaxed);
        if (m_capacity > 4096)
        {
            free(m_data);
            m_data = nullptr;
            m_capacity = 0;
        }
    }

    size_t Size() const { return m_size; }
    const LaxoriSpan& operator[](size_t index) const
    {
        Sort();
        return m_data[index];
    }

private:
    LaxoriSpan* m_data;
    size_t m_size;
    size_t m_capacity;
    mutable std::atomic<uint8_t> m_sort_state;
};

#endif // LAXORI_YOUNG_SPANS_H
