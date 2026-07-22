// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// ZeroGCPal.h - minimal Win32-compatibility shim for building ZeroGC on
// non-Windows hosts (currently: Linux).
//
// The gcenv.* headers (gcenv.base.h, gcenv.structs.h, ...) already define
// most Win32 type aliases (HRESULT, BOOL, DWORD, S_OK, E_OUTOFMEMORY, ...)
// portably under TARGET_UNIX, since the real GC ships on Linux/macOS too.
// This file only fills in the handful of additional Win32 names ZeroGC's
// own code (ZeroGC.h/.cpp, not the vendored gcenv headers) uses directly
// and that aren't already covered: CRITICAL_SECTION, VirtualAlloc/Free,
// QueryPerformanceCounter/Frequency + LARGE_INTEGER, the raw (non-template)
// Interlocked* function names, min/max, and E_INVALIDARG.
//
// Only included when HOST_WINDOWS is not defined (see ZeroGC.h) - on
// Windows, <windows.h> continues to provide the real versions of all of
// these, unchanged from before this file existed.
//
#ifndef __ZEROGC_PAL_H__
#define __ZEROGC_PAL_H__

#include <cstdint>
#include <cstring>
#include <cstdlib>
#include <pthread.h>
#include <sys/mman.h>
#include <sys/sysinfo.h>
#include <time.h>

// ---------------------------------------------------------------------------
// HRESULT extras not already defined by gcenv.base.h under TARGET_UNIX.
// ---------------------------------------------------------------------------
#ifndef E_INVALIDARG
#define E_INVALIDARG ((HRESULT)0x80070057L)
#endif

// ---------------------------------------------------------------------------
// min/max - on Windows these come from <windows.h>'s traditional macros
// (NOMINMAX is not defined by build.ps1). Provide equivalent generic
// templates here so ZeroGCHeap.cpp's unqualified min(...)/max(...) calls
// keep working unchanged.
// ---------------------------------------------------------------------------
template<typename T> inline T min(T a, T b) { return a < b ? a : b; }
template<typename T> inline T max(T a, T b) { return a > b ? a : b; }

// ---------------------------------------------------------------------------
// LONG64 (used in one explicit cast in ZeroGCHeap.cpp).
// ---------------------------------------------------------------------------
typedef int64_t LONG64;

// ---------------------------------------------------------------------------
// CRITICAL_SECTION -> pthread_mutex_t. ZeroGC only ever uses this for the
// small, rarely-touched frozen-segment table, never on any allocation hot
// path, so a plain (non-recursive) pthread mutex is sufficient.
// ---------------------------------------------------------------------------
struct CRITICAL_SECTION
{
    pthread_mutex_t Mutex;
};
inline void InitializeCriticalSection(CRITICAL_SECTION* cs) { pthread_mutex_init(&cs->Mutex, nullptr); }
inline void EnterCriticalSection(CRITICAL_SECTION* cs) { pthread_mutex_lock(&cs->Mutex); }
inline void LeaveCriticalSection(CRITICAL_SECTION* cs) { pthread_mutex_unlock(&cs->Mutex); }
inline void DeleteCriticalSection(CRITICAL_SECTION* cs) { pthread_mutex_destroy(&cs->Mutex); }

// ---------------------------------------------------------------------------
// LARGE_INTEGER / QueryPerformanceCounter/Frequency -> clock_gettime.
// Only used for ZeroGC's own informational uptime counter, never on a hot
// path, so the extra syscall vs. Windows' QPC is irrelevant.
// ---------------------------------------------------------------------------
union LARGE_INTEGER
{
    int64_t QuadPart;
};
inline void QueryPerformanceFrequency(LARGE_INTEGER* freq) { freq->QuadPart = 1000000000LL; } // clock_gettime ticks in ns
inline void QueryPerformanceCounter(LARGE_INTEGER* counter)
{
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    counter->QuadPart = (int64_t)ts.tv_sec * 1000000000LL + ts.tv_nsec;
}

// ---------------------------------------------------------------------------
// VirtualAlloc/VirtualFree -> mmap/mprotect/munmap. Mirrors Windows'
// reserve-then-commit two-step: MEM_RESERVE maps a PROT_NONE, MAP_NORESERVE
// range (so the kernel doesn't pre-account swap for the whole multi-GiB
// arena reservation, matching Windows' "reservation costs no physical
// memory" semantics); MEM_COMMIT then mprotect()s a sub-range to
// PROT_READ|PROT_WRITE, after which pages are demand-paged by the kernel
// exactly like a Windows MEM_COMMIT range.
// ---------------------------------------------------------------------------
#define MEM_RESERVE    0x2000u
#define MEM_COMMIT     0x1000u
#define MEM_DECOMMIT   0x4000u
#define MEM_RELEASE    0x8000u
#define PAGE_READWRITE 0x04u
#define PAGE_NOACCESS  0x01u

inline void* VirtualAlloc(void* addr, size_t size, uint32_t allocType, uint32_t protect)
{
    if (allocType & MEM_RESERVE)
    {
        void* p = mmap(addr, size, PROT_NONE, MAP_PRIVATE | MAP_ANONYMOUS | MAP_NORESERVE, -1, 0);
        return (p == MAP_FAILED) ? nullptr : p;
    }
    if (allocType & MEM_COMMIT)
    {
        int prot = (protect == PAGE_READWRITE) ? (PROT_READ | PROT_WRITE) : PROT_NONE;
        if (mprotect(addr, size, prot) != 0)
            return nullptr;
        return addr;
    }
    return nullptr;
}

inline bool VirtualFree(void* addr, size_t size, uint32_t freeType)
{
    if (freeType & MEM_RELEASE)
        return munmap(addr, size) == 0;
    if (freeType & MEM_DECOMMIT)
        return mprotect(addr, size, PROT_NONE) == 0;
    return false;
}

// ---------------------------------------------------------------------------
// Interlocked* (Win32 names, as called directly by ZeroGCHeap.cpp/
// ZeroGCHandles.cpp) -> GCC/Clang atomic builtins. These are distinct from
// (and simpler than) the portable templated `Interlocked::` class the
// vendored gcenv.interlocked.h provides - ZeroGC's own code was written
// against the raw Win32 function names, so this shim reproduces exactly
// those names/signatures instead of requiring call-site changes.
// ---------------------------------------------------------------------------
inline int64_t InterlockedExchangeAdd64(volatile int64_t* addend, int64_t value)
{
    return __sync_fetch_and_add(addend, value);
}
inline int64_t InterlockedIncrement64(volatile int64_t* addend)
{
    return __sync_add_and_fetch(addend, 1);
}
inline int64_t InterlockedDecrement64(volatile int64_t* addend)
{
    return __sync_sub_and_fetch(addend, 1);
}
inline int64_t InterlockedCompareExchange64(volatile int64_t* destination, int64_t exchange, int64_t comparand)
{
    return __sync_val_compare_and_swap(destination, comparand, exchange);
}
inline void* InterlockedCompareExchangePointer(void* volatile* destination, void* exchange, void* comparand)
{
    return __sync_val_compare_and_swap(destination, comparand, exchange);
}

// ---------------------------------------------------------------------------
// MEMORYSTATUSEX / GlobalMemoryStatusEx -> sysinfo(2). Only used for the
// informational GetMemoryLoad()/gc-info memory-load fields ZeroGC reports;
// sysinfo's coarser granularity (no distinction between "available" and
// "free" the way Windows does) is an acceptable approximation here since
// ZeroGC never uses this data to make any allocation/collection decision.
// ---------------------------------------------------------------------------
struct MEMORYSTATUSEX
{
    uint32_t dwLength;
    uint32_t dwMemoryLoad;
    uint64_t ullTotalPhys;
    uint64_t ullAvailPhys;
    uint64_t ullTotalPageFile;
    uint64_t ullAvailPageFile;
    uint64_t ullTotalVirtual;
    uint64_t ullAvailVirtual;
    uint64_t ullAvailExtendedVirtual;
};

inline bool GlobalMemoryStatusEx(MEMORYSTATUSEX* status)
{
    struct sysinfo info;
    if (sysinfo(&info) != 0)
        return false;

    uint64_t totalPhys = (uint64_t)info.totalram * info.mem_unit;
    uint64_t availPhys = (uint64_t)info.freeram * info.mem_unit;

    status->ullTotalPhys = totalPhys;
    status->ullAvailPhys = availPhys;
    status->ullTotalPageFile = totalPhys + (uint64_t)info.totalswap * info.mem_unit;
    status->ullAvailPageFile = availPhys + (uint64_t)info.freeswap * info.mem_unit;
    status->ullTotalVirtual = status->ullTotalPageFile;
    status->ullAvailVirtual = status->ullAvailPageFile;
    status->ullAvailExtendedVirtual = 0;
    status->dwMemoryLoad = totalPhys ? (uint32_t)(100 - (availPhys * 100 / totalPhys)) : 0;
    return true;
}

#endif // __ZEROGC_PAL_H__
