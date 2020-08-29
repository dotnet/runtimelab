// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "pal_config.h"
#include "pal_time.h"
#include "pal_utilities.h"

#include <assert.h>
#include <utime.h>
#include <time.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <sys/time.h>
#include <sys/resource.h>
#if HAVE_MACH_ABSOLUTE_TIME
#include <mach/mach_time.h>
#endif

enum
{
    SecondsToMicroSeconds = 1000000,   // 10^6
    SecondsToNanoSeconds = 1000000000, // 10^9
    MicroSecondsToNanoSeconds = 1000   // 10^3
};

int32_t SystemNative_UTimensat(const char* path, TimeSpec* times)
{
    int32_t result;
#if HAVE_UTIMENSAT
    struct timespec updatedTimes[2];
    updatedTimes[0].tv_sec = (time_t)times[0].tv_sec;
    updatedTimes[0].tv_nsec = (long)times[0].tv_nsec;

    updatedTimes[1].tv_sec = (time_t)times[1].tv_sec;
    updatedTimes[1].tv_nsec = (long)times[1].tv_nsec;    
    while (CheckInterrupted(result = utimensat(AT_FDCWD, path, updatedTimes, 0)));
#else
    struct timeval updatedTimes[2];
    updatedTimes[0].tv_sec = (long)times[0].tv_sec;
    updatedTimes[0].tv_usec = (int)times[0].tv_nsec / 1000;
    
    updatedTimes[1].tv_sec = (long)times[1].tv_sec;
    updatedTimes[1].tv_usec = (int)times[1].tv_nsec / 1000;
    while (CheckInterrupted(result = utimes(path, updatedTimes)));
#endif

    return result;
}

// Gets the number of "ticks per second" of the underlying monotonic timer.
//
// On most Unix platforms, the methods that query the resolution return a value
// that is "nanoseconds per tick" in which case we need to scale before returning.
uint64_t SystemNative_GetTimestampResolution()
{
#if HAVE_MACH_ABSOLUTE_TIME
    mach_timebase_info_data_t mtid;

    if (mach_timebase_info(&mtid) != KERN_SUCCESS)
    {
        return 0;
    }

    // (numer / denom) gives you the nanoseconds per tick, so the below code
    // computes the number of ticks per second. We explicitly do the multiplication
    // first in order to help minimize the error that is produced by integer division.

    return (SecondsToNanoSeconds * (uint64_t)(mtid.denom)) / (uint64_t)(mtid.numer);
#else
    // clock_gettime() returns a result in terms of nanoseconds rather than a count. This
    // means that we need to either always scale the result by the actual resolution (to
    // get a count) or we need to say the resolution is in terms of nanoseconds. We prefer
    // the latter since it allows the highest throughput and should minimize error propagated
    // to the user.

    return SecondsToNanoSeconds;
#endif
}

uint64_t SystemNative_GetTimestamp()
{
#if HAVE_MACH_ABSOLUTE_TIME
    return mach_absolute_time();
#else
    struct timespec ts;

    int result = clock_gettime(CLOCK_MONOTONIC, &ts);
    assert(result == 0); // only possible errors are if MONOTONIC isn't supported or &ts is an invalid address
    (void)result; // suppress unused parameter warning in release builds

    return ((uint64_t)(ts.tv_sec) * SecondsToNanoSeconds) + (uint64_t)(ts.tv_nsec);
#endif
}

#if HAVE_MACH_ABSOLUTE_TIME
static LowLevelMutex s_lock(true /* abortOnFailure */, nullptr /* successRef */);

mach_timebase_info_data_t g_machTimebaseInfo = {};
bool g_isMachTimebaseInfoInitialized = false;

mach_timebase_info_data_t *InitializeTimebaseInfo()
{
    s_lock.Acquire();

    if (!g_isMachTimebaseInfoInitialized)
    {
        kern_return_t machRet = mach_timebase_info(&g_machTimebaseInfo);
        assert(machRet == KERN_SUCCESS);
        if (machRet == KERN_SUCCESS)
        {
            g_isMachTimebaseInfoInitialized = true;
        }
    }

    s_lock.Release();

    return g_isMachTimebaseInfoInitialized ? &g_machTimebaseInfo : nullptr;
}
#endif

#define SECONDS_TO_MILLISECONDS 1000
#define MILLISECONDS_TO_MICROSECONDS 1000
#define MILLISECONDS_TO_NANOSECONDS 1000000 // 10^6

// Returns a 64-bit tick count with a millisecond resolution. It tries its best
// to return monotonically increasing counts and avoid being affected by changes
// to the system clock (either due to drift or due to explicit changes to system
// time).
extern "C" uint64_t CoreLibNative_GetTickCount64()
{
    uint64_t retval = 0;

#if HAVE_MACH_ABSOLUTE_TIME
    {
        mach_timebase_info_data_t *machTimebaseInfo = GetMachTimebaseInfo();
        retval = (mach_absolute_time() * machTimebaseInfo->numer / machTimebaseInfo->denom) / MILLISECONDS_TO_NANOSECONDS;
    }
#elif HAVE_CLOCK_MONOTONIC_COARSE || HAVE_CLOCK_MONOTONIC
    {
        clockid_t clockType =
#if HAVE_CLOCK_MONOTONIC_COARSE
            CLOCK_MONOTONIC_COARSE; // good enough resolution, fastest speed
#else
            CLOCK_MONOTONIC;
#endif
        struct timespec ts;
        if (clock_gettime(clockType, &ts) != 0)
        {
            assert(false);
            return retval;
        }
        retval = (ts.tv_sec * SECONDS_TO_MILLISECONDS) + (ts.tv_nsec / MILLISECONDS_TO_NANOSECONDS);
    }
#else
    {
        struct timeval tv;
        if (gettimeofday(&tv, NULL) == -1)
        {
            assert(false);
            return retval;
        }
        retval = (tv.tv_sec * SECONDS_TO_MILLISECONDS) + (tv.tv_usec / MILLISECONDS_TO_MICROSECONDS);
    }
#endif
    return retval;
}

int32_t SystemNative_GetCpuUtilization(ProcessCpuInformation* previousCpuInfo)
{
    uint64_t kernelTime = 0;
    uint64_t userTime = 0;

    struct rusage resUsage;
    if (getrusage(RUSAGE_SELF, &resUsage) == -1)
    {
        assert(false);
        return 0;
    }
    else
    {
        kernelTime =
            ((uint64_t)(resUsage.ru_stime.tv_sec) * SecondsToNanoSeconds) + 
            ((uint64_t)(resUsage.ru_stime.tv_usec) * MicroSecondsToNanoSeconds);
        userTime =
            ((uint64_t)(resUsage.ru_utime.tv_sec) * SecondsToNanoSeconds) +
            ((uint64_t)(resUsage.ru_utime.tv_usec) * MicroSecondsToNanoSeconds);
    }

    uint64_t resolution = SystemNative_GetTimestampResolution();
    uint64_t timestamp = SystemNative_GetTimestamp();

    uint64_t currentTime = (uint64_t)((double)timestamp * ((double)SecondsToNanoSeconds / (double)resolution));

    uint64_t lastRecordedCurrentTime = previousCpuInfo->lastRecordedCurrentTime;
    uint64_t lastRecordedKernelTime = previousCpuInfo->lastRecordedKernelTime;
    uint64_t lastRecordedUserTime = previousCpuInfo->lastRecordedUserTime;

    uint64_t cpuTotalTime = 0;
    if (currentTime > lastRecordedCurrentTime)
    {
        cpuTotalTime = (currentTime - lastRecordedCurrentTime);
    }

    uint64_t cpuBusyTime = 0;
    if (userTime >= lastRecordedUserTime && kernelTime >= lastRecordedKernelTime)
    {
        cpuBusyTime = (userTime - lastRecordedUserTime) + (kernelTime - lastRecordedKernelTime);
    }

    int32_t cpuUtilization = 0;
    if (cpuTotalTime > 0 && cpuBusyTime > 0)
    {
        cpuUtilization = (int32_t)(cpuBusyTime * 100 / cpuTotalTime);
    }

    previousCpuInfo->lastRecordedCurrentTime = currentTime;
    previousCpuInfo->lastRecordedUserTime = userTime;
    previousCpuInfo->lastRecordedKernelTime = kernelTime;

    return cpuUtilization;
}
