// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>
#include <stdlib.h>

#include "CommonMacros.h"

class Object;
struct ExceptionDispatchData
{
    ExceptionDispatchData(void* pDispatcherShadowFrame, Object** pManagedException)
        : DispatchShadowFrameAddress(pDispatcherShadowFrame)
        , ManagedExceptionAddress(pManagedException)
        , LastFault(nullptr)
    {
        ASSERT(pDispatcherShadowFrame != nullptr);
        ASSERT(pManagedException != nullptr);
    }

    // The layout of this struct must match the managed version in "ExceptionHandling.wasm.cs" exactly.
    void* DispatchShadowFrameAddress;
    Object** ManagedExceptionAddress;
    void* LastFault;
};

ExceptionDispatchData* BeginSingleFrameDispatch(void* pFrameDispatchData);
