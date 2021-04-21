// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#ifndef __GCENV_H__
#define __GCENV_H__

#ifdef _MSC_VER
#pragma warning( disable: 4189 )  // 'hp': local variable is initialized but not referenced -- common in GC
#pragma warning( disable: 4127 )  // conditional expression is constant -- common in GC
#endif

#include <stdlib.h>
#include <stdint.h>
#include <assert.h>
#include <cstddef>
#include <string.h>

#include "sal.h"
#include "gcenv.structs.h"
#include "gcenv.interlocked.h"
#include "gcenv.base.h"
#include "gcenv.os.h"

#include "Crst.h"
#include "event.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "TargetPtrs.h"
#include "eetype.h"
#include "ObjectLayout.h"
#include "rheventtrace.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "gcrhinterface.h"
#include "gcenv.interlocked.inl"

#include "slist.h"
#include "RWLock.h"
#include "shash.h"
#include "TypeManager.h"
#include "RuntimeInstance.h"
#include "eetype.inl"
#include "volatile.h"

#include "gcenv.inl"

#include "stressLog.h"
#ifdef FEATURE_ETW

    #ifndef _INC_WINDOWS
        typedef void* LPVOID;
        typedef uint32_t UINT;
        typedef void* PVOID;
        typedef uint64_t ULONGLONG;
        typedef uint32_t ULONG;
        typedef int64_t LONGLONG;
        typedef uint8_t BYTE;
        typedef uint16_t UINT16;
    #endif // _INC_WINDOWS

    #include "etwevents.h"
    #include "eventtrace.h"

#else // FEATURE_ETW

    #include "etmdummy.h"
    #define ETW_EVENT_ENABLED(e,f) false

#endif // FEATURE_ETW

#define MAX_LONGPATH 1024
#define LOG(x)

#ifndef YieldProcessor
#define YieldProcessor PalYieldProcessor
#endif

// Adapter for GC's view of Array
class ArrayBase : Array
{
public:
    DWORD GetNumComponents()
    {
        return m_Length;
    }

    static size_t GetOffsetOfNumComponents()
    {
        return offsetof(ArrayBase, m_Length);
    }
};

//
// -----------------------------------------------------------------------------------------------------------
//
// Bridge GC/HandleTable's version of MethodTable to Redhawk's EEType. Neither component tries to access any
// fields of MethodTable directly so this is mostly just a case of providing all the CLR-style accessors they
// need implemented on top of EEType functionality (we can simply recast the 'this' pointer into an EEType
// pointer).
//
// ****** NOTE: Do NOT attempt to add fields or virtual methods to this class! The pointer passed in 'this'
// ****** really does point to an EEType (there's no such thing as a MethodTable structure in RH).
//
class MethodTable
{
public:
    uint32_t GetBaseSize() { return ((EEType*)this)->get_BaseSize(); }
    uint16_t GetComponentSize() { return ((EEType*)this)->get_ComponentSize(); }
    uint16_t RawGetComponentSize() { return ((EEType*)this)->get_ComponentSize(); }
    uint32_t ContainsPointers() { return ((EEType*)this)->HasReferenceFields(); }
    uint32_t ContainsPointersOrCollectible() { return ((EEType*)this)->HasReferenceFields(); }
    UInt32_BOOL HasComponentSize() const { return TRUE; }
    UInt32_BOOL HasFinalizer() { return ((EEType*)this)->HasFinalizer(); }
    UInt32_BOOL HasCriticalFinalizer() { return FALSE; }
    bool IsValueType() { return ((EEType*)this)->get_IsValueType(); }
    UInt32_BOOL SanityCheck() { return ((EEType*)this)->Validate(); }
};

EXTERN_C uint32_t _tls_index;
inline uint16_t GetClrInstanceId()
{
    return (uint16_t)_tls_index;
}

class IGCHeap;
typedef DPTR(IGCHeap) PTR_IGCHeap;
typedef DPTR(uint32_t) PTR_uint32_t;

enum CLRDataEnumMemoryFlags : int;

/* _TRUNCATE */
#if !defined (_TRUNCATE)
#define _TRUNCATE ((size_t)-1)
#endif  /* !defined (_TRUNCATE) */

#endif // __GCENV_H__
