// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef LXR_DEFAULT_LIMITS_H
#define LXR_DEFAULT_LIMITS_H

#include <cstddef>
#include <cstdint>

// Port-specific. The heap limits that LxrGcHeap::Initialize uses when the user gives no setting.
//
// Before this rule the default Immix reserve was 128 MiB, the default LOS reserve 256 MiB and the native plane limit
// 4096 MiB. A program with more than about 100 MiB of small-object live data, or more than about 256 MiB of large
// objects, failed with OutOfMemoryException at the reserve, and a larger reserve alone did not help because the native
// planes (about four times the capacity) stopped the heap at about 1 GiB. A reserve is address space, not memory. The
// default of each reserve is now the memory limit of the process (the physical memory, or the container limit), and the
// native plane limit follows from the reservation. An explicit setting always wins.
struct LxrDefaultLimits
{
    // The reserve that was the default before the machine-size rule. It is the lowest default, and the sizing rule
    // of lxr_heap_sizing.h keeps this size as its ceiling for a small live size.
    static constexpr size_t LegacyImmixReserveMB = 128;
    // The lowest default of the native plane limit.
    static constexpr size_t MinimumNativePlaneLimitMB = 4096;
    // The native planes measured about four times the capacity; the limit adds one eighth to this.
    static constexpr size_t NativePlanePercent = 450;
    // The largest default reserve of each space. The per-block tables of the collector (card table, block flags, the dead
    // large-object list) are sized from the reservation, and a machine with more memory than this would pay their
    // address space for a range that a program does not use. A program that needs more sets the setting.
    static constexpr size_t MaximumImmixReserveMB = size_t{256} << 10;
    // The reserve that was the default of the LOS before the machine-size rule.
    static constexpr size_t LegacyLargeReserveMB = 256;
    static constexpr size_t MaximumLargeReserveMB = size_t{256} << 10;

    // The default reserves of the two spaces, in MB. Each space can use all the memory of the process (a program with
    // only small objects or only large objects), so each default is the memory limit, and the two reserves together can be
    // twice the limit. They are address space; the operating system enforces the memory limit. A reserve that is
    // not the default (immixDefault or largeDefault is false) is taken as it is, and the other one is made to fit.
    //   memoryLimitBytes: GCToOSInterface::GetPhysicalMemoryLimit() (a container or job limit is included).
    //   addressLimitMB: the largest reservation of both spaces together (the address space of the side metadata, or
    //   the limit that the host gave). hardLimitMB: LXRManagedHeapHardLimitMB, or 0. With a hard limit the reservation is
    //   made equal to it, so that the engine keeps its to-space borrowing (it needs hard limit == reservation).
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
    // The limit on the committed native planes for a reservation (Immix and LOS together). It is not lower than the
    // old default.
    static size_t NativePlaneLimitMB(size_t immixReserveMB, size_t largeReserveMB)
    {
        size_t total = immixReserveMB + largeReserveMB;
        size_t derived = total > SIZE_MAX / NativePlanePercent ? SIZE_MAX >> 20 : total * NativePlanePercent / 100;
        return derived < MinimumNativePlaneLimitMB ? MinimumNativePlaneLimitMB : derived;
    }

    // The commit limits of a Windows job object (lxr_gc.cpp, CanAdmitRange). Windows charges all committed memory to the
    // limit, also the pages that were never written: the reference-count logs alone commit about four times the admitted
    // heap. Before this rule the heap grew until the process had no commit left, and then a native allocation of the
    // pause (engine failure 4, a fatal error) or the guard page of a thread stack (an access violation) failed. A growth
    // must leave this headroom free for the native work lists of the collector, thread stacks and the runtime. The
    // allocation that needed the growth then fails with OutOfMemoryException.
    static constexpr size_t MinimumCommitHeadroomMB = 64;
    static constexpr size_t CommitHeadroomDivisor = 8;
    // The default headroom: one eighth of the limit, at least MinimumCommitHeadroomMB (and not more than the limit).
    static uint64_t CommitHeadroomBytes(uint64_t limitBytes)
    {
        uint64_t headroom = limitBytes / CommitHeadroomDivisor;
        uint64_t minimum = uint64_t{MinimumCommitHeadroomMB} << 20;
        headroom = headroom > minimum ? headroom : minimum;
        return headroom < limitBytes ? headroom : limitBytes;
    }
    // True if addedBytes more can be committed while committedBytes are committed now. A limit of 0 is no limit.
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

#endif // LXR_DEFAULT_LIMITS_H
