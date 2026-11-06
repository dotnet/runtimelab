// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
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

#include "common.h"
#include "gcenv.structs.h"
#include "gcenv.base.h"
#include "gcenv.os.h"

#if defined(TARGET_LINUX)

#include <errno.h>
#include <linux/futex.h>
#include <sys/syscall.h>
#include <unistd.h>
#include "globals.h"

GCGate::GCGate()
    : m_impl(nullptr), m_state(Blocking)
{
}

// returns true if was woken up. false if timed out
bool GCGate::TimedWait(int timeout)
{
    assert(timeout >= 0);

    timespec t;
    t.tv_sec  = (uint32_t)timeout / 1000;
    t.tv_nsec = ((uint32_t)timeout % 1000) * 1000 * 1000;

    // the timespec is a relative timeout with CLOCK_MONOTONIC clock by default.
    long waitResult = syscall(SYS_futex, &m_state, FUTEX_WAIT_PRIVATE, Blocking, &t, NULL, 0);

    // possible results: woken, not blocking, interrupted, timeout
    assert(waitResult == 0 || errno == EAGAIN || errno == EINTR || errno == ETIMEDOUT);

    // normal/immediate/spurious wakes are not timeouts
    // in release treat unexpected results as spurious wakes
    bool woken = waitResult == 0 || errno != ETIMEDOUT;
    if (woken)
    {
        // consume the wake
        m_state = Blocking;
    }

    return woken;
}

void GCGate::Wait()
{
    syscall(SYS_futex, &m_state, FUTEX_WAIT_PRIVATE, Blocking, NULL, NULL, 0);
    m_state = Blocking;
}

void GCGate::WakeAll()
{
    m_state = Open;
    syscall(SYS_futex, &m_state, FUTEX_WAKE_PRIVATE, INT_MAX, NULL, NULL, 0);
}

void GCGate::WakeOne()
{
    m_state = Open;
    syscall(SYS_futex, &m_state, FUTEX_WAKE_PRIVATE, 1, NULL, NULL, 0);
}

#else // TARGET_LINUX

#include <pthread.h>
#include <errno.h>
#include <time.h>
#include "config.gc.h"
#include "globals.h"

class GCGate::Impl
{
public:
    pthread_mutex_t m_cs;
    pthread_cond_t m_cv;
};

GCGate::GCGate()
    : m_impl(new (nothrow) Impl()), m_state(Blocking)
{
    pthread_mutex_init(&m_impl->m_cs, nullptr);
    pthread_condattr_t attrs;
    pthread_condattr_init(&attrs);
#if HAVE_PTHREAD_CONDATTR_SETCLOCK && !HAVE_CLOCK_GETTIME_NSEC_NP
    // Ensure that the pthread_cond_timedwait will use CLOCK_MONOTONIC
    pthread_condattr_setclock(&attrs, CLOCK_MONOTONIC);
#endif // HAVE_PTHREAD_CONDATTR_SETCLOCK && !HAVE_CLOCK_GETTIME_NSEC_NP
    pthread_cond_init(&m_impl->m_cv, &attrs);
    pthread_condattr_destroy(&attrs);
}

// returns true if was woken up
bool GCGate::TimedWait(int timeout)
{
    timespec endTime;
#if HAVE_CLOCK_GETTIME_NSEC_NP
    uint64_t endNanoseconds;
    uint64_t nanoseconds = (uint64_t)timeout * tccMilliSecondsToNanoSeconds;
    NanosecondsToTimeSpec(nanoseconds, &endTime);
    endNanoseconds = clock_gettime_nsec_np(CLOCK_UPTIME_RAW) + nanoseconds;
#elif HAVE_PTHREAD_CONDATTR_SETCLOCK
    clock_gettime(CLOCK_MONOTONIC, &endTime);
    TimeSpecAdd(&endTime, timeout);
#else
#error "Don't know how to perform timed wait on this platform"
#endif

    int waitResult = 0;
    pthread_mutex_lock(&m_impl->m_cs);
#if HAVE_CLOCK_GETTIME_NSEC_NP
    // Since OSX doesn't support CLOCK_MONOTONIC, we use relative variant of the timed wait.
    waitResult = m_state == Open ?
        0 :
        pthread_cond_timedwait_relative_np(&m_impl->m_cv, &m_impl->m_cs, &endTime);
#else // HAVE_CLOCK_GETTIME_NSEC_NP
    waitResult = m_state == Open ?
        0 :
        pthread_cond_timedwait(&m_impl->m_cv, &m_impl->m_cs, &endTime);
#endif // HAVE_CLOCK_GETTIME_NSEC_NP

    bool woken = waitResult == 0;
    if (woken)
    {
        // consume the wake while holding the lock, so that the check above and
        // the consumption are atomic with respect to the wakers.
        // NB: as on other platforms the wake could be spurious.
        m_state = Blocking;
    }

    pthread_mutex_unlock(&m_impl->m_cs);
    assert(waitResult == 0 || waitResult == ETIMEDOUT);

    return woken;
}

void GCGate::Wait()
{
    int waitResult;
    pthread_mutex_lock(&m_impl->m_cs);

    waitResult = m_state == Open ?
        0 :
        pthread_cond_wait(&m_impl->m_cv, &m_impl->m_cs);

    // consume the wake while holding the lock, so that the check above and
    // the consumption are atomic with respect to the wakers.
    // NB: as on other platforms the wake could be spurious.
    m_state = Blocking;
    pthread_mutex_unlock(&m_impl->m_cs);
    assert(waitResult == 0);
}

void GCGate::WakeOne()
{
    // NB: see the comment in WakeAll about publishing the state under the lock.
    pthread_mutex_lock(&m_impl->m_cs);
    m_state = Open;
    pthread_cond_signal(&m_impl->m_cv);
    pthread_mutex_unlock(&m_impl->m_cs);
}

void GCGate::WakeAll()
{
    // NB: the state must be published while holding the lock, otherwise a waiter
    //     could read the old state and start waiting after the signal is sent,
    //     which would result in a missed wake.
    pthread_mutex_lock(&m_impl->m_cs);
    m_state = Open;
    pthread_cond_broadcast(&m_impl->m_cv);
    pthread_mutex_unlock(&m_impl->m_cs);
}

#endif // TARGET_LINUX
