// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma once

#include "pal_compiler.h"
#include "pal_types.h"

PALEXPORT char* SystemNative_GetEnv(const char* variable);

PALEXPORT char** SystemNative_GetEnviron(void);

PALEXPORT void SystemNative_FreeEnviron(char** environ);

PALEXPORT int32_t SystemNative_SchedGetCpu(void);

PALEXPORT __attribute__((noreturn)) void SystemNative_Exit(int32_t exitCode);

PALEXPORT __attribute__((noreturn)) void SystemNative_Abort(void);

