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
#include "llvm/ADT/APFloat.h"
#ifdef TARGET_WASM


//llvm::detail::DoubleAPFloat(const llvm::detail::DoubleAPFloat &) = default;

class Llvm
{
public:
    void Compile(Compiler* pCompiler);
};

#endif

#endif /* End of _LLVM_H_ */
