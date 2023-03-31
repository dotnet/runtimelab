// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>
#include <stdlib.h>
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

// We do not use these helpers, but we also do not exclude code referencing them from the
// build, and so define these stubs to avoid undefined symbol warnings. TODO-LLVM: exclude
// the general EH code from WASM-targeting runtime build.
//
COOP_PINVOKE_HELPER(void*, RhpCallCatchFunclet, (void*, void*, void*, void*)) { abort(); }
COOP_PINVOKE_HELPER(bool, RhpCallFilterFunclet, (void*, void*, void*)) { abort(); }
COOP_PINVOKE_HELPER(void, RhpCallFinallyFunclet, (void*, void*)) { abort(); }
