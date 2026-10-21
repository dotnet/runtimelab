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

void GCGate::Wait()
{
    TimedWait(INFINITE);
}

bool GCGate::TimedWait(uint32_t timeout)
{
    timespec relativeTimeout;
    timespec* waitTimeout = nullptr;
    if (timeout != INFINITE)
    {
        uint64_t nanoseconds = static_cast<uint64_t>(timeout) * tccMilliSecondsToNanoSeconds;
        relativeTimeout.tv_sec = nanoseconds / tccSecondsToNanoSeconds;
        relativeTimeout.tv_nsec = nanoseconds % tccSecondsToNanoSeconds;
        waitTimeout = &relativeTimeout;
    }

    long result = syscall(SYS_futex, &m_state, FUTEX_WAIT_PRIVATE, Blocking, waitTimeout, nullptr, 0);
    if (result != 0)
    {
        int error = errno;
        if (error == ETIMEDOUT)
        {
            return false;
        }

        if (error != EAGAIN && error != EINTR)
        {
            assert(!"Failed to wait on GC gate");
            abort();
        }
    }

    m_state = Blocking;
    return true;
}

void GCGate::WakeOne()
{
    m_state = Open;
    if (syscall(SYS_futex, &m_state, FUTEX_WAKE_PRIVATE, 1) < 0)
    {
        assert(!"Failed to wake GC gate");
        abort();
    }
}

void GCGate::WakeAll()
{
    m_state = Open;
    if (syscall(SYS_futex, &m_state, FUTEX_WAKE_PRIVATE, INT_MAX) < 0)
    {
        assert(!"Failed to wake GC gate");
        abort();
    }
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
    pthread_mutex_t m_mutex;
    pthread_cond_t m_condition;
};

GCGate::GCGate()
    : m_impl(new (nothrow) Impl()), m_state(Blocking)
{
    if (m_impl == nullptr || pthread_mutex_init(&m_impl->m_mutex, nullptr) != 0)
    {
        assert(!"Failed to initialize GC gate");
        abort();
    }

    pthread_condattr_t attributes;
    if (pthread_condattr_init(&attributes) != 0)
    {
        assert(!"Failed to initialize GC gate");
        abort();
    }

#if HAVE_PTHREAD_CONDATTR_SETCLOCK && !HAVE_CLOCK_GETTIME_NSEC_NP
    if (pthread_condattr_setclock(&attributes, CLOCK_MONOTONIC) != 0)
    {
        assert(!"Failed to set GC gate wait clock");
        abort();
    }
#endif // HAVE_PTHREAD_CONDATTR_SETCLOCK && !HAVE_CLOCK_GETTIME_NSEC_NP

    int result = pthread_cond_init(&m_impl->m_condition, &attributes);
    int error = pthread_condattr_destroy(&attributes);
    if (result != 0 || error != 0)
    {
        assert(!"Failed to initialize GC gate");
        abort();
    }
}

void GCGate::Wait()
{
    TimedWait(INFINITE);
}

bool GCGate::TimedWait(uint32_t timeout)
{
    int error = pthread_mutex_lock(&m_impl->m_mutex);
    assert(error == 0);
    int result = 0;
    if (m_state == Blocking)
    {
        if (timeout == INFINITE)
        {
            result = pthread_cond_wait(&m_impl->m_condition, &m_impl->m_mutex);
        }
        else
        {
            timespec waitTimeout;
#if HAVE_CLOCK_GETTIME_NSEC_NP
            uint64_t nanoseconds = static_cast<uint64_t>(timeout) * tccMilliSecondsToNanoSeconds;
            waitTimeout.tv_sec = nanoseconds / tccSecondsToNanoSeconds;
            waitTimeout.tv_nsec = nanoseconds % tccSecondsToNanoSeconds;
            result = pthread_cond_timedwait_relative_np(&m_impl->m_condition, &m_impl->m_mutex, &waitTimeout);
#else
#if HAVE_PTHREAD_CONDATTR_SETCLOCK
            const clockid_t waitClock = CLOCK_MONOTONIC;
#else
            const clockid_t waitClock = CLOCK_REALTIME;
#endif // HAVE_PTHREAD_CONDATTR_SETCLOCK
            if (clock_gettime(waitClock, &waitTimeout) != 0)
            {
                assert(!"Failed to read GC gate wait clock");
                abort();
            }

            uint64_t nanoseconds = static_cast<uint64_t>(waitTimeout.tv_nsec) +
                static_cast<uint64_t>(timeout) * tccMilliSecondsToNanoSeconds;
            waitTimeout.tv_sec += nanoseconds / tccSecondsToNanoSeconds;
            waitTimeout.tv_nsec = nanoseconds % tccSecondsToNanoSeconds;
            result = pthread_cond_timedwait(&m_impl->m_condition, &m_impl->m_mutex, &waitTimeout);
#endif // HAVE_CLOCK_GETTIME_NSEC_NP
        }
    }

    if (result == 0)
    {
        m_state = Blocking;
    }
    error = pthread_mutex_unlock(&m_impl->m_mutex);
    assert(error == 0);
    (void)error;

    if (result != 0 && result != ETIMEDOUT)
    {
        assert(!"Failed to wait on GC gate");
        abort();
    }

    return result != ETIMEDOUT;
}

void GCGate::WakeOne()
{
    int error = pthread_mutex_lock(&m_impl->m_mutex);
    assert(error == 0);
    m_state = Open;
    int result = pthread_cond_signal(&m_impl->m_condition);
    error = pthread_mutex_unlock(&m_impl->m_mutex);
    assert(error == 0);
    (void)error;
    if (result != 0)
    {
        assert(!"Failed to wake GC gate");
        abort();
    }
}

void GCGate::WakeAll()
{
    int error = pthread_mutex_lock(&m_impl->m_mutex);
    assert(error == 0);
    m_state = Open;
    int result = pthread_cond_broadcast(&m_impl->m_condition);
    error = pthread_mutex_unlock(&m_impl->m_mutex);
    assert(error == 0);
    (void)error;
    if (result != 0)
    {
        assert(!"Failed to wake GC gate");
        abort();
    }
}

#endif // TARGET_LINUX
