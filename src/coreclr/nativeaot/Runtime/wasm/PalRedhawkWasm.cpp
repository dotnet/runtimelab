// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Platform Abstraction Layer (PAL) implementation of functionality not covered by Unix APIs on WASM.
//

#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"

#ifndef FEATURE_WASM_THREADS
//
// Note that we return the native stack bounds here, not shadow stack ones. Currently this functionality is mainly
// used for RuntimeHelpers.TryEnsureSufficientExecutionStack, and we do use the native stack in codegen, so this
// is an acceptable approximation.
//
extern "C" unsigned char __data_end;
extern "C" unsigned char __heap_base;
void PalGetMaximumStackBounds_SingleThreadedWasm(void** ppStackLowOut, void** ppStackHighOut)
{
    // See https://github.com/emscripten-core/emscripten/pull/18057 and https://reviews.llvm.org/D135910.
    // TODO-LLVM: update to use "__stack_low" and "__stack_high" when a recent enough linker becomes
    // available (which should be Emscripten 3.1.26, TBD WASI SDK).
    unsigned char* pStackLow = &__data_end;
    unsigned char* pStackHigh = &__heap_base;

    // Sanity check that we have the expected memory layout.
    ASSERT((pStackHigh - pStackLow) >= 64 * 1024);
    if (pStackLow >= pStackHigh)
    {
        PalPrintFatalError("\nFatal error. Unexpected stack layout.\n");
        RhFailFast();
    }

    *ppStackLowOut = pStackLow;
    *ppStackHighOut = pStackHigh;
}

#ifdef TARGET_WASI
// No-op stubs that assume a single-threaded environment.
int pthread_mutex_init(pthread_mutex_t * m, const pthread_mutexattr_t *__restrict a)
{
    return 0;
}

int pthread_mutexattr_init(pthread_mutexattr_t *a)
{
    return 0;
}

int pthread_mutexattr_settype(pthread_mutexattr_t *a, int type)
{
    return 0;
}

int pthread_mutex_destroy(pthread_mutex_t *mutex)
{
    return 0;
}

int pthread_mutexattr_destroy(pthread_mutexattr_t *)
{
    return 0;
}

int pthread_cond_init(pthread_cond_t *cond, const pthread_condattr_t *attr)
{
    return 0;
}

int pthread_condattr_init(pthread_condattr_t *attr)
{
    return 0;
}

int pthread_mutex_lock(pthread_mutex_t *)
{
    return 0;
}

int pthread_mutex_unlock(pthread_mutex_t *)
{
    return 0;
}

pthread_t pthread_self(void)
{
    return (pthread_t)0;
}

int pthread_equal(pthread_t, pthread_t)
{
    return 1; // only one thread
}

int pthread_attr_init(pthread_attr_t *)
{
    return 0;
}

int pthread_attr_destroy(pthread_attr_t *)
{
    return 0;
}

int pthread_condattr_destroy(pthread_condattr_t *)
{
    return 0;
}

int pthread_cond_broadcast(pthread_cond_t *)
{
    return 0;
}

int pthread_attr_setdetachstate(pthread_attr_t *, int)
{
    return 0;
}

#include <sys/mman.h>
int mprotect(void* addr, size_t len, int prot)
{
    return 0;
}

extern "C" int __cxa_thread_atexit(void (*func)(), void*, void*)
{
    return 0;
}
#endif // TARGET_WASI
#endif // !FEATURE_WASM_THREADS
