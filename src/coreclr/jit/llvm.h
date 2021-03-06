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

#define IMAGE_FILE_MACHINE_WASM32             0xFFFF
#define IMAGE_FILE_MACHINE_WASM64             0xFFFE // TODO: appropriate values for this?  Used to check compilation is for intended target




extern "C" void registerLlvmCallbacks(void* thisPtr, const char* outputFileName, const char* (*getMangledMethodNamePtr)(void*, CORINFO_METHOD_STRUCT_*));

class Llvm
{
public:
    static void Init();
    static void llvmShutdown();

    void Compile(Compiler* pCompiler);
};

#endif

#endif /* End of _LLVM_H_ */
