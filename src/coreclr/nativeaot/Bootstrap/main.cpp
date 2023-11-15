// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>

//
// This is the mechanism whereby multiple linked modules contribute their global data for initialization at
// startup of the application.
//
// ILC creates sections in the output obj file to mark the beginning and end of merged global data.
// It defines sentinel symbols that are used to get the addresses of the start and end of global data
// at runtime. The section names are platform-specific to match platform-specific linker conventions.
//
#if defined(_MSC_VER)

#pragma section(".modules$A", read)
#pragma section(".modules$Z", read)
extern "C" __declspec(allocate(".modules$A")) void * __modules_a[];
extern "C" __declspec(allocate(".modules$Z")) void * __modules_z[];

__declspec(allocate(".modules$A")) void * __modules_a[] = { nullptr };
__declspec(allocate(".modules$Z")) void * __modules_z[] = { nullptr };

//
// Each obj file compiled from managed code has a .modules$I section containing a pointer to its ReadyToRun
// data (which points at eager class constructors, frozen strings, etc).
//
// The #pragma ... /merge directive folds the book-end sections and all .modules$I sections from all input
// obj files into .rdata in alphabetical order.
//
#pragma comment(linker, "/merge:.modules=.rdata")

//
// Unboxing stubs need to be merged, folded and sorted. They are delimited by two special sections (.unbox$A
// and .unbox$Z). All unboxing stubs are in .unbox$M sections.
//
#pragma comment(linker, "/merge:.unbox=.text")

char _bookend_a;
char _bookend_z;

//
// Generate bookends for the managed code section.
// We give them unique bodies to prevent folding.
//

#pragma code_seg(".managedcode$A")
void* __managedcode_a() { return &_bookend_a; }
#pragma code_seg(".managedcode$Z")
void* __managedcode_z() { return &_bookend_z; }
#pragma code_seg()

//
// Generate bookends for the unboxing stub section.
// We give them unique bodies to prevent folding.
//

#pragma code_seg(".unbox$A")
void* __unbox_a() { return &_bookend_a; }
#pragma code_seg(".unbox$Z")
void* __unbox_z() { return &_bookend_z; }
#pragma code_seg()

#else // _MSC_VER

#if defined(__APPLE__)

extern void * __modules_a[] __asm("section$start$__DATA$__modules");
extern void * __modules_z[] __asm("section$end$__DATA$__modules");
extern char __managedcode_a __asm("section$start$__TEXT$__managedcode");
extern char __managedcode_z __asm("section$end$__TEXT$__managedcode");
extern char __unbox_a __asm("section$start$__TEXT$__unbox");
extern char __unbox_z __asm("section$end$__TEXT$__unbox");

#else // __APPLE__

extern "C" void * __start___modules[];
extern "C" void * __stop___modules[];
static void * (&__modules_a)[] = __start___modules;
static void * (&__modules_z)[] = __stop___modules;

extern "C" char __start___managedcode;
extern "C" char __stop___managedcode;
static char& __managedcode_a = __start___managedcode;
static char& __managedcode_z = __stop___managedcode;

extern "C" char __start___unbox;
extern "C" char __stop___unbox;
static char& __unbox_a = __start___unbox;
static char& __unbox_z = __stop___unbox;

#endif // __APPLE__

#endif // _MSC_VER

extern "C" bool RhInitialize(bool isDll);
extern "C" void RhSetRuntimeInitializationCallback(int (*fPtr)());

extern "C" bool RhRegisterOSModule(void * pModule,
    void * pvManagedCodeStartRange, uint32_t cbManagedCodeRange,
    void * pvUnboxingStubsStartRange, uint32_t cbUnboxingStubsRange,
    void ** pClasslibFunctions, uint32_t nClasslibFunctions);

extern "C" void* PalGetModuleHandleFromPointer(void* pointer);

// The runtime assumes classlib exports have a managed calling convention.
// For WASM, however, they are exported with the native calling convention
// by default so we must explicitly use the managed entrypoint here.
#ifdef HOST_WASM
#define MANAGED_RUNTIME_EXPORT(name) name##_Managed
#else
#define MANAGED_RUNTIME_EXPORT(name) name
#endif

extern "C" void MANAGED_RUNTIME_EXPORT(GetRuntimeException)();
extern "C" void MANAGED_RUNTIME_EXPORT(RuntimeFailFast)();
extern "C" void MANAGED_RUNTIME_EXPORT(AppendExceptionStackFrame)();
extern "C" void MANAGED_RUNTIME_EXPORT(GetSystemArrayEEType)();
extern "C" void MANAGED_RUNTIME_EXPORT(OnFirstChanceException)();
extern "C" void MANAGED_RUNTIME_EXPORT(OnUnhandledException)();
extern "C" void MANAGED_RUNTIME_EXPORT(IDynamicCastableIsInterfaceImplemented)();
extern "C" void MANAGED_RUNTIME_EXPORT(IDynamicCastableGetInterfaceImplementation)();
#ifdef FEATURE_OBJCMARSHAL
extern "C" void ObjectiveCMarshalTryGetTaggedMemory();
extern "C" void ObjectiveCMarshalGetIsTrackedReferenceCallback();
extern "C" void ObjectiveCMarshalGetOnEnteredFinalizerQueueCallback();
extern "C" void ObjectiveCMarshalGetUnhandledExceptionPropagationHandler();
#endif

typedef void(*pfn)();

static const pfn c_classlibFunctions[] = {
    &MANAGED_RUNTIME_EXPORT(GetRuntimeException),
    &MANAGED_RUNTIME_EXPORT(RuntimeFailFast),
    nullptr, // &UnhandledExceptionHandler,
    &MANAGED_RUNTIME_EXPORT(AppendExceptionStackFrame),
    nullptr, // &CheckStaticClassConstruction,
    &MANAGED_RUNTIME_EXPORT(GetSystemArrayEEType),
    &MANAGED_RUNTIME_EXPORT(OnFirstChanceException),
    &MANAGED_RUNTIME_EXPORT(OnUnhandledException),
    &MANAGED_RUNTIME_EXPORT(IDynamicCastableIsInterfaceImplemented),
    &MANAGED_RUNTIME_EXPORT(IDynamicCastableGetInterfaceImplementation),
#ifdef FEATURE_OBJCMARSHAL
    &ObjectiveCMarshalTryGetTaggedMemory,
    &ObjectiveCMarshalGetIsTrackedReferenceCallback,
    &ObjectiveCMarshalGetOnEnteredFinalizerQueueCallback,
    &ObjectiveCMarshalGetUnhandledExceptionPropagationHandler,
#else
    nullptr,
    nullptr,
    nullptr,
    nullptr,
#endif
};

#ifndef _countof
#define _countof(_array) (sizeof(_array)/sizeof(_array[0]))
#endif

extern "C" void InitializeModules(void* osModule, void ** modules, int count, void ** pClasslibFunctions, int nClasslibFunctions);

#ifndef NATIVEAOT_DLL
#define NATIVEAOT_ENTRYPOINT __managed__Main
#if defined(_WIN32)
extern "C" int __managed__Main(int argc, wchar_t* argv[]);
#else
extern "C" int __managed__Main(int argc, char* argv[]);
#endif
#else
#define NATIVEAOT_ENTRYPOINT __managed__Startup
extern "C" void __managed__Startup();

#ifdef TARGET_WASI
// _initialize is a function generated by the WASI SDK libc that calls the LLVM synthesized __wasm_call_ctors function for reactor components:
// https://github.com/WebAssembly/wasi-libc/blob/9f51a7102085ec6a6ced5778f0864c9af9f50000/libc-bottom-half/crt/crt1-reactor.c#L7-L27
// We define and call it for NATIVEAOT_DLL and TARGET_WASI to call all the global c++ static constructors.  This ensures the runtime is initialized
// when calling into WebAssembly Component Model components.
extern "C" void _initialize();

// CustomNativeMain programs are built using the same libbootstrapperdll as NATIVEAOT_DLL but wasi-libc will not provide an _initialize implementation,
// so create a dummy one here and make it weak to allow wasi-libc to provide the real implementation for WASI reactor components.
__attribute__((weak)) void _initialize()
{
}

// Guard the "_initialize" call so that well-behaving hosts do not get affected by this workaround.
static bool g_CalledInitialize = false;
struct WasiInitializationFlag { WasiInitializationFlag() { *(volatile bool*)&g_CalledInitialize = true; } };
WasiInitializationFlag g_WasiInitializationFlag;
#endif // TARGET_WASI
#endif // !NATIVEAOT_DLL

static int InitializeRuntime()
{
#if defined(NATIVEAOT_DLL) && defined(TARGET_WASI)
    if (!g_CalledInitialize)
    {
        _initialize();
    }
#endif

    if (!RhInitialize(
#ifdef NATIVEAOT_DLL
        /* isDll */ true
#else
        /* isDll */ false
#endif
        ))
        return -1;

    void * osModule = PalGetModuleHandleFromPointer((void*)&NATIVEAOT_ENTRYPOINT);

#if !defined HOST_WASM
    // TODO: pass struct with parameters instead of the large signature of RhRegisterOSModule
    if (!RhRegisterOSModule(
        osModule,
        (void*)&__managedcode_a, (uint32_t)((char *)&__managedcode_z - (char*)&__managedcode_a),
        (void*)&__unbox_a, (uint32_t)((char *)&__unbox_z - (char*)&__unbox_a),
        (void **)&c_classlibFunctions, _countof(c_classlibFunctions)))
    {
        return -1;
    }
#endif // HOST_WASM

    InitializeModules(osModule, __modules_a, (int)((__modules_z - __modules_a)), (void **)&c_classlibFunctions, _countof(c_classlibFunctions));

#ifdef NATIVEAOT_DLL
    // Run startup method immediately for a native library
    __managed__Startup();
#endif // NATIVEAOT_DLL

    return 0;
}

#ifdef NATIVEAOT_DLL
int (*g_RuntimeInitializationCallback)() = &InitializeRuntime;
#else
int (*g_RuntimeInitializationCallback)() = nullptr;
#endif

#ifndef NATIVEAOT_DLL

#if defined(_WIN32)
int __cdecl wmain(int argc, wchar_t* argv[])
#else
int main(int argc, char* argv[])
#endif
{
    int initval = InitializeRuntime();
    if (initval != 0)
        return initval;

    return __managed__Main(argc, argv);
}

#ifdef HAS_ADDRESS_SANITIZER
// We need to build the bootstrapper as a single object file, to ensure
// the linker can detect that we have ASAN components early enough in the build.
// Include our asan support sources for executable projects here to ensure they
// are compiled into the bootstrapper object.
#include "minipal/asansupport.cpp"
#endif // HAS_ADDRESS_SANITIZER

#endif // !NATIVEAOT_DLL
