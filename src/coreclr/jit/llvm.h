// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*****************************************************************************/
#ifndef _LLVM_H_
#define _LLVM_H_
#undef __PLACEMENT_NEW_INLINE

#include "alloc.h"
#include "jitpch.h"
#include <new>

// these break std::min/max in LLVM's headers
#undef min
#undef max
// this breaks StringMap.h
#undef NumItems
#ifdef TARGET_WASM

#define IMAGE_FILE_MACHINE_WASM32             0xFFFF
#define IMAGE_FILE_MACHINE_WASM64             0xFFFE // TODO: appropriate values for this?  Used to check compilation is for intended target

extern "C" void registerLlvmCallbacks(void*       thisPtr,
                                      const char* outputFileName,
                                      const char* triple,
                                      const char* dataLayout,
                                      const char* (*getMangledMethodNamePtr)(void*, CORINFO_METHOD_STRUCT_*),
                                      const char* (*_getMangledSymbolNamePtr)(void*, void*),
                                      const char* (*addCodeReloc)(void*, void*),
                                      const uint32_t (*isRuntimeImport)(void*, CORINFO_METHOD_STRUCT_*),
                                      const char* (*getDocumentFileName)(void*),
                                      const uint32_t (*firstSequencePointLineNumber)(void*),
                                      const uint32_t (*getOffsetLineNumber)(void*, unsigned int),
                                      const uint32_t (*structIsWrappedPrimitive)(void*, CORINFO_CLASS_STRUCT_*, CorInfoType));

class Llvm
{
private:
    bool _sigInfoIsValid;

public:
    Llvm(Compiler* pCompiler);

    static void llvmShutdown();

    void ConvertShadowStackLocals();
    void PlaceAndConvertShadowStackLocals();
    void Compile();
};

#endif

#endif /* End of _LLVM_H_ */
