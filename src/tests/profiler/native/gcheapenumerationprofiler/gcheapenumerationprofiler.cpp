// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "gcheapenumerationprofiler.h"
#include "../profilerstring.h"

#if WIN32
#define EXPORT
#else // WIN32
#define EXPORT __attribute__ ((visibility ("default")))
#endif // WIN32

GUID GCHeapEnumerationProfiler::GetClsid()
{
    // {8753F0E1-6D6D-4329-B8E1-334918869C15}
	GUID clsid = { 0x8753f0e1, 0x6d6d, 0x4329,{ 0xb8, 0xe1, 0x33, 0x49, 0x18, 0x86, 0x9c, 0x15 } };
	return clsid;
}

HRESULT GCHeapEnumerationProfiler::Initialize(IUnknown* pICorProfilerInfoUnk)
{
    Profiler::Initialize(pICorProfilerInfoUnk);
    printf("GCHeapEnumerationProfiler::Initialize\n");

    int eventMask = 0;
    constexpr ULONG bufferSize = 1024;
    ULONG envVarLen = 0;
    WCHAR envVar[bufferSize];

    HRESULT hr = pCorProfilerInfo->GetEnvironmentVariable(WCHAR("Set_Monitor_GC_Event_Mask"), bufferSize, &envVarLen, envVar);
    if (FAILED(hr))
    {
        printf("FAIL: ICorProfilerInfo::GetEnvironmentVariable() failed hr=0x%x", hr);
        IncrementFailures();
        return hr;
    }
    String envVarStr = envVar;
    String expectedEnvVarStr = reinterpret_cast<const WCHAR*>(u"TRUE");
    if (envVarStr == expectedEnvVarStr)
    {
        printf("Setting GarbageCollectionStarted event masks\n");
        eventMask |= COR_PRF_MONITOR_GC;
    }

    hr = pCorProfilerInfo->GetEnvironmentVariable(WCHAR("Test_Object_References_Only"), bufferSize, &envVarLen, envVar);
    if (FAILED(hr))
    {
        printf("FAIL: GetEnvironmentVariable for object-reference test failed hr=0x%x\n", hr);
        IncrementFailures();
        return hr;
    }
    _referenceOnly = String(envVar) == expectedEnvVarStr;
    if (_referenceOnly)
    {
        eventMask |= COR_PRF_MONITOR_OBJECT_ALLOCATED | COR_PRF_ENABLE_OBJECT_ALLOCATED;
    }

    hr = pCorProfilerInfo->SetEventMask2(eventMask, COR_PRF_HIGH_MONITOR_NONE);
    if (FAILED(hr))
    {
        printf("FAIL: ICorProfilerInfo::SetEventMask2() failed hr=0x%x", hr);
        IncrementFailures();
    }

    return hr;
}

void GCHeapEnumerationProfiler::IncrementFailures()
{
    _failures.fetch_add(1, std::memory_order_relaxed);
}

HRESULT GCHeapEnumerationProfiler::ObjectAllocated(ObjectID objectId, ClassID classId)
{
    SHUTDOWNGUARD();
    if (!_referenceOnly)
    {
        return S_OK;
    }

    String typeName = GetClassIDName(classId);
    if (typeName == WCHAR("Profiler.Tests.ObjectReferenceTrigger"))
    {
        return ValidateObjectReferences();
    }
    if (typeName != WCHAR("Profiler.Tests.ObjectReferenceGraph"))
    {
        return S_OK;
    }

    HRESULT hr = pCorProfilerInfo->CreateHandle(objectId, COR_PRF_HANDLE_TYPE_STRONG, &_referenceHandle);
    if (FAILED(hr))
    {
        printf("FAIL: CreateHandle for object-reference graph failed hr=0x%x\n", hr);
        IncrementFailures();
    }
    return hr;
}

HRESULT STDMETHODCALLTYPE GCHeapEnumerationProfiler::GarbageCollectionStarted(int cGenerations, BOOL generationCollected[], COR_PRF_GC_REASON reason)
{
    SHUTDOWNGUARD();
    printf("GCHeapEnumerationProfiler::GarbageCollectionStarted\n");
    _gcStartSleeping = TRUE;
    GCHeapEnumerationProfiler *instance = static_cast<GCHeapEnumerationProfiler*>(GCHeapEnumerationProfiler::Instance);
    // The current thread should block the subsequent background thread until GC completes
    // If the call to EnumerateGCHeapObjects doesn't wait for GC to complete, it should
    // observe IsGCStartSleeping() as true during the heap walk.
    _threadList.emplace_back(std::thread([instance]()
                {
                    printf("EnumerateGCHeapObject on native background thread\n");
                    instance->ValidateEnumerateGCHeapObjects(S_OK);
                }));
    std::this_thread::sleep_for(std::chrono::seconds(1));
    _gcStartSleeping = FALSE;
    return S_OK;
}

HRESULT STDMETHODCALLTYPE GCHeapEnumerationProfiler::GarbageCollectionFinished()
{
    SHUTDOWNGUARD();
    printf("GCHeapEnumerationProfiler::GarbageCollectionFinished\n");
    return S_OK;
}

bool GCHeapEnumerationProfiler::IsGCStartSleeping()
{
    return _gcStartSleeping;
}

HRESULT GCHeapEnumerationProfiler::Shutdown()
{
    // make sure async operations have finished
    for (auto& t : _threadList) {
        t.join();
    }
    if (_referenceOnly)
    {
        ShutdownGuard::WaitForInProgressHooks();
        if (_referenceHandle != nullptr)
        {
            HRESULT hr = pCorProfilerInfo->DestroyHandle(_referenceHandle);
            if (FAILED(hr))
            {
                printf("FAIL: DestroyHandle for object-reference graph failed hr=0x%x\n", hr);
                IncrementFailures();
            }
        }
        Profiler::Shutdown();
        if (!_referenceWalkValidated)
        {
            printf("FAIL: object-reference walking was not validated\n");
            IncrementFailures();
        }
        if (_failures == 0)
        {
            printf("PROFILER TEST PASSES\n");
        }
        fflush(stdout);
        return S_OK;
    }

    Profiler::Shutdown();

    if (_expectedExceptions == 1)
    {
        printf("GCHeapEnumerationProfiler::Shutdown: PASS: Encountered exception as expected.\n");
        printf("PROFILER TEST PASSES\n");
        fflush(stdout);
        return S_OK;
    }

    if (_objectsCount < 90)
    {
        printf("GCHeapEnumerationProfiler::Shutdown: FAIL: Expected at least 90 objects, got %d\n", _objectsCount.load());
        IncrementFailures();
    }

    if (_customGCHeapObjectTypesCount != 1)
    {
        printf("GCHeapEnumerationProfiler::Shutdown: FAIL: Expected 1 custom GCHeapObject type, got %d\n", _customGCHeapObjectTypesCount.load());
        IncrementFailures();
    }

    if (_failures == 0)
    {
        printf("PROFILER TEST PASSES\n");
    }
    else
    {
        // failures were printed earlier when _failures was incremented
    }
    fflush(stdout);

    return S_OK;
}

struct CallbackState
{
    std::atomic<int>* objectsCount;
    GCHeapEnumerationProfiler *instance;
    std::atomic<int>* customGcHeapObjectTypeCount;
};

struct ReferenceWalkState
{
    ObjectID owner;
    SIZE_T size;
    size_t limit;
    bool valid;
    std::vector<ObjectID> references;
};

static BOOL STDMETHODCALLTYPE reference_walk_fn(ObjectID owner, ObjectID* reference, void* context)
{
    ReferenceWalkState* state = static_cast<ReferenceWalkState*>(context);
    uintptr_t slot = reinterpret_cast<uintptr_t>(reference);
    if (owner != state->owner || reference == nullptr || *reference == 0 ||
        slot < owner || slot - owner >= state->size || slot % sizeof(ObjectID) != 0)
    {
        state->valid = false;
        return FALSE;
    }
    state->references.push_back(*reference);
    return state->references.size() < state->limit;
}

static bool ReadObjectReferences(ICorProfilerInfo15* info, ObjectID owner, size_t expected, std::vector<ObjectID>& references)
{
    SIZE_T size = 0;
    HRESULT hr = info->GetObjectSize2(owner, &size);
    if (FAILED(hr))
    {
        printf("FAIL: GetObjectSize2 failed hr=0x%x\n", hr);
        return false;
    }
    ReferenceWalkState state = {owner, size, expected + 1, true, {}};
    hr = info->EnumerateObjectReferences(owner, reference_walk_fn, &state);
    if (FAILED(hr) || !state.valid || state.references.size() != expected)
    {
        printf("FAIL: object-reference walk hr=0x%x, expected=%zu, actual=%zu\n", hr, expected, state.references.size());
        return false;
    }
    references = state.references;

    ReferenceWalkState stopped = {owner, size, 1, true, {}};
    hr = info->EnumerateObjectReferences(owner, reference_walk_fn, &stopped);
    size_t expectedStopped = expected == 0 ? 0 : 1;
    if (FAILED(hr) || !stopped.valid || stopped.references.size() != expectedStopped)
    {
        printf("FAIL: object-reference callback did not stop after its first reference\n");
        return false;
    }
    return true;
}

HRESULT GCHeapEnumerationProfiler::ValidateObjectReferences()
{
    if (_referenceHandle == nullptr)
    {
        printf("FAIL: object-reference graph allocation was not observed\n");
        IncrementFailures();
        return E_FAIL;
    }
    ObjectID owner = 0;
    HRESULT hr = pCorProfilerInfo->GetObjectIDFromHandle(_referenceHandle, &owner);
    if (FAILED(hr))
    {
        printf("FAIL: GetObjectIDFromHandle failed hr=0x%x\n", hr);
    }
    std::vector<ObjectID> arrays;
    bool valid = SUCCEEDED(hr) && ReadObjectReferences(pCorProfilerInfo, owner, 3, arrays);
    for (ObjectID array : arrays)
    {
        ClassID classId = 0;
        CorElementType elementType = ELEMENT_TYPE_END;
        ULONG rank = 0;
        if (FAILED(pCorProfilerInfo->GetClassFromObject(array, &classId)) ||
            pCorProfilerInfo->IsArrayClass(classId, &elementType, nullptr, &rank) != S_OK ||
            rank != 1 || (elementType != ELEMENT_TYPE_OBJECT && elementType != ELEMENT_TYPE_CLASS &&
                          elementType != ELEMENT_TYPE_VALUETYPE))
        {
            printf("FAIL: referenced array type could not be inspected (element=%u, rank=%u)\n",
                   static_cast<unsigned>(elementType), static_cast<unsigned>(rank));
            valid = false;
            break;
        }
        ULONG32 length = 0;
        int lowerBound = 0;
        BYTE* data = nullptr;
        if (FAILED(pCorProfilerInfo->GetArrayObjectInfo(array, 1, &length, &lowerBound, &data)))
        {
            printf("FAIL: referenced array bounds could not be inspected\n");
            valid = false;
            break;
        }
        size_t expected = length == 0 ? 0 : elementType == ELEMENT_TYPE_VALUETYPE ? 3 : 2;
        std::vector<ObjectID> children;
        valid = ReadObjectReferences(pCorProfilerInfo, array, expected, children) && valid;
    }
    if (!valid)
    {
        IncrementFailures();
        return E_FAIL;
    }
    _referenceWalkValidated = true;
    printf("Object, reference-array, struct-array, empty-array, and early-stop walks validated\n");
    return S_OK;
}

static BOOL STDMETHODCALLTYPE heap_walk_fn(ObjectID object, void* callbackState)
{
    CallbackState* state = static_cast<CallbackState*>(callbackState);

    if (state->instance->IsGCStartSleeping())
    {
        printf("Error: no callbacks expected during GC\n");
        state->instance->IncrementFailures();
        return FALSE;
    }

    state->objectsCount->fetch_add(1, std::memory_order_relaxed);

    ClassID classId{0};
    HRESULT hr = state->instance->pCorProfilerInfo->GetClassFromObject(object, &classId);
    if (FAILED(hr))
    {
        printf("Error: failed to get class ID from object with ID 0x%p. hr=0x%x\n", (void *)object, hr);
        state->instance->IncrementFailures();
        return FALSE;
    }

    String classIdName = state->instance->GetClassIDName(classId);
    String expectedCustomObjectClass = reinterpret_cast<const WCHAR*>(u"CustomGCHeapObject");
    if (classIdName == expectedCustomObjectClass)
    {
        state->customGcHeapObjectTypeCount->fetch_add(1, std::memory_order_relaxed);
    }

    bool isArrayClass = state->instance->pCorProfilerInfo->IsArrayClass(classId, NULL, NULL, NULL) == S_OK;
    String stringClass = reinterpret_cast<const WCHAR*>(u"System.String");
    bool isStringClass = classIdName == stringClass;
    if (!isArrayClass && !isStringClass)
    {
        // Validate that the class fields can be extracted from non-array, non-string objects
        // For simplicity, check that the call didn't error
        ULONG pcFieldOffset = 0;
        hr = state->instance->pCorProfilerInfo->GetClassLayout(classId, NULL, 0, &pcFieldOffset, NULL);
        if (FAILED(hr))
        {
            printf("Error: failed to get class fields for class ID 0x%p. hr=0x%x\n", (void *)classId, hr);
            state->instance->IncrementFailures();
            return FALSE;
        }
    }

    return TRUE;
}

HRESULT GCHeapEnumerationProfiler::ValidateEnumerateGCHeapObjects(HRESULT expected)
{
    printf("GCHeapEnumerationProfiler::ValidateEnumerateGCHeapObjects\n");
    GCHeapEnumerationProfiler *instance = static_cast<GCHeapEnumerationProfiler*>(GCHeapEnumerationProfiler::Instance);
    CallbackState state = { &_objectsCount, instance, &_customGCHeapObjectTypesCount };

    printf("Enumerating GC Heap Objects\n");
    HRESULT hr = pCorProfilerInfo->EnumerateGCHeapObjects(heap_walk_fn, &state);

    if (hr == expected)
    {
        if (FAILED(expected))
        {
            printf("Encountered exception as expected.\n");
            _expectedExceptions.fetch_add(1, std::memory_order_relaxed);
            return S_OK;
        }

        printf("EnumerateGCHeapObjects succeeded.\n");
        printf("Number of objects: %d\n", _objectsCount.load());
        printf("Number of custom GCHeapObject types: %d\n", _customGCHeapObjectTypesCount.load());
        return S_OK;
    }

    if (FAILED(hr) && FAILED(expected))
    {
        printf("EnumerateGCHeapObjects failed with exception hr=0x%x, but expected exception hr=0x%x\n", hr, expected);
    }
    else if (FAILED(expected))
    {
        printf("EnumerateGCHeapObjects succeeded, but expected exception hr=0x%x\n", expected);
    }
    else // FAILED(hr)
    {
        printf("Error: failed to enumerate GC heap objects. hr=0x%x\n", hr);
    }

    IncrementFailures();
    return E_FAIL;
}

extern "C" EXPORT void STDMETHODCALLTYPE EnumerateGCHeapObjectsWithoutProfilerRequestedRuntimeSuspension()
{
    printf("EnumerateGCHeapObjectsWithoutPriorRuntimeSuspension PInvoke\n");
    GCHeapEnumerationProfiler *instance = static_cast<GCHeapEnumerationProfiler*>(GCHeapEnumerationProfiler::Instance);
    if (instance == nullptr)
    {
        printf("Error: profiler instance is null.\n");
        return;
    }

    instance->ValidateEnumerateGCHeapObjects(S_OK);
}

extern "C" EXPORT void STDMETHODCALLTYPE EnumerateGCHeapObjectsWithinProfilerRequestedRuntimeSuspension()
{
    printf("EnumerateGCHeapObjectsWithinProfilerRequestedRuntimeSuspension PInvoke\n");
    GCHeapEnumerationProfiler *instance = static_cast<GCHeapEnumerationProfiler*>(GCHeapEnumerationProfiler::Instance);
    if (instance == nullptr)
    {
        printf("Error: profiler instance is null.\n");
        return;
    }

    printf("Profiler Suspending Runtime\n");
    HRESULT hr = instance->pCorProfilerInfo->SuspendRuntime();
    if (FAILED(hr))
    {
        printf("Error: failed to suspend runtime. hr=0x%x\n", hr);
        instance->IncrementFailures();
        return;
    }

    hr = instance->ValidateEnumerateGCHeapObjects(S_OK);
    if (FAILED(hr))
    {
        return;
    }

    printf("Profiler Resuming Runtime\n");
    hr = instance->pCorProfilerInfo->ResumeRuntime();
    if (FAILED(hr))
    {
        printf("Error: failed to resume runtime. hr=0x%x\n", hr);
        instance->IncrementFailures();
        return;
    }
}
