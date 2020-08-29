// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "pal_errno.h"

extern "C" int32_t CoreLibNative_GetErrNo()
{
    return errno;
}

extern "C" void CoreLibNative_ClearErrNo()
{
    errno = 0;
}

int32_t SystemNative_ConvertErrorPlatformToPal(int32_t platformErrno)
{
    return ConvertErrorPlatformToPal(platformErrno);
}

int32_t SystemNative_ConvertErrorPalToPlatform(int32_t error)
{
    return ConvertErrorPalToPlatform(error);
}

const char* SystemNative_StrErrorR(int32_t platformErrno, char* buffer, int32_t bufferSize)
{
    return StrErrorR(platformErrno, buffer, bufferSize);
}
