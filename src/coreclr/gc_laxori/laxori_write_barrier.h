// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// The LAXORI write barrier block: two assembly helpers (assign-ref and checked-assign-ref) that the runtime copies and
// installs through IGCHeap::GetAssignRefFunctions (PR #3316 mechanism, "specialized" barrier ABI). The assembly is in
// amd64/laxori_writebarrier.S and amd64/laxori_writebarrier.asm. This header does not depend on the GC headers, so the
// native barrier test can use it too.

#ifndef LAXORI_WRITE_BARRIER_H
#define LAXORI_WRITE_BARRIER_H

#include <stddef.h>
#include <stdint.h>

extern "C"
{
    // The helpers. Arguments are in the registers that the runtime barrier helpers use. They are not C calls:
    // they change only the registers that the JIT lets a barrier helper change.
    void LaxoriAssignRef(void** destination, void* reference);
    void LaxoriCheckedAssignRef(void** destination, void* reference);

    // Markers in the assembly. The block [LaxoriCheckedAssignRef, LaxoriWriteBarrierBlock_End) is relocatable.
    extern uint8_t LaxoriCheckedAssignRef_End;
    extern uint8_t LaxoriAssignRef_End;
    extern uint8_t LaxoriWriteBarrierBlock_End;

    // Places where a hardware fault means a null reference.
    extern uint8_t LaxoriCheckedAssignRefAVLocation;
    extern uint8_t LaxoriAssignRefMetadataAVLocation;
    extern uint8_t LaxoriAssignRefAVLocation;
    extern uint8_t LaxoriBarrierSlowAVLocation;

    // The patchable slots, 8 bytes each, inside the block.
    extern uint8_t LaxoriPatchable_MetadataBase;
    extern uint8_t LaxoriPatchable_HeapLow;
    extern uint8_t LaxoriPatchable_HeapSize;
    extern uint8_t LaxoriPatchable_SlowPath;
    extern uint8_t LaxoriPatchable_XsaveMask;
    // Laxori: the owner table (one 8-byte tag for each 32 KiB block of the reservation), the private bitmap (biased base:
    // byte base[A >> 6], bit (A >> 3) & 7), the shift of the region size, and the escape slow path
    // void LaxoriEscapeSlowPath(Object** destination, Object* reference).
    extern uint8_t LaxoriPatchable_OwnerTable;
    extern uint8_t LaxoriPatchable_PrivateBits;
    extern uint8_t LaxoriPatchable_RegionShift;
    extern uint8_t LaxoriPatchable_EscapePath;
    extern uint8_t LaxoriPatchable_PrivateHeapBytes;
    extern uint8_t LaxoriPrivateBitsAVLocation;
}

// The values that the slots cache. The runtime copies the current value of each global into its slot (right after it
// copies the block, and every time StompWriteBarrier runs). The layout of a pair is the same as
// GCWriteBarrierPatchableValue (gcinterface.h); laxori_gc.cpp checks it.
struct LaxoriWriteBarrierGlobals
{
    // Biased base of the FieldUnlogged plane: the metadata byte of address A is base[A >> 6], its bit is (A >> 3) & 7.
    uintptr_t metadata_base;
    // The LAXORI heap, [heap_low, heap_low + heap_size). The checked helper stores without a check outside of it.
    uintptr_t heap_low;
    uintptr_t heap_size;
    // void LaxoriBarrierSlowPath(Object** destination). Called with the whole vector state saved.
    uintptr_t slow_path;
    // The xsave components to save around the slow path; 0 means use fxsave.
    uintptr_t xsave_mask;
    uintptr_t owner_table;
    uintptr_t private_bits;
    uintptr_t region_shift;
    uintptr_t escape_path;
    // Pointer to the committed Immix high-water mark. LOS references cannot belong to a private region.
    uintptr_t private_heap_bytes;
};

extern "C" LaxoriWriteBarrierGlobals g_laxori_write_barrier_globals;

struct LaxoriWriteBarrierPatchable
{
    void* slot;
    void* source;
};

struct LaxoriWriteBarrierBlockInfo
{
    void* assign_ref;
    size_t assign_ref_size;
    void* checked_assign_ref;
    size_t checked_assign_ref_size;
    // Zero-terminated list of fault addresses.
    uintptr_t* av_locations;
    // Terminated by a null slot.
    LaxoriWriteBarrierPatchable* patchable;
};

// Describes the block in the image (the original, not the runtime copy).
void LaxoriWriteBarrierGetBlockInfo(LaxoriWriteBarrierBlockInfo* info);

// The xsave components that the slow path saves: the state that C++ code (or a library it calls) can change.
// Returns 0 when the OS does not enable xsave; the slow path then uses fxsave.
uintptr_t LaxoriWriteBarrierComputeXsaveMask();

// Sets the values that the patchable slots cache.
void LaxoriWriteBarrierSetGlobals(
    uintptr_t metadataBase, uintptr_t heapLow, uintptr_t heapSize, void* slowPath, uintptr_t xsaveMask,
    uint64_t* ownerTable, uintptr_t privateBitsBase, uintptr_t regionShift, void* escapePath,
    uint64_t* privateHeapBytes = nullptr);

#endif // LAXORI_WRITE_BARRIER_H
