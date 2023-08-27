// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "CommonTypes.h"
#include "CommonMacros.h"

COOP_PINVOKE_HELPER(void, RhpThrowNativeException, ())
{
    __builtin_wasm_throw(/* CPP_EXCEPTION_TAG */ 0, nullptr);
}

COOP_PINVOKE_HELPER(void, RhpReleaseNativeException, ())
{
}
