// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "CommonTypes.h"
#include "CommonMacros.h"

extern "C" void __cxa_end_catch();

COOP_PINVOKE_HELPER(void, RhpThrowNativeException, ())
{
    throw 0;
}

COOP_PINVOKE_HELPER(void, RhpReleaseNativeException, ())
{
    __cxa_end_catch();
}
