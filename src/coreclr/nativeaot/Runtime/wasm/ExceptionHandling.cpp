// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>
#include <exception>

#include "CommonMacros.h"

// Exception wrapper type that allows us to differentiate managed and native exceptions.
class Object;
struct ManagedExceptionWrapper : std::exception
{
    ManagedExceptionWrapper(Object* pManagedException) : m_pManagedException(pManagedException)
    {
    }
    
    Object* m_pManagedException;
};

COOP_PINVOKE_HELPER(void, RhpThrowNativeException, (Object* pManagedException))
{
    throw ManagedExceptionWrapper(pManagedException);
}

extern "C" uint32_t LlvmCatchFunclet(void* pHandlerIP, void* pvRegDisplay); 
extern "C" uint32_t RhpCallCatchFunclet(void * exceptionObj, void* pHandlerIP, void* pvRegDisplay, void *exInfo)
{
    return LlvmCatchFunclet(pHandlerIP, pvRegDisplay);
}

extern "C" uint32_t LlvmFilterFunclet(void* pHandlerIP, void* pvRegDisplay);
extern "C" uint32_t RhpCallFilterFunclet(void* exceptionObj, void * pHandlerIP, void* shadowStack)
{
    return LlvmFilterFunclet(pHandlerIP, shadowStack);
}

extern "C" void LlvmFinallyFunclet(void *finallyHandler, void *shadowStack);
extern "C" void RhpCallFinallyFunclet(void *finallyHandler, void *shadowStack)
{
    LlvmFinallyFunclet(finallyHandler, shadowStack);
}
