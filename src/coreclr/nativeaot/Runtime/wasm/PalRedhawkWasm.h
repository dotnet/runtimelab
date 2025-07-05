// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifdef FEATURE_WASM_MANAGED_THREADS
int __cxa_thread_atexit(void (*func)(), void *obj, void *dso_symbol);
void PalGetMaximumStackBounds_MultiThreadedWasm(void** ppStackLowOut, void** ppStackHighOut);
#else
void PalGetMaximumStackBounds_SingleThreadedWasm(void** ppStackLowOut, void** ppStackHighOut);
#endif // !FEATURE_WASM_MANAGED_THREADS
