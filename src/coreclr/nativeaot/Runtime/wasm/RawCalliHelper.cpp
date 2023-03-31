// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "CommonTypes.h"
#include "CommonMacros.h"

class Object;

COOP_PINVOKE_HELPER(Object*, RhpRawCalli_OI, (Object* (pfn)(intptr_t), intptr_t arg))
{
    return pfn(arg);
}
