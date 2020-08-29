// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "pal_threading.h"

#include <limits.h>
#include <sched.h>
#include <assert.h>
#include <stdbool.h>
#include <stdlib.h>

#include <pthread.h>

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// LowLevelMonitor - Represents a non-recursive mutex and condition

struct LowLevelMonitor
{
    pthread_mutex_t Mutex;
    pthread_cond_t Condition;
#ifdef DEBUG
    bool IsLocked;
#endif
};

static void SetIsLocked(LowLevelMonitor* monitor, bool isLocked)
{
#ifdef DEBUG
    assert(monitor->IsLocked != isLocked);
    monitor->IsLocked = isLocked;
#else
    (void)monitor; // unused in release build
    (void)isLocked; // unused in release build
#endif
}

LowLevelMonitor* SystemNative_LowLevelMonitor_Create()
{
    LowLevelMonitor* monitor = (LowLevelMonitor *)malloc(sizeof(LowLevelMonitor));
    if (monitor == NULL)
    {
        return NULL;
    }

    int error;

    error = pthread_mutex_init(&monitor->Mutex, NULL);
    if (error != 0)
    {
        free(monitor);
        return NULL;
    }

    error = pthread_cond_init(&monitor->Condition, NULL);
    if (error != 0)
    {
        error = pthread_mutex_destroy(&monitor->Mutex);
        assert(error == 0);
        free(monitor);
        return NULL;
    }

#ifdef DEBUG
    monitor->IsLocked = false;
#endif

    return monitor;
}

void SystemNative_LowLevelMonitor_Destroy(LowLevelMonitor* monitor)
{
    assert(monitor != NULL);

    int error;

    error = pthread_cond_destroy(&monitor->Condition);
    assert(error == 0);

    error = pthread_mutex_destroy(&monitor->Mutex);
    assert(error == 0);

    (void)error; // unused in release build

    free(monitor);
}

void SystemNative_LowLevelMonitor_Acquire(LowLevelMonitor* monitor)
{
    assert(monitor != NULL);

    int error = pthread_mutex_lock(&monitor->Mutex);
    assert(error == 0);
    (void)error; // unused in release build

    SetIsLocked(monitor, true);
}

void SystemNative_LowLevelMonitor_Release(LowLevelMonitor* monitor)
{
    assert(monitor != NULL);

    SetIsLocked(monitor, false);

    int error = pthread_mutex_unlock(&monitor->Mutex);
    assert(error == 0);
    (void)error; // unused in release build
}

void SystemNative_LowLevelMonitor_Wait(LowLevelMonitor* monitor)
{
    assert(monitor != NULL);

    SetIsLocked(monitor, false);

    int error = pthread_cond_wait(&monitor->Condition, &monitor->Mutex);
    assert(error == 0);
    (void)error; // unused in release build

    SetIsLocked(monitor, true);
}

int32_t SystemNative_LowLevelMonitor_TimedWait(LowLevelMonitor *monitor, int32_t timeoutMilliseconds);
{
    assert(timeoutMilliseconds >= -1);

    if (timeoutMilliseconds < 0)
    {
        SystemNative_LowLevelMonitor_Wait(timeoutMilliseconds);
        return true;
    }

    SetIsLocked(monitor, false);

    int error;

    // Calculate the time at which a timeout should occur, and wait. Older versions of OSX don't support clock_gettime with
    // CLOCK_MONOTONIC, so we instead compute the relative timeout duration, and use a relative variant of the timed wait.
    timespec timeoutTimeSpec;
#if HAVE_MACH_ABSOLUTE_TIME
    MillisecondsToTimeSpec(timeoutMilliseconds, &timeoutTimeSpec);
    error = pthread_cond_timedwait_relative_np(&m_condition, &m_mutex, &timeoutTimeSpec);
#else
    error = clock_gettime(CLOCK_MONOTONIC, &timeoutTimeSpec);
    assert(error == 0);
    AddMillisecondsToTimeSpec(timeoutMilliseconds, &timeoutTimeSpec);
    error = pthread_cond_timedwait(&m_condition, &m_mutex, &timeoutTimeSpec);
#endif
    assert(error == 0 || error == ETIMEDOUT);

    SetIsLocked(monitor, true);

    return error == 0;
}

void SystemNative_LowLevelMonitor_Signal_Release(LowLevelMonitor* monitor)
{
    assert(monitor != NULL);

    int error;

    error = pthread_cond_signal(&monitor->Condition);
    assert(error == 0);

    SetIsLocked(monitor, false);

    error = pthread_mutex_unlock(&monitor->Mutex);
    assert(error == 0);

    (void)error; // unused in release build
}

int32_t SystemNative_RuntimeThread_CreateThread(size_t stackSize, void *(*startAddress)(void*), void *parameter)
{
    bool result = false;
    pthread_attr_t attrs;

    int error = pthread_attr_init(&attrs);
    if (error != 0)
    {
        // Do not call pthread_attr_destroy
        return false;
    }

    error = pthread_attr_setdetachstate(&attrs, PTHREAD_CREATE_DETACHED);
    assert(error == 0);

    if (stackSize > 0)
    {
        if (stackSize < PTHREAD_STACK_MIN)
        {
            stackSize = PTHREAD_STACK_MIN;
        }

        error = pthread_attr_setstacksize(&attrs, stackSize);
        if (error != 0) goto CreateThreadExit;
    }

    pthread_t threadId;
    error = pthread_create(&threadId, &attrs, startAddress, parameter);
    if (error != 0) goto CreateThreadExit;

    result = true;

CreateThreadExit:
    error = pthread_attr_destroy(&attrs);
    assert(error == 0);

    return result;
}
