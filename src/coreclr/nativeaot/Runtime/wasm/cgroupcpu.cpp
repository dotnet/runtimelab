// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <cstdint>

//
// No CPU or memory limits on WASM as yet.
//
void InitializeCpuCGroup()
{
}

bool GetCpuLimit(uint32_t* val)
{
    return false;
}
