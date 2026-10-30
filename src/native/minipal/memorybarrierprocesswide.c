// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef HOST_WINDOWS

#include <stdbool.h>
#include <stdint.h>
#include <assert.h>
#include <minipal/memorybarrierprocesswide.h>

#ifndef HOST_WASM
#include <pthread.h>
#include <stdio.h>
#include <sys/mman.h>
#include <unistd.h>

#ifdef __APPLE__
#include <stdlib.h>
#include <mach/thread_state.h>
#include <mach/mach_time.h>
#include <minipal/cpufeatures.h>

#define CHECK_MACH(_msg, machret) do {                                      \
        if (machret != KERN_SUCCESS)                                        \
        {                                                                   \
            char _szError[1024];                                            \
            snprintf(_szError, ARRAY_SIZE(_szError), "%s: %u: %s", __FUNCTION__, __LINE__, _msg);  \
            mach_error(_szError, machret);                                  \
            abort();                                                        \
        }                                                                   \
    } while (false)

#endif // __APPLE__

#ifdef __linux__
#include <linux/membarrier.h>
#include <sys/syscall.h>
#define membarrier(...) syscall(__NR_membarrier, __VA_ARGS__)
#undef HAVE_SYS_MEMBARRIER_H
#define HAVE_SYS_MEMBARRIER_H 1
#elif HAVE_SYS_MEMBARRIER_H
#include <sys/membarrier.h>
#endif

#if HAVE_SYS_MEMBARRIER_H
static bool CanFlushUsingMembarrier(void)
{
#ifdef TARGET_ANDROID
    // Avoid calling membarrier on older Android versions (older than API 29) where membarrier
    // may be barred by seccomp causing the process to be killed.
    int apiLevel = android_get_device_api_level();
    if (apiLevel < __ANDROID_API_Q__)
    {
        return false;
    }
#endif

    // Starting with Linux kernel 4.14, process memory barriers can be generated
    // using MEMBARRIER_CMD_PRIVATE_EXPEDITED.

    int mask = membarrier(MEMBARRIER_CMD_QUERY, 0, 0);

    if (mask >= 0 &&
        mask & MEMBARRIER_CMD_PRIVATE_EXPEDITED &&
        // Register intent to use the private expedited command.
        membarrier(MEMBARRIER_CMD_REGISTER_PRIVATE_EXPEDITED, 0, 0) == 0)
    {
        return true;
    }

    return false;
}

//
// Tracks if the OS supports membarrier syscall
//
static bool s_flushUsingMemBarrier = false;
#endif // HAVE_SYS_MEMBARRIER_H

#ifndef HOST_APPLE
// Helper memory page used by the fallback path
static uint8_t* g_helperPage = NULL;

static size_t s_pageSize = 0;
#else // HOST_APPLE
// Set when running under the Apple Rosetta x64 emulator, where the thread_get_state
// based implementation cannot be used at all.
// See the comment in minipal_initialize_memory_barrier_process_wide.
static bool s_flushByWaiting = false;

// How long to wait, in microseconds, for other cores to drain their store buffers.
//
// Store buffers drain on their own and continuously - nothing has to poke a core to
// make that happen, and no store can sit in one indefinitely. The drain is bounded by
// memory system latency, which is tens to low hundreds of nanoseconds even when the
// line has to be pulled from another core. Five microseconds is more than an order of
// magnitude beyond that, and it is a hard lower bound on our side: we do not return
// early, so the margin cannot be eroded by anything the other threads are doing.
#define FLUSH_BARRIER_WAIT_USECS 5

// The wait above expressed in mach ticks, so that the loop is an integer compare.
//
// mach_absolute_time is the cheapest monotonic source available here - it is a vDSO
// style read of the same counter clock_gettime is built on, at 17ns per call under
// Rosetta versus 29ns for clock_gettime_nsec_np and 36ns for clock_gettime. Thread
// CPU time would be cheaper conceptually, since only this thread's progress matters,
// but CLOCK_THREAD_CPUTIME_ID is a real syscall at 312ns and is 18x worse.
// CLOCK_UPTIME_RAW_APPROX is cheaper still at 8ns, but it is a cached value: under
// Rosetta it advances only about every 65us, which cannot express a 5us wait at all.
static uint64_t s_flushWaitTicks = 0;

static void FlushBarrierPause(void)
{
#if defined(HOST_X86) || defined(HOST_AMD64)
    __asm__ __volatile__(
        "rep\n"
        "nop");
#elif defined(HOST_ARM)
    __asm__ __volatile__( "yield");
#elif defined(HOST_ARM64)
    __asm__ __volatile__(
        "dmb ishst\n"
        "yield"
        );
#endif
}
#endif // !HOST_APPLE

#ifndef HOST_APPLE
// Mutex to make the fallback path thread safe.
// The Apple paths need no serialization: thread_get_state does not mutate anything,
// and neither does waiting.
static pthread_mutex_t g_flushProcessWriteBuffersMutex;
#endif // !HOST_APPLE
#endif // !HOST_WASM

static bool s_initializedMemoryBarrierSuccessfullyInitialized = false;

bool minipal_initialize_memory_barrier_process_wide(void)
{
    if (s_initializedMemoryBarrierSuccessfullyInitialized)
    {
        return true;
    }

#ifdef HOST_WASM
    // browser/wasm is currently single threaded
#elif defined(HOST_APPLE)
    // Apple platforms do not support membarrier, so we normally use thread_get_state.
    //
    // That does not work under the Rosetta x64 emulator. Servicing thread_get_state for a
    // translated thread requires Rosetta to reconstruct the guest x86 state from the host
    // arm64 state, and it cannot always do so - notably while another thread is forking,
    // when the task is quiesced. Such a call never completes and hangs both threads.
    // thread_suspend fails the same way, for the same reason.
    //
    // None of the usual alternatives work here either:
    //
    //  - The mprotect helper page trick used on other Unix platforms relies on the kernel
    //    sending an IPI to every core to shoot down TLBs, and it is that interrupt which
    //    serializes the other cores. On arm64 TLB invalidation is a broadcast instruction
    //    handled in hardware, so no IPI is sent and no core is ever interrupted. Measured,
    //    it produces 0.003 interruptions per barrier per thread versus 2.4 for
    //    thread_get_state - it is a no-op, which is worse than a hang because it is silent.
    //
    //  - Interrupting each thread with a signal does work, but it cannot be relied on:
    //    a thread that has signals blocked is still reported as running yet cannot run the
    //    handler, so the wait has no exit. System.Native blocks all signals across
    //    fork+exec, which stalled the barrier for 3ms in testing, and spinning at such a
    //    thread steals the core it needs to get out of the masked region.
    //
    // So under Rosetta simply wait instead. Rosetta executes translated code with the core
    // in TSO mode, so the only thing that can delay one thread's store from being seen by
    // another is that thread's store buffer - and store buffers drain autonomously and
    // continuously. Nothing needs to poke a core to make it happen, which is precisely why
    // waiting is more robust here than any mechanism that has to reach the other threads:
    // there is no thread state to reconstruct, no signal to be blocked, no lock to be held
    // by a thread that is not running, and nothing to deadlock against.
    if (minipal_detect_rosetta())
    {
        mach_timebase_info_data_t timebase;

        if (mach_timebase_info(&timebase) != KERN_SUCCESS)
        {
            return false;
        }

        // ticks = usecs * 1000 * denom / numer, computed once so the wait loop below
        // is a plain integer compare with no conversion in it.
        s_flushWaitTicks =
            ((uint64_t)FLUSH_BARRIER_WAIT_USECS * 1000ull * timebase.denom + timebase.numer - 1) / timebase.numer;

        s_flushByWaiting = true;
    }
#else
#if HAVE_SYS_MEMBARRIER_H
    if (CanFlushUsingMembarrier())
    {
        s_flushUsingMemBarrier = true;
    }
    else
#endif // HAVE_SYS_MEMBARRIER_H
    {
        // Fallback implementation
        assert(g_helperPage == NULL);

        int pageSize = sysconf( _SC_PAGE_SIZE );

        s_pageSize = (size_t)((pageSize > 0) ? pageSize : 0x1000);
        g_helperPage = (uint8_t*)(mmap(0, s_pageSize, PROT_READ | PROT_WRITE, MAP_ANONYMOUS | MAP_PRIVATE, -1, 0));

        if (g_helperPage == MAP_FAILED)
        {
            return false;
        }

        // Verify that the s_helperPage is really aligned to the s_pageSize
        assert((((size_t)g_helperPage) & (s_pageSize - 1)) == 0);

        // Locking the page ensures that it stays in memory during the two mprotect
        // calls in the FlushProcessWriteBuffers below. If the page was unmapped between
        // those calls, they would not have the expected effect of generating IPI.
        int status = mlock(g_helperPage, s_pageSize);

        if (status != 0)
        {
            return false;
        }

        status = pthread_mutex_init(&g_flushProcessWriteBuffersMutex, NULL);
        if (status != 0)
        {
            munlock(g_helperPage, s_pageSize);
            return false;
        }
    }
#endif // !HOST_WASM && !HOST_APPLE

    s_initializedMemoryBarrierSuccessfullyInitialized = true;
    return true;
}

// Flush write buffers of processors that are executing threads of the current process
void minipal_memory_barrier_process_wide(void)
{
    assert(s_initializedMemoryBarrierSuccessfullyInitialized);

#ifdef HOST_WASM
    // browser/wasm is currently single threaded
#elif defined(HOST_APPLE)
    if (s_flushByWaiting)
    {
        // See the comment in minipal_initialize_memory_barrier_process_wide.
        //
        // Wait out the store buffers rather than trying to reach the other threads.
        // mach_absolute_time is monotonic and does not stop while the machine is awake,
        // so if this thread is itself descheduled we only ever wait longer, never less.
        uint64_t start = mach_absolute_time();

        while ((mach_absolute_time() - start) < s_flushWaitTicks)
        {
            FlushBarrierPause();
        }
    }
    else
    {
        mach_msg_type_number_t cThreads;
        thread_act_t *pThreads;
        kern_return_t machret = task_threads(mach_task_self(), &pThreads, &cThreads);
        CHECK_MACH("task_threads()", machret);

        uintptr_t sp;
        uintptr_t registerValues[128];

        // Iterate through each of the threads in the list.
        for (mach_msg_type_number_t i = 0; i < cThreads; i++)
        {
            // Request the threads pointer values to force the thread to emit a memory barrier
            size_t registers = 128;
            machret = thread_get_register_pointer_values(pThreads[i], &sp, &registers, registerValues);

            if (machret == KERN_INSUFFICIENT_BUFFER_SIZE)
            {
                CHECK_MACH("thread_get_register_pointer_values()", machret);
            }

            machret = mach_port_deallocate(mach_task_self(), pThreads[i]);
            CHECK_MACH("mach_port_deallocate()", machret);
        }
        // Deallocate the thread list now we're done with it.
        machret = vm_deallocate(mach_task_self(), (vm_address_t)pThreads, cThreads * sizeof(thread_act_t));
        CHECK_MACH("vm_deallocate()", machret);
    }
#else // !HOST_APPLE && !HOST_WASM
#if HAVE_SYS_MEMBARRIER_H
    if (s_flushUsingMemBarrier)
    {
        int status = membarrier(MEMBARRIER_CMD_PRIVATE_EXPEDITED, 0, 0);
        assert(status == 0 && "Failed to flush using membarrier");
    }
    else
#endif // !HAVE_SYS_MEMBARRIER_H
    {
        assert(g_helperPage != NULL);

        int status = pthread_mutex_lock(&g_flushProcessWriteBuffersMutex);
        (void)status; // unused in release config
        assert(status == 0 && "Failed to lock the flushProcessWriteBuffersMutex lock");

        // causes the OS to issue IPI to flush TLBs on all processors. This also
        // results in flushing the processor buffers.
        // Changing a helper memory page protection from read / write to no access
        status = mprotect(g_helperPage, s_pageSize, PROT_READ | PROT_WRITE);
        assert(status == 0 && "Failed to change helper page protection to read / write");

        // Ensure that the page is dirty before we change the protection so that
        // we prevent the OS from skipping the global TLB flush.
        __sync_add_and_fetch((size_t*)g_helperPage, 1);

        status = mprotect(g_helperPage, s_pageSize, PROT_NONE);
        assert(status == 0 && "Failed to change helper page protection to no access");

        status = pthread_mutex_unlock(&g_flushProcessWriteBuffersMutex);
        assert(status == 0 && "Failed to unlock the flushProcessWriteBuffersMutex lock");
    }
#endif // !HOST_APPLE && !HOST_WASM
}
#else // !HOST_WINDOWS

#include <windows.h>

void minipal_memory_barrier_process_wide(void)
{
    FlushProcessWriteBuffers();
}
#endif // !HOST_WINDOWS
