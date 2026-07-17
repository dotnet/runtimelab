// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// dllmain.cpp - standalone GC loader entry points.
//
// CoreCLR discovers a standalone GC by:
//   1. LoadLibrary'ing the DLL named by DOTNET_GCName (or the runtimeconfig.json
//      "System.GC.Name" knob), first from the app's own directory, then next
//      to coreclr.dll.
//   2. Calling the exported GC_VersionInfo to check ABI compatibility.
//   3. Calling the exported GC_Initialize to obtain an IGCHeap + IGCHandleManager.
//
// See: docs/design/coreclr/botr/standalone-gc-loading.md in dotnet/runtime.
//
#include "ZeroGC.h"

IGCToCLR* g_theGCToCLR = nullptr;
VersionInfo g_runtimeSupportedVersion = {};
bool g_oldMethodTableFlags = false;

static ZeroGCHandleManager* g_zeroGCHandleManager = nullptr;

extern "C" __declspec(dllexport) void GC_VersionInfo(VersionInfo* info)
{
    // On entry, `info` carries the interface version the runtime supports;
    // remember it so we know which optional IGCToCLR members are safe to call.
    g_runtimeSupportedVersion = *info;
    g_oldMethodTableFlags = g_runtimeSupportedVersion.MajorVersion < 2;

    info->MajorVersion = GC_INTERFACE_MAJOR_VERSION;
    info->MinorVersion = GC_INTERFACE_MINOR_VERSION;
    info->BuildVersion = 0;
    info->Name = "ZeroGC";
}

extern "C" __declspec(dllexport) HRESULT GC_Initialize(
    IGCToCLR* clrToGC,
    IGCHeap** gcHeap,
    IGCHandleManager** gcHandleManager,
    GcDacVars* gcDacVars)
{
    if (gcHeap == nullptr || gcHandleManager == nullptr || gcDacVars == nullptr)
        return E_INVALIDARG;

    g_theGCToCLR = clrToGC;

    memset(gcDacVars, 0, sizeof(*gcDacVars));

    g_zeroGCHandleManager = new (nothrow) ZeroGCHandleManager();
    if (g_zeroGCHandleManager == nullptr || !g_zeroGCHandleManager->Initialize())
        return E_OUTOFMEMORY;

    g_zeroGCHeap = ZeroGCHeap::CreateAndInitialize();
    if (g_zeroGCHeap == nullptr)
        return E_OUTOFMEMORY;

    *gcHeap = g_zeroGCHeap;
    *gcHandleManager = g_zeroGCHandleManager;
    return S_OK;
}

BOOL WINAPI DllMain(HINSTANCE hInstDll, DWORD reason, LPVOID reserved)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(hInstDll);
    }
    return TRUE;
}
