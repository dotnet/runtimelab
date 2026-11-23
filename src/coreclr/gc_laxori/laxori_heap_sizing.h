// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef LAXORI_HEAP_SIZING_H
#define LAXORI_HEAP_SIZING_H

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include "laxori_default_limits.h"

// Port-specific. The Java LAXORI runs in the fixed heap that -Xms/-Xmx give, so its heap-full trigger makes the whole free
// heap the nursery. The .NET host has no such setting, so the collector must choose the Immix capacity. The capacity is
// what the heap-full trigger compares with, so it sets the number of collections.
//
// The rule has two parts:
//  * A nursery budget. It starts at the initial capacity and doubles at each heap-full collection (slow start) until it
//    reaches a cap. A program that allocates little never leaves its small heap. A program that allocates a lot reaches
//    the cap after a few collections. The cap is a share of physical memory, as the standard collector limits its
//    gen0 budget, and is bounded by a constant above which a larger nursery gains little.
//  * The mature live size. The capacity must hold it next to the nursery, so the target is live + budget. The live
//    size is measured after the lazy sweep of the previous pause (occupied blocks, without the free lines of reusable
//    blocks), not predicted from block counts.
// The target never lowers the capacity and never exceeds a share of the reservation. The rest of the reservation is
// the room that a pause borrows for its to-space (P17 S5b). A failed allocation can still grow the heap beyond the
// ceiling (LaxoriGcHeap::TryGrow).
struct LaxoriHeapSizingConfiguration
{
    bool enabled = true;
    // Largest nursery budget in bytes. 0 keeps the capacity as configured.
    size_t nurseryCapBytes = 0;
    size_t growthFactor = 2;
    // Largest share of the Immix reservation that sizing admits.
    size_t ceilingPercent = 75;
};

class LaxoriHeapSizer final
{
public:
    static constexpr size_t DefaultNurseryCapBytes = size_t{96} << 20;
    static constexpr size_t MinimumNurseryCapBytes = size_t{16} << 20;
    // The standard collector keeps its gen0 budget below a share of physical memory (gen0size * heaps <= physical / 6).
    // The nursery here is one budget for the process, and the heap needs room for live data, so the share is smaller.
    static constexpr size_t PhysicalMemoryDivisor = 16;
    static constexpr size_t LiveReserveFactor = 2;

    static size_t DefaultNurseryCap(uint64_t physicalBytes)
    {
        uint64_t share = physicalBytes / PhysicalMemoryDivisor;
        if (share >= DefaultNurseryCapBytes)
        {
            return DefaultNurseryCapBytes;
        }
        return share > MinimumNurseryCapBytes ? static_cast<size_t>(share) : MinimumNurseryCapBytes;
    }

    // Keep the old small-heap policy when the reservation follows the process memory limit.
    static size_t PolicyReserveBytes(size_t reservationBytes, size_t liveBytes)
    {
        size_t floor = LaxoriDefaultLimits::LegacyImmixReserveMB << 20;
        size_t wanted = liveBytes > SIZE_MAX / LiveReserveFactor ? SIZE_MAX : liveBytes * LiveReserveFactor;
        wanted = wanted > floor ? wanted : floor;
        return wanted < reservationBytes ? wanted : reservationBytes;
    }

    void Configure(const LaxoriHeapSizingConfiguration& configuration, size_t initialCapacityBytes)
    {
        m_configuration = configuration;
        m_budget = initialCapacityBytes;
        m_requests = 0;
    }

    bool Enabled() const { return m_configuration.enabled && m_configuration.nurseryCapBytes != 0; }
    size_t BudgetBytes() const { return m_budget; }
    size_t Requests() const { return m_requests; }

    // Called when a heap-full collection starts. Returns the Immix capacity that the pause should leave: capacityBytes
    // when no growth is wanted.
    size_t TargetCapacity(size_t capacityBytes, size_t liveBytes, size_t reservationBytes)
    {
        if (!Enabled())
        {
            return capacityBytes;
        }
        size_t cap = m_configuration.nurseryCapBytes;
        if (m_budget < cap)
        {
            size_t factor = m_configuration.growthFactor < 2 ? 2 : m_configuration.growthFactor;
            m_budget = m_budget > cap / factor ? cap : m_budget * factor;
        }
        m_requests++;
        size_t percent = m_configuration.ceilingPercent;
        size_t ceiling = reservationBytes / 100 * percent + reservationBytes % 100 * percent / 100;
        size_t want = liveBytes > SIZE_MAX - m_budget ? SIZE_MAX : liveBytes + m_budget;
        size_t target = want < ceiling ? want : ceiling;
        return target > capacityBytes ? target : capacityBytes;
    }

private:
    LaxoriHeapSizingConfiguration m_configuration{};
    size_t m_budget = 0;
    size_t m_requests = 0;
};

#endif // LAXORI_HEAP_SIZING_H
