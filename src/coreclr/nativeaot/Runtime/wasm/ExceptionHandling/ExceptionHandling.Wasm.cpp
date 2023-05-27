// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "ExceptionHandling.h"

ExceptionDispatchData* BeginSingleFrameDispatch(void* pFrameDispatchData)
{
    return static_cast<ExceptionDispatchData*>(pFrameDispatchData);
}

COOP_PINVOKE_HELPER(void, RhpThrowNativeException, (void* pDispatcherShadowFrame, Object** pManagedException))
{
    ExceptionDispatchData* pException = new ExceptionDispatchData(pDispatcherShadowFrame, pManagedException);
    __builtin_wasm_throw(/* CPP_EXCEPTION_TAG */ 0, pException);
}

COOP_PINVOKE_HELPER(void, RhpReleaseNativeException, (ExceptionDispatchData* pDispatchData))
{
    delete pDispatchData;
}
