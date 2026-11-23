// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Laxori: the few compiler and CPU operations of the private tier that differ between MSVC (Windows) and GCC/Clang (Linux).

#pragma once

#include <stdint.h>
#include <stddef.h>

#if defined(_MSC_VER) && !defined(__clang__)
#include <intrin.h>
#endif

// The identity of the current thread for region ownership. Linux: the thread pointer (fs:0). Windows: the address of the TEB
// (gs:[0x30]). The write barrier (laxori_writebarrier.S and .asm) reads the same value and compares it with the owner table.
inline uintptr_t LaxoriCurrentThreadTag()
{
#if defined(_MSC_VER) && !defined(__clang__)
    return static_cast<uintptr_t>(__readgsqword(0x30));
#elif defined(_WIN32)
    uintptr_t tag;
    __asm__ volatile("mov %%gs:0x30, %0" : "=r"(tag));
    return tag;
#else
    uintptr_t tag;
    __asm__ volatile("mov %%fs:0, %0" : "=r"(tag));
    return tag;
#endif
}

inline uint64_t LaxoriReadTimestampCounter()
{
#if defined(_MSC_VER) && !defined(__clang__)
    return __rdtsc();
#else
    return __builtin_ia32_rdtsc();
#endif
}

// The number of trailing zero bits. The value is not zero.
inline unsigned LaxoriCountTrailingZeros64(uint64_t value)
{
#if defined(_MSC_VER) && !defined(__clang__)
    unsigned long index;
    _BitScanForward64(&index, value);
    return static_cast<unsigned>(index);
#else
    return static_cast<unsigned>(__builtin_ctzll(value));
#endif
}

// An acquire load of a naturally aligned word. On x64 an ordinary load already has acquire order; the volatile read keeps the
// compiler from moving it.
template <typename T>
inline T LaxoriLoadAcquire(const T* address)
{
#if defined(_MSC_VER) && !defined(__clang__)
    T value = *reinterpret_cast<const volatile T*>(address);
    _ReadWriteBarrier();
    return value;
#else
    return __atomic_load_n(address, __ATOMIC_ACQUIRE);
#endif
}

// A release store of a naturally aligned word (an ordinary x64 store has release order).
template <typename T>
inline void LaxoriStoreRelease(T* address, T value)
{
#if defined(_MSC_VER) && !defined(__clang__)
    _ReadWriteBarrier();
    *reinterpret_cast<volatile T*>(address) = value;
#else
    __atomic_store_n(address, value, __ATOMIC_RELEASE);
#endif
}
