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
