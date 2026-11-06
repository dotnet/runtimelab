// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef __GLOBALS_H__
#define __GLOBALS_H__

#include <stdint.h>
#include <time.h>
#include "config.gc.h"

const int tccSecondsToMilliSeconds = 1000;

// The number of microseconds in a second.
const int tccSecondsToMicroSeconds = 1000000;

// The number of nanoseconds in a second.
const int tccSecondsToNanoSeconds = 1000000000;

// The number of microseconds in a millisecond.
const int tccMilliSecondsToMicroSeconds = 1000;

// The number of nanoseconds in a millisecond.
const int tccMilliSecondsToNanoSeconds = 1000000;

#if HAVE_PTHREAD_CONDATTR_SETCLOCK
inline void TimeSpecAdd(timespec* time, uint32_t milliseconds)
{
    uint64_t nsec = time->tv_nsec + (uint64_t)milliseconds * tccMilliSecondsToNanoSeconds;
    if (nsec >= tccSecondsToNanoSeconds)
    {
        time->tv_sec += nsec / tccSecondsToNanoSeconds;
        nsec %= tccSecondsToNanoSeconds;
    }

    time->tv_nsec = nsec;
}
#endif // HAVE_PTHREAD_CONDATTR_SETCLOCK

#if HAVE_CLOCK_GETTIME_NSEC_NP
// Convert nanoseconds to the timespec structure
// Parameters:
//  nanoseconds - time in nanoseconds to convert
//  t           - the target timespec structure
inline void NanosecondsToTimeSpec(uint64_t nanoseconds, timespec* t)
{
    t->tv_sec = nanoseconds / tccSecondsToNanoSeconds;
    t->tv_nsec = nanoseconds % tccSecondsToNanoSeconds;
}
#endif // HAVE_CLOCK_GETTIME_NSEC_NP

#endif // __GLOBALS_H__
