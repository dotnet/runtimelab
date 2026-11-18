// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "laxori_write_barrier.h"

#if defined(_MSC_VER)
#include <intrin.h>
#else
#include <cpuid.h>
#endif

extern "C" LaxoriWriteBarrierGlobals g_laxori_write_barrier_globals = {};
static uint64_t s_privateHeapBytes;

static uintptr_t s_avLocations[] =
{
    reinterpret_cast<uintptr_t>(&LaxoriCheckedAssignRefAVLocation),
    reinterpret_cast<uintptr_t>(&LaxoriAssignRefMetadataAVLocation),
    reinterpret_cast<uintptr_t>(&LaxoriAssignRefAVLocation),
    reinterpret_cast<uintptr_t>(&LaxoriBarrierSlowAVLocation),
    reinterpret_cast<uintptr_t>(&LaxoriPrivateBitsAVLocation),
    0,
};

static LaxoriWriteBarrierPatchable s_patchable[] =
{
    { &LaxoriPatchable_MetadataBase, &g_laxori_write_barrier_globals.metadata_base },
    { &LaxoriPatchable_HeapLow, &g_laxori_write_barrier_globals.heap_low },
    { &LaxoriPatchable_HeapSize, &g_laxori_write_barrier_globals.heap_size },
    { &LaxoriPatchable_SlowPath, &g_laxori_write_barrier_globals.slow_path },
    { &LaxoriPatchable_XsaveMask, &g_laxori_write_barrier_globals.xsave_mask },
    { &LaxoriPatchable_OwnerTable, &g_laxori_write_barrier_globals.owner_table },
    { &LaxoriPatchable_PrivateBits, &g_laxori_write_barrier_globals.private_bits },
    { &LaxoriPatchable_RegionShift, &g_laxori_write_barrier_globals.region_shift },
    { &LaxoriPatchable_EscapePath, &g_laxori_write_barrier_globals.escape_path },
    { &LaxoriPatchable_PrivateHeapBytes, &g_laxori_write_barrier_globals.private_heap_bytes },
    { nullptr, nullptr },
};

void LaxoriWriteBarrierGetBlockInfo(LaxoriWriteBarrierBlockInfo* info)
{
    // Both entries cover the whole block: the runtime copies [first entry, end of the larger size), and the slow
    // path and the slots must be in the copy.
    uint8_t* checked = reinterpret_cast<uint8_t*>(&LaxoriCheckedAssignRef);
    uint8_t* assign = reinterpret_cast<uint8_t*>(&LaxoriAssignRef);
    info->checked_assign_ref = checked;
    info->checked_assign_ref_size = static_cast<size_t>(&LaxoriWriteBarrierBlock_End - checked);
    info->assign_ref = assign;
    info->assign_ref_size = static_cast<size_t>(&LaxoriWriteBarrierBlock_End - assign);
    info->av_locations = s_avLocations;
    info->patchable = s_patchable;
}

uintptr_t LaxoriWriteBarrierComputeXsaveMask()
{
    // x87 (1), SSE (2), AVX (4), opmask (32), ZMM_Hi256 (64), Hi16_ZMM (128). The size of the xsave frame in the
    // assembly (2816 bytes) is chosen for exactly this set.
    const uint64_t SavedComponents = 0xE7;
    uint32_t eax, ebx, ecx, edx;
#if defined(_MSC_VER)
    int regs[4];
    __cpuid(regs, 1);
    ecx = static_cast<uint32_t>(regs[2]);
    (void)eax; (void)ebx; (void)edx;
#else
    if (!__get_cpuid(1, &eax, &ebx, &ecx, &edx))
    {
        return 0;
    }
#endif
    const uint32_t Xsave = 1u << 26;
    const uint32_t OsXsave = 1u << 27;
    if ((ecx & (Xsave | OsXsave)) != (Xsave | OsXsave))
    {
        return 0;
    }

    uint64_t xcr0;
#if defined(_MSC_VER)
    xcr0 = _xgetbv(0);
#else
    uint32_t low, high;
    __asm__ volatile("xgetbv" : "=a"(low), "=d"(high) : "c"(0));
    xcr0 = (static_cast<uint64_t>(high) << 32) | low;
#endif
    return static_cast<uintptr_t>(xcr0 & SavedComponents);
}

void LaxoriWriteBarrierSetGlobals(
    uintptr_t metadataBase, uintptr_t heapLow, uintptr_t heapSize, void* slowPath, uintptr_t xsaveMask,
    uint64_t* ownerTable, uintptr_t privateBitsBase, uintptr_t regionShift, void* escapePath,
    uint64_t* privateHeapBytes)
{
    g_laxori_write_barrier_globals.metadata_base = metadataBase;
    g_laxori_write_barrier_globals.heap_low = heapLow;
    g_laxori_write_barrier_globals.heap_size = heapSize;
    g_laxori_write_barrier_globals.slow_path = reinterpret_cast<uintptr_t>(slowPath);
    g_laxori_write_barrier_globals.xsave_mask = xsaveMask;
    g_laxori_write_barrier_globals.owner_table = reinterpret_cast<uintptr_t>(ownerTable);
    g_laxori_write_barrier_globals.private_bits = privateBitsBase;
    g_laxori_write_barrier_globals.region_shift = regionShift;
    g_laxori_write_barrier_globals.escape_path = reinterpret_cast<uintptr_t>(escapePath);
    s_privateHeapBytes = heapSize;
    g_laxori_write_barrier_globals.private_heap_bytes =
        reinterpret_cast<uintptr_t>(privateHeapBytes == nullptr ? &s_privateHeapBytes : privateHeapBytes);
}
