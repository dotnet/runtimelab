// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// test_islargeobject.cpp
//
// Standalone, no-CoreCLR-needed correctness test for ZeroGCHeap::IsLargeObject().
//
// Why this exists: IsLargeObject()'s only two real call sites in dotnet/runtime
// (Object::ValidateInner in vm/object.cpp, and Object::ValidateHeap in
// gc/gcinternal.h) are both unreachable in a normal Release build - the former
// requires USE_CHECKED_OBJECTREFS (only defined under _DEBUG, see
// src/coreclr/inc/switches.h), the latter requires the VERIFY_HEAP compile-time
// define (only set for the CoreCLR reference GC's own build, not the EE/host
// consuming a standalone GC) plus a live heap-verification pass. This means
// ordinary smoke testing (running GCPerfSim/ConsoleApp against a Release
// coreclr.dll with ZeroGC.dll loaded) NEVER actually invokes IsLargeObject,
// regardless of whether its logic is correct. This test calls ZeroGCHeap's
// allocation and IsLargeObject methods directly, bypassing the EE entirely,
// to deterministically verify the map-based large-object-range tracking added
// to remove the MethodTable dependency (see ZeroGCHeap.cpp's g_largeObjectRanges).
//
#include "../ZeroGC.h"
#include <cstdio>

#define CHECK(cond, msg) \
    do { \
        total++; \
        if (cond) { passed++; } \
        else { printf("FAIL: %s\n", msg); } \
    } while (0)

int main()
{
    int total = 0, passed = 0;

    ZeroGCHeap* heap = ZeroGCHeap::CreateAndInitialize();
    if (heap == nullptr || FAILED(heap->Initialize()))
    {
        printf("FATAL: ZeroGCHeap::Initialize failed\n");
        return 1;
    }

    gc_alloc_context ctx = {};

    // 1. smallObj ordinary object (no flags) - well below the 85,000-byte
    //    LARGE_OBJECT_SIZE threshold - must NOT be classified as large.
    Object* smallObj = heap->Alloc(&ctx, 24, 0);
    CHECK(smallObj != nullptr, "smallObj alloc returned null");
    CHECK(!heap->IsLargeObject(smallObj), "24-byte object misclassified as large");

    // 2. Object at exactly the boundary just below LARGE_OBJECT_SIZE (85000).
    //    alignedSize for a requested size of 84992 (already 8-byte aligned)
    //    stays at 84992 < 85000 -> must NOT be large.
    Object* justUnder = heap->Alloc(&ctx, 84992, 0);
    CHECK(justUnder != nullptr, "just-under-threshold alloc returned null");
    CHECK(!heap->IsLargeObject(justUnder), "84992-byte object misclassified as large");

    // 3. Object at/above LARGE_OBJECT_SIZE with no explicit flag - the
    //    alignedSize >= LARGE_OBJECT_SIZE branch alone must classify it large
    //    (this is the core of the fix: previously relied on
    //    MethodTable::GetBaseSize(), now purely allocation-time bookkeeping).
    Object* big = heap->Alloc(&ctx, 100000, 0);
    CHECK(big != nullptr, "large alloc returned null");
    CHECK(heap->IsLargeObject(big), "100000-byte object NOT classified as large");

    // 4. Explicit GC_ALLOC_LARGE_OBJECT_HEAP flag with a size still below the
    //    byte threshold - the old MethodTable::GetBaseSize() check would have
    //    said "not large" (smallObj LOH-flagged objects, e.g. some string
    //    interning paths, are real). Must still say NOT large: the flag
    //    controls chunk *placement* only, not IsLargeObject's answer, and the
    //    fix intentionally preserves this exact narrower semantic (see the
    //    comment above the g_largeObjectRanges recording in AllocateFromArena).
    Object* smallLohFlagged = heap->Alloc(&ctx, 100, GC_ALLOC_LARGE_OBJECT_HEAP);
    CHECK(smallLohFlagged != nullptr, "smallObj LOH-flagged alloc returned null");
    CHECK(!heap->IsLargeObject(smallLohFlagged), "smallObj LOH-flagged object misclassified as large");

    // 5. Explicit GC_ALLOC_PINNED_OBJECT_HEAP flag with a smallObj size (typical
    //    POH usage, e.g. pinned buffers for interop) - routed through the
    //    same isLarge chunk-placement branch as LOH/big objects, but must
    //    NOT be reported as a large object.
    Object* smallPohFlagged = heap->Alloc(&ctx, 64, GC_ALLOC_PINNED_OBJECT_HEAP);
    CHECK(smallPohFlagged != nullptr, "smallObj POH-flagged alloc returned null");
    CHECK(!heap->IsLargeObject(smallPohFlagged), "smallObj POH-flagged object misclassified as large");

    // 6. A pointer that was never returned by Alloc at all (e.g. a stack
    //    address) must not be misidentified as falling inside some large
    //    object's tracked range.
    int stackVar = 0;
    CHECK(!heap->IsLargeObject((Object*)&stackVar), "unrelated stack address misclassified as large");

    // 7. Many smallObj objects allocated back-to-back must all still report
    //    "not large" - guards against the range map accidentally coalescing
    //    or misindexing adjacent smallObj allocations.
    bool allSmallOk = true;
    for (int i = 0; i < 64; i++)
    {
        Object* o = heap->Alloc(&ctx, 32, 0);
        if (o == nullptr || heap->IsLargeObject(o)) { allSmallOk = false; break; }
    }
    CHECK(allSmallOk, "a smallObj object in a back-to-back allocation run was misclassified as large");

    // 8. Interleave: allocate large, smallObj, large, smallObj - confirms the
    //    range map correctly distinguishes adjacent large and smallObj objects
    //    rather than treating everything after the first large object as large.
    Object* big2 = heap->Alloc(&ctx, 200000, 0);
    Object* small2 = heap->Alloc(&ctx, 16, 0);
    Object* big3 = heap->Alloc(&ctx, 90000, GC_ALLOC_LARGE_OBJECT_HEAP);
    Object* small3 = heap->Alloc(&ctx, 8, 0);
    CHECK(big2 && heap->IsLargeObject(big2), "interleaved big2 misclassified");
    CHECK(small2 && !heap->IsLargeObject(small2), "interleaved small2 misclassified");
    CHECK(big3 && heap->IsLargeObject(big3), "interleaved big3 (LOH-flagged, size>=threshold) misclassified");
    CHECK(small3 && !heap->IsLargeObject(small3), "interleaved small3 misclassified");

    printf("\n%d/%d checks passed.\n", passed, total);
    return (passed == total) ? 0 : 1;
}
