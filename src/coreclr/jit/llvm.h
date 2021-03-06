// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*****************************************************************************/
#ifndef _LLVM_H_
#define _LLVM_H_
#undef __PLACEMENT_NEW_INLINE

#include "alloc.h"
#include "jitpch.h"
#include <new>

// these break std::min/max
#undef min
#undef max
// this breaks StringMap.h
#undef NumItems
#ifdef TARGET_WASM


extern "C" void registerLlvmCallbacks(void* thisPtr, const char* (*getMangledMethodNamePtr)(void*, CORINFO_METHOD_STRUCT_*));

class Llvm
{
public:
    static void Init();
    static void llvmShutdown();

    void Compile(Compiler* pCompiler);
};

#endif

#endif /* End of _LLVM_H_ */
