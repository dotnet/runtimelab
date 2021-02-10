// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*
 * GCSCAN.H
 *
 * GC Root Scanning
 *

 *
 */

#ifndef _GCSCAN_H_
#define _GCSCAN_H_

#include "gc.h"

// Scanning dependent handles for promotion can become a complex operation due to cascaded dependencies and
// other issues (see the comments for GcDhInitialScan and friends in gcscan.cpp for further details). As a
// result we need to maintain a context between all the DH scanning methods called during a single mark phase.
// The structure below describes this context. We allocate one of these per GC heap at Ref_Initialize time and
// select between them based on the ScanContext passed to us by the GC during the mark phase.
struct DhContext
{
    bool            m_fUnpromotedPrimaries;     // Did last scan find at least one non-null unpromoted primary?
    bool            m_fPromoted;                // Did last scan promote at least one secondary?
    promote_func   *m_pfnPromoteFunction;       // GC promote callback to be used for all secondary promotions
    int             m_iCondemned;               // The condemned generation
    int             m_iMaxGen;                  // The maximum generation
    ScanContext    *m_pScanContext;             // The GC's scan context for this phase
};

struct GCDebugContract;

class GCScan
{
    friend struct GCDebugContract;

  public:

    static void GcScanSizedRefs(promote_func* fn, int condemned, int max_gen, ScanContext* sc);

    // Regular stack Roots
    static void GcScanRoots (promote_func* fn, int condemned, int max_gen, ScanContext* sc);

    //
    static void GcScanHandles (promote_func* fn, int condemned, int max_gen, ScanContext* sc);

    static void GcRuntimeStructuresValid (BOOL bValid);

    static bool GetGcRuntimeStructuresValid ();
#ifdef DACCESS_COMPILE
    static void EnumMemoryRegions(CLRDataEnumMemoryFlags flags);
#endif // DACCESS_COMPILE

    static void GcScanHandlesForProfilerAndETW (int max_gen, ScanContext* sc, handle_scan_fn fn);
    static void GcScanDependentHandlesForProfilerAndETW (int max_gen, ScanContext* sc, handle_scan_fn fn);

    // scan for dead weak pointers
    static void GcWeakPtrScan (promote_func* fn, int condemned, int max_gen, ScanContext*sc );
    static void GcWeakPtrScanBySingleThread (int condemned, int max_gen, ScanContext*sc );

    // scan for dead weak pointers
    static void GcShortWeakPtrScan (promote_func* fn, int condemned, int max_gen,
                                    ScanContext* sc);

    //
    // Dependent handle promotion scan support
    //

    // Perform initial (incomplete) scan which will deterimine if there's any further work required.
    static void GcDhInitialScan(promote_func* fn, int condemned, int max_gen, ScanContext* sc);

    // Called between scans to ask if any handles with an unpromoted secondary existed at the end of the last
    // scan.
    static bool GcDhUnpromotedHandlesExist(ScanContext* sc);

    // Rescan the handles for additonal primaries that have been promoted since the last scan. Return true if
    // any objects were promoted as a result.
    static bool GcDhReScan(ScanContext* sc);

    // post-promotions callback
    static void GcPromotionsGranted (int condemned, int max_gen,
                                     ScanContext* sc);

    // post-promotions callback some roots were demoted
    static void GcDemote (int condemned, int max_gen, ScanContext* sc);

    static size_t AskForMoreReservedMemory (size_t old_size, size_t need_size);

    static void VerifyHandleTable(int condemned, int max_gen, ScanContext* sc);

    static VOLATILE(int32_t) m_GcStructuresInvalidCnt;
};

// This structure is part of a in-memory serialization format that is used by diagnostic tools to
// reason about the runtime. As a contract with our diagnostic tools it must be kept up-to-date
// by changing the MajorVersion when breaking changes occur. If you are changing the runtime then
// you are responsible for understanding what changes are breaking changes. You can do this by
// reading the specification (Documentation\design-docs\diagnostics\ProcessMemoryFormatSpec.md)
// to understand what promises the runtime makes to diagnostic tools. Any change that would make that
// document become inaccurate is a breaking change.
//
// If you do want to make a breaking change please coordinate with diagnostics team as breaking changes
// require debugger side components to be updated, and then the new versions will need to be distributed
// to customers. Ideally you will check in updates to the runtime components, the debugger parser
// components, and the format specification at the same time.
//
// Although not guaranteed to be exhaustive, at a glance these are some potential breaking changes:
//   - Removing a field from this structure
//   - Reordering fields in the structure
//   - Changing the data type of a field in this structure
//   - Changing the data type of a field in another structure that is being refered to here with
//       the offsetof() operator
//   - Changing the data type of a global whose address is recorded in this structure
//   - Changing the meaning of a field or global refered to in this structure so that it can no longer
//     be used in the manner the format specification describes.
struct GCDebugContract
{
    const uint16_t MajorVersion = 1; // breaking changes
    const uint16_t MinorVersion = 0; // back-compatible changes
    volatile int32_t* const m_GcStructuresInvalidCntAddr = &GCScan::m_GcStructuresInvalidCnt;
};

#endif // _GCSCAN_H_
