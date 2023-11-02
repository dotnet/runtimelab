// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "CommonTypes.h"
#include "CommonMacros.h"

extern "C" thread_local int RhpExceptionThrown = 0;

COOP_PINVOKE_HELPER(void, RhpThrowNativeException, ())
{
    RhpExceptionThrown = 1;
}

COOP_PINVOKE_HELPER(void, RhpReleaseNativeException, ())
{
    ASSERT(RhpExceptionThrown == 0);
}
