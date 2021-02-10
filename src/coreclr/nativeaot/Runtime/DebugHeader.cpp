// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"
#include "gcenv.h"
#include "gcheaputilities.h"
#include "gcinterface.dac.h"
#include "rhassert.h"
#include "TargetPtrs.h"
#include "varint.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "holder.h"
#include "RuntimeInstance.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "threadstore.h"

GPTR_DECL(EEType, g_pFreeObjectEEType);

struct DebugTypeEntry
{
    DebugTypeEntry *Next;
    const char *TypeName;
    const char *FieldName;
    const uint32_t FieldOffset;
};

struct GlobalValueEntry
{
    GlobalValueEntry *Next;
    const char *Name;
    const void *Address;
};

struct DefineEntry
{
    DefineEntry *Next;
    const char *Name;
    const char *Value;
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
struct NativeAOTRuntimeDebugHeader
{
    // The cookie serves as a sanity check against process corruption or being requested
    // to treat some other non-.Net module as though it did contain the coreRT runtime.
    // It can also be changed if we want to make a breaking change so drastic that
    // earlier debuggers should treat the module as if it had no .Net runtime at all.
    // If the cookie is valid a debugger is safe to assume the Major/Minor version fields
    // will follow, but any contents beyond that depends on the version values.
    // The cookie value is currently set to 0x4E 0x41 0x44 0x48 (NADH in ascii)
    const uint8_t Cookie[4] = { 0x4E, 0x41, 0x44, 0x48 };

    // This counter can be incremented to indicate breaking changes
    // This field must be encoded little endian, regardless of the typical endianess of
    // the machine
    const uint16_t MajorVersion = 1;

    // This counter can be incremented to indicate back-compatible changes
    // This field must be encoded little endian, regardless of the typical endianess of
    // the machine
    const uint16_t MinorVersion = 0;

    // These flags must be encoded little endian, regardless of the typical endianess of
    // the machine. Ie Bit 0 is the least significant bit of the first byte.
    // Bit 0 - Set if the pointer size is 8 bytes, otherwise pointer size is 4 bytes
    // Bit 1 - Set if the machine is big endian
    // The high 30 bits are reserved. Changes to these bits will be considered a
    // back-compatible change.
    const uint32_t Flags = sizeof(void*) == 8 ? 0x1 : 0x0;

    // Reserved - Currently it only serves as alignment padding for the pointers which
    // follow but future usage will be considered a back-compatible change.
    const uint32_t ReservedPadding = 0;

    // Header pointers below here are encoded using the defined pointer size and endianess
    // specified in the Flags field. The data within the contracts they point to also uses
    // the same pointer size and endianess encoding unless otherwise specified.

    DebugTypeEntry *DebugTypesList = nullptr;

    GlobalValueEntry *GlobalsList = nullptr;

    DefineEntry *DefinesList = nullptr;
};

extern "C" NativeAOTRuntimeDebugHeader g_NativeAOTRuntimeDebugHeader = {};

#define MAKE_DEBUG_ENTRY(TypeName, FieldName, Value)                                                                        \
    do                                                                                                                      \
    {                                                                                                                       \
        currentType = new (nothrow) DebugTypeEntry{ previousType, #TypeName, #FieldName, Value };                             \
        previousType = currentType;                                                                                         \
    } while(0)

#define MAKE_DEBUG_FIELD_ENTRY(TypeName, FieldName) MAKE_DEBUG_ENTRY(TypeName, FieldName, offsetof(TypeName, FieldName))

// TODO: this would probably make more sense in the globals list
#define MAKE_DEFINE_ENTRY(Name, Value) MAKE_DEBUG_ENTRY(Globals, Name, Value)

#define MAKE_SIZE_ENTRY(TypeName) MAKE_DEBUG_ENTRY(TypeName, SIZEOF, sizeof(TypeName))

#define MAKE_GLOBAL_ENTRY(Name)                                                                                             \
    do                                                                                                                      \
    {                                                                                                                       \
        currentGlobal = new (nothrow) GlobalValueEntry{ previousGlobal, #Name, Name };                                      \
        previousGlobal = currentGlobal;                                                                                     \
    } while(0)                                                                                                              \

extern "C" void PopulateDebugHeaders()
{
    DebugTypeEntry *previousType = nullptr;
    DebugTypeEntry *currentType = nullptr;

    MAKE_SIZE_ENTRY(GcDacVars);
    MAKE_DEBUG_FIELD_ENTRY(GcDacVars, major_version_number);
    MAKE_DEBUG_FIELD_ENTRY(GcDacVars, minor_version_number);
    MAKE_DEBUG_FIELD_ENTRY(GcDacVars, generation_size);
    MAKE_DEBUG_FIELD_ENTRY(GcDacVars, total_generation_count);
    MAKE_DEBUG_FIELD_ENTRY(GcDacVars, built_with_svr);
    MAKE_DEBUG_FIELD_ENTRY(GcDacVars, finalize_queue);
    MAKE_DEBUG_FIELD_ENTRY(GcDacVars, generation_table);
    MAKE_DEBUG_FIELD_ENTRY(GcDacVars, ephemeral_heap_segment);
    MAKE_DEBUG_FIELD_ENTRY(GcDacVars, alloc_allocated);

    MAKE_SIZE_ENTRY(gc_alloc_context);
    MAKE_DEBUG_FIELD_ENTRY(gc_alloc_context, alloc_ptr);
    MAKE_DEBUG_FIELD_ENTRY(gc_alloc_context, alloc_limit);
    MAKE_DEBUG_FIELD_ENTRY(gc_alloc_context, alloc_bytes;);
    MAKE_DEBUG_FIELD_ENTRY(gc_alloc_context, alloc_bytes_uoh);
    MAKE_DEBUG_FIELD_ENTRY(gc_alloc_context, alloc_count);

    MAKE_SIZE_ENTRY(dac_generation);
    MAKE_DEBUG_FIELD_ENTRY(dac_generation, allocation_context);
    MAKE_DEBUG_FIELD_ENTRY(dac_generation, start_segment);
    MAKE_DEBUG_FIELD_ENTRY(dac_generation, allocation_start);

    MAKE_SIZE_ENTRY(dac_heap_segment);
    MAKE_DEBUG_FIELD_ENTRY(dac_heap_segment, allocated);
    MAKE_DEBUG_FIELD_ENTRY(dac_heap_segment, committed);
    MAKE_DEBUG_FIELD_ENTRY(dac_heap_segment, reserved);
    MAKE_DEBUG_FIELD_ENTRY(dac_heap_segment, used);
    MAKE_DEBUG_FIELD_ENTRY(dac_heap_segment, mem);
    MAKE_DEBUG_FIELD_ENTRY(dac_heap_segment, flags);
    MAKE_DEBUG_FIELD_ENTRY(dac_heap_segment, next);
    MAKE_DEBUG_FIELD_ENTRY(dac_heap_segment, background_allocated);
    MAKE_DEBUG_FIELD_ENTRY(dac_heap_segment, heap);

    MAKE_DEFINE_ENTRY(FinalizeExtraSegCount, dac_finalize_queue::ExtraSegCount);
    MAKE_DEFINE_ENTRY(MinObjectSize, MIN_OBJECT_SIZE);

    MAKE_SIZE_ENTRY(ThreadStore);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStore, m_ThreadList);

    MAKE_SIZE_ENTRY(ThreadBuffer);
    MAKE_DEBUG_FIELD_ENTRY(ThreadBuffer, m_pNext);
    MAKE_DEBUG_FIELD_ENTRY(ThreadBuffer, m_rgbAllocContextBuffer);
    MAKE_DEBUG_FIELD_ENTRY(ThreadBuffer, m_threadId);
    MAKE_DEBUG_FIELD_ENTRY(ThreadBuffer, m_pThreadStressLog);

    // EEThreadID is forward declared and not available
    MAKE_DEBUG_ENTRY(EEThreadID, SIZEOF, sizeof(void*));
    MAKE_DEBUG_ENTRY(EEThreadID, m_FiberPtrId, 0);

    MAKE_SIZE_ENTRY(EEType);
    MAKE_DEBUG_FIELD_ENTRY(EEType, m_uBaseSize);
    MAKE_DEBUG_FIELD_ENTRY(EEType, m_usComponentSize);
    MAKE_DEBUG_FIELD_ENTRY(EEType, m_usFlags);    MAKE_DEBUG_ENTRY(EEType, m_pBaseType, offsetof(EEType, m_RelatedType) + offsetof(EEType::RelatedTypeUnion, m_pBaseType));
    MAKE_DEBUG_ENTRY(EEType, m_ppBaseTypeViaIAT, offsetof(EEType, m_RelatedType) + offsetof(EEType::RelatedTypeUnion, m_ppBaseTypeViaIAT));
    MAKE_DEBUG_ENTRY(EEType, m_pCanonicalType, offsetof(EEType, m_RelatedType) + offsetof(EEType::RelatedTypeUnion, m_pCanonicalType));
    MAKE_DEBUG_ENTRY(EEType, m_ppCanonicalTypeViaIAT, offsetof(EEType, m_RelatedType) + offsetof(EEType::RelatedTypeUnion, m_ppCanonicalTypeViaIAT));
    MAKE_DEBUG_ENTRY(EEType, m_pRelatedParameterType, offsetof(EEType, m_RelatedType) + offsetof(EEType::RelatedTypeUnion, m_pRelatedParameterType));
    MAKE_DEBUG_ENTRY(EEType, m_ppRelatedParameterTypeViaIAT, offsetof(EEType, m_RelatedType) + offsetof(EEType::RelatedTypeUnion, m_ppRelatedParameterTypeViaIAT));
    MAKE_DEBUG_FIELD_ENTRY(EEType, m_VTable);

    MAKE_SIZE_ENTRY(StressLog);
    MAKE_DEBUG_FIELD_ENTRY(StressLog, facilitiesToLog);
    MAKE_DEBUG_FIELD_ENTRY(StressLog, levelToLog);
    MAKE_DEBUG_FIELD_ENTRY(StressLog, totalChunk);
    MAKE_DEBUG_FIELD_ENTRY(StressLog, logs);
    MAKE_DEBUG_FIELD_ENTRY(StressLog, tickFrequency);
    MAKE_DEBUG_FIELD_ENTRY(StressLog, startTimeStamp);
    MAKE_DEBUG_FIELD_ENTRY(StressLog, startTime);
    MAKE_DEBUG_FIELD_ENTRY(StressLog, moduleOffset);

    MAKE_SIZE_ENTRY(ThreadStressLog);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, next);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, threadId);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, isDead);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, readHasWrapped);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, writeHasWrapped);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, curPtr);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, readPtr);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, chunkListHead);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, chunkListTail);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, curReadChunk);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, curWriteChunk);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, chunkListLength);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, pThread);
    MAKE_DEBUG_FIELD_ENTRY(ThreadStressLog, origCurPtr);

    MAKE_SIZE_ENTRY(StressLogChunk);
    MAKE_DEFINE_ENTRY(StressLogChunk_ChunkSize, STRESSLOG_CHUNK_SIZE);
    MAKE_DEBUG_FIELD_ENTRY(StressLogChunk, prev);
    MAKE_DEBUG_FIELD_ENTRY(StressLogChunk, next);
    MAKE_DEBUG_FIELD_ENTRY(StressLogChunk, buf);
    MAKE_DEBUG_FIELD_ENTRY(StressLogChunk, dwSig1);
    MAKE_DEBUG_FIELD_ENTRY(StressLogChunk, dwSig2);

    MAKE_SIZE_ENTRY(StressMsg);
    MAKE_DEBUG_FIELD_ENTRY(StressMsg, fmtOffsCArgs);
    MAKE_DEBUG_FIELD_ENTRY(StressMsg, facility);
    MAKE_DEBUG_FIELD_ENTRY(StressMsg, timeStamp);
    MAKE_DEBUG_FIELD_ENTRY(StressMsg, args);

    MAKE_SIZE_ENTRY(Object);
    MAKE_DEBUG_FIELD_ENTRY(Object, m_pEEType);

    MAKE_SIZE_ENTRY(Array);
    MAKE_DEBUG_FIELD_ENTRY(Array, m_Length);

    MAKE_SIZE_ENTRY(RuntimeInstance);
    MAKE_DEBUG_FIELD_ENTRY(RuntimeInstance, m_pThreadStore);

    GlobalValueEntry *previousGlobal = nullptr;
    GlobalValueEntry *currentGlobal = nullptr;

    RuntimeInstance *g_pTheRuntimeInstance = GetRuntimeInstance();
    MAKE_GLOBAL_ENTRY(g_pTheRuntimeInstance);

    MAKE_GLOBAL_ENTRY(g_gcDacGlobals);

    MAKE_GLOBAL_ENTRY(g_pFreeObjectEEType);

    void *g_stressLog = &StressLog::theLog;
    MAKE_GLOBAL_ENTRY(g_stressLog);

    // Some DAC functions need to know the module base address, easiest way is with
    // the HANDLE to our module which is the base address.
    HANDLE moduleBaseAddress = PalGetModuleHandleFromPointer(&PopulateDebugHeaders);
    MAKE_GLOBAL_ENTRY(moduleBaseAddress);

    g_NativeAOTRuntimeDebugHeader.DebugTypesList = currentType;
    g_NativeAOTRuntimeDebugHeader.GlobalsList = currentGlobal;
}
