// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <exception>

#include "ExceptionHandling.h"

// The layout of this struct must match what codegen expects (see "jit/llvmcodegen.cpp, generateEHDispatch").
// Instances of it are shared between dispatchers across a single native frame.
//
struct FrameDispatchData
{
    struct {
        void* ExceptionData;
        int Selector;
    } CppExceptionTuple; // Owned by codegen.

    ExceptionDispatchData* DispatchData; // Owned by runtime.
};

struct ManagedExceptionWrapper : std::exception
{
    ManagedExceptionWrapper(ExceptionDispatchData dispatchData) : DispatchData(dispatchData)
    {
    }

    ExceptionDispatchData DispatchData;
};

extern "C" void* __cxa_begin_catch(void* pExceptionData);
extern "C" void __cxa_end_catch();

ExceptionDispatchData* BeginSingleFrameDispatch(void* pFrameDispatchData)
{
    FrameDispatchData* pData = static_cast<FrameDispatchData*>(pFrameDispatchData);
    if (pData->DispatchData == nullptr)
    {
        ASSERT(pData->CppExceptionTuple.ExceptionData != nullptr);
        ManagedExceptionWrapper* pException = (ManagedExceptionWrapper*)__cxa_begin_catch(pData->CppExceptionTuple.ExceptionData);
        ASSERT(pException != nullptr);
        pData->DispatchData = &pException->DispatchData;
    }

    return pData->DispatchData;
}

COOP_PINVOKE_HELPER(void, RhpThrowNativeException, (void* pDispatcherShadowFrame, Object** pManagedException))
{
    throw ManagedExceptionWrapper(ExceptionDispatchData(pDispatcherShadowFrame, pManagedException));
}

COOP_PINVOKE_HELPER(void, RhpReleaseNativeException, (ExceptionDispatchData* pDispatchData))
{
    __cxa_end_catch();
}
