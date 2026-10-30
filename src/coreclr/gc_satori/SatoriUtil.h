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
// SatoriUtil.h
//

#ifndef __SATORI_UTIL_H__
#define __SATORI_UTIL_H__

#include "common.h"
#include "../gc.h"
#include "../../inc/satoriconstants.h"

// ForwardGCSafeCopy/BackwardGCSafeCopy below use SSE intrinsics on amd64.
// MSVC gets these via <intrin.h> (included by gcenv.base.h), clang/gcc need
// <xmmintrin.h> explicitly. The condition must match the uses below.
#if defined(HOST_AMD64) && defined(__GNUC__)
#include <xmmintrin.h>
#endif

#if defined(HOST_ARM64) && defined(_MSC_VER)
// the virtual counter of the ARM generic timer
#ifndef ARM64_CNTVCT_EL0
#define ARM64_CNTVCT_EL0 ARM64_SYSREG(3, 3, 14, 0, 2)
#endif
#endif

namespace Satori
{
    class StackOnly {
    private:
        void* operator new(size_t size) throw() = delete;
        void* operator new[](size_t size) throw() = delete;
    };

    // page granularity is 1 Gb, but they can be bigger
    static const int PAGE_BITS = SATORI_PAGE_BITS;
    static const size_t PAGE_SIZE_GRANULARITY = (size_t)1 << PAGE_BITS;

    // regions are aligned at 2 Mb
    // objects can be larger than that and straddle multiple region granules.
    // all real objects start in the first granule though to allow for fixed size of the metadata.
    const static int REGION_BITS = 21;
    const static size_t REGION_SIZE_GRANULARITY = 1 << REGION_BITS;

    const static int INDEX_GRANULARITY_BITS = 11;
    const static int INDEX_GRANULARITY = 1 << INDEX_GRANULARITY_BITS;
    const static int INDEX_LENGTH = REGION_SIZE_GRANULARITY / INDEX_GRANULARITY;

    static const int ALLOCATOR_BUCKET_COUNT = PAGE_BITS - REGION_BITS;

    // object starts are aligned to this
    static const size_t OBJECT_ALIGNMENT = sizeof(size_t);

    // the unit of false sharing - what to align a hot field to, and how much to pad
    // around it. 128 where the hardware pairs lines or the line is simply wider.
    // NB: not CACHE_LINE_SIZE - that is a macro in the VM's per-arch cgencpu.h.
#if defined(TARGET_AMD64) || defined(TARGET_X86)
    static const size_t CACHE_LINE_GRANULARITY = 64;
#else
    static const size_t CACHE_LINE_GRANULARITY = 128;
#endif

    // minimal free size that can be made parseable.
    // we use a trivial array object to fill holes, thus this is the size of a shortest array object.
    static const size_t MIN_FREE_SIZE = 3 * sizeof(size_t);

    // If a single mark takes very roughly ~50ns (5-20 for CAS + some extra), then 1k objects marks in 50us
    // we set the chunk to roughly 1/2k to expect it mark in under 20us or so
    const static size_t MARK_CHUNK_COUNT = 512;

    // this includes header, so the number of objects is slightly less (by -2)
    const static size_t MARK_CHUNK_SIZE = MARK_CHUNK_COUNT * sizeof(size_t);

    // objects that are bigger are chunked into ranges when marking.
    // the threshold is slightly less than MARK_CHUNK_SIZE, so that object in the range
    // could fit into same chunk
    const static size_t MARK_RANGE_THRESHOLD = MARK_CHUNK_SIZE - 2 * sizeof(size_t);

    // if we have more than this much and work list is empty we can share half
    const static int SHARE_WORK_THRESHOLD = 8;

    // if we allocated this much, check if help is needed
    const static int PACE_BUDGET = REGION_SIZE_GRANULARITY / 16;

    // allocated bytes of small objects are counted in a shared counter per this many processors.
    // (see SatoriAllocator::PublishAllocatedBytes)
    const static int ALLOCATED_BYTES_COUNTER_PROCESSORS = 16;
    const static int MAX_ALLOCATED_BYTES_COUNTERS = 64;

    // allocation tick event is fired after at least this much is allocated since the last tick.
    // (same as etw_allocation_tick in the regular GC)
    // NB: this is typically silent. Allocated bytes are published in batches of PACE_BUDGET, which is larger,
    //     so ticks are already further apart than this. The purpose of this is to be a floor on the distance
    //     between ticks (i.e. to cap how often they happen), in case pacing is made more frequent.
    const static size_t ALLOCATION_TICK_BYTES = 100 * 1024;

    // address bits set to track finalizable that needs to be scheduled to F-queue
    const static size_t FINALIZATION_PENDING = 1;
    const static size_t FINALIZATION_PENDING_CRITICAL = 2;
    const static size_t FINALIZATION_PENDING_ANY = FINALIZATION_PENDING | FINALIZATION_PENDING_CRITICAL;

    // one card covers 512 bytes (64 possible pointers)
    static const int BYTES_PER_CARD_BYTE = 512;
    static const int BYTES_PER_CARD_GROUP = REGION_SIZE_GRANULARITY / 2;
    static const int CARD_BYTES_IN_CARD_GROUP = Satori::BYTES_PER_CARD_GROUP / BYTES_PER_CARD_BYTE;

    namespace CardState
    {
        static const int8_t EPHEMERAL = -128; // 0b10000000 only used in cards (not groups or higher)
        static const int8_t BLANK = 0;
        static const int8_t REMEMBERED = 1;
        static const int8_t PROCESSING = 2;
        static const int8_t DIRTY = 4;
    }

    // TUNING: this is just a threshold for cases when region has too much escaped content.
    //         The actual value may not matter a lot. Still may be worth revisiting.
    // When 1/4 escapes, we stop tracking escapes.
    static const int MAX_ESCAPE_SIZE = REGION_SIZE_GRANULARITY / 4;

    // A thread-local collection proceeds only if at least this much of the region will be free after it.
    static const int TLGC_MIN_FREE_PERCENT = 70;

    // objects smaller than this go into regular region.
    static const int LARGE_OBJECT_THRESHOLD = 32 * 1024;

    // freelist bucket contais items that fit at least 1 << (i + min_bits)
    // min freelist capacity is 4K+
    static const int MIN_FREELIST_CAPACITY_BITS = 11;
    static const size_t MIN_FREELIST_CAPACITY = 1 << MIN_FREELIST_CAPACITY_BITS;
    static const int FREELIST_COUNT = Satori::REGION_BITS - MIN_FREELIST_CAPACITY_BITS;

    // large bucket size is 32K+ - so that any small obj would fit
    static const int LARGE_BUCKETS = 6;

    // we will limit number of demoted objects to not use too many chunks (up to 4)
    // it will softly limit the occupancy as well.
    const static size_t MAX_DEMOTED_OBJECTS_IN_REGION = (MARK_CHUNK_COUNT - 2) * 4;
}

class SatoriUtil
{
public:
    static size_t RoundDownPwr2(size_t value)
    {
        _ASSERTE(value > 0);
        DWORD highestBit;
        BitScanReverse64(&highestBit, value);
        return (size_t)1 << highestBit;
    }

    static size_t RoundUpPwr2(size_t value)
    {
        _ASSERTE(value > 0);
        if (value == 1)
        {
            return 1;
        }

        DWORD highestBit;
        BitScanReverse64(&highestBit, value - 1);
        return (size_t)2 << highestBit;
    }

    static int32_t BucketForAlloc(size_t allocSize)
    {
        if (allocSize <= Satori::MIN_FREELIST_CAPACITY)
        {
            return 0;
        }
        else
        {
            // skip buckets that could not possibly fit allocSize
            DWORD bucket;
            BitScanReverse64(&bucket, allocSize - 1);
            bucket = bucket - Satori::MIN_FREELIST_CAPACITY_BITS;

            // this bucket may be able to fit allocSize or may be not,
            // but we will just use the next bucket, which guarantees the allocSize will fit.
            bucket++;
            return (int32_t)bucket;
        }
    }

    // must match what is used in barriers.
    static size_t GetCurrentThreadTag()
    {
#if TARGET_WINDOWS
        // we use linear address of TEB on NT
#if defined(TARGET_ARM64)
        return (size_t)__getReg(18);
#else
        return (size_t)__readgsqword(0x30);
#endif

#elif defined(TARGET_APPLE)

        size_t tag;
#if defined(TARGET_ARM64)
        __asm__ ("mrs %0, tpidrro_el0" : "=r" (tag));
        tag &= (size_t)~7;
#else
        __asm__ ("movq %%gs:0, %0" : "=r" (tag));
#endif
        return tag;

#elif defined (TARGET_LINUX)

        size_t tag;
#if defined(TARGET_ARM64)
        __asm__ ("mrs %0, tpidr_el0" : "=r" (tag));
#else
        __asm__ ("movq %%fs:0, %0" : "=r" (tag));
#endif
        return tag;
#else
        UNSUPPORTED_PLATFORM
#endif
    }

    static void Prefetch(void* addr)
    {
#if TARGET_WINDOWS
#if defined(TARGET_ARM64)
        __prefetch(addr);
#else
        _mm_prefetch((char *) addr, _MM_HINT_T0);
#endif
#else
        __builtin_prefetch(addr);
#endif
    }

    // A cheap monotonically increasing timestamp, for deadline checks and similar heuristics.
    // This reads the hardware counter inline - typically just a few instructions.
    //
    // The counter may be slightly out of sync between cores and some parts are known to be
    // unreliable, so do not use this where an occasional wrong reading is not tolerated.
    // Durations that are reported or feed into heuristics should use the OS timer instead.
    // The read is not serialized either, so it may drift within the out-of-order window.
    //
    // The unit is 1/GetTimeStampFrequency() of a second.
    static FORCEINLINE int64_t GetTimeStamp()
    {
        // until the rate of the hardware counter is known, ask the OS instead.
        if (s_timeStampFrequency == 0)
        {
            return GetTimeStampSlow();
        }

        return ReadHwTimeStamp();
    }

    // The number of GetTimeStamp() ticks per second.
    static int64_t GetTimeStampFrequency()
    {
        return s_timeStampFrequency != 0 ? s_timeStampFrequency : s_osTimeStampFrequency;
    }

    // GetTimeStamp() ticks in one microsecond, for spinning in units of wall time.
    // Never 0, so it can be used as a divisor.
    static int64_t TimeStampTicksPerUsec()
    {
        return s_ticksPerUsec;
    }

    // Chooses between the hardware counter and the OS timer and measures the rate of
    // the former. Takes ~100 usec. Must be called before GetTimeStamp()/GetTimeStampFrequency().
    static void Initialize();

    // Copies of InlinedForwardGCSafeCopyHelper/InlinedBackwardGCSafeCopyHelper from
    // vm/arraynative.inl - the VM headers are not reachable from the (standalone) GC.
    //
    // Unlike memmove these copy in pointer-sized chunks, so that a reference is never
    // torn and could be observed by another thread only as old or new value.
    //
    // Requirements:
    //   dest, src and len must be pointer-aligned, len != 0, dest != src.
    //   Forward: the destination must not start inside the source  (dest < src or no overlap).
    //   Backward: the source must not start inside the destination (dest > src or no overlap).
    //   NB: the checks below are unsigned, thus a "negative" difference passes.
    static FORCEINLINE void ForwardGCSafeCopy(void* dest, const void* src, size_t len)
    {
        _ASSERTE(dest != nullptr);
        _ASSERTE(src != nullptr);
        _ASSERTE(dest != src);
        _ASSERTE(len != 0);

        // To be able to copy forwards, the destination buffer cannot start inside the source buffer
        _ASSERTE((size_t)dest - (size_t)src >= len);

        _ASSERTE(((size_t)dest & (sizeof(size_t) - 1)) == 0);
        _ASSERTE(((size_t)src & (sizeof(size_t) - 1)) == 0);
        _ASSERTE((len & (sizeof(size_t) - 1)) == 0);

        size_t* dptr = (size_t*)dest;
        size_t* sptr = (size_t*)src;

        while (true)
        {
            if ((len & sizeof(size_t)) != 0)
            {
                *dptr = *sptr;

                len ^= sizeof(size_t);
                if (len == 0)
                {
                    return;
                }
                ++sptr;
                ++dptr;
            }

#if defined(HOST_AMD64) && (defined(_MSC_VER) || defined(__GNUC__))
            if ((len & (2 * sizeof(size_t))) != 0)
            {
                __m128 v = _mm_loadu_ps((float*)sptr);
                _mm_storeu_ps((float*)dptr, v);

                len ^= 2 * sizeof(size_t);
                if (len == 0)
                {
                    return;
                }
                sptr += 2;
                dptr += 2;
            }

            // Align the destination pointer to 16 bytes for the next set of 16-byte copies
            if (((size_t)dptr & sizeof(size_t)) != 0)
            {
                *dptr = *sptr;

                ++sptr;
                ++dptr;
                len -= sizeof(size_t);
                if (len < 4 * sizeof(size_t))
                {
                    continue;
                }
            }

            // Copy 32 bytes at a time
            _ASSERTE(len >= 4 * sizeof(size_t));
            do
            {
                __m128 v = _mm_loadu_ps((float*)sptr);
                _mm_store_ps((float*)dptr, v);
                v = _mm_loadu_ps((float*)(sptr + 2));
                _mm_store_ps((float*)(dptr + 2), v);

                sptr += 4;
                dptr += 4;
                len -= 4 * sizeof(size_t);
            } while (len >= 4 * sizeof(size_t));
            if (len == 0)
            {
                return;
            }
#else
            if ((len & (2 * sizeof(size_t))) != 0)
            {
                // Read two values and write two values to hint the use of wide loads and stores
                size_t p0 = sptr[0];
                size_t p1 = sptr[1];
                dptr[0] = p0;
                dptr[1] = p1;

                len ^= 2 * sizeof(size_t);
                if (len == 0)
                {
                    return;
                }
                sptr += 2;
                dptr += 2;
            }

            // Copy 16 (on 32-bit systems) or 32 (on 64-bit systems) bytes at a time
            _ASSERTE(len >= 4 * sizeof(size_t));
            while (true)
            {
                // Read two values and write two values to hint the use of wide loads and stores
                size_t p0 = sptr[0];
                size_t p1 = sptr[1];
                dptr[0] = p0;
                dptr[1] = p1;
                p0 = sptr[2];
                p1 = sptr[3];
                dptr[2] = p0;
                dptr[3] = p1;

                len -= 4 * sizeof(size_t);
                if (len == 0)
                {
                    return;
                }
                sptr += 4;
                dptr += 4;
            }
#endif
        }
    }

    static FORCEINLINE void BackwardGCSafeCopy(void* dest, const void* src, size_t len)
    {
        _ASSERTE(dest != nullptr);
        _ASSERTE(src != nullptr);
        _ASSERTE(dest != src);
        _ASSERTE(len != 0);

        // To be able to copy backwards, the source buffer cannot start inside the destination buffer
        _ASSERTE((size_t)src - (size_t)dest >= len);

        _ASSERTE(((size_t)dest & (sizeof(size_t) - 1)) == 0);
        _ASSERTE(((size_t)src & (sizeof(size_t) - 1)) == 0);
        _ASSERTE((len & (sizeof(size_t) - 1)) == 0);

        size_t* dptr = (size_t*)((uint8_t*)dest + len);
        size_t* sptr = (size_t*)((uint8_t*)src + len);

        while (true)
        {
            if ((len & sizeof(size_t)) != 0)
            {
                --sptr;
                --dptr;

                *dptr = *sptr;

                len ^= sizeof(size_t);
                if (len == 0)
                {
                    return;
                }
            }

#if defined(HOST_AMD64) && (defined(_MSC_VER) || defined(__GNUC__))
            if ((len & (2 * sizeof(size_t))) != 0)
            {
                sptr -= 2;
                dptr -= 2;

                __m128 v = _mm_loadu_ps((float*)sptr);
                _mm_storeu_ps((float*)dptr, v);

                len ^= 2 * sizeof(size_t);
                if (len == 0)
                {
                    return;
                }
            }

            // Align the destination pointer to 16 bytes for the next set of 16-byte copies
            if (((size_t)dptr & sizeof(size_t)) != 0)
            {
                --sptr;
                --dptr;

                *dptr = *sptr;

                len -= sizeof(size_t);
                if (len < 4 * sizeof(size_t))
                {
                    continue;
                }
            }

            // Copy 32 bytes at a time
            _ASSERTE(len >= 4 * sizeof(size_t));
            do
            {
                sptr -= 4;
                dptr -= 4;

                __m128 v = _mm_loadu_ps((float*)(sptr + 2));
                _mm_store_ps((float*)(dptr + 2), v);
                v = _mm_loadu_ps((float*)sptr);
                _mm_store_ps((float*)dptr, v);

                len -= 4 * sizeof(size_t);
            } while (len >= 4 * sizeof(size_t));
            if (len == 0)
            {
                return;
            }
#else
            if ((len & (2 * sizeof(size_t))) != 0)
            {
                sptr -= 2;
                dptr -= 2;

                // Read two values and write two values to hint the use of wide loads and stores
                size_t p1 = sptr[1];
                size_t p0 = sptr[0];
                dptr[1] = p1;
                dptr[0] = p0;

                len ^= 2 * sizeof(size_t);
                if (len == 0)
                {
                    return;
                }
            }

            // Copy 16 (on 32-bit systems) or 32 (on 64-bit systems) bytes at a time
            _ASSERTE(len >= 4 * sizeof(size_t));
            do
            {
                sptr -= 4;
                dptr -= 4;

                // Read two values and write two values to hint the use of wide loads and stores
                size_t p0 = sptr[2];
                size_t p1 = sptr[3];
                dptr[2] = p0;
                dptr[3] = p1;
                p0 = sptr[0];
                p1 = sptr[1];
                dptr[0] = p0;
                dptr[1] = p1;

                len -= 4 * sizeof(size_t);
            } while (len != 0);
            return;
#endif
        }
    }

    // When doing regular allocation we clean this much memory
    // if we do cleaning, and if available.
    // Zeroing a contiguous range takes a fraction of every set in L1, so we aim for no more than
    // about half of L1 per thread, to leave room for whatever else the thread is working with.
    //  - x64 has 32-48K L1D, typically shared by 2 SMT threads.
    //    Other architectures that we may run on have 32-64K L1D, some with SMT.
    //  - arm64 currently has no SMT, and 64K+ L1D on Apple, Neoverse (Graviton, Cobalt), Oryon
    //    and current big Cortex cores.
    static const size_t MinZeroInitSize()
    {
#if defined(TARGET_ARM64)
        return 32 * 1024;
#else
        return 16 * 1024;
#endif
    }

    // Zeroes memory that allocations are about to use, from the start up.
    // We zero in blocks, from the last block to the first one, so that the start is written last
    // and is the most likely to be still in cache when used. Whatever the cache size, the part
    // that fits stays hot, instead of losing all of it once the range does not fit.
    // Within a block we use memset, which knows the best way to zero on the given hardware.
    // Bigger ranges are object-sized zeroing rather than zeroing ahead. We leave them to memset,
    // which may choose to not pollute the cache with them.
    static void ZeroInitForAllocation(size_t start, size_t end)
    {
        const size_t blockSize = 4 * 1024;

        _ASSERTE(start <= end);
        if (end - start > 2 * MinZeroInitSize())
        {
            memset((void*)start, 0, end - start);
            return;
        }

        while (end > start)
        {
            size_t blockStart = max(start, ALIGN_DOWN(end - 1, blockSize));
            memset((void*)blockStart, 0, end - blockStart);
            end = blockStart;
        }
    }

    // DOTNET_gcConservative
    static bool IsConservativeMode()
    {
        return (GCConfig::GetConservativeGC());
    }

    // DOTNET_gcConcurrent
    static bool IsConcurrentEnabled()
    {
        return (GCConfig::GetConcurrentGC());
    }

    // DOTNET_gcForceCompact
    static bool IsForceCompact()
    {
        return (GCConfig::GetForceCompact());
    }

    // DOTNET_gcRelocatingGen1
    static bool IsRelocatingInGen1()
    {
        return (GCConfig::GetRelocatingInGen1());
    }

    // DOTNET_gcRelocatingGen2
    static bool IsRelocatingInGen2()
    {
        return (GCConfig::GetRelocatingInGen2());
    }

    // DOTNET_gcGen0
    static bool IsGen0Enabled()
    {
        return (GCConfig::GetGen0GC());
    }

    // DOTNET_gcGen1
    static bool IsGen1Enabled()
    {
        return (GCConfig::GetGen1GC());
    }

    // DOTNET_gcTHP
    static bool UseTHP()
    {
        return (GCConfig::GetUseTHP());
    }

    // DOTNET_gcTrim
    static bool IsTrimmingEnabled()
    {
        return (GCConfig::GetTrimmigGC());
    }

    // DOTNET_gcPace
    static bool IsPacingEnabled()
    {
        return (GCConfig::GetPacingGC());
    }

    // DOTNET_gcTrackBackoff
    // After a thread's escape-tracked region ends without a productive thread-local GC,
    // the thread allocates the next 1, 2, 4 ... up to this many eligible regions untracked.
    static int TrackBackoffCap()
    {
        int64_t cap = GCConfig::GetTrackBackoff();
        if (cap < 0)
        {
            return 16;
        }

        return (int)min(cap, (int64_t)64);
    }

    // DOTNET_GCLatencyMode
    static bool IsLowLatencyMode()
    {
        return (GCConfig::GetLatencyMode()) >= 2;
    }

    static int HandlePartitionsCount()
    {
        int partitionCount = (int)GCConfig::GetHeapCount();
        if (partitionCount < 1)
        {
            partitionCount = GCToOSInterface::GetTotalProcessorCount();
        }

        return partitionCount;
    }

    // DOTNET_gcParallel
    static int MaxWorkersCount()
    {
        return (int)GCConfig::GetParallelGC();
    }

    // DOTNET_gcRate
    static int GcRate()
    {
        int gcRate = (int)GCConfig::GetGCRate();
        if (gcRate == -1)
        {
#if _DEBUG
            // minimum rate-limiting in debug
            return 0;
#else
            return 2;
#endif
        }

        return gcRate;
    }
    
    // DOTNET_gcSpin
    static int GcSpin()
    {
        int gcSpin = (int)GCConfig::GetGCSpin();
        if (gcSpin == -1)
        {
            return 10;
        }

        return gcSpin;
    }

    // DOTNET_gcReusableTarget
    // How much free space may be parked in reusable regions, as a percentage of expected
    // gen1 allocation. Reusables are drained into ephemeral queues when a blocking GC starts,
    // so holding much more than the mutator consumes before then only adds to that drain.
    static int ReusableTarget()
    {
        int target = (int)GCConfig::GetReusableTarget();
        if (target < 0)
        {
            return 200;
        }

        return target;
    }

    // DOTNET_GCHighMemPercent
    static uint32_t HighMemoryPercent()
    {
        uint32_t percent = (uint32_t)GCConfig::GetGCHighMemPercent();
        return percent == 0 ? 99 : min(99u, percent);
    }

    // DOTNET_gcGen2Target
    static int Gen2Target()
    {
        int target = (int)GCConfig::GetGen2Target();
        if (target < 100)
        {
            // target must be > 100%
            // if we see less, just default to triggering GC when heap doubles
            target = 200;
        }

        return target;
    }

    // DOTNET_gcGen1Target
    static int Gen1Target()
    {
        int target = (int)GCConfig::GetGen1Target();
        if (target < 100)
        {
            // target must be > 100%
            // if we see less, just default to triggering GC when ephemeral heap quadruples in size
            target = Gen2Target() * 2;
        }

        return target;
    }

    static size_t CommitGranularity()
    {
        // we can support sizes that are > OS page and binary fractions of REGION_SIZE_GRANULARITY.
        // we can also support PAGE_SIZE_GRANULARITY
        size_t result = 1024 * 32;

#if defined(TARGET_LINUX)

#if defined(TARGET_ARM64)
        result = max(result, GCToOSInterface::GetPageSize());
#endif

        if (UseTHP())
        {
            result = Satori::REGION_SIZE_GRANULARITY;
        }
#endif

        // result = Satori::REGION_SIZE_GRANULARITY;

        // result = Satori::PAGE_SIZE_GRANULARITY;

        return result;
    }

private:
    // the rate of the inline hardware counter, in Hz. 0 if we are not using it.
    static int64_t s_timeStampFrequency;
    static int64_t s_osTimeStampFrequency;
    static int64_t s_ticksPerUsec;

    static int64_t MeasureTimeStampFrequency();

    static FORCEINLINE int64_t ReadHwTimeStamp()
    {
#if defined(HOST_X86) || defined(HOST_AMD64)
#ifdef _MSC_VER
        return (int64_t)__rdtsc();
#else
        uint32_t lo, hi;
        __asm__ __volatile__("rdtsc" : "=a"(lo), "=d"(hi));
        return (int64_t)(((uint64_t)hi << 32) | lo);
#endif
#elif defined(HOST_ARM64)
#ifdef _MSC_VER
        return (int64_t)_ReadStatusReg(ARM64_CNTVCT_EL0);
#else
        int64_t t;
        __asm__ __volatile__("mrs %0, cntvct_el0" : "=r"(t));
        return t;
#endif
#else
        // unreachable - s_timeStampFrequency stays 0 when there is no inline counter.
        return 0;
#endif
    }

    NOINLINE
    static int64_t GetTimeStampSlow();
};

#endif
