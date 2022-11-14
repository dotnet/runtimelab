// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*****************************************************************************/

#include "jitpch.h"
#ifdef _MSC_VER
#pragma hdrstop
#endif

#include "target.h"

const char*            Target::g_tgtCPUName           = "wasm";
const Target::ArgOrder Target::g_tgtArgOrder          = ARG_ORDER_R2L;
const Target::ArgOrder Target::g_tgtUnmanagedArgOrder = ARG_ORDER_R2L;

const regNumber intArgRegs [] = { REG_R0 };
const regMaskTP intArgMasks[] = { RBM_R0 };
const regNumber fltArgRegs [] = { REG_F0 };
const regMaskTP fltArgMasks[] = { RBM_F0 };
// clang-format on
