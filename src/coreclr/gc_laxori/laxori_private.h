// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Laxori: the private tier. State of one thread-owned private region.

#ifndef LAXORI_PRIVATE_H
#define LAXORI_PRIVATE_H

#include <stddef.h>
#include <stdint.h>

// A region is a run of aligned Immix blocks that one thread owns. Addresses in this structure are header addresses (the
// address of the object header, one word before the object pointer), like the spans of the engine.
//
// Invariant: a live object of a region lies inside one block. The Immix engine promotes and sweeps by block, and it looks at the
// block of the start of an object only. The space of the region that holds no live object is the list of gaps. A gap lies inside one
// block. It holds no object and is never parsed; it can have any size (also 8 or 16 bytes).
struct LaxoriPrivateGap
{
    uintptr_t start;
    uintptr_t end;
};

struct LaxoriPrivateRegion
{
    uintptr_t start = 0;           // first header address of the region. Zero when the thread has no region.
    uintptr_t end = 0;             // start + size
    uintptr_t cursor = 0;          // header address of the next object while the runtime buffer is retired
    uintptr_t parseEnd = 0;        // end of the space that objects can use: end - 8, so that the bits of the last object stay inside
    size_t escapedSize = 0;        // bytes of escaped objects
    size_t survivors = 0;          // bytes marked by the thread-local collection in progress
    int32_t markStack = 0;         // offset of the object pointer on top of the mark stack from start. Zero: empty
    bool buffer = false;           // the runtime buffer [alloc_ptr, alloc_limit) lies in this region
    bool freshGaps = false;        // the gaps are the unused blocks of a new region: they are zero memory
    uint32_t blocks = 0;           // blocks of the run
    uint64_t collections = 0;      // thread-local collections of this region
    LaxoriPrivateGap* gaps = nullptr;   // the dead space of the region, in address order
    uint32_t gapCount = 0;
    uint32_t gapNext = 0;          // the next gap to look at when the buffer is full
    uint32_t gapCurrent = 0;       // 1 + the index of the gap that the buffer was taken from. Zero: none
    uint32_t gapCapacity = 0;
    LaxoriPrivateGap* nextGaps = nullptr;   // the gaps that a thread-local collection plans; they replace `gaps` when it succeeds
    uint32_t nextGapCount = 0;
    uint32_t nextGapCapacity = 0;
};

#endif // LAXORI_PRIVATE_H
