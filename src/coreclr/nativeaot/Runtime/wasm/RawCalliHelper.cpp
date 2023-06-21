// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "CommonTypes.h"
#include "CommonMacros.h"

class Object;

template <typename TReturn, typename... TArgs>
TReturn Call(void* pfn, TArgs... args)
{
    return ((TReturn(*)(TArgs...))pfn)(args...);
}

COOP_PINVOKE_HELPER(Object*, RhpRawCalli_OI, (void* pfn, intptr_t arg))
{
    return Call<Object*>(pfn, arg);
}
