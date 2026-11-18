// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef LAXORI_DEFAULT_LIMITS_H
#define LAXORI_DEFAULT_LIMITS_H

#include <cstddef>
#include <cstdint>

struct LaxoriDefaultLimits
{
    static constexpr size_t LegacyImmixReserveMB = 128;
    static constexpr size_t LegacyLargeReserveMB = 256;
    static constexpr size_t MaximumImmixReserveMB = size_t{256} << 10;
    static constexpr size_t MaximumLargeReserveMB = size_t{256} << 10;
    static constexpr size_t MinimumNativePlaneLimitMB = 4096;
    static constexpr size_t NativePlanePercent = 450;

    // Each space can hold the process memory limit; reservation does not commit that memory.
    // Explicit reserves stay authoritative. A larger managed hard limit supplies the default split.
    static void ReservesMB(
        uint64_t memoryLimitBytes, size_t addressLimitMB, size_t hardLimitMB,
        bool immixDefault, size_t* immixMB, bool largeDefault, size_t* largeMB)
    {
        size_t limitMB = static_cast<size_t>(memoryLimitBytes >> 20);
        size_t immix = *immixMB;
        size_t large = *largeMB;
        if (hardLimitMB > LegacyImmixReserveMB + LegacyLargeReserveMB)
        {
            if (immixDefault && largeDefault)
            {
                large = hardLimitMB / 2 > LegacyLargeReserveMB ? hardLimitMB / 2 : LegacyLargeReserveMB;
                immix = hardLimitMB - large;
            }
            else if (immixDefault)
            {
                immix = hardLimitMB > large + LegacyImmixReserveMB ? hardLimitMB - large : immix;
            }
            else if (largeDefault)
            {
                large = hardLimitMB > immix + LegacyLargeReserveMB ? hardLimitMB - immix : large;
            }
        }
        else if (hardLimitMB == 0)
        {
            if (immixDefault)
            {
                immix = limitMB < MaximumImmixReserveMB ? limitMB : MaximumImmixReserveMB;
                immix = immix > LegacyImmixReserveMB ? immix : LegacyImmixReserveMB;
            }
            if (largeDefault)
            {
                large = limitMB < MaximumLargeReserveMB ? limitMB : MaximumLargeReserveMB;
                large = large > LegacyLargeReserveMB ? large : LegacyLargeReserveMB;
            }
            if (immix + large > addressLimitMB)
            {
                if (immixDefault && largeDefault)
                {
                    large = addressLimitMB / 2 > LegacyLargeReserveMB ? addressLimitMB / 2 : LegacyLargeReserveMB;
                    immix = addressLimitMB > large ? addressLimitMB - large : LegacyImmixReserveMB;
                }
                else if (immixDefault)
                {
                    immix = addressLimitMB > large ? addressLimitMB - large : LegacyImmixReserveMB;
                }
                else if (largeDefault)
                {
                    large = addressLimitMB > immix ? addressLimitMB - immix : LegacyLargeReserveMB;
                }
                immix = immix > LegacyImmixReserveMB ? immix : LegacyImmixReserveMB;
                large = large > LegacyLargeReserveMB ? large : LegacyLargeReserveMB;
            }
        }
        *immixMB = immix;
        *largeMB = large;
    }

    static size_t NativePlaneLimitMB(size_t immixReserveMB, size_t largeReserveMB)
    {
        size_t total = immixReserveMB + largeReserveMB;
        size_t derived = total > SIZE_MAX / NativePlanePercent ? SIZE_MAX >> 20 : total * NativePlanePercent / 100;
        return derived < MinimumNativePlaneLimitMB ? MinimumNativePlaneLimitMB : derived;
    }

    // Windows charges committed pages even before they are written. Leave room for pause work lists,
    // thread stacks and the runtime, so a failed growth can report OutOfMemoryException.
    static constexpr size_t MinimumCommitHeadroomMB = 64;
    static constexpr size_t CommitHeadroomDivisor = 8;

    static uint64_t CommitHeadroomBytes(uint64_t limitBytes)
    {
        uint64_t headroom = limitBytes / CommitHeadroomDivisor;
        uint64_t minimum = uint64_t{MinimumCommitHeadroomMB} << 20;
        headroom = headroom > minimum ? headroom : minimum;
        return headroom < limitBytes ? headroom : limitBytes;
    }

    static bool FitsCommitLimit(uint64_t limitBytes, uint64_t headroomBytes, uint64_t committedBytes, uint64_t addedBytes)
    {
        if (limitBytes == 0)
        {
            return true;
        }
        if (headroomBytes > limitBytes || committedBytes > limitBytes - headroomBytes)
        {
            return false;
        }
        return addedBytes <= limitBytes - headroomBytes - committedBytes;
    }
};

#endif // LAXORI_DEFAULT_LIMITS_H
