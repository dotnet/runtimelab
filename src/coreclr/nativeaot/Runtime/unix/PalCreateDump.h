// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma once

#include <signal.h>

extern bool PalCreateDumpInitialize();
extern void PalCreateCrashDumpIfEnabled();

#if !defined(HOST_WASM)
extern void PalCreateCrashDumpIfEnabled(int signal, siginfo_t* siginfo = nullptr, void* exceptionRecord = nullptr);
#endif // !defined(HOST_WASM)
extern void PalCreateCrashDumpIfEnabled(void* pExceptionRecord, void* pExContext);
