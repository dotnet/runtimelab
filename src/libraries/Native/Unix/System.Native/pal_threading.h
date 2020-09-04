// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "pal_compiler.h"
#include "pal_types.h"

typedef struct LowLevelMonitor LowLevelMonitor;

PALEXPORT LowLevelMonitor *SystemNative_LowLevelMonitor_Create(void);

PALEXPORT void SystemNative_LowLevelMonitor_Destroy(LowLevelMonitor* monitor);

PALEXPORT void SystemNative_LowLevelMonitor_Acquire(LowLevelMonitor* monitor);

PALEXPORT void SystemNative_LowLevelMonitor_Release(LowLevelMonitor* monitor);

PALEXPORT void SystemNative_LowLevelMonitor_Wait(LowLevelMonitor* monitor);

PALEXPORT int32_t SystemNative_LowLevelMonitor_TimedWait(LowLevelMonitor *monitor, int32_t timeoutMilliseconds);

PALEXPORT void SystemNative_LowLevelMonitor_Signal_Release(LowLevelMonitor* monitor);

PALEXPORT int32_t SystemNative_RuntimeThread_CreateThread(uintptr_t stackSize, void *(*startAddress)(void*), void *parameter);
